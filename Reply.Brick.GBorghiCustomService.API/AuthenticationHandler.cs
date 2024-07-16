using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Linq;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;

namespace Reply.Brick.GBorghiCustomService.API {
    public class AuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions> {
        public AuthenticationHandler(
           IOptionsMonitor<AuthenticationSchemeOptions> options,
           ILoggerFactory logger,
           UrlEncoder encoder,
           ISystemClock clock)
           : base(options, logger, encoder, clock) {
        }
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync() {
            var claims = new[] {
                new Claim(ClaimTypes.NameIdentifier, "ADMIN_REPLY", ClaimValueTypes.String),
                new Claim(ClaimTypes.Name, "ADMIN_REPLY", ClaimValueTypes.String),
            };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
    }
}
