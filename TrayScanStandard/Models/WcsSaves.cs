using Camera.Fs.Common;
using LanguageExt;
using LinxUniverse.Algo.Common;
using LinxUniverse.Utils;
using MugenCamera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Models.CZPallet;

namespace TrayScanStandard.Models
{
    public class WcsSaves() : SaveBase("TrayScanStandard")
    {
        public string WcsIP{ get; set; } = string.Empty;

        public ushort WcsHeartPort { get; set; } = 10001;

        public string PlcIp{ get; set; } = string.Empty;

        /// <summary>
        /// 物流线 Modbus TCP 端口
        /// </summary>
        public ushort ModbusTcpPort { get; set; } = 502;

        /// <summary>
        /// 物流线 Modbus 站号
        /// </summary>
        public byte ModbusUnitId { get; set; } = 1;

        /// <summary>
        /// 物流线触发寄存器，默认 mw300
        /// </summary>
        public ushort ModbusTriggerRegisterAddress { get; set; } = 300;

        /// <summary>
        /// 物流线结果反馈寄存器，默认 mw301
        /// </summary>
        public ushort ModbusResultRegisterAddress { get; set; } = 301;

        /// <summary>
        /// 托盘到位触发值，默认 1
        /// </summary>
        public ushort ModbusTriggerValue { get; set; } = 1;

        /// <summary>
        /// 扫码任务开始前的清零值
        /// </summary>
        public ushort ModbusResultResetValue { get; set; } = 0;

        /// <summary>
        /// WCS 提交成功反馈值
        /// </summary>
        public ushort ModbusResultOkValue { get; set; } = 1;

        /// <summary>
        /// WCS 提交失败反馈值
        /// </summary>
        public ushort ModbusResultNgValue { get; set; } = 2;

        public ushort WcsDataPort { get; set; } = 10000;

        public string TrayScanStandardName { get; set; } = "整盘扫码";

        public string SystemCode { get; set; } = string.Empty;
        public Visibility BackGroundEnable
        {
            get;
            set;
        }

        public XYLStage? Stage { get; set; } = null;
        public bool TwoOk { get; set; } = false;
        //public PLCCpuType PLCCpuType { get; set; } = PLCCpuType.小端;

        /// <summary>
        /// 日志删除天数
        /// </summary>
        public int LogDeleteDay { get; set; } = 30;


        /// <summary>
        /// 托盘条码规则
        /// </summary>
        public List<PalletTypeRule> PalletTypeRules { get; set; } = [];
        public bool TestMode { get; set; }
        public Dictionary<PowerEnum, Dictionary<RoleEnum, bool>> PowerTable { get; set; } = [];

        public ApiEnableTable ApiEnableTable { get; set; } = new();

        public CameraSetting[] ConnectAddresses { get; set; } = Enumerable.Range(0,32).Select(s => new CameraSetting()).ToArray();

        public int CameraCnt { get; set; } = 2;
        public int DecodeRetryTimes { get; set; } = 2;

        //public Dictionary<string, string> ApiUrlTable { get; set; } = new();

        /// <summary>
        /// 语言
        /// </summary>
        public int Lang { get; set; }
        public OKNGCnt OKNGCnt { get; set; } = new();

        /// <summary>
        /// 工位设置 // 防止有多工位的
        /// </summary>
        public StageSetting StageSetting { get; set; } = new();        
        public int SelectBatteryId { get; set; } = 0;
        public LightInfo[] LightInfos { get; set; } = [];
        public bool CameraEnable { get; set; } = false;
        
        /// <summary>
        /// BcrBorder控件的位置信息
        /// </summary>
        public Dictionary<int, BcrPosition> BcrPositions { get; set; } = new();
        public bool IsAlgoEnable { get; set; } = true;
    }

    /// <summary>
    /// 光源
    /// </summary>
    /// <param name="Com">串口号</param>
    /// <param name="Values">亮度值</param>
    public record LightInfo(string Com, int[] Values, string ControllerType = "Wordop");
    public record CameraSetting()
    {
        /// <summary>
        /// 相机编号。用于把当前相机下 ROI 的本地通道号换算成上传时的全局通道号。
        /// </summary>
        public string CameraNumber { get; set; }
        public CameraAddress CameraAddresses { get; set; } = new HKAddress(new Key(""));
        public int[] Exposure { get; set; } = Enumerable.Range(0, 3).Select(s => 100).ToArray();
        public int[] ExposureBackup { get; set; } = Enumerable.Range(0, 3).Select(s => 100).ToArray();
        //public static CameraSetting CreateDefault() =>
        //    new CameraSetting(
        //        new HKAddress(new Key("")),
        //        Enumerable.Range(0, 3).Select(s => 100).ToArray(),
        //        Enumerable.Range(0, 3).Select(s => 100).ToArray()
        //    );
    }
    public class ApiEnableTable
    {
        /// <summary>
        /// 托盘物料等级校验/获取
        /// </summary>
        public bool CheckPalletMaterialEnable
        {
            get; set;
        } = true;

        /// <summary>
        /// 拆组盘开始使能
        /// </summary>
        public bool GrabActionInsertEnable
        {
            get; set;
        } = true;

        /// <summary>
        /// 拆组盘结束使能
        /// </summary>
        public bool GrabActionRemoveEnable
        {
            get; set;
        } = true;
        /// <summary>
        /// 申请电芯使能
        /// </summary>
        public bool ApplyMaterialEnable
        {
            get; set;
        } = true;
        /// <summary>
        /// 托盘到达使能
        /// </summary>
        public bool PalletArrivedEnable { get; set; }
        /// <summary>
        /// 托盘离开使能
        /// </summary>
        public bool PalletOutEnable { get; set; }
        /// <summary>
        /// 入料校验使能
        /// </summary>
        public bool CheckMaterialEnable { get; set; }

        /// <summary>
        /// 组盘完成上传FMS使能
        /// </summary>
        public bool UploadFmsDataEnable { get; set; }
        /// <summary>
        /// 抓取数据上传MES使能
        /// </summary>
        public bool UploadMesDataEnable { get; set; }
        /// <summary>
        /// 高温进站使能
        /// </summary>
        public bool HighTempEnable { get; set; }
        /// <summary>
        /// 化成进站使能
        /// </summary>
        public bool HCInStationEnable { get; set; }
        /// <summary>
        /// 分选挡位查询接口
        /// </summary>
        public bool FXQueryEnable { get; set; }
        public bool GrabDataUploadEnable { get; set; }
    }

    public class StageSetting
    {
        /// <summary>
        /// 设备号
        /// </summary>
        public string DeviceCode { get; set; } = string.Empty;
        /// <summary>
        /// 系统号
        /// </summary>
        public string SystemCode { get; set; } = string.Empty;
        /// <summary>
        /// 仓库号
        /// </summary>
        public string HouseCode { get; set; } = string.Empty;
        /// <summary>
        /// 位置号
        /// </summary>
        public string LocationCode { get; set; } = string.Empty;
        /// <summary>
        /// 库位号
        /// </summary>
        public string SeatId { get; set; } = string.Empty;
        /// <summary>
        /// 工序号
        /// </summary>
        public string ProcessCode { get; set; } = string.Empty;
        /// <summary>
        /// FMS地址
        /// </summary>
        public string FMSAddress { get; set; } = string.Empty;
        /// <summary>
        /// APP名称
        /// </summary>
        public string AppName { get; set; } = string.Empty;

        /// <summary>
        /// 等待假电池时间
        /// </summary>
        public double WaitFakeTime { get; set; }
    }

    public class BcrInfo
    {
        public string Key { get; set; } = string.Empty;

        public List<int> Exposure { get; set; } = [.. MainStorage.DefaultExp];

        public float Gamma { get; set; } = 1.0f;
        public float Gain { get; set; } = 1f;

        public string DeviceCode { get; set; } = "ccd";
    }

    /// <summary>
    /// BcrBorder控件的位置信息
    /// </summary>
    public class BcrPosition
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; } = 320;
        public double Height { get; set; } = 320;
    }

    public class BarCodeRegionInfo
    {
        public int Top { get; set; } = 100;
        public int Left { get; set; } = 100;
        public int Width { get; set; } = 600;
        public int Height { get; set; } = 600;

        public int ChannelIdx { get; set; } = 0;

        public ROI ToROI()
        {
            return new ROI(new LinxUniverse.Common.Rect<float>(Left, Top, Width, Height), ChannelIdx);
        } 

    }
}
