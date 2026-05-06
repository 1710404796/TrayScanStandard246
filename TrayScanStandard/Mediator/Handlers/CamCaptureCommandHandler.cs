using Camera.Fs.Common;
using HKCamera.Fs.NET.Controls;
using LinxUniverse.Utils;
using MediatR;
using Microsoft.Extensions.Logging;
using MugenCamera;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Services;
using TrayScanStandard.Utils;

namespace TrayScanStandard.Mediator.Handlers
{
    // 此处用于处理相机拍照的命令
    public class CamCaptureCommandHandler(
        WordopLightService wordopLightService, 
        ILogger<CamCaptureCommandHandler> logger) 
        : IRequestHandler<CamCaptureCommand, Either<string, IEnumerable<Image2DResult[]>>>
    {
        public async Task<Either<string, IEnumerable<Image2DResult[]>>> Handle(CamCaptureCommand request, CancellationToken cancellationToken)
        {
        
            //var a = request.CaptureInfos.Select(s => 1);
            if (!MainStorage.Saves.CameraEnable)
            {
                return Either<string, IEnumerable<Image2DResult[]>>.Right
               ([..request.CaptureInfos
               .Select((s, i) => new Image2DResult[]
                       {
                           new Image2DResult(File.ReadAllBytes( @$"testImg\{i}.png"))
                           //new Image2DResult(File.ReadAllBytes( @$"C:\Users\admin\Pictures\20211110085254_736a4.jpeg"))
                       }
                 )
               ]);
            }

            await TurnOnWordopBeforeCaptureAsync();
            try
            {
                // 原逻辑（保留）：
                // return DetectUtil.UseLight(
                //     () => request.CaptureInfos
                //         .Map(s => s.ToEither("相机未初始化"))
                //         .Traverse(s => s)
                //         .Bind(c =>
                //             c
                //             .Select(ProcessCaptureInfo)
                //             .Traverse(s => s)
                //         )
                // ).Apply(Task.FromResult);

                var captureResult = DetectUtil.UseLight(
                    () => request.CaptureInfos
                        .Map(s => s.ToEither("相机未初始化"))
                        .Traverse(s => s)
                        .Bind(c =>
                            c
                            //.AsParallel()    // 华睿相机并行拍照会有问题，暂时不使用
                            .Select(ProcessCaptureInfo)
                            .Traverse(s => s)
                        )
                    );

                return captureResult;
            }
            finally
            {
                await TurnOffWordopAfterCaptureAsync();
            }

            // 本地函数：处理单个CaptureInfo
            Either<string, Image2DResult[]> ProcessCaptureInfo(CaptureInfo s)
            {
                var aa = s.Exps.Map(e =>
                        s.Camera
                        .SetControl(new AcquisitionControl { ExposureTime = e })
                        .Bind(DetectUtil.CaptureOne)
                    )
                .Traverse(s => s)
                .Map(s => s.ToArray());
                return aa;
            }
        }

        /// <summary>
        /// 拍摄前开启同步
        /// </summary>
        /// <returns></returns>
        private async Task TurnOnWordopBeforeCaptureAsync()
        {
            // 原逻辑（保留）：只走 DetectUtil.UseLight（CST）
            // 新逻辑：若 Wordop 已连接，拍照前先按配置点亮 1-4 通道
            if (!wordopLightService.IsConnected)
            {
                return;
            }

            try
            {
                await wordopLightService.ApplyConfiguredBrightnessAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Wordop拍照前亮灯失败，继续执行拍照流程");
            }
        }

        /// <summary>
        /// 关闭捕获后同步
        /// </summary>
        /// <returns></returns>
        private async Task TurnOffWordopAfterCaptureAsync()
        {
            // 原逻辑（保留）：由 DetectUtil.UseLight finally 灭灯（CST）
            // 新逻辑：额外确保 Wordop 1-4 通道在拍照后全部熄灭
            if (!wordopLightService.IsConnected)
            {
                return;
            }

            try
            {
                await wordopLightService.TurnOffAllChannelsAsync();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Wordop拍照后灭灯失败");
            }
        }
    }

}
