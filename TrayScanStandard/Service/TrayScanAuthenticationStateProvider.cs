using LinxUniverse.Auth;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TrayScanStandard.Service
{
    /// <summary>
    /// 覆盖基线库15分钟重验证强制登出策略。
    /// 当前项目使用MainWindow空闲计时控制锁屏/登出。
    /// </summary>
    public class TrayScanAuthenticationStateProvider
        : StandLinxAuthenticationStateProvider<LinxUser>
    {
        public TrayScanAuthenticationStateProvider(
            ILogger<StandLinxAuthenticationStateProvider> logger,
            IServiceScopeFactory scopeFactory) : base(logger, scopeFactory)
        {
        }

        // 与MainWindow空闲阈值一致，避免15分钟被基线重验证提前踢下线
        protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

        // 认证有效性由业务登录/退出流程控制；空闲锁屏由MainWindow处理
        protected override Task<bool> ValidateAuthenticationStateAsync(
            AuthenticationState authenticationState,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }
    }
}
