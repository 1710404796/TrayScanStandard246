using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LinxUniverse.Auth;
using LinxUniverse.DI;
using MediatR;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;
using TrayScanStandard.Mediator.Commands;
using TrayScanStandard.Models;

namespace TrayScanStandard.ViewModel
{
    public partial class MainViewModel : ObservableRecipient
    {
        public static WcsSaves Saves => MainStorage.Saves;

        [ObservableProperty]
        private Visibility _hasBackGround = Saves.BackGroundEnable;


        [ObservableProperty]
        Visibility _isLock = Visibility.Visible;

        [ObservableProperty]
        Visibility _isBusy = Visibility.Collapsed;

        [ObservableProperty] private int _progg = 0;
        [ObservableProperty] private string _busyMessage = string.Empty;
        [ObservableProperty] private string _errorText = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsLoading))]
        // 標準化一下
        private bool _isWcsEnable = false;
        private Thread _someThread;
        private volatile bool _shouldStopThread = false;

        public bool IsLoading => !IsWcsEnable;

        public IMediator Mediator { get; }
        public LinxAuthenticationStateProvider AuthenticationStateProvider { get; }
        public ILogger<MainViewModel> Logger { get; }
        [ObservableProperty] private string _username;


        [RelayCommand]
        public async Task SignOut()
        {
            Logger.LogInformation($"{Username} 登出");

            // _authenticationStateProvider.
            await Mediator.Send(new LogOutCommand());
        }
        public string WcsStatus => WcsIsRunning ? Properties.Resources.Online : Properties.Resources.Offline;
        public SolidColorBrush WcsColor => WcsIsRunning ? Brushes.Green : Brushes.Red;

        [NotifyPropertyChangedFor(nameof(WcsStatus))]
        [NotifyPropertyChangedFor(nameof(WcsColor))]
        [ObservableProperty]
        private bool _wcsIsRunning = false;

        public string PlcStatus => PlcIsRunning ? Properties.Resources.Run : Properties.Resources.Stop;
        public SolidColorBrush PlcColor => PlcIsRunning ? Brushes.Green : Brushes.Red;

        [NotifyPropertyChangedFor(nameof(PlcStatus))]
        [NotifyPropertyChangedFor(nameof(PlcColor))]
        [ObservableProperty]
        private bool _plcIsRunning = false;

        public MainViewModel(IMediator mediator,
                LinxAuthenticationStateProvider authenticationStateProvider,
                ILogger<MainViewModel> logger, CacheService cacheService)
        {

            Task.Run(async () =>
            {

                await mediator.Send(new InitMeCommand());
                //await Task.WhenAll(mediator.Send(new ChangeCstAllCommand(30)));

                //await codeReaderService.Init();
                Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
                // 考虑这个位置

                ;

            }).ContinueWith(_ => MainStorage.IsWcsEnable = IsWcsEnable = true);
            authenticationStateProvider.AuthenticationStateChanged += task =>
            {
                // 需要根据类型嗼
                Dispatcher.CurrentDispatcher.Invoke<Task>(async () =>
                {
                    System.Security.Claims.ClaimsPrincipal user = (await task).User;
                    IsLock = user.Identity.IsAuthenticated ? Visibility.Collapsed : Visibility.Visible;
                    if (user.Identity.IsAuthenticated)
                        Username = $"{user.GetUserName()}({user.GetUserRole()})";
                    logger.LogInformation($"{Username} 登录");
                    // 输出切换后用户名称
                    // Console.WriteLine(IsLock);

                });
            };
            _someThread = new Thread(SomeLoop);
            _someThread.Start();
            Mediator = mediator;
            AuthenticationStateProvider = authenticationStateProvider;
            Logger = logger;
            CacheService = cacheService;
        }

        public CancellationTokenSource Maints = new CancellationTokenSource();
        public readonly CacheService CacheService;

        private async void SomeLoop(object? obj)
        {
            while (!IsWcsEnable && !_shouldStopThread)
            {
                await Task.Delay(1000);
            }
            while (!CacheService.Token.IsCancellationRequested && !_shouldStopThread)
            {
                //PlcIsRunning = XcplcService.IsPlcRunning;
                await Task.Delay(1000);
            }
        }

        /// <summary>
        /// 用于停止后台线程并防止内存泄漏的清理方法
        /// </summary>
        public void Cleanup()
        {
            _shouldStopThread = true;
            
            // Give the thread a moment to finish gracefully
            if (_someThread != null && _someThread.IsAlive)
            {
                if (!_someThread.Join(TimeSpan.FromSeconds(2)))
                {
                    // Log warning if thread doesn't stop gracefully
                    Logger?.LogWarning("后台线程在超时后未正常终止");
                }
            }

            Maints?.Cancel();
            Maints?.Dispose();
        }
    }


}
