using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TrayScanStandard.Models;

namespace TrayScanStandard.View.CZPallet
{
    /// <summary>
    /// PalletView.xaml 的交互逻辑
    /// </summary>
    public partial class PalletView : UserControl, IDisposable
    {
        /// <summary>
        /// 电池通道
        /// </summary>
        private Border[] _borders;
        private readonly ILogger<PalletView> _logger;
        private bool _disposed = false;

        public bool ClickAvaliable { get; set; } = true;


        public XYLStation Station { get; set; }

        public PalletView()
        {
            DataContext = this;
            // ViewModel好像需要别人给
            InitializeComponent();

            _logger = App.GetService<ILogger<PalletView>>();
        }


        public Border GetBatteryBorder(int id, int col)
        {
            // id需不需要处理一下
            Border res = new Border
            {
                Margin = col >= 0 ? new Thickness(35, 2, 2, 2) : new Thickness(2),
                Width = 400,
                Height = 60,
                BorderThickness = new Thickness(2.5),
                CornerRadius = new CornerRadius(2),
            };
            res.SetBinding(Border.BorderBrushProperty, new Binding($"Channels[{id}].BorderBrush")
            {
                Source = Station,
                Mode = BindingMode.OneWay,
            });
            Grid grid = new();

            res.Child = grid;
            TextBlock textBlock1 = new()
            {
                Text = (id + 1).ToString(),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(-30, 0, 0, 0),

                FontSize = 18
            };
            textBlock1.SetResourceReference(TextBlock.ForegroundProperty, "SystemControlPageTextBaseHighBrush");


            TextBlock textBlock2 = new()
            {
                Margin = new Thickness(0, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontWeight = FontWeights.Black,
                FontSize = 24,
                TextAlignment = TextAlignment.Center
            };
            //ViewModel.Pallet.BindBattery(2, "sadasadasdasdasddasdassd");
            textBlock2.SetResourceReference(TextBlock.ForegroundProperty, "SystemControlPageTextBaseHighBrush");
            textBlock2.SetBinding(TextBlock.TextProperty, new Binding($"Channels[{id}].Code")
            //textBlock2.SetBinding(TextBlock.TextProperty, new Binding($"aa[{id}]")
            {
                Source = Station,
                Mode = BindingMode.OneWay,

            });

            // 还需要有点击修改事件
            if (ClickAvaliable)
                res.MouseDown += (sender, e) => Res_MouseDown(sender, e, id);

            grid.Children.Add(textBlock1);
            grid.Children.Add(textBlock2);
            return res;
        }

        /// <summary>
        /// 双击编辑条码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void Res_MouseDown(object sender, MouseButtonEventArgs e, int id)
        {
            //App.Current.MainWindow.IsEnabled= false;
            var zqd = new ChannelModify(Station, id);
            zqd.ShowDialog();


            //App.Current.MainWindow.IsEnabled = true;



            // 双击编辑
            //throw new NotImplementedException();
        }

        private void InitBorder()
        {
            if (Station.Column == 0) return;
            if (Station != null)
            {
                _borders = new Border[Station.ChannelNum];

                for (int i = 0; i < Station.Column; i++)
                {
                    StackPanel stackPanel = new();
                    PalletStack.Children.Add(stackPanel);
                    stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
                    stackPanel.Orientation = Orientation.Vertical;
                    for (int j = 0; j < Station.ChannelNum / Station.Column; j++)
                    {

                        int border1Id = i * Station.ChannelNum / Station.Column + j;

                        _borders[border1Id] = GetBatteryBorder(border1Id, i);

                        stackPanel.Children.Add(_borders[border1Id]);
                    }

                }
                PalletBorder.Height = (_borders.Length * 64 / Station.Column + 40) * 1;
                PalletBorder.Width = (Station.Column * 430 + 60) * 1;





            }
            else
            {
                _logger.LogError("试图显示null托盘");

            }
        }

        public void Refesh()
        {
            // 如何销毁（？ 清除后再初始化
            CleanupEventHandlers();
            PalletStack.Children.Clear();
            InitBorder();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            // 初始化

            InitBorder();
        }
        /// <summary>
        /// 跳转至工位详细信息
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PalletBorder_MouseDown(object sender, MouseButtonEventArgs e)
        {
            return;
            //MainWindow.NageTo(new DiaoSuDetail ( new DiaoSuDetailViewModel { PalletViewModel = ViewModel });
        }

        private void CleanupEventHandlers()
        {
            if (_borders != null)
            {
                foreach (var border in _borders)
                {
                    if (border != null)
                    {
                        // Clear all mouse down event handlers to prevent memory leaks
                        //border.MouseDown -= null;
                        // Clear binding to release references
                        BindingOperations.ClearAllBindings(border);
                        
                        // Clean up child controls and their bindings
                        if (border.Child is Grid grid)
                        {
                            foreach (UIElement child in grid.Children)
                            {
                                BindingOperations.ClearAllBindings(child);
                            }
                            grid.Children.Clear();
                        }
                    }
                }
                _borders = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    CleanupEventHandlers();
                    // Clear children to ensure proper cleanup
                    PalletStack?.Children.Clear();
                    // Clear data context binding
                    BindingOperations.ClearAllBindings(this);
                }
                _disposed = true;
            }
        }
    }
}
