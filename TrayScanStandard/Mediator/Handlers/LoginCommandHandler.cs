using System.Security.Claims;
using System.Windows;
using LinxUniverse.Auth;
using MediatR;
//using Microsoft.IdentityModel.Logging;
using TrayScanStandard.Mediator.Commands;

//using ClaimTypes = System.IdentityModel.Claims.ClaimTypes;

namespace TrayScanStandard.Mediator.Handlers
{
    /// <summary>
    /// 登录命令处理程序
    /// </summary>
    /// <param name="authenticationStateProvider"></param>
    /// <param name="mediator"></param>
    public class LoginCommandHandler(LinxAuthenticationStateProvider authenticationStateProvider, IMediator mediator, LinxUniverse.Auth.UserManager<LinxUser> userManager) : IRequestHandler<LoginCommand, bool>
    {
        public async Task<bool> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            if (request.userId == "linx0304")
            {
                // 新定义一个provider admin免认证
                var admin = new ClaimsPrincipal(new ClaimsIdentity("login"));
                admin.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")]));
                admin.AddIdentity(new ClaimsIdentity([new Claim("role", "admin")]));

                // Todo: 想一想这里
                (authenticationStateProvider as StandLinxAuthenticationStateProvider)
                    .SetAuthenticationState(Task.FromResult(new AuthenticationState(admin)));

                return true;
            }


            var users = await userManager.GetAllUserAsync();
            var user = users.FirstOrDefault(u => u.Password == request.userId);
            if (user == null)
            {
                MessageBox.Show("用户不存在，请检查输入的ID卡号是否正确。", "登录失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
            var roles = await userManager.GetUserRolesAsync(user);
            string role = roles.First();


            var aa = new ClaimsPrincipal(new ClaimsIdentity("login"));
            aa.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Name, user.UserName)]));
            aa.AddIdentity(new ClaimsIdentity([new Claim("role", role)]));


            //var res = await mediator.Send(new GetIdCardCheckCommand(request.userId));
            //if (res == null) return false;
            ////var aa = new ClaimsPrincipal(new LinxIdentity(){IsAuthenticated = true});
            //var aa = new ClaimsPrincipal(new ClaimsIdentity("login"));
            //aa.AddIdentity(new ClaimsIdentity([new Claim(ClaimTypes.Name, res.Staff)]));
            //aa.AddIdentity(new ClaimsIdentity([new Claim("cardId", request.userId)]));
            //aa.AddIdentity(new ClaimsIdentity([new Claim("level", res.Level)]));
            //aa.AddIdentity(new ClaimsIdentity([new Claim("role", LoginHelper.GetPowerName(res.Level))]));


            //// 看看信息加哪里
            (authenticationStateProvider as StandLinxAuthenticationStateProvider)
                .SetAuthenticationState(Task.FromResult(new AuthenticationState(aa)));

            return true;
        }
    }
}
