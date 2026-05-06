using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    public class MoveFakeCellRequest
    {
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
        [JsonPropertyName("containerCode")]
        public string ContainerCode { get; set; } = string.Empty;
    }

    public class MoveFakeCellResponse : IWcsResponse, ILinxResponse
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
        [JsonPropertyName("data")]
        public string ResponseMessage { get; set; } = string.Empty;
        [JsonPropertyName("responseMessage")]

        public string ContainerCode { get; set; } = string.Empty;
        public bool Success => ResponseCode == 0;


        public string? Message => ResponseMessage;
    }
}
