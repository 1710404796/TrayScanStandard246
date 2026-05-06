using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    public sealed class MaterialOnlineRequest
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


    public sealed class MaterialOnlineInfo
    {
        /// <summary>
        /// materials List明细
        /// </summary>
        [Required]
        [JsonPropertyName("materialCode")]
        public string? MaterialCode { get; set; }

        [Required]
        [JsonPropertyName("channel")]
        /// <summary>
        /// 通道
        /// materials List明细
        /// </summary>
        public string Channel { get; set; }
    }

    public sealed class MaterialOnlineResponse : IWcsResponse, ILinxResponse
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

        public bool Success => ResponseCode == 0; 

        public string? Message => ResponseMessage;
    }


    public sealed class MaterialOnlineReturnInfo
    {
        /// <summary>
        /// 物料编号
        /// </summary>
        [JsonPropertyName("materialCode")]
        public string? MaterialCode { get; set; }
        /// <summary>
        /// 物料状态
        /// 暂定ok为0 ； ng 为 1 其他为扩展
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; } = 1;
        /// <summary>
        /// 物料类型
        /// </summary>
        [JsonPropertyName("materialType")]
        public string? MaterialType { get; set; }
    }


}
