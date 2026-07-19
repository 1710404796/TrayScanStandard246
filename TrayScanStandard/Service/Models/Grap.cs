using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    public sealed class GrabRequest
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
        /// 来源托盘号
        /// </summary>
        [JsonPropertyName("containerFrom")]
        public string? ContainerFrom { get; set; }
        /// <summary>
        /// 来源工位号
        /// </summary>
        [Required]
        [JsonPropertyName("stationFrom")]
        public string? StationFrom { get; set; }
        /// <summary>
        /// 去向托盘号
        /// </summary>
        [JsonPropertyName("containerTo")]
        public string? ContainerTo { get; set; }
        /// <summary>
        /// 去向工位号
        /// </summary>
        [Required]
        [JsonPropertyName("stationTo")]
        public string? StationTo { get; set; }
        /// <summary>
        /// 物料抓取信息
        /// </summary>
        [Required]
        [JsonPropertyName("materials")]
        public List<GrabMaterialInfo> Materials { get; set; } = new();
    }



    public sealed class GrabMaterialInfo
    {
        /// <summary>
        /// 物料编号
        /// 组盘时必需，拆盘/挑选时为空
        /// </summary>
        [JsonPropertyName("materialCode")]
        public string? MaterialCode { get; set; }
        /// <summary>
        /// 通道来源
        /// </summary>
        [JsonPropertyName("channelFrom")]
        public int ChannelFrom { get; set; }
        /// <summary>
        /// 通道去向
        /// </summary>
        [JsonPropertyName("channelTo")]
        public int ChannelTo { get; set; }
    }



    public sealed class GrabResponse : IWcsResponse, ILinxResponse
    {
        /// <summary>
        /// 请求结果代码
        /// 0-正常；非0-异常
        /// </summary>
        [Required]
        [JsonPropertyName("responseCode")]
        public int ResponseCode { get; set; }
        /// <summary>
        /// 请求结果描述
        /// responseCode != 0时可填入异常描述信息
        /// </summary>
        [JsonPropertyName("responseMessage")]
        public string? ResponseMessage { get; set; }
        /// <summary>
        /// 字段不够时可用拓展性补充
        /// </summary>
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();

        public static GrabResponse DebugDefault => new()
        {
            ResponseCode = 0,
            ResponseMessage = "Success",
            Parameters = new()
            {
                { "debug", "true" }
            }
        };

        public bool Success => ResponseCode == 0;     

        public string? Message => ResponseMessage;
    }



}
