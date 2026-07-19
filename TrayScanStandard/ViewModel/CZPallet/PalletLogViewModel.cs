using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using LinxUniverse.Utils;
using Microsoft.EntityFrameworkCore;
using TrayScanStandard.Data;
using TrayScanStandard.Data.Models;
using TrayScanStandard.Messages;
using TrayScanStandard.Models.CZPallet;

namespace TrayScanStandard.ViewModel.CZPallet
{
    public partial class PalletLogViewModel : ObservableRecipient
    {
        private readonly LinxContext context;

        //public ObservableCollection<PalletLogViewModel> Log { get; set; } = [];
        [ObservableProperty] private ObservableCollection<PalletLogExt> _palletLogs = [];
        [ObservableProperty] private bool? _isAllSelected = false;
        public DateTime StartTime { get; set; } = DateTime.Today.AddDays(-30);
        public DateTime EndTime { get; set; } = DateTime.Today.AddDays(1);

        public string Code { get; set; } = string.Empty;
        private bool _isUpdatingSelectionState;

        public PalletLogViewModel(LinxContext context)
        {
            this.context = context;
            WeakReferenceMessenger.Default.Register<PalletLogCreatedMessage>(this, (r, m) =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var ext = new PalletLogExt(m.Value);
                    ext.PropertyChanged += PalletLog_PropertyChanged;
                    PalletLogs.Insert(0, ext);
                });
            });
        }


        public void RefreshContext()
        {
            Search();
        }

        partial void OnIsAllSelectedChanged(bool? value)
        {
            if (_isUpdatingSelectionState || value is null)
            {
                return;
            }

            SetAllSelections(value.Value);
        }

        public bool IsNowLog(WarningLog log)
        {

            return log.WarningTime >= StartTime && log.WarningTime <= EndTime;
        }

        private void SetAllSelections(bool isSelected)
        {
            if (PalletLogs.Count == 0)
            {
                return;
            }

            _isUpdatingSelectionState = true;
            try
            {
                foreach (var log in PalletLogs)
                {
                    log.IsSelect = isSelected;
                }
            }
            finally
            {
                _isUpdatingSelectionState = false;
            }

            UpdateSelectAllState();
        }

        private void ReplacePalletLogs(IEnumerable<PalletLogExt> logs)
        {
            foreach (var log in PalletLogs)
            {
                log.PropertyChanged -= PalletLog_PropertyChanged;
            }

            PalletLogs = new ObservableCollection<PalletLogExt>(logs);

            foreach (var log in PalletLogs)
            {
                log.PropertyChanged += PalletLog_PropertyChanged;
            }

            UpdateSelectAllState();
        }

        private void PalletLog_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PalletLogExt.IsSelect))
            {
                UpdateSelectAllState();
            }
        }

        private void UpdateSelectAllState()
        {
            _isUpdatingSelectionState = true;
            try
            {
                IsAllSelected = PalletLogs.Count switch
                {
                    0 => false,
                    _ when PalletLogs.All(s => s.IsSelect) => true,
                    _ when PalletLogs.All(s => !s.IsSelect) => false,
                    _ => null
                };
            }
            finally
            {
                _isUpdatingSelectionState = false;
            }
        }

        [RelayCommand]
        public void ExportAll()
        {
            StringBuilder sb = new(1000);
            string[] titles = ["托盘编号", "组盘时间", "电池数量", "电池条码"];

            sb.AppendLine(string.Join(",", titles));

            foreach (var log in PalletLogs)
            {
                //sb.AppendLine($"{log.PalletCode},{log.ZuPanTime},{log.ChannelCount},[{string.Join("|", log.BatteryInfo.Select(s => s.BatteryCode))}]");
                sb.AppendLine($"{log.PalletCode},{log.ZuPanTime.ToString("yyyy-MM-dd HH:mm:ss")},{log.ChannelCount},[{string.Join("|", log.BatteryInfo.Select(s => s.BatteryCode))}]");

            }

            string fileName = $"InsertLog/exportall-{FilenameHelper.FileName}.csv";
            System.IO.File.WriteAllBytes(fileName, Encoding.GetEncoding("gb2312").GetBytes(sb.ToString()));
            MessageBox.Show($"export to {fileName}");

        }
        [RelayCommand]
        public void Export()
        {
            StringBuilder sb = new(1000);
            string[] titles = ["托盘编号", "组盘时间", "电池数量", "电池条码"];

            sb.AppendLine(string.Join(",", titles));

            foreach (var log in PalletLogs.Where(s => s.IsSelect))
            {
                //sb.AppendLine($"{log.PalletCode},{log.ZuPanTime},{log.ChannelCount},[{string.Join("|", log.BatteryInfo.Select(s => s.BatteryCode))}]");
                sb.AppendLine($"{log.PalletCode},{log.ZuPanTime.ToString("yyyy-MM-dd HH:mm:ss")},{log.ChannelCount},[{string.Join("|", log.BatteryInfo.Select(s => s.BatteryCode))}]");

            }

            string fileName = $"InsertLog/export-{FilenameHelper.FileName}.csv";
            System.IO.File.WriteAllBytes(fileName, Encoding.GetEncoding("gb2312").GetBytes(sb.ToString()));
            MessageBox.Show($"export to {fileName}");

        }
        [RelayCommand]
        public void Search()
        {
            IQueryable<PalletLog> afterFilter;
            lock (context)
            {
                afterFilter = context.PalletLogs
                    .AsNoTracking()
                    .Where(s => s.PalletType == PalletType.组盘)
                    .Where(s => s.ZuPanTime >= StartTime && s.ZuPanTime <= EndTime)
                    .OrderByDescending(s => s.Id);
            }

            if (!string.IsNullOrEmpty(Code))
            {
                afterFilter = afterFilter.Where(s => s.PalletCode.Contains(Code, StringComparison.InvariantCultureIgnoreCase));
            }

            ReplacePalletLogs(afterFilter.Select(s => new PalletLogExt(s)));
        }
    }
}
