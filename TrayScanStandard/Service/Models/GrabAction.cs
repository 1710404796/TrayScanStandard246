using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    public sealed class GrabActionRequest
    {
        /// <summary>
        /// 系统编码
        /// 用于判断由哪个系统发出的任务请求
        /// </summary>
        [Required]
        [JsonPropertyName("systemCode")]
        public string SystemCode { get; set; } = Saves.StageSetting.SystemCode;
        /// <summary>
        /// 仓库编码
        /// 用于判断由哪个库发出的任务请求
        /// </summary>
        [Required]
        [JsonPropertyName("houseCode")]
        public string HouseCode { get; set; } = Saves.StageSetting.HouseCode;
        /// <summary>
        /// 扩展项
        /// 字段不够时可用拓展性补充
        /// </summary>
        [Required]
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();
        /// <summary>
        /// 设备编号
        /// 1001
        /// </summary>
        [Required]
        [JsonPropertyName("deviceCode")]
        public string DeviceCode { get; set; } = Saves.StageSetting.DeviceCode;
        /// <summary>
        /// 工位号
        /// </summary>
        [Required]
        [JsonPropertyName("stationCode")]
        public string StationCode { get; set; } = string.Empty;
        /// <summary>
        /// 容器编号/托盘号
        /// </summary>
        [Required]
        [JsonPropertyName("containerCode")]
        public string ContainerCode { get; set; } = string.Empty;

        [JsonIgnore]
        public ActionType SelfActionType { get; set; }
        /// <summary>
        /// 动作类型
        /// insertStart-组开始；
        /// removeStart-拆开始；
        /// insertFinish-组结束；
        /// removeFinish-拆结束
        /// i注意：分选的即使用“拆”???
        /// </summary>
        [Required]
        [JsonPropertyName("actionType")]
        public string ActionType { get => SelfActionType.ToString(); set => SelfActionType = (ActionType)Enum.Parse(typeof(ActionType), value); }
        /// <summary>
        /// 物料数量
        /// </summary>
        [Required]
        [JsonPropertyName("materialQuantity")]
        public int MaterialQuantity { get; set; }

    }

    public sealed class GrabActionResponse : IWcsResponse, ILinxResponse
    {
        /// <summary>
        /// 请求结果代码
        /// 0-正常；非0-异常
        /// </summary>
        [JsonPropertyName("responseCode")]
        public int ResponseCode { get; set; }
        /// <summary>
        /// 请求结果描述
        /// responseCode != 0时可填入异常描述信息
        /// </summary>
        [JsonPropertyName("responseMessage")]
        public string ResponseMessage { get; set; } = string.Empty;
        /// <summary>
        /// 字段不够时可用拓展性补充
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();

        public bool Success => ResponseCode == 0;

        public string? Message => ResponseMessage;

        public static GrabActionResponse DebugDefault => new()
        {
            ResponseCode = 0,
            ResponseMessage = "Success",
            Parameters = new()
            {
                { "debug", "true" }
            }
        };
    }

    public enum ActionType
    {
        insertStart,
        removeStart,
        insertFinish,
        removeFinish,
    }

}
