using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using LinxUniverse.Mediator;
using static TrayScanStandard.MainStorage;

namespace TrayScanStandard.Service.Models
{
    [NotLog]
    public sealed class PostDeviceStatusRequest
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
        [JsonPropertyName("locationCode")]
        public string LocationCode { get; set; } = Saves.StageSetting.LocationCode;
        [JsonPropertyName("deviceStatus")]
        public string DeviceStatus { get; set; } = "";
        /// <summary>
        /// 状态类型
        /// </summary>
        [JsonPropertyName("statusType")]
        public PostDeviceStatusType StatusType { get; set; }
        [JsonPropertyName("deviceType")]
        public string DeviceType
        {
            get => DeviceTypeEnum.ToString();
            set => Enum.Parse(typeof(DeviceTypeEnum), value);
        }
        [JsonIgnore]
        public DeviceTypeEnum DeviceTypeEnum { get; set; } = DeviceTypeEnum.Manipulator;
    }

    public sealed class PostDeviceStatusResponse : IWcsResponse, ILinxResponse
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
        //[JsonIgnore]
        //public string ResponseCode => StatusCode.ToString();
        [JsonIgnore]
        public string? ResponseMessage => StatusMessage;

        public bool Success => StatusCode == 0;

        public string? Message => StatusMessage;

        public int ResponseCode => throw new NotImplementedException();
    }
    /// <summary>
    /// 状态类型
    /// </summary>
    public enum PostDeviceStatusType
    {
        脱机 = 0,
        正常待机 = 1,
        设备工作中 = 4,
        设备故障 = 7,
        维修中 = 9,
    }

    public enum DeviceTypeEnum
    {
        OCV,
        Formation,
        Stacker,
        Manipulator,
        Sorting,
        Dcir,

    }
}
