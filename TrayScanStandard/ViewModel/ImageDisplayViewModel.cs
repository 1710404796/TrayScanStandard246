
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Models;
using TrayScanStandard.Models.CZPallet;

namespace TrayScanStandard.ViewModel
{
    public partial class ImageDisplayViewModel : ObservableRecipient
    {
        [ObservableProperty]
        bool _isFree = true;
        [ObservableProperty]

        string _imgPath1 = string.Empty;
        [ObservableProperty]
        string _imgPath2 = string.Empty;


        [ObservableProperty]
        private BatteryTypeInfo _selectBatteryInfo;
        [ObservableProperty]

        ObservableCollection<BatteryTypeInfo> _batteryInfos = [];
        private readonly LinxContext _linxContext;


        public XYLStation XYLStation { get; set; }
        partial void OnSelectBatteryInfoChanged(BatteryTypeInfo value)
        {
            if (value == null)
            {
                return;
            }
            MainStorage.SelectBattery = value;
            MainStorage.Saves.SelectBatteryId = value.Id;
            XYLStation.ChannelNum = value.Count;
            XYLStation.Column = value.Column;

            MainStorage.SaveManager.Save();
        }

        public ImageDisplayViewModel(LinxContext linxContext)
        {
            XYLStation =  new Pallet(1,"1") { Column = MainStorage.SelectBattery?.Column ?? 1, ChannelNum = MainStorage.SelectBattery?.Count ?? 0 };
            _linxContext = linxContext;
            Refresh();
            SelectBatteryInfo = BatteryInfos.FirstOrDefault(s => s.Id == MainStorage.SelectBattery?.Id)
                ?? BatteryInfos.FirstOrDefault()
                ?? new();
        }



        //BatteryTypeInfo[] _batteryInfoSource = [];




        [RelayCommand]
        public void Refresh()
        {
            BatteryInfos = [.. _linxContext.BatteryTypeInfos.ToArray()];

        }

        async IAsyncEnumerable<int> GetNumbers()
        {
            for (int i = 0; i < 10; i++)
            {
                yield return await Task.FromResult(1);
            }
        }

        [RelayCommand]
        public void Reset()
        {
            //throw new NotImplementedException();
            ImgPath1 = string.Empty;
            ImgPath2 = string.Empty;
            XYLStation.ClearStage();
        }
        [RelayCommand]
        public void UploadData()
        {
            throw new NotImplementedException();
        }


    }


}
