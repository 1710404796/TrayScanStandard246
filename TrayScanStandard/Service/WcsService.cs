using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LanguageExt.Common;
using MediatR;
using Microsoft.Extensions.Logging;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Service.Models;

namespace TrayScanStandard.Service
{
    public class WcsService (HttpClient httpClient, ILogger<WcsService> logger, IMediator mediator, LinxContext linxContext)
    {

        // 高阶函数包裹一下 // 这是中间件吗
        public static Func<TIn, Task<TOut>> MakeApi<TIn, TOut>
            (Func<TIn, Task<TOut>> func, Func<TIn, TOut> failResult, Func<bool> enable)
            => enable() ? func : (data) => Task.FromResult(failResult(data));


        public string BaseUrl => $"http://{MainStorage.Saves.StageSetting.FMSAddress}/{MainStorage.Saves.StageSetting.AppName}/restful/api/v3/";

        public Task<Result<GrabActionResponse>> GrabActionAsync(GrabActionRequest data)
        {
            return LogMiddleWare<GrabActionResponse>("wcs/grabAction", data);
        }

        public Task<Result<MaterialOnlineResponse>> MaterialOnlineAsync(MaterialOnlineRequest data)
        {
            return LogMiddleWare<MaterialOnlineResponse>("wcs/materialOnline", data);
        }
        object _lockobj = new();
        public async Task<Result<QueryAssembleInfoResponse>> QueryAssembleInfoAsync(QueryAssembleInfoRequest data)
        {
            var res = await LogMiddleWare<QueryAssembleInfoResponse>("wcs/queryAssembleInfo", data);
            // 假电芯通知
            //if (res?.MaterialType == "1")
            //{
            //    lock (_lockobj)
            //    {
            //        _fakeCellSet.Add(data.ContainerCode);
            //    }
            //    //_fakeCellSet.Add(data.ContainerCode);
            //}

            return res;
        }

        public Task<Result<GrabResponse>> GrabAsync(GrabRequest data)
        {
            return LogMiddleWare<GrabResponse>("wcs/grab", data);
        }

        public Task<Result<PostDeviceStatusResponse>> PostDeviceStatusAsync(PostDeviceStatusRequest data)
        {
            return LogMiddleWare<PostDeviceStatusResponse>("inventory/postDeviceStatus", data);
        }

        public Task<Result<DeviceErrorResponse>> DeviceErrorAsync(DeviceErrorRequest data)
        {
            return LogMiddleWare<DeviceErrorResponse>("issue/deviceError", data);
        }

        public Task<Result<AskFakeCellResponse>> AskFakeCellResponse(AskFakeCellRequest data)
        {
            return LogMiddleWare<AskFakeCellResponse>("wcs/askFakeCell", data);
        }

        public Task<Result<MoveFakeCellResponse>> MoveFakeCellResponse(MoveFakeCellRequest data)
        {
            return LogMiddleWare<MoveFakeCellResponse>("wcs/moveFakeCell", data);
        }

        System.Collections.Generic.HashSet<string> _fakeCellSet = [];
        public async Task WaitForFakeCell(string stationName, CancellationToken cancellationToken)
        {
            string fakeCell = string.Empty;
            var request = new AskFakeCellRequest()
            {
                StationCode = stationName
            };

            //do
            //{
            //    var data = await AskFakeCellResponse(request);
            //    if (data != null)
            //    {
            //        fakeCell = data.ContainerCode;
            //        //fakeCell = data.data;
            //    }
            //    await Task.Delay(TimeSpan.FromSeconds(MainStorage.Saves.StageSetting.WaitFakeTime));

            //} while (!cancellationToken.IsCancellationRequested || _fakeCellSet.Contains(fakeCell));
            //lock (_lockobj)
            //{
            //    _fakeCellSet.Remove(fakeCell);
            //}
        }
        static object _lockobjdb = new object();

        public async Task<Result<T>> LogMiddleWare<T>(string api, object data) where T : ILinxResponse
        {
            LinxContext linxContext = App.GetService<LinxContext>();
            DateTime reqTime = DateTime.Now;
            string req = JsonSerializer.Serialize(data);

            logger.LogInformation("请求url: {url}", api);
            logger.LogInformation("请求MES数据: {data}", req);

            var data1 = await SendRequest<T>(api, data);

            WcsLog log = data1.Match(
                Succ: s =>
                    new WcsLog
                    {
                        IsSuccess = true,
                        ApiName = api,
                        RequestTime = reqTime,
                        Request = req,
                        Response = JsonSerializer.Serialize(s),
                        ResponseTime = DateTime.Now,
                    },
                Fail: f =>
                    new WcsLog
                    {
                        IsSuccess = false,
                        ApiName = api,
                        RequestTime = reqTime,
                        Request = req,
                        Response = f.Message,
                        ResponseTime = DateTime.Now,
                    }

                );

            //if 
            // 这里是否存日志要判断一下
            try
            {
                linxContext.WcsLogs.Add(log);
                linxContext.SaveChanges();
            }
            catch (Exception ex)
            {

                logger.LogError(ex, "数据发生错误");
            }
            var mediator = App.GetService<IMediator>();
            if (data1.IsSuccess)
            {
                return data1;
            }
            else
            {
                var err = data1.Match(Succ: s => "", Fail: f => f.Message);
                var wres = await mediator.Send(new WcsDataWarningBoxCommand
                        ("Name" + "工作站\n" + $"MES返回错误信息，发生问题 Error:" + err + "\n点击ok重新发送此请求，点击cancel终止此任务！",
                        Button: System.Windows.MessageBoxButton.OKCancel));
                if (wres == System.Windows.MessageBoxResult.OK)
                {
                    return await SendRequest<T>(api, data);
                }
                else
                {
                    return data1;
                }
            }
        }
        public async Task<Result<T>> SendRequest<T>(string api, object data) where T : ILinxResponse
        {
            //string _url = $"http://{MainStorage.Saves.KeysSave[Name].MESIP}/core/api/public/{api}";

            logger.LogInformation("请求MES地址: {url}", api);

            try
            {
                var res = await httpClient.PostAsJsonAsync(api, data);

                if (res.IsSuccessStatusCode)
                {
                    logger.LogInformation("请求MES成功");
                    var resdata = await res.Content.ReadAsStringAsync();

                    logger.LogInformation("MES返回数据: {resdata}", resdata);
                    var response = JsonSerializer.Deserialize<T>(resdata);
                    if (response != null)
                    {
                        if (response.Success == false)
                        {
                            return new(new MesException(response.Message));

                        }
                        return response;
                    }
                    else
                    {
                        return new(new MesException("json为空"));
                    }
                }
                else
                {
                    return new(new MesException($"请求MES失败, {res.StatusCode}"));
                }
            }
            catch (Exception ex)
            {
                return new(new MesException($"请求MES异常, {ex.Message}"));
            }


        }

        [Obsolete]
        public async Task<T?> SendRequestAsync<T>(string apiName, object? data = null) where T : IWcsResponse
        {
            // 加个日志中间件
            string requestStr = JsonSerializer.Serialize(data);
            var type = typeof(T);

            WcsLog log = new();

            log.ApiName = apiName;
            log.Request = requestStr;
            bool shouldLog = type != typeof(PostDeviceStatusResponse);
            if (shouldLog)
            {
                logger.LogInformation("SendRequestAsync: {apiname} data: {data}", apiName, requestStr);
                linxContext.WcsLogs.Add(log);

            }
            try
            {


                var response = await httpClient.PostAsJsonAsync($"{BaseUrl}{apiName}", data);
                if (shouldLog)
                {
                    //logger.LogInformation("请求状态: {status}", response.StatusCode);
                    logger.LogInformation(Properties.Resources.RequestStatus, "{status}", response.StatusCode);

                }
                if (response.IsSuccessStatusCode)
                {
                    var resStr = await response.Content.ReadAsStringAsync();
                    if (shouldLog)
                    {
                        logger.LogInformation("response: {apiname} data: {data}", apiName, resStr);
                    }
                    var resData = JsonSerializer.Deserialize<T>(resStr);
                    if (resData == null)
                    {
                        throw new Exception(Properties.Resources.ErrorAccessingInterfaceResultIsNull);
                    }
                    log.IsSuccess = true;
                    log.ResponseTime = DateTime.Now;
                    //string resStr = JsonSerializer.Serialize(resData);
                    log.Response = resStr;

                    if (resData.ResponseCode != 0)
                    {
                        if (shouldLog)
                            await mediator.Send(new WcsDataWarningBoxCommand(resData.ResponseMessage));

                    }
                    else
                    {

                        lock (_lockobjdb)
                        {
                            linxContext.SaveChanges();
                        }
                        return resData;
                    }


                }
            }
            catch (Exception ex)
            {
                log.Response = ex.Message;
                if (shouldLog)
                    await mediator.Send(new WcsDataWarningBoxCommand(ex.Message));
                //throw;
            }
            finally
            {

            }
            lock (_lockobjdb)
            {
                if (shouldLog)
                {
                    linxContext.SaveChanges();

                }
            }
            return default;
        }
        public class MesException : Exception
        {
            public MesException()
            {
            }

            public MesException(string? message) : base(message)
            {
            }

            public MesException(string? message, Exception? innerException) : base(message, innerException)
            {
            }

            protected MesException(SerializationInfo info, StreamingContext context) : base(info, context)
            {
            }
        }

    }
}
