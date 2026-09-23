using IdentityMail.Web.DTOs.UserDtos;
using IdentityMail.Web.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.Threading.Tasks;

namespace IdentityMail.Web.Controllers
{
    public class AuthController(UserManager<AppUser> _userManager,
                                SignInManager<AppUser> _signInManager) : Controller
    {
        //private readonly UserManager<AppUser> userManager;

        //public AuthController(UserManager<AppUser> userManager)
        //{
        //    this.userManager = userManager;
        //}

        public IActionResult Register()
        {
            
            return View();
        }
        [HttpPost]
        public async Task<IActionResult> Register(RegisterDto registerDto)
        {
            if (registerDto.Password!=registerDto.ConfirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Şifreler birbiriyle uyumlu değil.");
                return View(registerDto);
                
            }
            var user = new AppUser
            {
                Email = registerDto.Email,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                UserName = registerDto.UserName
            };
            var result= await _userManager.CreateAsync(user,registerDto.Password);

            if (!result.Succeeded) 
            { 
                foreach(var error in result.Errors)
                {
                    ModelState.AddModelError(error.Code, error.Description);
                }
                return View(registerDto);
            }
            return RedirectToAction("Login");
        }
        public IActionResult Login()
        {

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginDto loginDto)
        {
            var user=await _userManager.FindByEmailAsync(loginDto.Email);
            if (user==null)
            {
                ModelState.AddModelError(string.Empty, "Bu email sistemde kayıtlı değil");
                return View(loginDto);
            }

            var result =await _signInManager.PasswordSignInAsync(user, loginDto.Password,false,false);
            if (!result.Succeeded)
            {
                ModelState.AddModelError(string.Empty, "Email veya Şifre hatalı");
                return View(loginDto);
            }
            return RedirectToAction("Index", "Message");
        }
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto forgotPasswordDto)
        {
            var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Bu e-posta sistemde kayıtlı değil.");
                return View(forgotPasswordDto);
            }
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            return RedirectToAction("ResetPassword", new { userId = user.Id, token });
        }

        public IActionResult ResetPassword(string userId, string token)
        {
            if(string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
            {
                return RedirectToAction("Login");
            }
            var model = new ResetPasswordDto
            {
                UserId = userId,
                Token = token
            };
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto resetPasswordDto)
        {
            if(resetPasswordDto.NewPassword!=resetPasswordDto.ConfirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Şifreler birbiriyle uyumlu değil.");
                return View(resetPasswordDto);
            }

            var user=await _userManager.FindByIdAsync(resetPasswordDto.UserId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Kullanıcı bulunamadı.");
                return View(resetPasswordDto);
            }
            var result = await _userManager.ResetPasswordAsync(
                user,
                resetPasswordDto.Token,
                resetPasswordDto.NewPassword);

            if (!result.Succeeded)
            {
                foreach(var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty,error.Description);
                }
                return View(resetPasswordDto);
            }
            ViewBag.SuccessMessage = "Şifreniz güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
            return View(resetPasswordDto);
        }

        public async Task<IActionResult> LogOut()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

    }
}
