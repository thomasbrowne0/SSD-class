using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace VulnTracker.Pages;

public class LogoutModel : PageModel
{
    // POST only (with antiforgery token), so another site can't log the user out with a link.
    public IActionResult OnPost() =>
        SignOut(CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme);
}
