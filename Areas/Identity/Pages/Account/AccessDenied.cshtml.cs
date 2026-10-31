#nullable disable

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Shown when a signed-in user tries to open a page they aren't allowed
    /// to (e.g. a Donor visiting the Employee dashboard).
    /// </summary>
    [AllowAnonymous]
    public class AccessDeniedModel : PageModel
    {
        public void OnGet() { }
    }
}
