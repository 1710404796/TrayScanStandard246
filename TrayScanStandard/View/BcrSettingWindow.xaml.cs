// filepath: d:\wcsrepo\TrayScanStandard\TrayScanStandard\TrayScanStandard\View\BcrSettingWindow.xaml.cs
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Models;
using TrayScanStandard.Service;
using Camera.Fs.Common;
using MugenCamera;

namespace TrayScanStandard.View
{
    /// <summary>
    /// BcrSettingWindow.xaml 的交互逻辑
    /// </summary>
    public partial class BcrSettingWindow : Window
    {
        public CameraSetting Setting { get; set; }
        public readonly IMediator mediator;
        
        public BcrSettingWindow(CameraSetting setting)
        {
            mediator = App.GetService<IMediator>();
            Setting = setting;
            InitializeComponent();
            
            // Initialize camera type
            InitializeCameraTypeSelection();
            
            // Initialize connection settings
            InitializeConnectionSettings();
            
            // Initialize exposure values
            ExpPattern.Text = string.Join("\n", Setting.Exposure);
            
            // Initialize backup exposure values
            ExpBackupPattern.Text = string.Join("\n", Setting.ExposureBackup);
        }
        
        /// <summary>
        /// Initialize the camera type dropdown based on the current setting
        /// </summary>
        private void InitializeCameraTypeSelection()
        {
            // Default to HikVision if it's the current type
            if (Setting.CameraAddresses is HKAddress)
            {
                CameraTypeCombo.SelectedIndex = 0; // HikVision
            }
            else if (Setting.CameraAddresses is HuaruiAddress)
            {
                CameraTypeCombo.SelectedIndex = 1; // Huarui
            }
            else
            {
                CameraTypeCombo.SelectedIndex = 0; // Default to HikVision
            }
            // Add more camera types initialization as needed
        }
        
        /// <summary>
        /// Initialize the connection type and value based on the current setting
        /// </summary>
        private void InitializeConnectionSettings()
        {
            if (Setting.CameraAddresses is HKAddress hkAddress)
            {
                if (hkAddress.ConnectAddress is Camera.Fs.Common.Key key)
                {
                    ConnectionTypeCombo.SelectedIndex = 0; // Key
                    ConnectionValueBox.Text = key.Value;
                }
                else if (hkAddress.ConnectAddress is Camera.Fs.Common.IPAddress ipAddress)
                {
                    ConnectionTypeCombo.SelectedIndex = 1; // IP
                    ConnectionValueBox.Text = ipAddress.Value;
                }
                else if (hkAddress.ConnectAddress is Camera.Fs.Common.Serial serial)
                {
                    ConnectionTypeCombo.SelectedIndex = 2; // Serial
                    ConnectionValueBox.Text = serial.Value;
                }
            }
            else if (Setting.CameraAddresses is HuaruiAddress hrAddress)
            {
                if (hrAddress.ConnectAddress is Camera.Fs.Common.Key key)
                {
                    ConnectionTypeCombo.SelectedIndex = 0; // Key
                    ConnectionValueBox.Text = key.Value;
                }
                else if (hrAddress.ConnectAddress is Camera.Fs.Common.IPAddress ipAddress)
                {
                    ConnectionTypeCombo.SelectedIndex = 1; // IP
                    ConnectionValueBox.Text = ipAddress.Value;
                }
                else if (hrAddress.ConnectAddress is Camera.Fs.Common.Serial serial)
                {
                    ConnectionTypeCombo.SelectedIndex = 2; // Serial
                    ConnectionValueBox.Text = serial.Value;
                }
            }
        }
        
        /// <summary>
        /// Handle camera type selection change
        /// </summary>
        private void CameraType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Currently we only support HikVision, but this can be expanded in the future
        }
        
        /// <summary>
        /// Handle connection type selection change
        /// </summary>
        private void ConnectionType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // You can add specific logic based on connection type if needed
            // For example, showing different UI elements or validation rules
        }
        
        /// <summary>
        /// Save button click handler - save all settings
        /// </summary>
        private async void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ConnectionValueBox.Text))
                {
                    MessageBox.Show("连接参数不能为空，请填写相机 Key/IP/Serial");
                    return;
                }

                // Handle main exposure settings
                string exposureLogMessage = $"曝光{string.Join(",", Setting.Exposure)}修改为{ExpPattern.Text}";
                await mediator.Send(new OperationLogCommand(exposureLogMessage));
                Setting.Exposure = ExpPattern.Text.Trim().Split('\n').Select(int.Parse).ToArray();
                
                // Handle backup exposure settings
                string backupExposureLogMessage = $"备用曝光{string.Join(",", Setting.ExposureBackup)}修改为{ExpBackupPattern.Text}";
                await mediator.Send(new OperationLogCommand(backupExposureLogMessage));
                Setting.ExposureBackup = ExpBackupPattern.Text.Trim().Split('\n').Select(int.Parse).ToArray();
                
                // Handle camera type and connection value changes
                UpdateCameraAddresses();

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"有不合法数值，修改失败! 错误信息: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Update the camera address settings based on UI selections
        /// </summary>
        private void UpdateCameraAddresses()
        {
            // Get the connection value from the UI
            string connectionValue = ConnectionValueBox.Text;
            
            // Create the appropriate connection address type based on the selected type
            ConnectAddress connectAddress;
            switch (ConnectionTypeCombo.SelectedIndex)
            {
                case 0: // Key
                    connectAddress = new Camera.Fs.Common.Key(connectionValue);
                    break;
                case 1: // IP
                    connectAddress = new Camera.Fs.Common.IPAddress(connectionValue);
                    break;
                case 2: // Serial
                    connectAddress = new Camera.Fs.Common.Serial(connectionValue);
                    break;
                default:
                    connectAddress = new Camera.Fs.Common.Key(connectionValue);
                    break;
            }
            
            // Set the camera address based on camera type selection
            switch (CameraTypeCombo.SelectedIndex)
            {
                case 0: // HikVision
                    Setting.CameraAddresses = new HKAddress(connectAddress);
                    break;
                case 1: // HikVision
                    Setting.CameraAddresses = new HuaruiAddress(connectAddress);
                    break;

                default:
                    break;
                // Add more camera types as needed
            }
        }
    }
}
