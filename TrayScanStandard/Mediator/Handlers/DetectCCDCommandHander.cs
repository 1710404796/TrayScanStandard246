using Camera.Fs.Common;
using HKCamera.Fs.NET;
using LanguageExt.Common;
using LinxUniverse.Algo.Common;
using LinxUniverse.Utils;
using MediatR;
using Microsoft.Extensions.Logging;
using MugenCamera;
using MugenCodeDetecter;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Threading;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Mediator.Queries;
using TrayScanStandard.Service;
using TrayScanStandard.ViewModel;
using VMWebAIClient;

namespace TrayScanStandard.Mediator.Handlers
{
    internal class DetectCCDCommandHander
        (ScanCameraService scanCameraService, 
        ILogger<ScanCameraService> logger, 
        IMediator mediator,
        IVMWebAIClient vMWebAIClient,
        ImageDisplayViewModel imageDisplayViewModel): IRequestHandler<DetectCCDCommand, Either<string, DetectResult>>
    // 这个就是默认全部的
    {
        public async Task<Either<string, DetectResult>> Handle(DetectCCDCommand request, CancellationToken cancellationToken)
        {

            if (request.BatteryTypeInfo == null)
            {
                return Either<string, DetectResult>.Left("电池信息为空");
            }
            var captureInfos = scanCameraService.MugenCameras       // 相机设备列表       Zip:数据配对
                .Zip(scanCameraService.Image2DViewModels.Select(s => s.CameraSetting))          // 从Image2DViewModels提取CameraSetting,生成结果(MugenCameras,CameraSetting)元组序列
                .Map(s => s.Item1.Map(c => new CaptureInfo(c, s.Item2.Exposure))).ToArray();    // 外层的Map对每个配对的元组进行转换，内层的s.Iteml.Map对单个相机设备进行转换
                                                                                                // 为每个相机设备创建CaptureInfo对象，传入相机设备（c）和对应的曝光设置数组（s.Item2.Exposure）

            // 执行拍照命令
            var data = await mediator.Send(new CamCaptureCommand(captureInfos));
            Console.WriteLine("testData");


            // 将图片存为文件
            //data.IfRight(s => { Console.WriteLine(s.Count()); });
            //data.IfRight(s => { Console.WriteLine(s.FirstOrDefault().Length.ToString() ?? "dani"); });


            // 实现数据帧暂存

            var data2DPath = Path.Combine(FilenameHelper.AppPath, "Data2D");
            if (!Directory.Exists(data2DPath))
            {
                Directory.CreateDirectory(data2DPath);
            }

            var dataFilenames = data.Map((IEnumerable<Image2DResult[]> d) =>
                                        d.Map(
                                            (ci, s) =>         // ci-Camera Index相机索引代表第几个相机，s是该相机的多张图片
                                                s.Map((ei, s1) =>       // ei-Exposure Index 曝光索引代表第几次曝光索引，s1是单张图片
                                                {
                                                    var name = Path.Combine(data2DPath, $"{FilenameHelper.FileName}_{ci}_{ei}.png");
                                                    File.WriteAllBytes(name, s1.Data);
                                                    return name;
                                                }).ToArray()
                                            ).ToArray()
                                    );
            ;
            Console.WriteLine("testdataFilenames");
            dataFilenames.IfRight(s => { Console.WriteLine(s.Count()); });
            dataFilenames.IfRight(s => { Console.WriteLine(s.FirstOrDefault()?.Count().ToString() ?? "dani"); });
            var tempResult = new DetectResult(
                Enumerable.Range(0, request.BatteryTypeInfo.Count)
                          .Select(s => new CodeInfo($"Test{s:000}", s, new())).ToArr()
                );

            dataFilenames.IfRight(f =>
            {
                var d = f.Select(s => s.First());
                mediator.Send(new PushImgCommand(d.ToArray()));
            });
            var res = (await dataFilenames.BindAsync(
                    camImgs =>
                        camImgs
                        .Zip(request.BatteryTypeInfo.Regions.Take(MainStorage.Saves.CameraCount))
                        .Map(
                            imgs => imgs.Item1
                                .Map(s =>
                                        ( s, imgs.Item2
                                                .Map(s => s.ToROI())
                                                .ToArray()
                                        ) 
                                    )
                                .Map(s =>
                                {
                                    logger.LogInformation("检测{idx}图片", s.s);
                                    // 解析二维码
                                    return vMWebAIClient.DetectCodesV1Async(s.s, s.Item2);
                                })
                                .TraverseSerial(s => s)
                                .Map(s =>
                                    s.Traverse(s1 => s1.Codes)
                                    .Map(s1 => s1.SelectMany(s => s))
                                )
                            //.Map(s => s.SelectMany(d => d.Codes).DistinctBy(d => d.Index)) // 看看要不要考虑重复位置
                            )
                        .TraverseSerial(s => s)
                        .Map(s =>
                                s.Traverse(s1 => s1)
                        //.Map(s1 => s1.SelectMany(s2 => s2))
                        )
                    // 这里merge一下
                    ))
                //.Map(res => res.DistinctBy(s => s.Index))

                ;

            logger.LogInformation("识别完毕，待推送");
            res.Iter(s =>
                    s.Zip(scanCameraService.Image2DViewModels)
                    .Iter(d =>
                    {
                        d.Item2.TempResult = new CodeDetectResult(d.Item1.ToArr());
                    })
            );
            logger.LogInformation("推送完成 获取结果");

            var res1 = res.Map(r =>
                r.SelectMany(s => s)
                .DistinctBy(s => s.Index)
                ).Map(s => new DetectResult(s.ToArr()));



            logger.LogInformation("保存数据帧");

            //from f in dataFilenames
            //from detectResult in res1


            // 保存数据帧
            dataFilenames.IfRight(f =>
            {
                res1.IfRight(async detectResult =>
                {
                    // 构建CamImages数组
                    try
                    {
                        var camImages = f.Select((imageFiles, cameraIndex) =>
                        {
                            // 获取相机序列号，如果没有则使用默认值
                            var cameraSerial = $"Camera_{cameraIndex + 1}";
                            if (cameraIndex < scanCameraService.MugenCameras.Length)
                            {
                                var camera = scanCameraService.MugenCameras[cameraIndex];
                                // 这里假设MugenCamera有Serial属性，如果没有则使用默认序列号
                                cameraSerial = camera.Match(
                                    cam => ((cam as HikVision)?.Cam?.Address as Key)?.Value ?? $"Camera_{cameraIndex + 1}", // 如果相机存在，使用索引作为序列号
                                    () => $"Camera_{cameraIndex + 1}"   // 如果相机不存在，使用默认序列号
                                );
                            }
                            // 获取当前相机的曝光设置数组（每个相机的曝光设置是固定的）
                            var cameraExposureArray = scanCameraService.Image2DViewModels[cameraIndex].CameraSetting.Exposure;

                            var imageInfos = imageFiles.Select((imagePath, imageIndex) =>
                            {
                                // 从曝光数组中选择对应的曝光值，如果索引超出范围则使用第一个值
                                var exposure = imageIndex < cameraExposureArray.Length ? cameraExposureArray[imageIndex] : cameraExposureArray[0];
                                return new ImageInfo(imagePath, exposure);
                            }).ToArray();

                            return new CamImages(cameraSerial, imageInfos);
                        }).ToArray();

                        // 构建CodeInfo数组，直接使用detectResult.Channels中的CodeInfo
                        var codeInfos = detectResult.Channels.ToArray();

                        // 创建数据帧
                        var dataFrame = new DataFrame(camImages, request.BatteryTypeInfo, codeInfos);
                        await mediator.Send(new SaveDataFrameCommand(dataFrame));

                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "存数据帧错误");
                    }
                    

                    // 发送保存命令
                });
            });

            // 清空一下？
            imageDisplayViewModel.XYLStation.ClearStage();
            logger.LogInformation("清空数据，推送新数据");
            //Dispatcher.CurrentDispatcher.Invoke(new Action(() =>
            res1.Iter(s =>
                s.Channels.Iter
                    (async c => await imageDisplayViewModel.XYLStation.BindBattery(c.Index - 1, c.Code, true, Models.BatteryLevel.OK)
                    )
             );
            //));
            logger.LogInformation("返回数据");
            foreach (var item in scanCameraService.Image2DViewModels)
            {
                item.Update();
            }
            //dataFilenames.IfRight(f =>
            //{
            //    var d = f.Select(s => s.First());

            //    mediator.Send(new PushImgCommand(d.ToArray()));

            //});
            return res1;
            //).Traverse(s => s)
            //    ));
        }
    }
}
