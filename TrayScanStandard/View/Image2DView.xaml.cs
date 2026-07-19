using LinxUniverse.Algo.Common;
using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Data;
using TrayScanStandard.Models;
using TrayScanStandard.Utils;
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.View
{
    /// <summary>
    /// Image2DView.xaml 的交互逻辑
    /// </summary>
    [PowerView(PowerEnum.扫码枪设置)]
    public partial class Image2DView : UserControl, IDisposable
    {

        public Image2DViewModel ViewModel { get; set; }

        private List<(Border, BarCodeRegionInfo)> _rois = [];




        Border _nowBorder;
        private Border? _draggingBorder;
        private Canvas? _dragCanvas;
        private Point _dragStartMousePosition;
        private Thickness _dragStartMargin;


        public Image2DView(Image2DViewModel image2DViewModel, bool hidden = false)
        {
            DataContext = this;
            ViewModel = image2DViewModel;
            ViewModel.RefreshBatteryInfos();

            _context = App.GetService<LinxContext>();

            //ViewModel = App.GetService<Image2DViewModel>();
            InitializeComponent();
            if (hidden)
            {
                ImgBorder.Visibility = Visibility.Collapsed;
                Grid.SetColumnSpan(ImgGrid, 2);
            }

        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshBorder();

            ViewModel.ColorUpdate += ViewModel_ColorUpdate;
            ViewModel_ColorUpdate();

            ViewModel.ResultUpdate += ViewModel_ResultUpdate;

        }

        private void ViewModel_ResultUpdate()
        {
            Dispatcher.Invoke(() =>
            {
                img2d.ResultCanvas.Children.Clear();
                resultRects.Clear();
                //foreach (var item in resultRects)
                //{
                //    img2d.ResultCanvas.Children.Remove(item);
                //}
                ViewModel.TempResult.IfSome(s => // 这里显示有点问题
                {
                    s.Codes.Iter(
                        c =>
                        {
                            var border = new Border()
                            {
                                Width = c.Rect.Rect.Width,
                                Height = c.Rect.Rect.Height,
                                LayoutTransform = new RotateTransform(c.Rect.Angle),
                                BorderBrush = Brushes.Aqua,
                                BorderThickness = new Thickness(5),
                                Margin = new Thickness(c.Rect.Rect.X, c.Rect.Rect.Y, 0 , 0),
                            };
                            img2d.ResultCanvas.Children.Add(border);
                            resultRects.Add(border);
                        }
                        );
                 
                });


            });
        }

        List<TextBlock> codes = [];
        List<Border> resultRects = [];
        private void ViewModel_ColorUpdate()
        {
            Dispatcher.Invoke(() =>
            {
                foreach (var item in codes)
                {
                    img2d.BorderCanvas.Children.Remove(item);
                }
                codes.Clear();
                foreach (var region in _rois)
                {
                    region.Item1.BorderBrush = ViewModel.Colors[region.Item2.ChannelIdx];
                    (region.Item1.Child as TextBlock).Foreground = ViewModel.Colors[region.Item2.ChannelIdx];
                    TextBlock textBlock = new TextBlock()
                    {
                        Text = ViewModel.Codes[region.Item2.ChannelIdx],
                        FontSize = 60,
                        TextWrapping = TextWrapping.Wrap,
                         Width = 500,
                        Foreground = ViewModel.Colors[region.Item2.ChannelIdx],
                        Margin = new Thickness(region.Item2.Left, region.Item2.Top + region.Item2.Height, 0, 0)
                    };

                    codes.Add(textBlock);
                    img2d.BorderCanvas.Children.Add(textBlock);
                }
            });
           
        }

        private void Capture_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SetCam_Click(object sender, RoutedEventArgs e)
        {
            if ( ViewModel.CameraSetting is not null)
            {
                //new BcrSettingWindow(ViewModel.BcrInfo).ShowDialog();
            if ( new BcrSettingWindow(ViewModel.CameraSetting).ShowDialog()??false) MainStorage.SaveManager.Save();

            }
        }

        private void AddBorder_Click(object sender, RoutedEventArgs e)
        {
            Border border = CreateBorder();
            img2d.BorderCanvas.Children.Add(border);
            BarCodeRegionInfo barCodeRegionInfo = new();

            var lastBordor = _rois.LastOrDefault().Item2;
            if (lastBordor is not null) {
                barCodeRegionInfo.Width = lastBordor.Width;
                barCodeRegionInfo.Height = lastBordor.Height;
                border.Width = lastBordor.Width;
                border.Height = lastBordor.Height;
            }


            border.Tag = barCodeRegionInfo;
            barCodeRegionInfo.ChannelIdx = (_rois.LastOrDefault().Item2?.ChannelIdx + 1) ?? + 1;
            (border.Child as TextBlock).Text = barCodeRegionInfo.ChannelIdx.ToString();
            UpdateBorderThickness(border);
            _rois.Add((border, barCodeRegionInfo));
        }       
        
        
        private void SaveBtn_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectBattery is null)
            {
                return;
            }

            try
            {
                var cameraIdx = ViewModel.CameraIdx;
                if (cameraIdx < 1 || cameraIdx > ViewModel.SelectBattery.Regions.Count)
                {
                    MessageBox.Show($"相机索引 {cameraIdx} 超出范围 (1~{ViewModel.SelectBattery.Regions.Count})",
                        "保存失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var regionList = ViewModel.SelectBattery.Regions[cameraIdx - 1];
                regionList.Clear();
                regionList.AddRange(_rois.Select(s => s.Item2).ToList());

                var cnt = ViewModel.LinxContext.SaveChanges();

                // 保存到JSON持久化文件
                BatteryRoiJsonStore.SaveBattery(ViewModel.SelectBattery);

                MainStorage.SelectBattery = ViewModel.SelectBattery;

                // 标记已保存
                ViewModel.MarkAsSaved();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SaveBtn] 保存ROI失败: {ex}");
                MessageBox.Show($"保存失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // 记得保存一下
            RefreshBorder();
        }

        private void RefreshBorder()
        {
            if (ViewModel.SelectBattery is null)
            {
                return;
            }

            img2d.BorderCanvas.Children.Clear();
            CreateBorder();
            _rois.Clear();
            foreach (var regionInfo in ViewModel.SelectBattery.Regions[ViewModel.CameraIdx - 1])
            {
                var border = CreateBorder();

                border.Margin = new Thickness(regionInfo.Left, regionInfo.Top, 0, 0);
                border.Width = regionInfo.Width;
                border.Height = regionInfo.Height;

                (border.Child as TextBlock).Text = regionInfo.ChannelIdx.ToString();

                border.Tag = regionInfo; // 考虑加上颜色绑定

                img2d.BorderCanvas.Children.Add(border);
                UpdateBorderThickness(border);

                _rois.Add((border, regionInfo));
            }

           

        }



        private Border CreateBorder()
        {
            Border border = new()
            {
                BorderBrush = Brushes.Red,
                BorderThickness = new Thickness(20),
                Width = 200,
                Height = 200,
                Margin = new Thickness(100, 100, 100, 100),
                Background = Brushes.Transparent,
                Cursor = Cursors.Hand,

            };

            border.MouseRightButtonDown += Border_MouseRightButtonDown;
            border.MouseRightButtonUp  += Border_MouseRightButtonUp;
            border.MouseMove += Border_MouseMove;
            border.LostMouseCapture += Border_LostMouseCapture;
            border.MouseLeftButtonDown += Border_MouseLeftButtonDown;
            TextBlock textBlock = new()
            {
                Text = "0",
                Foreground = Brushes.Red,
                FontSize = 80,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = textBlock;
            UpdateBorderThickness(border);
            
            // 可能需要直接显示通道号

            return border;
        }
        private void Border_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // 阻止事件冒泡到父级控件
            e.Handled = true;

            if (sender is Border border)
            {
                EndBorderDrag(border);
            }
        }
        private static void UpdateBorderThickness(Border border)
        {
            border.BorderThickness = new Thickness(Math.Max(border.Width, border.Height) / 100 * 3 + 6);
            (border.Child as TextBlock).FontSize = Math.Min(border.Width * 2 / 3, border.Height * 2 / 3) + 1;
        }
        private void Border_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            //throw new NotImplementedException();
            var border = (sender as Border);
            var region = border.Tag as BarCodeRegionInfo;

            _nowBorder = border;
            ViewModel.SelectBarCodeRegionInfo = region;

            //ViewModel.RefreshBarCodeRegionData();

        }

        private LinxContext _context;

        private void Border_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 阻止事件冒泡到父级控件（防止影响大图拖动）
            e.Handled = true;

            if (sender is not Border border || border.Parent is not Canvas canvas)
            {
                return;
            }

            _nowBorder = border;
            ViewModel.SelectBarCodeRegionInfo = border.Tag as BarCodeRegionInfo;

            _draggingBorder = border;
            _dragCanvas = canvas;
            _dragStartMousePosition = e.GetPosition(canvas);
            _dragStartMargin = border.Margin;

            border.CaptureMouse();
        }

        private void Border_MouseMove(object sender, MouseEventArgs e)
        {
            if (sender is not Border border || _draggingBorder != border || _dragCanvas is null)
            {
                return;
            }

            if (e.RightButton != MouseButtonState.Pressed)
            {
                EndBorderDrag(border);
                return;
            }

            var currentPosition = e.GetPosition(_dragCanvas);
            var offset = currentPosition - _dragStartMousePosition;
            var margin = _dragStartMargin;

            margin.Left = Math.Max(0, margin.Left + offset.X);
            margin.Top = Math.Max(0, margin.Top + offset.Y);

            border.Margin = margin;
        }

        private void Border_LostMouseCapture(object sender, MouseEventArgs e)
        {
            if (sender is Border border && _draggingBorder == border)
            {
                EndBorderDrag(border, releaseCapture: false);
            }
        }

        private void EndBorderDrag(Border border, bool releaseCapture = true)
        {
            if (_draggingBorder != border)
            {
                return;
            }

            if (releaseCapture && border.IsMouseCaptured)
            {
                border.ReleaseMouseCapture();
            }

            _draggingBorder = null;
            _dragCanvas = null;

            if (border.Tag is not BarCodeRegionInfo region)
            {
                return;
            }

            _nowBorder = border;
            ViewModel.SelectBarCodeRegionInfo = region;

            region.Top = (int)border.Margin.Top;
            region.Left = (int)border.Margin.Left;
            region.Width = (int)border.Width;
            region.Height = (int)border.Height;

            ViewModel.RefreshBarCodeRegionData();
            ViewModel.MarkAsChanged();
        }

        private async void TopBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.SelectBarCodeRegionInfo is null)
            {
                return;
            }

            // 在 await 之前捕获所有需要的引用，防止 Unloaded 期间被清空导致 NRE
            var border = _nowBorder;
            var vm = ViewModel;
            if (border is null)
            {
                return;
            }
            await Task.Delay(20);
            // 二次校验：view 可能已卸载，border 已从视觉树移除
            if (_nowBorder != border || vm.SelectBarCodeRegionInfo is null)
            {
                return;
            }

            border.Margin = new Thickness(vm.SelectBarCodeRegionInfo.Left, vm.SelectBarCodeRegionInfo.Top, 0, 0);
            border.Width = vm.SelectBarCodeRegionInfo.Width;
            border.Height = vm.SelectBarCodeRegionInfo.Height;
            UpdateBorderThickness(border);

            // 标记有未保存的修改
            vm.MarkAsChanged();
        }        private async void ChannelBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ViewModel.SelectBarCodeRegionInfo is null)
            {
                return;
            }
            // 在 await 之前捕获所有需要的引用，防止 Unloaded 期间被清空导致 NRE
            var border = _nowBorder;
            var vm = ViewModel;
            if (border is null)
            {
                return;
            }
            await Task.Delay(20);
            // 二次校验：view 可能已卸载，border 已从视觉树移除
            if (_nowBorder != border || vm.SelectBarCodeRegionInfo is null)
            {
                return;
            }

            (border.Child as TextBlock).Text = vm.SelectBarCodeRegionInfo.ChannelIdx.ToString();

            // 标记有未保存的修改
            vm.MarkAsChanged();
        }private void Delete_Border_Click(object sender, RoutedEventArgs e)
        {
            DeleteBorder(_nowBorder);
            _nowBorder = null!;
            ViewModel.SelectBarCodeRegionInfo = null;
        }     
        
        private void ClearAllBoxes_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要清理所有选定框吗？此操作将删除所有ROI选择框。", "确认操作", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            // 清除所有边框
            var bordersToRemove = _rois.ToList(); // 创建副本以避免在迭代过程中修改集合
            foreach (var (border, regionInfo) in bordersToRemove)
            {
                DeleteBorder(border);
            }

            // 清除选中状态
            _nowBorder = null!;
            ViewModel.SelectBarCodeRegionInfo = null;
        }

        private void DeleteBorder(Border border)
        {
            if (border is null)
            {
                return;
            }
            img2d.BorderCanvas.Children.Remove(border);


            border.MouseRightButtonDown -= Border_MouseRightButtonDown;
            border.MouseRightButtonUp -= Border_MouseRightButtonUp;
            border.MouseMove -= Border_MouseMove;
            border.LostMouseCapture -= Border_LostMouseCapture;
            border.MouseLeftButtonDown -= Border_MouseLeftButtonDown;
            _rois.Remove(_rois.Find(s => s.Item1 == border));

        }        
        
        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            // 如果已通过 Dispose() 清理过，跳过重复处理
            if (_disposed) return;

            // 检查是否有未保存的修改
            if (ViewModel.HasUnsavedChanges)
            {
                var result = MessageBox.Show(
                    "检测到有未保存的修改，是否保存设置？", 
                    "保存确认", 
                    MessageBoxButton.YesNoCancel, 
                    MessageBoxImage.Question);
                
                if (result == MessageBoxResult.Yes)
                {
                    // 自动保存
                    SaveBtn_Click(this, new RoutedEventArgs());
                }
                else if (result == MessageBoxResult.Cancel)
                {
                    // 用户取消，这里我们只能记录，因为Unloaded事件无法阻止页面切换
                    // 但我们可以提醒用户修改已丢失
                    MessageBox.Show(
                        "注意：未保存的修改将丢失！", 
                        "提示", 
                        MessageBoxButton.OK, 
                        MessageBoxImage.Warning);
                }
                ViewModel.MarkAsSaved(); // 标记为已保存，避免重复提示
                // 如果选择No，则不保存，直接离开
            }

            // 1. 先取消 ViewModel 事件订阅，防止后续 ColorUpdate/ResultUpdate 触发 Dispatcher.Invoke 访问已清理的控件
            ViewModel.ColorUpdate -= ViewModel_ColorUpdate;
            ViewModel.ResultUpdate -= ViewModel_ResultUpdate;

            // 2. 释放图片资源
            img2d.Source = null!;

            // 3. 清理所有 Border 的事件处理（必须在 _rois 清空和 Canvas.Clear 之前完成）
            foreach (var (border, _) in _rois)
            {
                if (border != null)
                {
                    border.MouseRightButtonDown -= Border_MouseRightButtonDown;
                    border.MouseRightButtonUp -= Border_MouseRightButtonUp;
                    border.MouseMove -= Border_MouseMove;
                    border.LostMouseCapture -= Border_LostMouseCapture;
                    border.MouseLeftButtonDown -= Border_MouseLeftButtonDown;
                }
            }
            img2d.BorderCanvas.Children.Clear();
            img2d.ResultCanvas.Children.Clear();

            // 4. 清空所有字段引用，使对象可被正常 GC
            _draggingBorder = null;
            _dragCanvas = null;
            _nowBorder = null!;
            _rois.Clear();
            resultRects.Clear();
            codes.Clear();

            // 注意：已移除 GC.Collect() 和 GC.WaitForPendingFinalizers()，
            // 它们会阻塞 UI 线程且有引发死锁的风险。当前所有引用已断开，交由自然 GC 回收即可。
        }
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            //MainStorage.Saves.ScanRatios[ViewModel.CameraIdx - 1].OkCnt = MainStorage.Saves.ScanRatios[ViewModel.CameraIdx - 1].ScanCnt = 0;
            //ViewModel.UpdateRatio();
        }

        private void Capture_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private async void AutoRoi_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要自动生成ROI吗？此操作将覆盖当前所有ROI设置。", "确认操作", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            await ViewModel.AutoROI();
            RefreshBorder();
        }

        private async void SortRoi_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要自动排序ROI吗？此操作将根据当前设置的通道顺序重新排列所有ROI。", "确认操作", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }
            await ViewModel.AutoSortROI();
            RefreshBorder();
        }        private void ApplyBatchSize_Click(object sender, RoutedEventArgs e)
        {
            // 批量应用宽高到所有边框
            foreach (var (border, regionInfo) in _rois)
            {
                border.Width = ViewModel.BatchWidth;
                border.Height = ViewModel.BatchHeight;
                
                regionInfo.Width = ViewModel.BatchWidth;
                regionInfo.Height = ViewModel.BatchHeight;
                
                UpdateBorderThickness(border);
            }
            
            // 如果当前有选中的边框，更新ViewModel中的数据
            if (ViewModel.SelectBarCodeRegionInfo != null)
            {
                ViewModel.RefreshBarCodeRegionData();
            }
            
            // 标记有未保存的修改
            ViewModel.MarkAsChanged();
        }

        private bool _disposed = false;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            // 取消 ViewModel 事件订阅
            if (ViewModel != null)
            {
                ViewModel.ColorUpdate -= ViewModel_ColorUpdate;
                ViewModel.ResultUpdate -= ViewModel_ResultUpdate;
            }

            // 清理所有 Border 事件
            foreach (var (border, _) in _rois)
            {
                if (border != null)
                {
                    border.MouseRightButtonDown -= Border_MouseRightButtonDown;
                    border.MouseRightButtonUp -= Border_MouseRightButtonUp;
                    border.MouseMove -= Border_MouseMove;
                    border.LostMouseCapture -= Border_LostMouseCapture;
                    border.MouseLeftButtonDown -= Border_MouseLeftButtonDown;
                }
            }

            // 释放图片资源
            if (img2d != null)
            {
                img2d.Source = null;
                img2d.BorderCanvas.Children.Clear();
                img2d.ResultCanvas.Children.Clear();
            }

            // 清空字段引用
            _draggingBorder = null;
            _dragCanvas = null;
            _nowBorder = null!;
            _rois.Clear();
            resultRects.Clear();
            codes.Clear();

            // 清除数据上下文绑定
            DataContext = null;
        }
    }
}
