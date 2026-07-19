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

        /// <summary>
        /// 读取光源命令
        /// </summary>
        /// <param name="execute"></param>
        /// <param name="canExecute"></param>
        /// <exception cref="ArgumentNullException"></exception>
        public RelayLightCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        /// <summary>
        /// 引发 CanExecuteChanged 事件  可执行已更改的内容
        /// </summary>
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
