using Quartz;
using TrayScanStandard.Service;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.Jobs
{
    /// <summary>
    /// 心跳工作
    /// </summary>
    /// <param name="wcsTrayScanStandardServer"></param>
    /// <param name="scanCameraService"></param>
    /// <param name="mainViewModel"></param>
    internal class HeartbeatJobs(
        WcsTrayScanStandardServer wcsTrayScanStandardServer,
        ScanCameraService scanCameraService,
        MainViewModel mainViewModel) : IJob
    {
        public Task Execute(IJobExecutionContext context)
        {
            if (!mainViewModel.IsWcsEnable)
            {
                return Task.CompletedTask;
            }

            var data = scanCameraService.Image2DViewModels
                .Take(MainStorage.Saves.CameraCount)
                .Select((viewModel, index) => GetHeartbeatCode(viewModel, index))
                .ToList();

            wcsTrayScanStandardServer.SendHeartbeat(new Models.HeartbeatsRequest { Heartbeat = data });
            return Task.CompletedTask;
        }

        /// <summary>
        /// 读取心跳码，优先使用连接地址，如果没有则使用默认的ccd1、ccd2、ccd3、ccd4
        /// </summary>
        /// <param name="viewModel"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        private static string GetHeartbeatCode(Image2DViewModel viewModel, int index)
        {
            var key = viewModel.CameraSetting.CameraAddresses switch
            {
                MugenCamera.HKAddress hk => hk.ConnectAddress switch
                {
                    Camera.Fs.Common.Key connectKey => connectKey.Value,
                    Camera.Fs.Common.IPAddress ip => ip.Value,
                    Camera.Fs.Common.Serial serial => serial.Value,
                    _ => null
                },
                MugenCamera.HuaruiAddress hr => hr.ConnectAddress switch
                {
                    Camera.Fs.Common.Key connectKey => connectKey.Value,
                    Camera.Fs.Common.IPAddress ip => ip.Value,
                    Camera.Fs.Common.Serial serial => serial.Value,
                    _ => null
                },
                _ => null
            };

            return string.IsNullOrWhiteSpace(key) ? $"ccd{index + 1}" : key;
        }
    }
}
