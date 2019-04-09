using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebApi.Data;
using WebApi.ViewModels.Account;

namespace WebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet("")]
        [AllowAnonymous]
        public ActionResult<string> Index()
        {
            return Ok("authorization server is working.");
        }

        [HttpPost("login")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginUserBinding binding)
        {
            if (ModelState.IsValid)
            {
                // This doesn't count login failures towards account lockout
                // To enable password failures to trigger account lockout, set lockoutOnFailure: true
                var result = await _signInManager.PasswordSignInAsync(binding.Email, binding.Password, binding.RememberMe, lockoutOnFailure: false);
                if (result.Succeeded)
                {
                    return Ok();
                }
                if (result.RequiresTwoFactor)
                {
                    throw new NotImplementedException();
                    //return RedirectToAction(nameof(SendCode), new { ReturnUrl = returnUrl, binding.RememberMe });
                }
                if (result.IsLockedOut)
                {
                    throw new NotImplementedException();
                }
                else
                {
                    ModelState.AddModelError(string.Empty, "Invalid login attempt.");
                }
            }
            return BadRequest(ModelState);
        }

        [HttpPost("register")]
        [AllowAnonymous]
        [ValidateAntiForgeryToken] 
        public async Task<IActionResult> Register(RegisterUserBinding binding)
        {
            if (ModelState.IsValid)
            {
                var user = new ApplicationUser { UserName = binding.Email, Email = binding.Email };
                var result = await _userManager.CreateAsync(user, binding.Password);
                if (result.Succeeded)
                {
                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return Created("",result);
                }
                AddErrors(result);
            }

            return BadRequest(ModelState);
        }

        [HttpPost("logout")]
        [ValidateAntiForgeryToken]
        [AllowAnonymous]
        public async Task<IActionResult> LogOff()
        {
            await _signInManager.SignOutAsync();
            return NoContent();
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }
    }
}