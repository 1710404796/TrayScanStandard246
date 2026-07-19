using MediatR;
using Microsoft.Extensions.Logging;
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
using TrayScanStandard;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Models;
using TrayScanStandard.Service;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.View
{
    /// <summary>
    /// AllBcrListView.xaml 的交互逻辑
    /// </summary>
    //[PowerView(PowerEnum.相机列表)]
    public partial class AllBcrListView : Page
    {
        List<BcrBorder> _borderList = [];
        public ScanCameraService CRService { get; }

        private readonly IMediator meditor;
        private readonly ILogger<AllBcrListView> logger;
        private readonly LinxContext linxContext;
        WcsSaves WcsSaves => MainStorage.Saves;
        CancellationTokenSource? _cts;

        // 拖拽相关变量
        private bool _isDragging = false;
        private BcrBorder? _draggingElement = null;
        private Point _lastMousePosition;

        public int DebugExp { get; set; }

        public AllBcrListView()
        {
            DataContext = this;
            CRService = App.GetService<ScanCameraService>();
            meditor = App.GetService<IMediator>();
            logger = App.GetService<ILogger<AllBcrListView>>();
            linxContext = App.GetService<LinxContext>();
            InitializeComponent();
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            UpdateRatio();
            if (CRService.BcrBorderViewModels.Length != MainStorage.Saves.CameraCnt)
            {
                CRService.Init();
            }

            int idx = 0;
            double x = 10, y = 10; // 初始位置
            int itemsPerRow = 3; // 每行显示3个
            double itemWidth = 320, itemHeight = 320;
            double spacing = 10;

            foreach (var item in CRService.BcrBorderViewModels.Take(MainStorage.Saves.CameraCnt))
            {
                int i = idx;
                var bborder = new BcrBorder(item) { Width = itemWidth, Height = itemHeight };
                // 检查是否有保存的位置信息
                if (WcsSaves.BcrPositions.ContainsKey(idx))
                {
                    var savedPosition = WcsSaves.BcrPositions[idx];
                    Canvas.SetLeft(bborder, savedPosition.X);
                    Canvas.SetTop(bborder, savedPosition.Y);
                    bborder.Width = savedPosition.Width;
                    bborder.Height = savedPosition.Height;
                }
                else
                {
                    // 使用默认位置
                    Canvas.SetLeft(bborder, x);
                    Canvas.SetTop(bborder, y);
                    
                    // 计算下一个位置
                    x += itemWidth + spacing;
                    if ((idx + 1) % itemsPerRow == 0)
                    {
                        x = 10;
                        y += itemHeight + spacing;
                    }
                }

                // 添加拖拽事件
                bborder.MouseLeftButtonDown += BcrBorder_MouseLeftButtonDown;
                bborder.MouseLeftButtonUp += BcrBorder_MouseLeftButtonUp;
                bborder.MouseMove += BcrBorder_MouseMove;

                BcrPanel.Children.Add(bborder);

                bborder.MouseDoubleClick += (o, s) => Bborder_MouseDoubleClick(i);
                _borderList.Add(bborder);

                idx++;
            }

            // 设置Canvas背景鼠标事件
            BcrPanel.MouseLeftButtonUp += Canvas_MouseLeftButtonUp;
            BcrPanel.MouseMove += Canvas_MouseMove;
        }

        #region 拖拽功能实现

        private void BcrBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var border = sender as BcrBorder;
            if (border != null && e.ClickCount == 1) // 单击才开始拖拽，双击交给双击事件处理
            {
                _isDragging = true;
                _draggingElement = border;
                _lastMousePosition = e.GetPosition(BcrPanel);
                border.CaptureMouse();
                
                // 将当前元素置于最顶层
                Panel.SetZIndex(border, 1000);
                
                e.Handled = true;
            }
        }        private void BcrBorder_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && _draggingElement != null)
            {
                _isDragging = false;
                _draggingElement.ReleaseMouseCapture();
                Panel.SetZIndex(_draggingElement, 0); // 恢复正常层级
                
                // 保存位置信息
                SaveBcrPosition(_draggingElement);
                
                _draggingElement = null;
                e.Handled = true;
            }
        }

        private void BcrBorder_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggingElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(BcrPanel);
                
                // 计算移动距离
                double deltaX = currentPosition.X - _lastMousePosition.X;
                double deltaY = currentPosition.Y - _lastMousePosition.Y;
                
                // 获取当前位置
                double left = Canvas.GetLeft(_draggingElement);
                double top = Canvas.GetTop(_draggingElement);
                
                // 处理NaN值（第一次设置时可能为NaN）
                if (double.IsNaN(left)) left = 0;
                if (double.IsNaN(top)) top = 0;
                
                // 计算新位置
                double newLeft = left + deltaX;
                double newTop = top + deltaY;
                
                // 边界检查，防止拖拽出Canvas范围
                if (newLeft < 0) newLeft = 0;
                if (newTop < 0) newTop = 0;
                if (newLeft + _draggingElement.Width > BcrPanel.ActualWidth && BcrPanel.ActualWidth > 0)
                    newLeft = BcrPanel.ActualWidth - _draggingElement.Width;
                if (newTop + _draggingElement.Height > BcrPanel.ActualHeight && BcrPanel.ActualHeight > 0)
                    newTop = BcrPanel.ActualHeight - _draggingElement.Height;
                
                // 设置新位置
                Canvas.SetLeft(_draggingElement, newLeft);
                Canvas.SetTop(_draggingElement, newTop);
                
                _lastMousePosition = currentPosition;
                e.Handled = true;
            }
        }

        private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging && _draggingElement != null)
            {
                _isDragging = false;
                _draggingElement.ReleaseMouseCapture();
                Panel.SetZIndex(_draggingElement, 0);
                _draggingElement = null;
            }
        }

        private void Canvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggingElement != null && e.LeftButton == MouseButtonState.Pressed)
            {
                BcrBorder_MouseMove(_draggingElement, e);
            }
        }

        /// <summary>
        /// 保存BcrBorder的位置信息
        /// </summary>
        /// <param name="border"></param>
        private void SaveBcrPosition(BcrBorder border)
        {
            // 找到这个border在列表中的索引
            int index = _borderList.IndexOf(border);
            if (index >= 0)
            {
                var position = new BcrPosition
                {
                    X = Canvas.GetLeft(border),
                    Y = Canvas.GetTop(border),
                    Width = border.Width,
                    Height = border.Height
                };

                // 处理NaN值
                if (double.IsNaN(position.X)) position.X = 0;
                if (double.IsNaN(position.Y)) position.Y = 0;

                WcsSaves.BcrPositions[index] = position;
                
                // 保存到文件
                MainStorage.SaveManager.Save();
            }
        }

        #endregion

        private void UpdateRatio()
        {
            //Ratio.Text = $"{Properties.Resources.NumberOfSuccessfulAttempts}: {MainStorage.Saves.OkCnt} {Properties.Resources.TotalNumberOfTimes}: {MainStorage.Saves.ScanCnt} {Properties.Resources.SuccessRate}: {MainStorage.Saves.OkCnt * 1.0 / MainStorage.Saves.ScanCnt:P}";
        }

        private void Bborder_MouseDoubleClick(int idx)
        {
            // 双击事件，只有在非拖拽状态下才触发
            if (!_isDragging)
            {
                MainWindow.NageTo(new Image2DView(CRService.Image2DViewModels[idx]));
            }
        }

        private BcrBorder CreateBorder(BcrBorderViewModel viewModel)
        {
            var bborder = new BcrBorder(viewModel) { Width = 200, Height = 200 };
            return bborder;
        }

        private void Page_Unloaded(object sender, RoutedEventArgs e)
        {
            // Properly dispose of each BcrBorder
            foreach (var bborder in _borderList)
            {
                bborder.MouseLeftButtonDown -= BcrBorder_MouseLeftButtonDown;
                bborder.MouseLeftButtonUp -= BcrBorder_MouseLeftButtonUp;
                bborder.MouseMove -= BcrBorder_MouseMove;
                // Call dispose if you implement IDisposable
                (bborder as IDisposable)?.Dispose();
            }
            
            _borderList.Clear();
            BcrPanel.Children.Clear();
            _cts?.Cancel();
            
            // Force garbage collection after cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        private async void AllCapture_Click(object sender, RoutedEventArgs e)
        {
            (sender as Button)!.IsEnabled = false;

            try
            {
                var battery = EnsureSelectedBattery();
                if (battery == null)
                {
                    MessageBox.Show("电池信息为空");
                    return;
                }

                logger.LogInformation("全相机扫码开始：电池={Battery}，通道数={Count}",
                    $"{battery.Id}:{battery.TypeName}", battery.Count);
                var res = await meditor.Send(new DetectCCDCommand(battery));

                res.Match(
                    Right: r =>
                    {
                        
                        if (r.Channels.All(c => c.Code == "noread"))
                        {
                            logger.LogWarning("全部通道未识别到条码 (noread)");
                        }
                    },
                    Left: l =>
                    {
                        logger.LogError("全相机扫码失败：{Error}", l);
                        meditor.Send(new AddToWarningCommand(l));
                    }
                    );

                UpdateRatio();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "全相机扫码异常");
            }

            (sender as Button)!.IsEnabled = true;
        }

        private async void ManualStart_Click(object sender, RoutedEventArgs e)
        {
            await Task.Yield();
            //await meditor.Send(new StartDelectTaskCommand(true));
        }        private void ManualStop_Click(object sender, RoutedEventArgs e)
        {
            //await meditor.Send(new StartDelectTaskCommand(false));
        }

        static object obj = new object();
        private async void DebugCapture_Click(object sender, RoutedEventArgs e)
        {
            if (_cts != null && !_cts.IsCancellationRequested)
            {
                (sender as Button)!.Content = Properties.Resources.DebuggingQRCodeScanning;
                logger.LogInformation("调试扫码已停止");
                _cts.Cancel();
            }
            else
            {
                (sender as Button)!.Content = Properties.Resources.StopDebugging;
                var battery = EnsureSelectedBattery();
                logger.LogInformation("调试扫码已启动：电池={Battery}",
                    battery != null ? $"{battery.Id}:{battery.TypeName}" : "未选择");
                _cts = new CancellationTokenSource();
                
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        battery = EnsureSelectedBattery();
                        if (battery == null)
                        {
                            logger.LogWarning("调试扫码失败：电池信息为空");
                            await Task.Delay(1000);
                            continue;
                        }

                        var res = await meditor.Send(new DetectCCDCommand(battery));
                        UpdateRatio();
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "调试扫码异常");
                    }
                    finally
                    {
                        await Task.Delay(3000);
                    }
                }
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            GC.Collect();
            //MainStorage.Saves.OkCnt = MainStorage.Saves.ScanCnt = 0;    
            UpdateRatio();
        }

        private BatteryTypeInfo? EnsureSelectedBattery()
        {
            if (MainStorage.SelectBattery != null)
            {
                return MainStorage.SelectBattery;
            }

            try
            {
                var battery = linxContext.BatteryTypeInfos.FirstOrDefault(s => s.Id == MainStorage.Saves.SelectBatteryId)
                    ?? linxContext.BatteryTypeInfos.FirstOrDefault();

                MainStorage.SelectBattery = battery;
                if (battery != null)
                {
                    MainStorage.Saves.SelectBatteryId = battery.Id;
                    MainStorage.SaveManager.Save();
                    logger.LogInformation("兜底加载电池信息成功：Id={Id}", battery.Id);
                }

                return battery;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "兜底加载电池信息失败");
                return null;
            }
        }
    }
}
