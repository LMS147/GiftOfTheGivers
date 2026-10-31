#nullable disable

using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Areas.Identity.Pages.Account
{
    /// <summary>
    /// Login page (overrides the default Identity UI page so it matches the
    /// site theme). After a successful login the user is routed to their
    /// role dashboard: Staff → /Staff/Dashboard, Volunteer → /Volunteer/Dashboard,
    /// Donor → wherever they were heading.
    /// </summary>
    [AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(
            SignInManager<IdentityUser> signInManager,
            UserManager<IdentityUser> userManager,
            ILogger<LoginModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Email is required.")]
            [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required(ErrorMessage = "Password is required.")]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [Display(Name = "Remember me")]
            public bool RememberMe { get; set; }
        }

        public void OnGet(string returnUrl = null)
        {
            ReturnUrl = returnUrl ?? Url.Content("~/");
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            if (ModelState.IsValid)
            {
                Input.Email = Input.Email.Trim();
                // Existing account/password checks are handled by ASP.NET Core Identity.
                var user = await _userManager.FindByEmailAsync(Input.Email);
                var result = user is null ? Microsoft.AspNetCore.Identity.SignInResult.Failed
                    : await _signInManager.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: false);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User logged in.");

                    // ---- Role-based routing: send users to their own dashboard ----
                    if (user is not null)
                    {
                        if (await _userManager.IsInRoleAsync(user, "Staff"))
                            return RedirectToAction("Dashboard", "Staff", new { area = "Staff" });

                        if (await _userManager.IsInRoleAsync(user, "Volunteer"))
                            return RedirectToAction("Dashboard", "VolunteerDashboard", new { area = "Volunteer" });
                    }

                    if (user is not null && await _userManager.IsInRoleAsync(user, "Donor") && returnUrl == Url.Content("~/"))
                        return RedirectToAction("Index", "Donor", new { area = "" });
                    return LocalRedirect(returnUrl);
                }
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    return RedirectToPage("./Lockout");
                }
                ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            }

            return Page();
        }
    }
}
