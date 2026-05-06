using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using LinxUniverse.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Brushes1 = System.Drawing.Brushes;
using Bitmap = System.Drawing.Bitmap;
using Image = System.Drawing.Image;
using Graphics = System.Drawing.Graphics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TrayScanStandard.Models;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Data;
using MediatR;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Service;
using LinxUniverse.Algo.Common;
using Microsoft.Extensions.Logging;
using MugenCodeDetecter;
using VMWebAIClient;
using System.Windows.Threading;


namespace TrayScanStandard.ViewModel
{
    public partial class Image2DViewModel : ObservableRecipient
    {
        [ObservableProperty] string _errorText = String.Empty;
        [ObservableProperty]
        string _resultImg = Environment.CurrentDirectory + @"/Images/black.jpg";
        //string _resultImg = @"D:\wcsrepo\XCCCD2024\XCCCD2024\bin\Debug\net8.0-windows\Images\mio.jpg";

        [ObservableProperty]
        int _selectIdx = 0;
        public Brush[] Colors { get; set; } = Enumerable.Repeat(Brushes.Red, 128).ToArray();
        public string[] Codes { get; set; } = Enumerable.Repeat("", 128).ToArray();
        //public ObservableCollection<Brush> Colors { get; set; } = new(new Brush[128]);

        public event Action ColorUpdate;
        public event Action ResultUpdate;


        public Option<CodeDetectResult> TempResult
        {
            get => _tempResult;
            set
            {
                _tempResult = value;
                Colors = Enumerable.Repeat(Brushes.Red, 128).ToArray();
                Codes = Enumerable.Repeat("", 128).ToArray();
                //Dispatcher.CurrentDispatcher.Invoke(() =>
                //{
                    if (value.IsSome)
                    {
                        var r = value.First();
                        r.Codes.Do(s =>
                        {
                            if (s.Index < Colors.Length)
                            {
                                Colors[s.Index] = Brushes.Green;
                                Codes[s.Index] = s.Code;
                            }
                        });
                    }
                    else
                    {
                        RatioText = string.Empty;
                    }
                //});
            }
        }
        public int DebugExpoure
        {
            get; set;
        } = 2000;

        [ObservableProperty]
        int _roiPadding = 100;

        public int CameraIdx { get; set; } = 0;

        public CameraSetting CameraSetting
        {
            get; set;
        }


        //public BatteryTypeInfo? SelectBattery => SelectIdx < 0 ? null : BatteryInfos[SelectIdx];

        [ObservableProperty]
        BarCodeRegionInfo? _selectBarCodeRegionInfo;

        // 批量配置边框尺寸的属性
        [ObservableProperty]
        private int _batchWidth = 200;

        [ObservableProperty]
        private int _batchHeight = 200;

        // 跟踪是否有未保存的修改
        [ObservableProperty]
        private bool _hasUnsavedChanges = false;

        // 标记有未保存的修改
        public void MarkAsChanged()
        {
            HasUnsavedChanges = true;
        }

        // 标记已保存
        public void MarkAsSaved()
        {
            HasUnsavedChanges = false;
        }

        public BatteryTypeInfo[] BatteryInfos { get; set; } = [];

        //public HuaRuiBCR HuaRuiBCR
        //{
        //    get; set;
        //}
        public required
            ScanCameraService
            Service { get; init; }


        public Image2DViewModel()
        {
            _mediator = App.GetService<IMediator>();
            _logger = App.GetService<ILogger<Image2DViewModel>>();
            _vmClient = App.GetService<IVMWebAIClient>();
        }
        public LinxContext LinxContext;
        public void RefreshBatteryInfos()
        {
            LinxContext = App.GetService<LinxContext>();
            BatteryInfos = LinxContext.BatteryTypeInfos.ToArray();
            SelectIdx = System.Array.FindIndex(BatteryInfos, s => s.Id == MainStorage.Saves.SelectBatteryId);
        }

        partial void OnSelectIdxChanged(int oldValue, int newValue)
        {
            MainStorage.Saves.SelectBatteryId = newValue < 0 ? 0 : BatteryInfos[newValue].Id;
            MainStorage.SelectBattery = SelectBattery;

            //MainStorage.SaveManager.Save();
        }

        public void RefreshBarCodeRegionData()
        {
            OnPropertyChanged(nameof(SelectBarCodeRegionInfo));
        }
        [ObservableProperty]
        private string _ratioText = string.Empty;

        public void Update() => ColorUpdate?.Invoke();
        public void UpdateResult() => ResultUpdate?.Invoke();

        public CancellationTokenSource Cts
        {
            get; set;
        }
        public BatteryTypeInfo? SelectBattery => SelectIdx < 0 ? null : BatteryInfos[SelectIdx];
        public IMediator _mediator { get; }

        private ILogger<Image2DViewModel> _logger;
        private readonly IVMWebAIClient _vmClient;

        [RelayCommand]
        public async void DebugCapture()
        {

            if (SelectBattery == null)
            {
                MessageBox.Show("未选择托盘类型");
                return;
            }

            if (Cts != null && !Cts.IsCancellationRequested)
            {
                Cts.Cancel();
            }
            else
            {
                Cts = new();

                while (!Cts.Token.IsCancellationRequested)
                {

                    
                }

                // 撕烤一下更新
            }

            Update();
        }

        public void UpdateRatio()
        {
            //var s = MainStorage.Saves.ScanRatios[CameraIdx - 1];
            //RatioText = $"{Properties.Resources.NumberOfSuccessfulAttempts}{s.OkCnt},{Properties.Resources.TotalNumberOfTimes}{s.ScanCnt},{Properties.Resources.SuccessRate}{s.OkCnt * 1.0 / s.ScanCnt:P}";
        }

        
        [RelayCommand]
        public async Task Detect()
        {
            var data = await _mediator.Send(new DetectCodeCommand([new ROIDetectParam(tempImg, 
                SelectBattery?.Regions[CameraIdx - 1]
                .Map(s => s.ToROI())
                .ToArray() ?? [])])
                );
            data.Match(
                Right: r =>
                {
                    // 绘制到图上？

                    _logger.LogInformation(r.ToArr().ToString());


                },
                Left: l =>
                {
                    _mediator.Send (new WarningBoxCommand(l)).Wait();

                },
                Bottom: () => 
                {
                    _mediator.Send(new WarningBoxCommand("检测失败")).Wait();
                    _logger.LogError("检测失败");
                }
                );
            // 理论上 一定会有结果
            TempResult = data.ToOption().Map(s => s.First());
            UpdateResult();
            Update();
        }
        public byte[] tempImg = [];
        private Option<CodeDetectResult> _tempResult = None;

        [RelayCommand]
        public async Task AutoSortROI()
        {
            // 整理ROI编号顺序 从左上角开始，按列排序，左右有容忍度
            if (SelectBattery == null)
            {
                await _mediator.Send(new WarningBoxCommand("未选择电池类型"));
                return;
            }
            var regions = SelectBattery.Regions[CameraIdx - 1];
            if (regions == null || regions.Count == 0)
                return;
            // 容忍度（像素）
            int tolerance = 200;
            // 按Left分组（同一列），再组内按Top排序
            var columns = regions
                .OrderBy(r => r.Left)
                .GroupBy(r =>
                {
                    // 计算该ROI属于第几列
                    // 找到已分组的列的最小Left，若与当前Left差小于容忍度则归为该列，否则新列
                    // 这里用累加器实现
                    int col = 0;
                    int lastLeft = int.MinValue;
                    foreach (var g in regions.OrderBy(x => x.Left).Select(x => x.Left).Distinct())
                    {
                        if (Math.Abs(r.Left - g) <= tolerance)
                        {
                            return g;
                        }
                    }
                    return r.Left;
                })
                .OrderBy(g => g.Key)
                .ToList();
            var sorted = new List<BarCodeRegionInfo>();
            foreach (var col in columns)
            {
                sorted.AddRange(col.OrderBy(r => r.Top));
            }
            // 重新编号ChannelIdx，从1开始
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].ChannelIdx = i + 1;
            }
            SelectBattery.Regions[CameraIdx - 1] = sorted;
            LinxContext.SaveChanges();
            Update();
        }        [RelayCommand]
        public async Task AutoROI()
        {
            if (SelectBattery == null)
            {
                await _mediator.Send(new WarningBoxCommand("未选择电池类型"));
                return ;
            }
            
            //var data = MainStorage.Algo.Bind(s => s.GetROIList(tempImg, RoiPadding));

            var data = await _vmClient.GetROIListAsync(ResultImg, RoiPadding);
            SelectBattery.Regions[CameraIdx - 1] =
                data.Match(
                    Right: r =>
                    {
                        return r.Map(s => new BarCodeRegionInfo() {
                            Left = (int)s.Rect.X,
                            Top = (int)s.Rect.Y,
                            Width = (int)s.Rect.Width,
                            Height = (int)s.Rect.Height,
                            ChannelIdx = s.Index

                        }).ToList(); 
                    }
                    , Left: l =>
                    {
                        _mediator.Send(new WarningBoxCommand(l)).Wait();
                        return SelectBattery.Regions[CameraIdx - 1];
                    }
                    );
            await AutoSortROI();
            //Update();
        }

        [RelayCommand]
        public async Task Capture()
        {
            var data = await _mediator.Send(new CamCaptureCommand(
                [
                    //Service.GetMugen(CameraIdx).Map(s => new CaptureInfo(s, CameraSetting.Exposure))
                    Service.GetMugen(CameraIdx).Map(s => new CaptureInfo(s, [DebugExpoure]))
                ] 
                ));


            data.Match(
                Right: r =>
                {
                    var img = tempImg  = r.First().First().Data; // 对结果要验证一下 加个验证器
                    var name = $"Data2d/single-{CameraIdx}-{FilenameHelper.FileName}.png";
                    File.WriteAllBytes(name, img);
                    ResultImg = FilenameHelper.AppPath + "/" + name;
                },
                Left: l =>
                {

                }

                );

            #region
            //if (SelectBattery == null)
            //{
            //    MessageBox.Show("未选择托盘类型");
            //    return;
            //}
            //CodeReaderDelectResult? res = null;
            //if (MainStorage.Saves.TestMode)
            //{
            //    res = new BCR.Common.CodeReaderDelectResult
            //    {
            //        CodeDatas = [new CodeData { Code = "1234567890", rect = [
            //            new (100, 200),
            //            new (100, 300),
            //            new (200, 200),
            //            new (200, 300),
            //        ] }],
            //    };
            //}
            //else
            //{
            //    HuaRuiBCR.StartStream();

            //    for (int i = 0; i < BcrInfo.Exposure.Count; i++)
            //    {
            //        HuaRuiBCR.SetControl(new ExposureControl { ExposureTime = BcrInfo.Exposure[i] });

            //        var singleRes = HuaRuiBCR?.GetOneFrameV2();
            //        if (res == null)
            //        {
            //            res = singleRes;
            //        }
            //        else
            //        {
            //            res.CodeDatas.AddRange(singleRes.CodeDatas);
            //            res.Image = singleRes.Image;
            //        }
            //    }

            //    HuaRuiBCR.StopStream();

            //    if (res != null)
            //    {
            //        res.CodeDatas = res.CodeDatas.DistinctBy(s => s.Code).ToList();

            //    }


            //}
            //if (res == null)
            //{

            //    MessageBox.Show("failed"); return;
            //}
            //var name = $"Data2d/single-{CameraIdx}-{FilenameHelper.FileName}.jpg";
            //if (!MainStorage.Saves.TestMode)
            //{
            //    File.WriteAllBytes(name, res.Image);
            //    ResultImg = FilenameHelper.AppPath + "/" + name;
            //}

            //var coderes = ScanUtils.GetCodeChannel(SelectBattery.Regions[CameraIdx - 1], res);

            //for (int i = 0; i < Colors.Length; ++i)
            //{
            //    Colors[i] = Brushes.Red;
            //}


            //foreach (var code in coderes)
            //{
            //    if (string.IsNullOrEmpty(code.Code))
            //    {
            //        //Colors[code.Channel] = Brushes.Red;
            //    }
            //    else
            //    {
            //        Colors[code.Channel] = Brushes.Green;
            //        Codes[code.Channel] = code.Code;
            //    }
            //}

            // 撕烤一下更新
            #endregion
            Update();
        }

        public void DrawAImage()
        {

            #region
            //if (SelectBattery == null)
            //{
            //    MessageBox.Show("未选择托盘类型");
            //    return;
            //}
            //using var ms = new MemoryStream(File.ReadAllBytes(ResultImg));
            //using var nImage = Image.FromStream(ms);
            //using var g = Graphics.FromImage(nImage);

            //System.Drawing.Font font = new("Aria", 100);
            //System.Drawing.Font font1 = new("Aria", 30);
            //foreach (var region in SelectBattery.Regions[CameraIdx - 1])
            //{
            //    var c = Colors[region.ChannelIdx] == Brushes.Red ? Brushes1.Red : Brushes1.Green;
            //    g.DrawRectangle(new System.Drawing.Pen(c, 10), new System.Drawing.Rectangle(region.Left, region.Top, region.Width, region.Height));
            //    g.DrawString(region.ChannelIdx.ToString(), font, c, new System.Drawing.Point(region.Left, region.Top + region.Height / 2 - 70));

            //    if (Codes[region.ChannelIdx].Length > 14)
            //    {
            //        g.DrawString(Codes[region.ChannelIdx][..14], font1, Brushes1.LightGreen, new System.Drawing.Point(region.Left - 10, region.Top + region.Height + 10));
            //        g.DrawString(Codes[region.ChannelIdx][14..], font1, Brushes1.LightGreen, new System.Drawing.Point(region.Left - 10, region.Top + region.Height + 50));
            //    }


            //}
            //nImage.Save(ResultImg.Replace(".jpg", "-Result.jpg"));
            #endregion
        }
    }
}
