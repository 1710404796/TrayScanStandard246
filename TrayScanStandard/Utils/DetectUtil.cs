using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Camera.Fs.Common;
using LinxUniverse.CST;
using MugenCamera;
namespace TrayScanStandard.Utils
{    public static class DetectUtil
    {

         /// <summary>
         /// 强制关闭所有光源（可在超时或异常后独立调用，不依赖 UseLight 的 finally）
         /// </summary>
         public static void TurnOffLights()
         {
             ControlLights(false);
         }
 
         // 还需要一个光源的中间件

        /// <summary>
        /// 控制光源的开启和关闭
        /// </summary>
        /// <param name="turnOn">true为开启光源，false为关闭光源</param>
        private static void ControlLights(bool turnOn)
        {
            var action = turnOn ? "开启" : "关闭";
            var st = Environment.TickCount;

            if (!MainStorage.CST.Any())
            {
                if (MainStorage.Saves.LightInfos.Any())
                {
                    Serilog.Log.Warning("[DetectUtil.ControlLights] 光源{Action}: 已配置{Count}个光源但CST串口连接为空，请检查InitCST", action, MainStorage.Saves.LightInfos.Length);
                }
                else
                {
                    Serilog.Log.Information("[DetectUtil.ControlLights] 光源{Action}: 未配置光源，跳过", action);
                }
                return;
            }

            var lightInfos = MainStorage.Saves.LightInfos.Zip(MainStorage.CST);
            var successCount = 0;
            var failCount = 0;

            Serilog.Log.Information("[DetectUtil.ControlLights] 光源{Action}开始: LightInfos={Infos}, CST连接={Csts}", action, MainStorage.Saves.LightInfos.Length, MainStorage.CST.Count());

            lightInfos.Iter(s =>
            {
                var com = s.Item1.Com;
                var type = s.Item1.ControllerType;
                var cst = s.Item2;

                if (cst == null)
                {
                    failCount++;
                    Serilog.Log.Warning("[DetectUtil.ControlLights]    CST实例为null，跳过 (COM={Com}, 型号={Type})", com, type);
                    return;
                }

                s.Item1.Values.Iter((i, v) =>
                {
                    var ch = i + 1;
                    var targetValue = turnOn ? v : 0;
                    var ok = false;
                    try
                    {
                        ok = cst.SetLight(ch, targetValue);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "[DetectUtil.ControlLights]    CH{Ch}={Value} 异常，跳过（光源可能未连接） (COM={Com}, 型号={Type})", ch, targetValue, com, type);
                    }
                    if (ok)
                    {
                        successCount++;
                        Serilog.Log.Information("[DetectUtil.ControlLights]    CH{Ch}={Value} 成功 (COM={Com}, 型号={Type})", ch, targetValue, com, type);
                    }
                    else
                    {
                        failCount++;
                        Serilog.Log.Warning("[DetectUtil.ControlLights]    CH{Ch}={Value} 失败!! (COM={Com}, 型号={Type})", ch, targetValue, com, type);
                    }

                    // 通道间添加小延迟，避免部分 Wordop 控制器因命令过于密集而丢失指令
                    System.Threading.Thread.Sleep(20);
                });
            });

            var elapsed = Environment.TickCount - st;
            Serilog.Log.Information("[DetectUtil.ControlLights] 光源{Action}完成: 成功{Success}个, 失败{Fail}个, 耗时={Elapsed}ms", action, successCount, failCount, elapsed);
        }        /// <summary>
        /// 在开启光源的环境下执行同步操作
        /// </summary>
        public static Either<string, TResult> UseLight<TResult>
            (Func<Either<string, TResult>> func)
        {
            var cstCount = MainStorage.CST.Count();
            var infoCount = MainStorage.Saves.LightInfos.Length;
            Serilog.Log.Information("[DetectUtil.UseLight] 准备执行操作: LightInfos={Infos}, CST连接={Csts}", infoCount, cstCount);

            ControlLights(true);
            try
            {
                var result = func();
                result.Match(
                    Right: _ => { },
                    Left: err =>
                    {
                        if (infoCount > 0 && cstCount == 0)
                        {
                            Serilog.Log.Error("[DetectUtil.UseLight] 操作失败: [{Error}]。检测到已配置{Count}个光源但CST未初始化，光源未正常触发！", err, infoCount);
                        }
                        else
                        {
                            Serilog.Log.Warning("[DetectUtil.UseLight] 操作失败: [{Error}]。光源已正常操作(CST={CstCount})，问题可能在其他环节", err, cstCount);
                        }
                    }
                );
                return result;
            }
            finally
            {
                try
                {
                    ControlLights(false);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "[DetectUtil.UseLight] 关闭光源时发生异常，将重置 CST 连接");
                    MainStorage.InitCST();
                }
            }
        }

        /// <summary>
        /// 在开启光源的环境下执行异步操作
        /// </summary>
        public static async Task<Either<string, TResult>> UseLight<TResult>
            (Func<Task<Either<string, TResult>>> func)
        {
            var cstCount = MainStorage.CST.Count();
            var infoCount = MainStorage.Saves.LightInfos.Length;
            Serilog.Log.Information("[DetectUtil.UseLight] 准备执行操作(异步): LightInfos={Infos}, CST连接={Csts}", infoCount, cstCount);

            ControlLights(true);
            try
            {
                var result = await func();
                result.Match(
                    Right: _ => { },
                    Left: err =>
                    {
                        if (infoCount > 0 && cstCount == 0)
                        {
                            Serilog.Log.Error("[DetectUtil.UseLight] 操作失败(异步): [{Error}]。检测到已配置{Count}个光源但CST未初始化，光源未正常触发！", err, infoCount);
                        }
                        else
                        {
                            Serilog.Log.Warning("[DetectUtil.UseLight] 操作失败(异步): [{Error}]。光源已正常操作(CST={CstCount})，问题可能在其他环节", err, cstCount);
                        }
                    }
                );
                return result;
            }
            finally
            {
                try
                {
                    ControlLights(false);
                }
                catch (Exception ex)
                {
                    Serilog.Log.Error(ex, "[DetectUtil.UseLight] 关闭光源时发生异常(异步)，将重置 CST 连接");
                    MainStorage.InitCST();
                }
            }
        }        public static Either<string, TResult> UseCamera<TResult>(
            MugenCamera.MugenCamera mugenCamera,
            Func<MugenCamera.MugenCamera, Either<string, TResult>> action)
        {
            try
            {
                mugenCamera.StopGrab();
            }
            catch
            {
                // StopGrab may throw if camera disconnected; ignore and continue
            }
            var camSession = mugenCamera.StartGrab();
            // Execute the action only if StartGrab was successful
            var result = camSession.Bind(action);
            // Ensure StopGrab is called if StartGrab succeeded, regardless of action result
            camSession.Bind(s => s.StopGrab());
            return result;
        }


        public static Either<string, Image2DResult> CaptureOne(this MugenCamera.MugenCamera mugenCamera)
        {
            // Define the specific action: Software Trigger then Capture

            return 
                UseCamera(mugenCamera, 
                    cam =>
                    cam.SoftwareTrigger() // Assuming SoftwareTrigger returns Either<string, MugenCamera>
                       .Bind(s => s.Capture(TimeSpan.FromSeconds(3)).Map(s => (s as Image2DResult)!)));
            // Use the helper method to execute the action within a session
            //return ;
        }

        //public static Either<string, ImageData> CaptureOne(MugenCamera.MugenCamera mugenCamera)
        //{
        //    var cam = mugenCamera.StartGrab();
        //    var img = cam.Bind(MugenCameraExtensions.SoftwareTrigger)
        //        .Bind(s => s.Capture(TimeSpan.FromSeconds(3)));
        //    cam = cam.Bind(s => s.StopGrab());
        //    return cam.Bind(s => img);
        //}
        public static Either<string, IEnumerable<Image2DResult>> Capture(MugenCamera.MugenCamera[] mugenCameras)
        {
            return UseLight(
                () =>
                {
                    var results = mugenCameras
                        //.AsParallel()
                        //.AsOrdered()
                        .Select(cam => cam.CaptureOne())
                        .Traverse(s => s);
                    return results;
                });
            // 让所有相机拍个照
        }


        public static void CamCapture()
        {
            // 让所有相机拍个照

        }

        // 光源中间件‘

        public static void LightMiddle<T>(Func<T> f)
        {
            // 让所有光源中间件都亮
        }
    }
}
