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
using TrayScanStandard.ViewModel;

namespace TrayScanStandard.View
{
    /// <summary>
    /// LightSourceControlView.xaml 的交互逻辑
    /// </summary>
    public partial class LightSourceControlView : Page
    {
        public LightSourceControlViewModel ViewModel
        {
            get;
        }

        public LightSourceControlView()
        {
            DataContext = this;
            ViewModel = App.GetService<LightSourceControlViewModel>();

            InitializeComponent();
        }

        
    }
}
