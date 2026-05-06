using HKCamera.Fs.NET;
using HKCamera.Fs.NET.Controls;
using LanguageExt;
using LinxUniverse.DI;
using MediatR;
using Microsoft.Extensions.Logging;
using MugenCamera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TrayScanStandard.ViewModel;
using static MvCamCtrl.NET.MyCamera;
namespace TrayScanStandard.Service
{
    public class ScanCameraService(IMediator mediator, ILogger<ScanCameraService> logger, CacheService cacheService)
    {
        public Option<MugenCamera.MugenCamera>[] MugenCameras { get; set; } = [];
        private Thread? _listenThread;
        private CancellationTokenSource? _listenCts;
        private readonly object _initLock = new();
        public BcrBorderViewModel[] BcrBorderViewModels = [];
        public Image2DViewModel[] Image2DViewModels = [];
        public void Init()
        {
            lock (_initLock)
            {
                _listenCts?.Cancel();
                if (_listenThread != null && _listenThread.IsAlive)
                {
                    _listenThread.Join(TimeSpan.FromSeconds(1));
                }

                _listenCts?.Dispose();
                _listenCts = CancellationTokenSource.CreateLinkedTokenSource(cacheService.Token);

                // 遍历以确保所有相机已连接
                var settings = MainStorage.Saves.ConnectAddresses
                    .Take(MainStorage.Saves.CameraCount)
                    .ToArray();
                logger.LogInformation($"开始初始化相机，目标数量: {settings.Length}");
                var cameras = settings
                    .Select((setting, index) => InitCameraWithLog(setting, index + 1, "startup"))
                    .ToArray();
                //MugenCameras = cameras.Match
                MugenCameras = [.. cameras.Map(c => c.ToOption())];

                Image2DViewModels =
                [.. settings.Map((i, s) => new Image2DViewModel() {
                    CameraSetting = s,
                    CameraIdx = i + 1,
                    Service = this
                })];

                BcrBorderViewModels = [.. Image2DViewModels.Map(s => new BcrBorderViewModel() { Image2DViewModel = s })];
                _listenThread = new Thread(() => Listen(_listenCts.Token));
                _listenThread.Start();
            }
        }

        public Option<MugenCamera.MugenCamera> GetMugen(int idx) => MugenCameras[idx - 1];

        /// <summary>
        /// 单相机连接
        /// </summary>
        /// <param name="cameraIdx"></param>
        /// <returns></returns>
        public Either<string, bool> ReconnectCamera(int cameraIdx)
        {
            lock (_initLock)
            {
                if (cameraIdx <= 0 || cameraIdx > MainStorage.Saves.CameraCount)
                {
                    return Left($"相机索引越界: {cameraIdx}");
                }

                int arrayIdx = cameraIdx - 1;
                //if (arrayIdx >= MainStorage.Saves.ConnectAddresses.Length)
                //{
                //    return Left($"相机配置不存在: {cameraIdx}");
                //}

                var setting = MainStorage.Saves.ConnectAddresses[arrayIdx];
                //if (setting.CameraAddresses is not HKAddress && setting.CameraAddresses is not HuaruiAddress)
                //{
                //    return Left($"相机[{cameraIdx}]当前类型不支持重连，仅支持海康/华睿");
                //}

                var result = InitCameraWithLog(setting, cameraIdx, "manual_reconnect");
                return result.Match(
                    Right: camera =>
                    {
                        if (arrayIdx < MugenCameras.Length)
                        {
                            MugenCameras[arrayIdx] = Some(camera);
                        }

                        if (arrayIdx < BcrBorderViewModels.Length)
                        {
                            BcrBorderViewModels[arrayIdx].IsConnect = camera.IsConnect();
                        }

                        logger.LogInformation("手动重连相机[{CameraIdx}]成功", cameraIdx);
                        return Right<string, bool>(true);
                    },
                    Left: err =>
                    {
                        if (arrayIdx < MugenCameras.Length)
                        {
                            MugenCameras[arrayIdx] = Option<MugenCamera.MugenCamera>.None;
                        }

                        if (arrayIdx < BcrBorderViewModels.Length)
                        {
                            BcrBorderViewModels[arrayIdx].IsConnect = false;
                        }

                        logger.LogWarning("手动重连相机[{CameraIdx}]失败: {Error}", cameraIdx, err);
                        return Left<string, bool>(err);
                    });
            }
        }

        private static Either<string, MugenCamera.MugenCamera> InitCamera(TrayScanStandard.Models.CameraSetting s)
        {
            return s.CameraAddresses.Create()
                .Bind(MugenCameraExtensions.Connect)
                .Bind(s =>
                    {
                        switch (s)
                            {
                                // 海康相机
                                case HikVision:
                                    return s.SetControl(new AcquisitionControl
                                        {
                                            TriggerMode = MV_CAM_TRIGGER_MODE.MV_TRIGGER_MODE_ON,
                                            TriggerSource = MV_CAM_TRIGGER_SOURCE.MV_TRIGGER_SOURCE_SOFTWARE
                                        });
                                // 华睿相机
                                 case HuaruiCam:
                                    return s.SetControl(new HuaRui.Fs.NET.Controls.AcquisitionControl
                                        {
                                            TriggerMode = HuaRui.Fs.NET.Controls.TriggerModel.On,
                                            TriggerSource = HuaRui.Fs.NET.Controls.TriggerSource.Software
                                        });
                                 default:
                                    return Right(s);
                             };
                                    //return s.SetControl(c);
                                }).Bind(s => s.SetControl(new
                                {
                                    // 设置一下心跳
                                    GevHeartbeatTimeout = (long?)5000
                                }));
        }

        private async void Listen(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                MugenCameras = MugenCameras.Map((i, s) =>
                {
                    return s.Bind(camera =>
                    {
                        var checkResult = camera.CheckConnect();
                        checkResult.IfLeft(error =>
                            logger.LogWarning($"相机[{i + 1}]连接检测失败: {error}" ));
                        return checkResult.ToOption();
                    });
                }).ToArray();

                MugenCameras = MugenCameras.Map((i, s) =>
                {
                    var address = MainStorage.Saves.ConnectAddresses[i];
                    return s.Match(
                        Some: camera =>
                        {
                            if (camera.IsConnect())
                            {
                                return Right(camera);
                            }
                            else
                            {
                                logger.LogWarning($"相机[{i + 1}]已断开，尝试重连");
                                return InitCameraWithLog(address, i + 1, "reconnect");
                            }
                        },
                        None: () =>
                        {
                            // logger.LogWarning("相机[{CameraIdx}]无可用实例，尝试重建连接", i + 1);
                            return InitCameraWithLog(address, i + 1, "recreate");
                        }).ToOption();
                }).ToArray();

                MugenCameras.Iter((i, s) =>
                {
                    var vm = BcrBorderViewModels[i];

                    vm.IsConnect = s.Match(
                        Some: s =>
                        {
                            return s.IsConnect();
                        },
                        None: () =>
                        {
                            return false;
                        });
                });

                try
                {
                    await Task.Delay(10000, token);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        private Either<string, MugenCamera.MugenCamera> InitCameraWithLog(TrayScanStandard.Models.CameraSetting setting, int cameraIdx, string stage)
        {
            var addressText = GetAddressText(setting);
            //if (string.IsNullOrWhiteSpace(addressText))
            //{
            //    logger.LogWarning("相机[{CameraIdx}]连接参数为空，阶段: {Stage}", cameraIdx, stage);
            //}

            var result = InitCamera(setting);
            result.Match(
                Right: _ =>
                {
                    logger.LogInformation($"相机[{cameraIdx}]连接成功", string.IsNullOrWhiteSpace(addressText) ? "<empty>" : addressText);
                    return 0;
                },
                Left: error =>
                {
                    logger.LogWarning($"相机[{cameraIdx}]连接失败，错误: {error}",string.IsNullOrWhiteSpace(addressText) ? "<empty>" : addressText, error);
                    return 0;
                });

            return result;
        }

        private static string GetAddressText(TrayScanStandard.Models.CameraSetting setting)
        {
            return setting.CameraAddresses switch
            {
                HKAddress hk => hk.ConnectAddress switch
                {
                    Camera.Fs.Common.Key key => key.Value,
                    Camera.Fs.Common.IPAddress ip => ip.Value,
                    Camera.Fs.Common.Serial serial => serial.Value,
                    _ => string.Empty
                },
                HuaruiAddress hr => hr.ConnectAddress switch
                {
                    Camera.Fs.Common.Key key => key.Value,
                    Camera.Fs.Common.IPAddress ip => ip.Value,
                    Camera.Fs.Common.Serial serial => serial.Value,
                    _ => string.Empty
                },
                _ => string.Empty
            };
        }

    }
}
