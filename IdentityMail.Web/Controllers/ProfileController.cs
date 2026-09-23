using IdentityMail.Web.DTOs.UserDtos;
using IdentityMail.Web.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace IdentityMail.Web.Controllers
{
    [Authorize]
    public class ProfileController(
        UserManager<AppUser> _userManager,
        IWebHostEnvironment _env,
        SignInManager<AppUser> _signInManager) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            var updateProfileDto = new UpdateProfileDto
            {
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email,
                ExistingProfileImageUrl = user.ProfileImageUrl
            };
            return View(updateProfileDto);
        }

        
        [HttpPost]
        public async Task<IActionResult> Index(UpdateProfileDto updateProfileDto)
        {
            // User (büyük U) = cookie'deki oturum. GetUserAsync onu tablodaki AppUser satırına çevirir.
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            // Form kuralları tutmuyorsa kaydetme; sayfayı tekrar göster.
            // E-posta ve eski foto yolu formdan gelmeyebilir; tablodan geri koyuyoruz.
            if (!ModelState.IsValid)
            {
                updateProfileDto.Email = user.Email;
                updateProfileDto.ExistingProfileImageUrl = user.ProfileImageUrl;
                return View(updateProfileDto);
            }
            // Tablodaki adı-soyadı formdakilerle değiştir. E-postaya dokunmuyoruz.
            user.FirstName = updateProfileDto.FirstName;
            user.LastName = updateProfileDto.LastName;
            // IFormFile = tarayıcıdan gelen dosya. Seçilmediyse veya boşsa foto kısmını atla.
            if (updateProfileDto.ProfileImage != null && updateProfileDto.ProfileImage.Length > 0)
            {
                // _env.WebRootPath = wwwroot'un disk yolu. Fotoğrafı oraya kaydedeceğiz.
                var folder = Path.Combine(_env.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(folder); // klasör yoksa oluştur
                                                   // Uzantı (.jpg, .png). Dosya adı: kullanıcıId + rastgele sayı + uzantı (çakışmasın).
                var extension = Path.GetExtension(updateProfileDto.ProfileImage.FileName);
                var fileName = $"{user.Id}_{Guid.NewGuid()}{extension}";
                var fullPath = Path.Combine(folder, fileName);
                // Tarayıcıdaki dosyayı diske yaz.
                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await updateProfileDto.ProfileImage.CopyToAsync(stream);
                }
                // Tabloya resmi değil, siteden açılacak yolu koy.
                user.ProfileImageUrl = $"/uploads/profiles/{fileName}";
            }
            // Identity satırı veritabanına yazar.
            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                updateProfileDto.Email = user.Email;
                updateProfileDto.ExistingProfileImageUrl = user.ProfileImageUrl;
                return View(updateProfileDto);
            }
            // Kayıt bitti. Tekrar GET Index: taze veri, F5 ile formu iki kez göndermesin.
            return RedirectToAction("Index");
        }

        public IActionResult ChangePassword()
        {
            return View(new ChangePasswordDto());
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(ChangePasswordDto changePasswordDto)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }

            //Yeni ile tekrar aynı değilse Identitye gitme
            if (changePasswordDto.NewPassword != changePasswordDto.ConfirmPassword)
            {
                ModelState.AddModelError(string.Empty, "Şifreler birbiriyle uyumlu değil.");
                return View(changePasswordDto);
            }

            //Identity: mevcut şifre doğru mu, yeni kurala uyuyor mu, hashi değiştir.
            var result = await _userManager.ChangePasswordAsync(
                user,
                changePasswordDto.CurrentPassword,
                changePasswordDto.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                return View(changePasswordDto);
            }

            //Şifre değişince güvenlik damgası yenilenir , oturumu düşürmemek için cookie'yi tazele.
            await _signInManager.RefreshSignInAsync(user);
            return RedirectToAction("Index");
        }
    }
}
