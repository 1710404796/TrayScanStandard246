using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static TrayScanStandard.MainStorage;

// 这里是不是能表达式树
namespace TrayScanStandard.Service.Models
{
    public sealed class MaterialOfflineCheckRequest
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

        [Required]
        [JsonPropertyName("materials")]
        /// <summary>
        /// 物料信息
        /// </summary>
        public List<MaterialOnlineInfo> Materials { get; set; } = new();
    }
    public sealed class MaterialOfflineCheckResponse : IWcsResponse
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
        public string? ResponseMessage { get; set; }
        /// <summary>
        /// 扩展项
        /// 字段不够时可用拓展性补充
        /// </summary>
        [Required]
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();
        /// <summary>
        /// 物料信息
        /// </summary>
        [JsonPropertyName("materials")]
        public List<MaterialOnlineReturnInfo> Materials { get; set; } = new();
    }
}
