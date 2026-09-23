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

            ViewBag.fullName=user.FirstName + "" + user.LastName;   

            var messages=await _context.UserMessages.Include(X=>X.Sender).Where(x=>x.ReceiverId==user.Id &&!x.IsDraft &&!x.IsDeletedByReceiver).ToListAsync();

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
            var receiver=await _userManager.FindByEmailAsync(sendMailDto.ReceiverMail);

            if(receiver is null)
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
                .Include(x=> x.Receiver)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (user == null || message == null || message.ReceiverId != user.Id)
            {
                return RedirectToAction("Index");
            }
            if (!message.IsRead)
            {
                message.IsRead = true;
                await _context.SaveChangesAsync();
            }
            return View(message);
        }

        public async Task<IActionResult> ToggleImportant(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message=await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.ReceiverId==user.Id);
            if(user==null || message == null)
            {
                return RedirectToAction("Index");
            }
            message.IsImportant = !message.IsImportant;
            await _context.SaveChangesAsync();
            return RedirectToAction("MailDetail",new { id=message.Id });
        }

        public async Task<IActionResult> MoveToTrash(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.ReceiverId == user.Id);
            if (user == null || message == null)
            {
                return RedirectToAction("Index");
            }
            message.IsDeletedByReceiver = true;
            await _context.SaveChangesAsync();
            return RedirectToAction("Index");
        }

    }
}
