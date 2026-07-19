using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using LinxUniverse.Algo.Common;
using LinxUniverse.CST;
using LinxUniverse.Utils;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Models;

namespace TrayScanStandard
{
    public static class MainStorage
    {
        public static SaveManager<WcsSaves> SaveManager = new();
        public static WcsSaves Saves => SaveManager.SaveFile;

        public static bool IsWcsEnable { get; set; }
        public static Either<string, CodeDetecter> Algo { get; set; }
        public static Either<string, CodeDetecter> AlgoCnn { get; set; }
        public static int[] DefaultExp = [7500, 15000, 22500, 30000];
        public static PowerEnum[] PowerEnums => Enum.GetValues<PowerEnum>().ToArray();
        public static RoleEnum[] RoleEnums => Enum.GetValues<RoleEnum>().SkipLast(1).ToArray();
        public static BatteryTypeInfo? SelectBattery { get; set; }
        public static IEnumerable<LightCST> CST { get; set; } = [];

        public static void Init()
        {
            SaveManager.Load();
            InitCST();
        }

        /// <summary>
        /// 从配置的光源信息初始化 CST 串口连接，确保 DetectUtil.ControlLights 能正常控制光源
        /// </summary>
        public static void InitCST()
        {
            // 先释放旧的串口连接
            foreach (var old in CST)
            {
                try { old.Dispose(); } catch { /* ignore */ }
            }

            var lights = new List<LightCST>();
            foreach (var info in Saves.LightInfos)
            {
                var cst = new LightCST();
                var match = Regex.Match(info.Com, @"\d+");
                if (!match.Success)
                {
                    cst.Dispose();
                    Serilog.Log.Warning("[MainStorage.InitCST] 无法从COM端口名解析编号: {Com}", info.Com);
                    continue;
                }
                var comIdx = int.Parse(match.Value);

                bool ok;
                if (string.Equals(info.ControllerType, "Wordop", StringComparison.OrdinalIgnoreCase))
                {
                    ok = cst.InitializeWordop(comIdx);
                }
                else
                {
                    ok = cst.Initialize(comIdx);
                }

                if (ok)
                {
                    lights.Add(cst);
                    Serilog.Log.Information("[MainStorage.InitCST] 光源初始化成功: COM={Com}, 型号={Type}, 亮度=[{Values}], comIdx={Idx}", info.Com, info.ControllerType, string.Join(", ", info.Values), comIdx);
                }
                else
                {
                    cst.Dispose();
                    Serilog.Log.Warning("[MainStorage.InitCST] 光源初始化失败: COM={Com}, 型号={Type}, comIdx={Idx}", info.Com, info.ControllerType, comIdx);
                }
            }

            CST = lights;
            if (lights.Count > 0)
            {
                Serilog.Log.Information("[MainStorage.InitCST] ===== 光源连接完成, 共 {Count} 个 =====", lights.Count);
                foreach (var info in Saves.LightInfos)
                {
                    Serilog.Log.Information("[MainStorage.InitCST]    COM: {Com}, 型号: {Type}, 亮度: [{Values}]", info.Com, info.ControllerType, string.Join(", ", info.Values));
                }
            }
            else if (Saves.LightInfos.Length > 0)
            {
                Serilog.Log.Warning("[MainStorage.InitCST] 已配置 {Count} 个光源，但全部初始化失败，请检查串口连接", Saves.LightInfos.Length);
            }
            else
            {
                Serilog.Log.Information("[MainStorage.InitCST] 未配置光源，跳过初始化");
            }
        }
    }
}
