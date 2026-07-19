using System;
using System.Text;
using System.Text.RegularExpressions;

namespace TrayScanStandard.Services
{
    /// <summary>
    /// 沃德普光源控制器通信协议（仅ASCII模式）
    /// </summary>
    public class WordopProtocol
    {
        /// <summary>
        /// 命令类型
        /// </summary>
        public enum CommandType
        {
            SetBrightness,      // 设置亮度
            GetBrightness,      // 读取亮度
            ReadVersion,        // 读取版本号
            ReadParameters      // 读取所有参数
        }

        /// <summary>
        /// 构建设置亮度指令
        /// Demo格式: $L0=20# / $L1=20# / ...
        /// </summary>
        /// <param name="channel">通道 (1-4)</param>
        /// <param name="brightness">亮度 (0-255)</param>
        public byte[] BuildSetBrightness(int channel, int brightness)
        {
            int deviceChannel = Math.Clamp(channel - 1, 0, 3);
            int value = Math.Clamp(brightness, 0, 255);
            string command = $"$L{deviceChannel}={value}#";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 构建读取亮度指令
        /// Demo主要通过读取参数获取L0~L3，统一走 $RD=9999#
        /// </summary>
        public byte[] BuildGetBrightness(int channel)
        {
            string command = "$RD=9999#";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 构建读取版本号指令
        /// </summary>
        public byte[] BuildReadVersion()
        {
            string command = "$VD=1#";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 构建读取所有参数指令
        /// </summary>
        public byte[] BuildReadParameters()
        {
            string command = "$RD=9999#";
            return Encoding.ASCII.GetBytes(command);
        }

        /// <summary>
        /// 统一构建指令接口
        /// </summary>
        public byte[] BuildCommand(CommandType type, int channel = 1, int value = 0)
        {
            switch (type)
            {
                case CommandType.SetBrightness:
                    return BuildSetBrightness(channel, value);
                case CommandType.GetBrightness:
                    return BuildGetBrightness(channel);
                case CommandType.ReadVersion:
                    return BuildReadVersion();
                case CommandType.ReadParameters:
                    return BuildReadParameters();
                default:
                    throw new NotSupportedException($"不支持的命令: {type}");
            }
        }

        /// <summary>
        /// 解析响应数据，提取亮度值
        /// </summary>
        /// <param name="buffer">响应字节数组</param>
        /// <param name="bytesRead">实际读取的字节数</param>
        /// <param name="channel">请求的通道号</param>
        /// <returns>亮度值，失败返回-1</returns>
        public int ParseBrightnessResponse(byte[] buffer, int bytesRead, int channel)
        {
            if (buffer == null || bytesRead == 0)
                return -1;

            // 只取实际读取的部分
            string result = Encoding.ASCII.GetString(buffer, 0, bytesRead).Trim();

            // Demo返回中亮度字段格式为 L0=20,L1=20,...
            int deviceChannel = Math.Clamp(channel - 1, 0, 3);
            var match = Regex.Match(result, $@"\bL{deviceChannel}=(\d+)\b", RegexOptions.IgnoreCase);
            if (!match.Success)
                return -1;

            if (!int.TryParse(match.Groups[1].Value, out int brightness))
                return -1;

            return Math.Clamp(brightness, 0, 255);
        }

        /// <summary>
        /// 解析响应数据为字符串
        /// </summary>
        public string ParseResponseString(byte[] response)
        {
            if (response == null || response.Length == 0)
                return string.Empty;

            return Encoding.ASCII.GetString(response).Trim();
        }
    }
}
