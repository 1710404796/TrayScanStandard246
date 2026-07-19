
using MediatR;
using Serilog;
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
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.View
{
    /// <summary>
    /// ImageDisplayView.xaml 的交互逻辑
    /// </summary>
    public partial class ImageDisplayView : Page
    {
        private IMediator _meditor;
        private readonly ILogger _logger;

        public ImageDisplayViewModel ViewModel { get; }
        public ImageDisplayView(ImageDisplayViewModel viewModel)
        {
            _meditor = App.GetService<IMediator>();
            _logger = Log.ForContext<ImageDisplayView>();
            ViewModel = viewModel;
            DataContext = this;
            InitializeComponent();
            viewModel.XYLStation.ChannelNum = viewModel.SelectBatteryInfo.Count;
            palletv.Station = viewModel.XYLStation;
        }

        private async void DelectBtn_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null)
            {
                return;
            }

            var battery = ViewModel.SelectBatteryInfo;
            _logger.Information("手动扫码开始：电池={Battery}，通道数={Count}",
                $"{battery.Id}:{battery.TypeName}", battery.Count);

            btn.IsEnabled = false;
            var res = await _meditor.Send(new DetectCCDCommand(battery));

            await res.MatchAsync(

                LeftAsync: async l =>
                {
                    _logger.Error("手动扫码失败：{Error}", l);
                    await _meditor.Send(new WarningBoxCommand(l));
                    return LanguageExt.Unit.Default;
                },
                RightAsync: async r =>
                {
                    var noreadCount = r.Channels.Count(c => c.Code == "noread");
                    var okCount = r.Channels.Length - noreadCount;
                    _logger.Information("手动扫码完成：通道数={TotalCount}，成功={OkCount}，未识别={NoreadCount}",
                        r.Channels.Length, okCount, noreadCount);
                    if (r.Channels.All(c => c.Code == "noread") || r.Channels.Length == 0)
                    {
                        _logger.Warning("手动扫码：全部通道未识别到条码 (noread)");
                    }
                    await _meditor.Send(new InformationBoxCommand("检测完成"));
                    return LanguageExt.Unit.Default;

                }
                
                

                );

            ////Dispatcher.Invoke(() => btn.IsEnabled = true);
            btn.IsEnabled = true;

        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {

        }

        private void ComboBox_KeyUp(object sender, KeyEventArgs e)
        {

        }

        private void ComboBox_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {

        }

        private async void ComboBox_SelectionChanged_1(object sender, SelectionChangedEventArgs e)
        {
            await Task.Delay(100);
            palletv.Refesh();
        }
    }
}
