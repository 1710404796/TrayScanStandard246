using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using CommunityToolkit.Mvvm.ComponentModel;

namespace TrayScanStandard.Models
{
    public static class BatteryLevelHelper
    {

        //
        public static bool IsNG(this string code)
            => string.IsNullOrEmpty(code) || code.StartsWith("noread", StringComparison.CurrentCultureIgnoreCase);

        public static bool IsFake(this string code)
            => code.StartsWith("fake", StringComparison.CurrentCultureIgnoreCase);

        /// <summary>
        /// 获取枚举对于的PLC状态码
        /// </summary>
        /// <param name="batteryLevel"></param>
        /// <returns></returns>
        public static int GetPlcCode(this BatteryLevel batteryLevel)
        {
            return batteryLevel switch
            {
                BatteryLevel.OK => 1,
                BatteryLevel.NG => 2,
                BatteryLevel.Fake => 3,
                BatteryLevel.REWORK => 4,
                BatteryLevel.EMPTY or BatteryLevel.None => 5,
                BatteryLevel.ManualReWork => 6,
                BatteryLevel.OK2 => 7,
                BatteryLevel.REWORK2 => 8,
                BatteryLevel.E99 => 100,
                BatteryLevel.DSD => 101,
                _ => 999,
            };
        }

        public static BatteryLevel GetBatteryLevelFromWcs(this string codeStr)
        {
            return codeStr switch
            {
                "1" => BatteryLevel.OK,
                "2" => BatteryLevel.E99,
                "3" => BatteryLevel.Fake,
                "4" => BatteryLevel.REWORK,
                "6" => BatteryLevel.NG,
                "7" => BatteryLevel.DSD,
                "8" => BatteryLevel.REWORK2,
                "9" => MainStorage.Saves.TwoOk ? BatteryLevel.OK2 : BatteryLevel.OK,
                "11"=>BatteryLevel.ERROR,
                _ => BatteryLevel.NG
            };
        }
        // 这里要实现双方转换
    }
    /// <summary>
    /// 电池状态
    /// </summary>
    public enum BatteryLevel
    {
        None = 0,
        OK = 1,
        NG = 2,
        Fake = 3,
        REWORK = 4,
        EMPTY = 5,
        ManualReWork = 6,
        OK2 = 7,
        REWORK2 = 8,
        Unknown = 999,
        ERROR=11,
        E99 = 100,
        DSD = 101,
    }
    /// <summary>
    /// 电池状态
    /// </summary>

    public partial class StationChannel : ObservableObject
    {
        /// <summary>
        /// 条码默认值 避免range出错 默认是30个空格
        /// </summary>
        public readonly static string DefaultCode = string.Empty;
        /// <summary>
        /// 电池来自的容器的暂存
        /// </summary>
        public string ContainerFrom { get; set; } = string.Empty;

        /// <summary>
        /// 电池来自的工位的暂存
        /// </summary>
        public int StationFrom { get; set; } = 0;
        /// <summary>
        /// 电池来自的通道的暂存
        /// </summary>
        public int ChannelFrom { get; set; } = 0;

        /// <summary>
        /// 电池等级
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderBrush))]
        BatteryLevel _batteryLevel = BatteryLevel.EMPTY;


        /// <summary>
        /// 通道条码
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BorderBrush))]
        string _code = DefaultCode;

        /// <summary>
        /// 通道号
        /// </summary>
        [JsonIgnore]
        public int Location { get; set; }

        /// <summary>
        /// 电池显示颜色
        /// </summary>

        [JsonIgnore]
        public Brush BorderBrush => GetBrush();

        public string GetFakeCode()
        {
            return $"{Random.Shared.Next(10000000):00000000}{Random.Shared.Next(10000000):00000000}FAKEFAKE";
        }

        public byte GetPlcCode()
        {
            if (BatteryLevel == BatteryLevel.None || Code == DefaultCode) return (byte)BatteryLevel.EMPTY;

            return (byte)BatteryLevel;
        }
        Brush colorDefault = (Brush)Application.Current.Resources["SystemControlPageTextBaseHighBrush"];
        /// <summary>
        /// 获取等级颜色
        /// </summary>
        /// <returns></returns>
        public Brush GetBrush()
        {
            if (Code == DefaultCode)
            {
                return colorDefault;
            }
            switch (BatteryLevel)
            {
                case BatteryLevel.None:
                    break;
                case BatteryLevel.OK:
                    return Brushes.LightGreen;
                case BatteryLevel.NG:
                    return Brushes.Red;
                case BatteryLevel.Fake:
                    return Brushes.Purple;
                case BatteryLevel.REWORK:
                    return Brushes.Blue;
                case BatteryLevel.EMPTY:
                    return colorDefault;
                case BatteryLevel.ManualReWork:
                    return Brushes.Brown;
                case BatteryLevel.E99:
                    return Brushes.Cyan;
                default:
                    break;
            }
            return colorDefault;
        }
        public void Reset()
        {
            Code = DefaultCode;
            BatteryLevel = BatteryLevel.EMPTY;
            ContainerFrom = string.Empty;
            StationFrom = 0;
            ChannelFrom = 0;
        }
    }
}