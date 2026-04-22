using System;
using System.Windows.Input;

namespace TrayScanStandard.ViewModels
{
    /// <summary>
    /// 读取命令（RelayCommand）实现了 ICommand 接口，用于绑定按钮的命令行为
    /// </summary>
    public class RelayLightCommand : ICommand
    {
        private readonly Action<object> _execute;       // 要执行的方法
        private readonly Func<object, bool> _canExecute;

        // 传入要执行的方法和可选的条件
        public RelayLightCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }
        public void RaiseCanExecuteChanged()
        {
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;

        // 执行方法（用户点击按钮时触发）
        public void Execute(object parameter) => _execute(parameter);

        // 当 CanExecute 的条件变化时，通知界面刷新按钮状态
        public event EventHandler CanExecuteChanged;

    }
}
