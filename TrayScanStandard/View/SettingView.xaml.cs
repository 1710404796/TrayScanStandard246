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
using System.Windows.Navigation;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using TrayScanStandard.Service;
using TrayScanStandard.View.CZPallet;
using TrayScanStandard.View.User;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.View
{
    /// <summary>
    /// SettingView.xaml 的交互逻辑
    /// </summary>
    public partial class SettingView : Page
    {

        public SettingViewModel ViewModel
        {
            get;
        }

        public SettingView()
        {
            DataContext = this;
            ViewModel = App.GetService<SettingViewModel>();
            InitializeComponent();
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            SaveButton.IsEnabled = false;

            try
            {
                MainStorage.SaveManager.Save();

                bool hasWcsIp = !string.IsNullOrWhiteSpace(MainStorage.Saves.WcsIP);
                bool hasPlcIp = !string.IsNullOrWhiteSpace(MainStorage.Saves.PlcIp);

                if (hasWcsIp || hasPlcIp)
                {
                    App.GetService<WcsTrayScanStandardServer>().ReloadSettings();
                }

                SaveButton.Content = "√ 保存成功";
                await Task.Delay(1000);
            }
            finally
            {
                SaveButton.Content = "保存设定";
                SaveButton.IsEnabled = true;
            }
        }

        private void SetPower_Click(object sender, RoutedEventArgs e)
        {
            new PowerSettingWindows().ShowDialog();

        }

        private void StationSetting_Click(object sender, RoutedEventArgs e)
        {
            App.GetService<StationSettingView>().ShowDialog();

        }
    }
}
