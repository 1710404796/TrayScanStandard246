using System.Threading.Tasks;

namespace TrayScanStandard.Services
{
    public interface ILightService
    {
        bool IsConnected { get; }
        string ServiceName { get; }

        /// <summary>
        /// 连接同步
        /// </summary>
        /// <param name="comPort">串口号</param>
        /// <param name="baudRate">波特率</param>
        /// <returns></returns>
        Task<bool> ConnectAsync(string comPort, int baudRate);

        /// <summary>
        /// 断开连接
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 设置亮度
        /// </summary>
        /// <param name="channel">通道</param>
        /// <param name="brightness">亮度</param>
        /// <returns></returns>
        Task<bool> SetBrightnessAsync(int channel, int brightness);

        /// <summary>
        /// 获取亮度
        /// </summary>
        /// <param name="channel">通道</param>
        /// <returns></returns>
        Task<int> GetBrightnessAsync(int channel);

        /// <summary>
        /// 读取参数
        /// </summary>
        /// <returns></returns>
        Task<string> ReadParameterAsync();

        /// <summary>
        /// 读取版本号
        /// </summary>
        /// <returns></returns>
        Task<string> ReadVersionAsync();
    }
}