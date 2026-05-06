using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    public sealed class DeviceErrorRequest
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
        /// 库位号（设备号） // 设备号？？？
        /// </summary>
        [Required]
        [JsonPropertyName("locationCode")]
        public string LocationCode { get; set; } = Saves.StageSetting.LocationCode;
        /// <summary>
        /// 托盘编号
        /// </summary>
        [JsonPropertyName("containerCode")]
        public string? ContainerCode { get; set; }
        /// <summary>
        /// ??? 不该是int
        /// 异常编码
        /// errorCode=0 表示无异常errorCode>0 表示存在异常
        /// </summary>
        [Required]
        [JsonPropertyName("errorCode")]
        public string ErrorCode { get; set; }
        /// <summary>
        /// 异常内容
        /// </summary>
        [JsonPropertyName("errorMessage")]
        public string ErrorMessage { get; set; }
    }
    public sealed class DeviceErrorResponse : IWcsResponse, ILinxResponse
    {
        /// <summary>
        /// 请求结果状态码
        /// 0-正常；非0-异常
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int StatusCode { get; set; } = 0;
        /// <summary>
        /// 请求结果描述
        /// </summary>
        [JsonPropertyName("statusMessage")]
        public string? StatusMessage { get; set; }

        public int ResponseCode => StatusCode;

        public string? ResponseMessage => StatusMessage;

        public bool Success => StatusCode == 0;

        public string? Message => StatusMessage;
    }
}
