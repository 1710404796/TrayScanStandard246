using Camera.Fs.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using MugenCamera;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using TrayScanStandard.Models;

namespace TrayScanStandard.ViewModel
{
    public partial class BcrBorderViewModel: ObservableRecipient
    {
        [ObservableProperty]
        [NotifyPropertyChangedFor("BorderBrush")]
        [NotifyPropertyChangedFor("Code")]
        [NotifyPropertyChangedFor("ConnectText")]
        private bool _isConnect = false;

        public Brush BorderBrush => IsConnect ? Brushes.Green : Brushes.Red;
        public string ConnectText => IsConnect ? Properties.Resources.Connected : Properties.Resources.Disconnect;

        public CameraSetting BcrInfo => Image2DViewModel.CameraSetting;
        public string Code
        {
            get
            {
                var c =( BcrInfo.CameraAddresses switch
                {
                    HKAddress hk => hk.ConnectAddress as Key,
                    HuaruiAddress hr => hr.ConnectAddress as Key,
                    _ => null
                })?.Value;
                return c?? "";
            }
        }

        public int CameraIdx => Image2DViewModel?.CameraIdx ?? 0;

        public required Image2DViewModel Image2DViewModel { get; set; } //?



    }
}
