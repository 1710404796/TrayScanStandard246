using Humanizer;
using LinxUniverse.Auth;
using MediatR;
using System.Windows;
using TrayScanStandard;
using TrayScanStandard.Attritubes;
using TrayScanStandard.Mediator.Queries;


namespace TrayScanStandard.Mediator.Handlers
{
    /// <summary>
    /// 检查权限查询处理程序 // 根据需要去查询权限
    /// </summary>
    /// <param name="authenticationStateProvider"></param>
    public class CheckPowerQueryHandler(LinxAuthenticationStateProvider authenticationStateProvider) : IRequestHandler<CheckPowerQuery, bool>
    {
        public async Task<bool> Handle(CheckPowerQuery request, CancellationToken cancellationToken)
        {
            string aa = (await authenticationStateProvider.GetAuthenticationStateAsync()).User.GetUserRole();

            if (aa == "admin") return true;

            RoleEnum roleEnum = aa.DehumanizeTo<RoleEnum>();

            if (roleEnum == RoleEnum.SuperAdmin || (MainStorage.Saves.PowerTable.ContainsKey(request.PowerEnum) &&
                MainStorage.Saves.PowerTable[request.PowerEnum][roleEnum]))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}