

using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.Apis
{
    [ApiController]
    [Route("[controller]")]
    public class WcsController(
        LinxContext linxContext, 
        IMediator mediator, 
        ILogger<WcsController> logger
        , MainViewModel mainViewModel
        ): ControllerBase
    {
        //[HttpGet("/CaptureImage")] 北京时代
        //[HttpPost("/Photo")] 福鼎时代
        private const string ERROR = "ERROR";
        [HttpGet("/CaptureImage")]
        public async Task<QRCodeResult> Delect()
        {
            while (!mainViewModel.IsWcsEnable)
            {
                await Task.Delay(1000);
            }
            logger.LogInformation("收到检测任务");

           if (MainStorage.SelectBattery is null) return new QRCodeResult() { ErrorCode = ErrorType.SomeResultError };

            var startTime = DateTime.UtcNow;
            var data = await mediator.Send(new DetectCCDCommand(MainStorage.SelectBattery));
            GC.Collect();
            data.IfRight(
                r =>
                {
                    var codes = r.Channels.ToDictionary(s => s.Index, s => s.Code);
                    var batteryCodes = Enumerable.Range(1, MainStorage.SelectBattery.Count)
                        .Select(s => codes.GetValueOrDefault(s, ""))
                        .ToList();
                    var log = new PalletLog
                    {
                        Column = MainStorage.SelectBattery.Column, // 这里要可派之
                        PalletType = Models.CZPallet.PalletType.组盘,

                        ChannelCount = MainStorage.SelectBattery.Count,
                        BatteryInfo
                            = batteryCodes.Map(s => new BatteryInfo
                            {
                                BatteryCode = s,
                                BatteryLevel = string.IsNullOrEmpty(s) ? Models.BatteryLevel.EMPTY : Models.BatteryLevel.OK
                            })
                            .ToList()

                    };
                    linxContext.PalletLogs.Add(log);
                    linxContext.SaveChanges();
                }
                );

            var elapsed = (DateTime.UtcNow - startTime).TotalSeconds;
            data.Match(
                Right: r =>
                {
                    var successCount = r.Channels.Count(c => !string.IsNullOrEmpty(c.Code));
                    var failCount = r.Channels.Count - successCount;
                    var failChannels = r.Channels.Where(c => string.IsNullOrEmpty(c.Code)).Select(c => c.Index);
                    logger.LogInformation(
                        "WCS扫码任务完成，耗时{Elapsed:F1}秒，成功{SuccessCount}个，未扫到{FailCount}个，未扫到通道[{FailChannels}]",
                        elapsed, successCount, failCount, string.Join(",", failChannels));
                },
                Left: e =>
                {
                    logger.LogInformation("WCS扫码任务失败，耗时{Elapsed:F1}秒，错误：{Error}", elapsed, e);
                }
            );

            //retry<>
            return data.Match(
                Right: r =>
                {
                    var codes = r.Channels.ToDictionary(s => s.Index, s => s.Code);
                    var batteryCodes = Enumerable.Range(1, MainStorage.SelectBattery.Count)
                        .Select(s => codes.GetValueOrDefault(s, ""))
                        .ToList(); // Todo: 如何简化
                    return new QRCodeResult()
                    {
                        ErrorCode = ErrorType.Successed,
                        Data = batteryCodes
                            .Select((s, i) => new { Index = i + 1, Code = s }) //Todo: 看看+1
                            .ToDictionary(s => s.Index, s => s.Code)
                    };
                },
                Left: e =>
                {
                    mediator.Send(new WarningBoxCommand($"检测失败 错误原因:{e}")).Wait();
                    return new QRCodeResult() { ErrorCode = ErrorType.CameraError };
                }
            );
        }
    }

    public enum ErrorType
    {
        Successed,
        CameraError,
        //ERROR,
        SomeResultError,
    }

    public class QRCodeResult
    {
        public ErrorType ErrorCode { get; set; }
        public Dictionary<int, string> Data { get; set; }
    }
}
