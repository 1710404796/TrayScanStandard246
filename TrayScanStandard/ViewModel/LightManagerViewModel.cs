using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TrayScanStandard.Models; // Added for LightInfo and WcsSaves

namespace TrayScanStandard.ViewModel
{
    public partial class LightManagerViewModel : ObservableRecipient
    {

        /// <summary>
        /// 假设 WcsSaves 已被注入或可访问
        /// </summary>
        private WcsSaves _wcsSaves => MainStorage.Saves;

        /// <summary>
        ///  光源信息列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<LightInfoViewModel> _lightInfos;

        /// <summary>
        /// 可用 COM 端口列表
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _availableComPorts = new ObservableCollection<string>();   

        /// <summary>
        /// 新增 COM 端口输入
        /// </summary>
        [ObservableProperty]
        private string _newComPort = string.Empty; 

        /// <summary>
        /// 逗号分隔值输入
        /// </summary>
        [ObservableProperty]
        private string _newValuesString = string.Empty; 

        /// <summary>
        /// 当前选定的光源信息
        /// </summary>
        [ObservableProperty]
        private LightInfoViewModel? _selectedLightInfo;  

        /// <summary>
        /// 编辑 COM 端口输入
        /// </summary>
        [ObservableProperty]
        private string _editComPort = string.Empty;  

        /// <summary>
        /// 编辑逗号分隔值输入
        /// </summary>
        [ObservableProperty]
        private string _editValuesString = string.Empty; 

        /// <summary>
        /// 错误消息显示
        /// </summary>
        [ObservableProperty]
        private string _errorMessage = string.Empty;  

        /// <summary>
        /// 是否处于编辑模式
        /// </summary>
        [ObservableProperty]
        private bool _isEditMode = false;       

        // 构造函数 - 假设WcsSaves通过依赖注入（DI）实现或通过静态获取，
        // 请根据实际依赖注入配置进行相应调整
        public LightManagerViewModel()
        {
            LightInfos = new ObservableCollection<LightInfoViewModel>(
                _wcsSaves.LightInfos.Select(li => new LightInfoViewModel(li)));

            // 初始化可用的 COM 端口
            RefreshAvailableComPorts();
        }

        /// <summary>
        /// 刷新可用端口
        /// </summary>
        [RelayCommand]
        private void RefreshAvailableComPorts()
        {
            try
            {
                // 获取系统中所有可用的COM端口
                var ports = System.IO.Ports.SerialPort.GetPortNames();

                // 清除现有收集项并添加新端口
                AvailableComPorts.Clear();

                // 将所有发现的端口添加至集合
                foreach (var port in ports.OrderBy(p => p))
                {
                    AvailableComPorts.Add(port);
                }
                
                ErrorMessage = string.Empty;
            }
            catch (Exception ex)
            {
                ErrorMessage = $"获取COM端口列表失败: {ex.Message}";
            }
        }

        /// <summary>
        /// 添加光源
        /// </summary>
        [RelayCommand]
        private void AddLight()
        {
            if (string.IsNullOrWhiteSpace(NewComPort) || string.IsNullOrWhiteSpace(NewValuesString))
            {
                ErrorMessage = "COM端口和值必须填写";
                return;
            }

            try
            {
                // 将逗号分隔字符串解析为整数数组
                int[] values = NewValuesString.Split(',')
                                              .Select(s => int.Parse(s.Trim()))
                                              .ToArray();
                
                if (values.Length == 0)
                {
                    ErrorMessage = "至少需要一个光源值";
                    return;
                }

                // 检查COM端口是否已存在
                if (LightInfos.Any(li => li.Com.Equals(NewComPort, StringComparison.OrdinalIgnoreCase)))
                {
                    ErrorMessage = $"COM端口 '{NewComPort}' 已经存在";
                    return;
                }

                var newLightInfo = new LightInfo(NewComPort, values);
                LightInfos.Add(new LightInfoViewModel(newLightInfo));

                // 清除输入字段
                NewComPort = string.Empty;
                NewValuesString = string.Empty;
                ErrorMessage = string.Empty;
                SaveChanges();
            }
            catch (FormatException)
            {
                ErrorMessage = "光源值必须是以逗号分隔的整数";
            }
            catch (Exception)
            {
                ErrorMessage = "添加光源时发生错误";
            }
        }

        /// <summary>
        /// 删除光源
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDeleteLight))]
        private void DeleteLight()
        {
            if (SelectedLightInfo != null)
            {
                LightInfos.Remove(SelectedLightInfo);
                ErrorMessage = string.Empty;
                SaveChanges(); // 由 集合已更改 处理程序调用
            }
        }
        
        /// <summary>
        /// 编辑光源
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanEditLight))]
        private void EditLight()
        {
            if (SelectedLightInfo == null || string.IsNullOrWhiteSpace(EditComPort) 
                || string.IsNullOrWhiteSpace(EditValuesString))
            {
                ErrorMessage = "请选择要编辑的光源并填写有效的值";
                return;
            }

            try
            {
                // 将逗号分隔字符串解析为整数数组
                int[] values = EditValuesString.Split(',')
                                               .Select(s => int.Parse(s.Trim()))
                                               .ToArray();
                
                if (values.Length == 0)
                {
                    ErrorMessage = "至少需要一个光源值";
                    return;
                }

                // 检查COM端口是否已存在（当前选定项除外）
                if (LightInfos.Any(li => li != SelectedLightInfo && 
                                         li.Com.Equals(EditComPort, StringComparison.OrdinalIgnoreCase)))
                {
                    ErrorMessage = $"COM端口 '{EditComPort}' 已被其他光源使用";
                    return;
                }

                // 更新所选项目
                SelectedLightInfo.Com = EditComPort;
                SelectedLightInfo.Values = values;
                var a = SelectedLightInfo;
                // 强制刷新更新后的项目用户界面
                var index = LightInfos.IndexOf(SelectedLightInfo);

                LightInfos.RemoveAt(index);
                LightInfos.Insert(index, a);
                
                IsEditMode = false;
                ErrorMessage = string.Empty;

                SaveChanges();

            }
            catch (FormatException)
            {
                ErrorMessage = "光源值必须是以逗号分隔的整数";
            }
            catch (Exception)
            {
                ErrorMessage = "编辑光源时发生错误";
            }
        }

        /// <summary>
        ///  可编辑灯光
        /// </summary>
        /// <returns></returns>                                         
        private bool CanEditLight() => SelectedLightInfo != null;

        /// <summary>
        /// 可取消灯光
        /// </summary>
        /// <returns></returns>
        private bool CanDeleteLight() => SelectedLightInfo != null;

        /// <summary>
        /// 部分光源信息已更改
        /// </summary>
        /// <param name="value"></param>
        partial void OnSelectedLightInfoChanged(LightInfoViewModel? value)
        {
            // 选择项变更时更新 可执行 状态
            DeleteLightCommand.NotifyCanExecuteChanged();
            EditLightCommand.NotifyCanExecuteChanged();
            
            if (value != null)
            {
                // 加载所选灯光的数值以编辑字段
                EditComPort = value.Com;
                EditValuesString = value.ValuesString;
            }
        }
        
        /// <summary>
        /// 保存
        /// </summary>
        private void SaveChanges()
        {
            // 用当前列表更新WcsSaves实例
            _wcsSaves.LightInfos = LightInfos.Select(vm => vm.ToModel()).ToArray();
            // 保存更改以保持状态
            MainStorage.SaveManager.Save();
        }
    }

    // LightInfo的简易视图模型封装器，用于处理未来可能出现的UI特定逻辑
    // 如果后续需要内联编辑，则可能还会触发 INotifyPropertyChanged
    public partial class LightInfoViewModel : ObservableObject
    {
        /// <summary>
        /// 串口号
        /// </summary>
        [ObservableProperty]
        private string _com;

        /// <summary>
        /// 亮度值
        /// </summary>
        [ObservableProperty]
        private int[] _values;

        // 显示值数组的字符串
        public string ValuesString => string.Join(", ", Values);

        public LightInfoViewModel(LightInfo model)
        {
            _com = model.Com;
            _values = model.Values;
        }

        public LightInfo ToModel() => new LightInfo(Com, Values);

        // 如有需要，可覆盖 `对外部对象` 以在列表中获得更佳显示效果；不过，`DataGrid` 的列显示效果更为理想。
        public override string ToString() => $"COM: {Com}, Values: {ValuesString}";
    }
}