using IdentityMail.Web.Context;
using IdentityMail.Web.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;

namespace IdentityMail.Web.Controllers
{
    [Authorize]
    public class CategoryController(UserManager<AppUser> _userManager, AppDbContext _context) : Controller
    {
        public async Task<IActionResult> Index(int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            var categories = await _context.Categories
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.Name)
                .ToListAsync();
            var pageSize = 10;
            if (page < 1)
            {
                page = 1;
            }
            var totalCount = categories.Count;
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page > totalPages)
            {
                page = totalPages;
            }
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            categories = categories.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return View(categories);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        //Yeni Kategori Ekler
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string name)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                return View();
            }

            name=name.Trim();
            var exist=await _context.Categories
                .AnyAsync(x => x.UserId == user.Id && x.Name.ToLower() == name.ToLower());
            if (exist)
            {
                ModelState.AddModelError(string.Empty, "Bu isimde bir kategori zaten var.");
                return View();
            }
            _context.Categories.Add(new Category
            {
                Name = name,
                UserId = user.Id,
            });
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
            }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var category = await _context.Categories
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == user.Id);
            if (user == null || category == null)
            {
                return RedirectToAction("Index");
            }
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        
    }
}
