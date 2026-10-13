using IdentityMail.Web.Context;
using IdentityMail.Web.DTOs.UserMessagesDtos;
using IdentityMail.Web.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IdentityMail.Web.Controllers
{
    [Authorize]
    public class MessageController(UserManager<AppUser> _userManager,
                                               AppDbContext _context) : Controller
    {
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);

            ViewBag.fullName = user.FirstName + "" + user.LastName;

            var messages = await _context.UserMessages.Include(X => X.Sender).Where(x => x.ReceiverId == user.Id && !x.IsDraft && !x.IsDeletedByReceiver).ToListAsync();

            return View(messages);
        }

        public IActionResult SendMail()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> SendMail(SendMailDto sendMailDto)
        {
            var sender = await _userManager.FindByNameAsync(User.Identity.Name);
            var receiver = await _userManager.FindByEmailAsync(sendMailDto.ReceiverMail);

            if (receiver is null)
            {
                ModelState.AddModelError(string.Empty, "Girdiğiniz Mail ile sistemde kayıtlı kullanıcı bulunamadı.");
                return View(sendMailDto);
            }


            var newMessage = new UserMessage
            {
                SendDate = DateTime.Now,
                ReceiverId = receiver.Id,
                SenderId = sender.Id,
                Subject = sendMailDto.Subject,
                Body = sendMailDto.Body

            };

            _context.UserMessages.Add(newMessage);
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");

        }

        public async Task<IActionResult> MailDetail(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages
                .Include(x => x.Sender)
                .Include(x => x.Receiver)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (user == null || message == null || (message.ReceiverId != user.Id && message.SenderId != user.Id))
            {
                return RedirectToAction("Index");
            }
            var canSeeAsReceiver = message.ReceiverId == user.Id && !message.IsPurgedByReceiver;
            var canSeeAsSender = message.SenderId == user.Id && !message.IsPurgedBySender;
            if (!canSeeAsReceiver && !canSeeAsSender)
            {
                return RedirectToAction("Index");
            }

            var canSeeInbox = message.ReceiverId == user.Id && !message.IsDeletedByReceiver && !message.IsPurgedByReceiver;
            var canSeeSent = message.SenderId == user.Id && !message.IsDeletedBySender && !message.IsPurgedBySender && !message.IsDraft;
            var inTrash = !canSeeInbox && !canSeeSent;
            ViewBag.IsInTrash = inTrash;
            ViewBag.CanStar = message.ReceiverId == user.Id && canSeeInbox;
            ViewBag.CanMarkUnread = message.ReceiverId == user.Id && canSeeInbox;
            ViewBag.BackAction = inTrash
                ? "Trash"
                : (canSeeSent && !canSeeInbox ? "Sent" : "Index");

            if (message.ReceiverId == user.Id && !message.IsRead && !inTrash)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }

            return View(message);
        }

        public async Task<IActionResult> ToggleImportant(int id, string returnUrl)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.ReceiverId == user.Id);
            if (user == null || message == null)
            {
                return RedirectToAction("Index");
            }
            if (message.IsDeletedByReceiver || message.IsPurgedByReceiver)
            {
                return RedirectToAction("Index");
            }
            message.IsImportant = !message.IsImportant;
            await _context.SaveChangesAsync();
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("MailDetail", new { id = message.Id });
        }

        public async Task<IActionResult> MoveToTrash(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && (x.ReceiverId == user.Id || x.SenderId == user.Id));
            if (user == null || message == null)
            {
                return RedirectToAction("Index");
            }
            if (message.ReceiverId == user.Id)
            {
                message.IsDeletedByReceiver = true;
            }
            else if (message.SenderId == user.Id)
            {
                message.IsDeletedBySender = true;
            }
            await _context.SaveChangesAsync();
            if (message.SenderId == user.Id && message.ReceiverId != user.Id)
            {
                return RedirectToAction("Sent");
            }
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> Trash()
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var messages = await _context.UserMessages.Include(x => x.Sender).Include(x => x.Receiver)
                .Where(x => (x.ReceiverId == user.Id && x.IsDeletedByReceiver && !x.IsPurgedByReceiver)
                    || (x.SenderId == user.Id && x.IsDeletedBySender && !x.IsPurgedBySender))
                .ToListAsync();
            return View(messages);
        }
        public async Task<IActionResult> Important()
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var messages = await _context.UserMessages.Include(x => x.Sender)
                           .Where(x => x.ReceiverId == user.Id && x.IsImportant && !x.IsDraft && !x.IsDeletedByReceiver)
                           .ToListAsync();
            return View(messages);
        }

        public async Task<IActionResult> Sent()
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var messages = await _context.UserMessages.Include(x => x.Receiver)
                .Where(x => x.SenderId == user.Id && !x.IsDraft && !x.IsDeletedBySender)
                .ToListAsync();
            return View(messages);
        }
        public async Task<IActionResult> Restore(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && (x.ReceiverId == user.Id || x.SenderId == user.Id));
            if (user == null || message == null)
            {
                return RedirectToAction("Trash");
            }
            RestoreForUser(message, user);
            await _context.SaveChangesAsync();
            return RedirectToAction("Trash");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MoveSelectedToTrash(int[] ids, string returnAction)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var allowed = new[] { "Index", "Sent", "Important", "Trash" };
            if (string.IsNullOrEmpty(returnAction) || !allowed.Contains(returnAction))
            {
                returnAction = "Index";
            }
            if (user == null || ids == null || ids.Length == 0)
            {
                return RedirectToAction(returnAction);
            }
            var messages = await _context.UserMessages
                .Where(x => ids.Contains(x.Id) && (x.ReceiverId == user.Id || x.SenderId == user.Id))
                .ToListAsync();
            foreach (var message in messages)
            {
                if (returnAction == "Sent" && message.SenderId == user.Id)
                {
                    message.IsDeletedBySender = true;
                }
                else if (message.ReceiverId == user.Id)
                {
                    message.IsDeletedByReceiver = true;
                }
            }
            await _context.SaveChangesAsync();
            return RedirectToAction(returnAction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RestoreSelected(int[] ids)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null || ids == null || ids.Length == 0)
            {
                return RedirectToAction("Trash");
            }
            var messages = await _context.UserMessages
                .Where(x => ids.Contains(x.Id) && (x.ReceiverId == user.Id || x.SenderId == user.Id))
                .ToListAsync();
            foreach (var message in messages)
            {
                RestoreForUser(message, user);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Trash");
        }

        public async Task<IActionResult> DeleteForever(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && (x.ReceiverId == user.Id || x.SenderId == user.Id));
            if (user == null || message == null)
            {
                return RedirectToAction("Trash");
            }
            PurgeForUser(message, user);
            RemoveIfFullyPurged(message);
            await _context.SaveChangesAsync();
            return RedirectToAction("Trash");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelectedForever(int[] ids)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null || ids == null || ids.Length == 0)
            {
                return RedirectToAction("Trash");
            }
            var messages = await _context.UserMessages
                .Where(x => ids.Contains(x.Id) && (x.ReceiverId == user.Id || x.SenderId == user.Id))
                .ToListAsync();
            foreach (var message in messages)
            {
                PurgeForUser(message, user);
                RemoveIfFullyPurged(message);
            }
            await _context.SaveChangesAsync();
            return RedirectToAction("Trash");
        }

        public async Task<IActionResult> MarkUnread(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.ReceiverId == user.Id && !x.IsDeletedByReceiver && !x.IsPurgedByReceiver);
            if (user == null || message == null)
            {
                return RedirectToAction("Index");
            }
            message.IsRead = false;
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

        private static void RestoreForUser(UserMessage message, AppUser user)
        {
            if (message.ReceiverId == user.Id && message.IsDeletedByReceiver && !message.IsPurgedByReceiver)
            {
                message.IsDeletedByReceiver = false;
            }
            if (message.SenderId == user.Id && message.IsDeletedBySender && !message.IsPurgedBySender)
            {
                message.IsDeletedBySender = false;
            }
        }

        private static void PurgeForUser(UserMessage message, AppUser user)
        {
            if (message.ReceiverId == user.Id && message.IsDeletedByReceiver)
            {
                message.IsPurgedByReceiver = true;
            }
            if (message.SenderId == user.Id && message.IsDeletedBySender)
            {
                message.IsPurgedBySender = true;
            }
        }

        private void RemoveIfFullyPurged(UserMessage message)
        {
            if (message.IsDraft || (message.IsPurgedByReceiver && message.IsPurgedBySender))
            {
                _context.UserMessages.Remove(message);
            }
        }
    }
}
