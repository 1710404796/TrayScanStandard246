using Camera.Fs.Common;
using HKCamera.Fs.NET.Controls;
using LinxUniverse.Utils;
using MediatR;
using MugenCamera;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Utils;

namespace TrayScanStandard.Mediator.Handlers
{
    public class CamCaptureCommandHandler : IRequestHandler<CamCaptureCommand, Either<string, IEnumerable<Image2DResult[]>>>
    {
        private static readonly string _logPrefix = "[CamCapture]";
        private static readonly SemaphoreSlim _captureLock = new(1, 1);

        public async Task<Either<string, IEnumerable<Image2DResult[]>>> Handle(CamCaptureCommand request, CancellationToken cancellationToken)
        {
            if (!await _captureLock.WaitAsync(0))
            {
                Log.Warning("{Prefix} 上一次拍照尚未完成，拒绝重复请求", _logPrefix);
                return Either<string, IEnumerable<Image2DResult[]>>.Left("上一次拍照未完成，请等待");
            }
            try
            {
                return await HandleCore(request, cancellationToken);
            }
            finally
            {
                _captureLock.Release();
            }
        }

        private async Task<Either<string, IEnumerable<Image2DResult[]>>> HandleCore(CamCaptureCommand request, CancellationToken cancellationToken)
        {
            if (!MainStorage.Saves.CameraEnable)
            {
                return Either<string, IEnumerable<Image2DResult[]>>.Right(
                    [..request.CaptureInfos
                        .Select((s, i) => new Image2DResult[] {
                            new Image2DResult(File.ReadAllBytes(@$"testImg\{i}.png"))
                        })
                    ]
                );
            }

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Log.Information("{Prefix} 后台拍照任务开始: 相机数={Count}", _logPrefix, request.CaptureInfos.Length);

            return await Task.Run(() =>
            {
                // 整个 UseLight(光源控制+相机拍照) 在专用线程执行
                // 超时使用 Stopwatch（硬件计数器），避免 DateTime.UtcNow 受系统时钟跳变影响
                Either<string, IEnumerable<Image2DResult[]>>? nullableResult = null;
                Exception? exception = null;
                var completedFlag = false;

                var worker = new Thread(() =>
                {
                    try
                    {
                        nullableResult = DetectUtil.UseLight(() =>
                            request.CaptureInfos
                                .Map(s => s.ToEither("相机未初始化"))
                                .Traverse(s => s)
                                .Bind(c => RunCapture(c.ToArray(), sw))
                        );
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                    }
                    finally
                    {
                        Volatile.Write(ref completedFlag, true);
                    }
                });
                worker.IsBackground = true;
                worker.Name = "CaptureUseLight";
                worker.Start();

                // 轮询等待 — 用 Stopwatch 计时，不受系统时钟跳变影响
                while (!Volatile.Read(ref completedFlag) && sw.ElapsedMilliseconds < 8000)
                {
                    Thread.Sleep(50);
                }

                 if (!Volatile.Read(ref completedFlag))
                 {
                     Log.Error("{Prefix} 超时({Elapsed:F1}s) - UseLight 未在 8s 内返回，强制关闭光源",
                         _logPrefix, sw.Elapsed.TotalSeconds);
                     try
                     {
                         DetectUtil.TurnOffLights();
                     }
                     catch (Exception ex)
                     {
                         Log.Error(ex, "{Prefix} 强制关闭光源时异常", _logPrefix);
                     }
                     return Either<string, IEnumerable<Image2DResult[]>>
                         .Left("拍照+光源操作超时(>8s)");
                 }

                if (exception != null)
                {
                    Log.Error(exception, "{Prefix} 拍照任务异常", _logPrefix);
                    throw exception;
                }

                var captureResult = nullableResult
                    ?? throw new InvalidOperationException("拍照线程返回但结果为空");

                Log.Information("{Prefix} Handle 返回: IsRight={IsRight}, 总耗时={Elapsed:F1}s",
                    _logPrefix, captureResult.IsRight, sw.Elapsed.TotalSeconds);
                return captureResult;
            });
        }

        private static Either<string, IEnumerable<Image2DResult[]>> RunCapture(
            CaptureInfo[] captureInfos,
            System.Diagnostics.Stopwatch sw)
        {
            Log.Information("{Prefix} 亮灯完成，开始并行拍摄 {Count} 台相机", _logPrefix, captureInfos.Length);

            var results = new Either<string, Image2DResult[]>[captureInfos.Length];
            var threads = new Thread[captureInfos.Length];
            var threadFlags = new bool[captureInfos.Length];

            for (int i = 0; i < captureInfos.Length; i++)
            {
                var idx = i;
                var info = captureInfos[idx];
                var camIdx = idx + 1;
                Log.Information("{Prefix}   -> 启动相机[{Cam}] 拍摄任务", _logPrefix, camIdx);

                threads[idx] = new Thread(() =>
                {
                    try
                    {
                        Log.Information("{Prefix}     相机[{Cam}] 任务开始执行: CheckConnect + 曝光", _logPrefix, camIdx);
                        results[idx] = ProcessCaptureInfo(info);
                        Log.Information("{Prefix}     相机[{Cam}] 任务完成", _logPrefix, camIdx);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "{Prefix}     相机[{Cam}] 任务异常", _logPrefix, camIdx);
                        results[idx] = Either<string, Image2DResult[]>.Left($"相机[{camIdx}]异常: {ex.Message}");
                    }
                    finally
                    {
                        Volatile.Write(ref threadFlags[idx], true);
                    }
                });
                threads[idx].IsBackground = true;
                threads[idx].Name = $"Cam{camIdx}";
                threads[idx].Start();
            }

            // 轮询等待 — 用 Stopwatch 计时，不受系统时钟跳变影响
            while (sw.ElapsedMilliseconds < 8000)
            {
                var allDone = true;
                for (int i = 0; i < threadFlags.Length; i++)
                {
                    if (!Volatile.Read(ref threadFlags[i]))
                    {
                        allDone = false;
                        break;
                    }
                }
                if (allDone)
                    break;
                Thread.Sleep(50);
            }

            Log.Information("{Prefix} 并行等待结束: 耗时={Elapsed:F1}s", _logPrefix, sw.Elapsed.TotalSeconds);

            for (int i = 0; i < threads.Length; i++)
            {
                if (!Volatile.Read(ref threadFlags[i]))
                {
                    Log.Warning("{Prefix} 相机[{Cam}] 拍摄超时(>8s)，标记为失败", _logPrefix, i + 1);
                    results[i] = Either<string, Image2DResult[]>.Left($"相机[{i + 1}]操作超时(>8s)");
                }
            }

            var okCount = results.Count(r => r.IsRight);
            var timeoutCount = results.Length - okCount;
            Log.Information("{Prefix} 拍照结果: 成功={Ok}, 失败={Fail}/{Total}",
                _logPrefix, okCount, timeoutCount, results.Length);

            return results.Traverse(s => s);
        }

       private static Either<string, Image2DResult[]> ProcessCaptureInfo(CaptureInfo s)
       {
           return s.Camera.CheckConnect()
               .Bind(cam => s.Exps.Map(e =>
                       cam
                       .SetControl(new AcquisitionControl { ExposureTime = e })
                       .Bind(DetectUtil.CaptureOne)
                   )
               .Traverse(x => x)
               .Map(x => x.ToArray()));
       }
    }
}
