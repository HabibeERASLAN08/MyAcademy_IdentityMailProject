using IdentityMail.Web.Context;
using IdentityMail.Web.DTOs.AdminDtos;
using IdentityMail.Web.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityMail.Web.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController(UserManager<AppUser> _userManager, AppDbContext _context) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users.ToListAsync();
            var passiveCount = users.Count(x => x.LockoutEnd != null && x.LockoutEnd > DateTimeOffset.UtcNow);
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            var topSenders = await _context.UserMessages
                .Where(x => !x.IsDraft)
                .GroupBy(x => x.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();
            var topCategories = await _context.UserMessages
                .Where(x => x.CategoryId != null)
                .GroupBy(x => x.CategoryId)
                .Select(g => new { CategoryId = g.Key, Count = g.Count() })
                .OrderByDescending(x => x.Count)
                .Take(5)
                .ToListAsync();
            var categories = await _context.Categories.ToListAsync();
            ViewBag.UserCount = users.Count;
            ViewBag.ActiveCount = users.Count - passiveCount;
            ViewBag.MessageCount = await _context.UserMessages.CountAsync(x => !x.IsDraft);
            ViewBag.TodayCount = await _context.UserMessages.CountAsync(x => !x.IsDraft && x.SendDate >= today && x.SendDate < tomorrow);
            ViewBag.UnreadCount = await _context.UserMessages.CountAsync(x => !x.IsDraft && !x.IsRead && !x.IsDeletedByReceiver);
            ViewBag.TrashCount = await _context.UserMessages.CountAsync(x =>
                (x.IsDeletedByReceiver && !x.IsPurgedByReceiver)
                || (x.IsDeletedBySender && !x.IsPurgedBySender));
            ViewBag.TopSenders = topSenders.Select(x =>
            {
                var sender = users.FirstOrDefault(u => u.Id == x.SenderId);
                var name = sender == null ? "-" : (sender.FirstName + " " + sender.LastName).Trim();
                return new AdminStatRow { Name = name, Count = x.Count };
            }).ToList();
            ViewBag.TopCategories = topCategories.Select(x =>
            {
                var category = categories.FirstOrDefault(c => c.Id == x.CategoryId);
                return new AdminStatRow { Name = category?.Name ?? "-", Count = x.Count };
            }).ToList();
            return View();
        }

        public async Task<IActionResult> Users(int page = 1)
        {
            var current = await _userManager.GetUserAsync(User);
            var users = await _userManager.Users.OrderBy(x => x.FirstName).ToListAsync();
            var rows = new List<AdminUserRow>();
            foreach (var user in users)
            {
                rows.Add(new AdminUserRow
                {
                    Id = user.Id,
                    FullName = (user.FirstName + " " + user.LastName).Trim(),
                    Email = user.Email,
                    IsAdmin = await _userManager.IsInRoleAsync(user, "Admin"),
                    IsPassive = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow
                });
            }
            ViewBag.CurrentUserId = current?.Id;
            rows = ApplyPaging(rows, page);
            return View(rows);
        }

        public async Task<IActionResult> Complaints(int page = 1)
        {
            var complaints = await _context.MessageComplaints
                .Include(x => x.Message).ThenInclude(x => x.Sender)
                .Include(x => x.Reporter)
                .OrderBy(x => x.IsReviewed)
                .ThenByDescending(x => x.CreatedDate)
                .ToListAsync();
            complaints = ApplyPaging(complaints, page);
            return View(complaints);
        }

        public async Task<IActionResult> ComplaintDetail(int id)
        {
            var complaint = await _context.MessageComplaints
                .Include(x => x.Message).ThenInclude(x => x.Sender)
                .Include(x => x.Message).ThenInclude(x => x.Receiver)
                .Include(x => x.Reporter)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (complaint == null)
            {
                return RedirectToAction("Complaints");
            }
            var sender = complaint.Message?.Sender;
            ViewBag.SenderIsPassive = sender != null && sender.LockoutEnd != null && sender.LockoutEnd > DateTimeOffset.UtcNow;
            return View(complaint);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TogglePassive(int id)
        {
            var current = await _userManager.GetUserAsync(User);
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || current == null || user.Id == current.Id)
            {
                return RedirectToAction("Users");
            }
            var isPassive = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
            if (isPassive)
            {
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                await _userManager.SetLockoutEnabledAsync(user, true);
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleAdmin(int id)
        {
            var current = await _userManager.GetUserAsync(User);
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || current == null || user.Id == current.Id)
            {
                return RedirectToAction("Users");
            }
            var isAdmin = await _userManager.IsInRoleAsync(user, "Admin");
            if (isAdmin)
            {
                await _userManager.RemoveFromRoleAsync(user, "Admin");
            }
            else
            {
                await _userManager.AddToRoleAsync(user, "Admin");
            }
            return RedirectToAction("Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewComplaint(int id, bool makeSenderPassive = false)
        {
            var current = await _userManager.GetUserAsync(User);
            var complaint = await _context.MessageComplaints
                .Include(x => x.Message)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (complaint == null)
            {
                return RedirectToAction("Complaints");
            }
            if (makeSenderPassive && complaint.Message != null && current != null && complaint.Message.SenderId != current.Id)
            {
                var sender = await _userManager.FindByIdAsync(complaint.Message.SenderId.ToString());
                if (sender != null)
                {
                    await _userManager.SetLockoutEnabledAsync(sender, true);
                    await _userManager.SetLockoutEndDateAsync(sender, DateTimeOffset.UtcNow.AddYears(100));
                }
            }
            complaint.IsReviewed = true;
            await _context.SaveChangesAsync();
            return RedirectToAction("Complaints");
        }

        private List<T> ApplyPaging<T>(List<T> rows, int page)
        {
            var pageSize = 10;
            if (page < 1)
            {
                page = 1;
            }
            var totalCount = rows.Count;
            var totalPages = totalCount == 0 ? 1 : (int)Math.Ceiling(totalCount / (double)pageSize);
            if (page > totalPages)
            {
                page = totalPages;
            }
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            return rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }
    }
}
