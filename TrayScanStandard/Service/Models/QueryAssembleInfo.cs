using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    public sealed class QueryAssembleInfoRequest
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
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();

        /// <summary>
        /// 容器编号/托盘号
        /// </summary>
        [Required]
        [JsonPropertyName("containerCode")]
        public string ContainerCode { get; set; }

    }



    public sealed class QueryAssembleInfoResponse : IWcsResponse, ILinxResponse
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
        public string ResponseMessage { get; set; }
        /// <summary>
        /// 扩展项
        /// 字段不够时可用拓展性补充
        /// </summary>
        [Required]
        [JsonPropertyName("parameters")]
        public Dictionary<string, string> Parameters { get; set; } = new();

        /// <summary>
        /// 容器类型
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// 批次
        /// </summary>
        [JsonPropertyName("lot")]
        public string Lot { get; set; }

        /// <summary>
        /// 容器工序
        /// </summary>
        [JsonPropertyName("process")]
        public string Process { get; set; }

        /// <summary>
        /// 是否为空
        /// </summary>
        [JsonPropertyName("policy")]
        public string Policy { get; set; }

        /// <summary>
        /// 详细信息
        /// </summary>
        [JsonPropertyName("details")]
        public Dictionary<string, string> Details { get; set; }

        /// <summary>
        /// 物料类型
        /// </summary>
        [JsonPropertyName("materialType")]
        public string MaterialType { get; set; }

        /// <summary>
        /// 组盘内的物料信息集合
        /// </summary>
        [JsonPropertyName("materials")]
        public List<AssembleMaterialInfo> Materials { get; set; } = new();

        public bool Success => ResponseCode == 0;


        public string? Message => ResponseMessage;

        public static QueryAssembleInfoResponse DebugDefault => new()
        {
            ResponseCode = 0,
            ResponseMessage = "Success",
            Materials = []
        };
    }



    public sealed class AssembleMaterialInfo
    {
        /// <summary>
        /// 物料号
        /// </summary>
        [Required]
        [JsonPropertyName("materialCode")]
        public string MaterialCode { get; set; } = string.Empty;


        /// <summary>
        /// 批次
        /// </summary>
        [JsonPropertyName("lot")]
        public string? Lot { get; set; }

        /// <summary>
        /// 容器类型
        /// </summary>
        [JsonPropertyName("type")]
        public string? Type { get; set; }

        /// <summary>
        /// 电池状态（电池分档）
        /// </summary>
        [JsonPropertyName("level")]
        public string Level { get; set; } = "";

        /// <summary>
        /// 信息
        /// </summary>
        [JsonPropertyName("info")]
        public string? Info { get; set; }

        /// <summary>
        /// 详细信息
        /// </summary>
        [JsonPropertyName("details")]
        public Dictionary<string, string> Details { get; set; } = new();

        /// <summary>
        /// 电池放置的通道号
        /// </summary>
        [JsonPropertyName("channel")]
        public int Channel { get; set; }

        /// <summary>
        /// 数量
        /// </summary>
        [JsonPropertyName("quantity")]
        public int? Quantity { get; set; }

        /// <summary>
        /// 单位
        /// </summary>
        [JsonPropertyName("unit")]
        public string? Unit { get; set; }

        /// <summary>
        /// 重量
        /// </summary>
        [JsonPropertyName("weight")]
        public int? Weight { get; set; }
    }


}
