using System.Threading.Tasks;

namespace TrayScanStandard.Services
{
    public interface ILightService
    {
        bool IsConnected { get; }
        string ServiceName { get; }
        Task<bool> ConnectAsync(string comPort, int baudRate);
        void Disconnect();
        Task<bool> SetBrightnessAsync(int channel, int brightness);
        Task<int> GetBrightnessAsync(int channel);
        Task<string> ReadParameterAsync();
        Task<string> ReadVersionAsync();
    }
}