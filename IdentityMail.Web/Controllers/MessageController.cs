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
        // Gelen kutusu: bana gelen, taslak olmayan, çöpe atmadığım mailler.
        public async Task<IActionResult> Index(int? categoryId, bool? isRead, bool? isImportant, string? search, string? subject, DateTime? startDate, DateTime? endDate, string? sortOrder, int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);

            ViewBag.fullName = user.FirstName + " " + user.LastName;

            var messages = await _context.UserMessages.Include(x => x.Sender)
                .Where(x => x.ReceiverId == user.Id && !x.IsDraft && !x.IsDeletedByReceiver)
                .ToListAsync();
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                messages = InMarkedThreads(messages, x => x.CategoryId == categoryId.Value);
            }

            if (isRead == false)
            {
                messages = InMarkedThreads(messages, x => !x.IsRead);
            }
            else if (isRead == true)
            {
                var unreadKeys = messages.Where(x => !x.IsRead).Select(x => x.ThreadId ?? x.Id).ToHashSet();
                messages = messages.Where(x => !unreadKeys.Contains(x.ThreadId ?? x.Id)).ToList();
            }
            if (isImportant == true)
            {
                messages = InMarkedThreads(messages, x => x.IsImportant);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                messages = InMarkedThreads(messages, x =>
                    ContainsText(x.Sender.FirstName + " " + x.Sender.LastName, term)
                    || ContainsText(x.Sender.Email, term)
                    || ContainsText(x.Subject, term)
                    || ContainsText(x.Body, term));
            }
            if (startDate.HasValue)
            {
                messages = InMarkedThreads(messages, x => x.SendDate.Date >= startDate.Value.Date);
            }
            if (endDate.HasValue)
            {
                messages = InMarkedThreads(messages, x => x.SendDate.Date <= endDate.Value.Date);
            }
            ViewBag.IsImportantFilter = isImportant;
            ViewBag.IsReadFilter = isRead;
            ViewBag.CategoryId = categoryId;
            ViewBag.Search = search;
            ViewBag.StartDate = startDate;
            ViewBag.EndDate = endDate;
            ViewBag.SortOrder = sortOrder;

            ViewBag.Categories = await _context.Categories
                                .Where(x => x.UserId == user.Id)
                                .OrderBy(x => x.Name)
                                .ToListAsync();
            var rows = GroupByThread(messages);
            if (sortOrder == "asc")
            {
                rows = rows.OrderBy(x => x.SendDate).ToList();
            }
            ViewBag.RowUnread = UnreadRowIds(messages);
            ViewBag.RowStarred = MarkedRowIds(messages, rows, x => x.IsImportant);
            ViewBag.RowCategoryIds = RowCategoryMap(messages, rows);
            rows = ApplyPaging(rows, page);
            return View(rows);
        }

        // Yeni mesaj sayfası (GET). Asıl yazma ekranı layout’taki overlay; bu action doğrulama hatasında yedek view açar.
        public IActionResult SendMail()
        {
            return View();
        }

        [HttpPost]
        // Yeni mesajı kayıtlı kullanıcıya gönderir (POST).
        public async Task<IActionResult> SendMail(SendMailDto sendMailDto)
        {
            var sender = await _userManager.FindByNameAsync(User.Identity.Name);
            AppUser receiver = null;
            if (!string.IsNullOrWhiteSpace(sendMailDto.ReceiverMail))
            {
                receiver = await _userManager.FindByEmailAsync(sendMailDto.ReceiverMail);
            }

            if (receiver is null)
            {
                ModelState.AddModelError(string.Empty, "Girdiğiniz Mail ile sistemde kayıtlı kullanıcı bulunamadı.");
                return View(sendMailDto);
            }

            UserMessage message;
            if (sendMailDto.Id.HasValue)
            {
                message = await _context.UserMessages.FirstOrDefaultAsync(x =>
                    x.Id == sendMailDto.Id.Value && x.SenderId == sender.Id && x.IsDraft);
                if (message == null)
                {
                    return RedirectAfterCompose(sendMailDto.ReturnUrl, "Drafts");
                }
            }
            else
            {
                message = new UserMessage
                {
                    SenderId = sender.Id
                };
                _context.UserMessages.Add(message);
            }
            message.SendDate = DateTime.Now;
            message.ReceiverId = receiver.Id;
            message.Subject = sendMailDto.Subject ?? "";
            message.Body = sendMailDto.Body ?? "";



            if (sendMailDto.ReplyToId.HasValue)
            {
                var parent = await _context.UserMessages.FirstOrDefaultAsync(x =>
                    x.Id == sendMailDto.ReplyToId.Value
                    && (x.SenderId == sender.Id || x.ReceiverId == sender.Id));
                if (parent != null)
                {
                    message.ThreadId = parent.ThreadId ?? parent.Id;
                }
            }
            message.IsDraft = false;


            await _context.SaveChangesAsync();
            if (sendMailDto.ReplyToId.HasValue)
            {
                return RedirectToAction("MailDetail", new { id = sendMailDto.ReplyToId.Value });
            }
            return RedirectAfterCompose(sendMailDto.ReturnUrl, "Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveDraft(SendMailDto sendMailDto)
        {
            var sender = await _userManager.FindByNameAsync(User.Identity.Name);
            if (sender == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            AppUser receiver = null;
            if (!string.IsNullOrWhiteSpace(sendMailDto.ReceiverMail))
            {
                receiver = await _userManager.FindByEmailAsync(sendMailDto.ReceiverMail);
            }
            var hasText = !string.IsNullOrWhiteSpace(sendMailDto.Subject)
                || !string.IsNullOrWhiteSpace(sendMailDto.Body);
            if (!sendMailDto.Id.HasValue && receiver == null && !hasText)
            {
                return RedirectAfterCompose(sendMailDto.ReturnUrl, "Drafts");
            }
            UserMessage draft;
            if (sendMailDto.Id.HasValue)
            {
                draft = await _context.UserMessages.FirstOrDefaultAsync(x =>
                    x.Id == sendMailDto.Id.Value && x.SenderId == sender.Id && x.IsDraft);
                if (draft == null)
                {
                    return RedirectAfterCompose(sendMailDto.ReturnUrl, "Drafts");
                }
            }
            else
            {
                draft = new UserMessage
                {
                    SenderId = sender.Id,
                    IsDraft = true
                };
                _context.UserMessages.Add(draft);
            }
            draft.SendDate = DateTime.Now;
            draft.ReceiverId = receiver != null ? receiver.Id : null;
            draft.Subject = sendMailDto.Subject ?? "";
            draft.Body = sendMailDto.Body ?? "";

            if (sendMailDto.ReplyToId.HasValue)
            {
                var parent = await _context.UserMessages.FirstOrDefaultAsync(x =>
                    x.Id == sendMailDto.ReplyToId.Value
                    && (x.SenderId == sender.Id || x.ReceiverId == sender.Id));
                if (parent != null)
                {
                    draft.ThreadId = parent.ThreadId ?? parent.Id;
                }
            }
            draft.IsDraft = true;
            await _context.SaveChangesAsync();
            return RedirectAfterCompose(sendMailDto.ReturnUrl, "Drafts");
        }

        // Mailin içini açar. Alıcıysa okundu işaretler. Çöpteyse yıldız/okunmadı göstermez.
        public async Task<IActionResult> MailDetail(int id, string? from)
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
            var fromReported = from == "Reported";
            var inTrash = !fromReported && !canSeeInbox && !canSeeSent;
            ViewBag.IsInTrash = inTrash;
            ViewBag.CanStar = message.ReceiverId == user.Id && (canSeeInbox || fromReported);
            ViewBag.CanMarkUnread = message.ReceiverId == user.Id && (canSeeInbox || fromReported);
            ViewBag.FromFolder = from;
            ViewBag.BackAction = fromReported
                ? "Reported"
                : (inTrash ? "Trash" : (canSeeSent && !canSeeInbox ? "Sent" : "Index"));

            ViewBag.CanSetCategory = message.ReceiverId == user.Id && canSeeInbox;
            ViewBag.Categories = await _context.Categories
                .Where(x => x.UserId == user.Id)
                .OrderBy(x => x.Name)
                .ToListAsync();

            var threadKey = message.ThreadId ?? message.Id;
            var thread = await _context.UserMessages
                .Include(x => x.Sender)
                .Include(x => x.Receiver)
                .Where(x => (x.ThreadId == threadKey || x.Id == threadKey)
                    && !x.IsDraft
                    && (x.SenderId == user.Id || x.ReceiverId == user.Id)
                    && !((x.SenderId == user.Id && x.IsPurgedBySender)
                        || (x.ReceiverId == user.Id && x.IsPurgedByReceiver)))
                .OrderBy(x => x.SendDate)
                .ToListAsync();
            ViewBag.Thread = thread;

            if (!inTrash)
            {
                foreach (var item in thread)
                {
                    if (item.ReceiverId == user.Id && !item.IsRead)
                    {
                        item.IsRead = true;
                    }
                }
                await _context.SaveChangesAsync();
            }

            var last = thread.LastOrDefault() ?? message;
            var other = last.SenderId == user.Id ? last.Receiver : last.Sender;
            ViewBag.ReplyToId = last.Id;
            ViewBag.ReplyMail = other != null ? other.Email : "";
            var replySubject = last.Subject ?? "";
            if (!replySubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
            {
                replySubject = "Re: " + replySubject;
            }
            ViewBag.ReplySubject = replySubject;
            ViewBag.ShowReplyBox = !inTrash && other != null;
            ViewBag.CanReport = message.ReceiverId == user.Id && message.SenderId != user.Id;
            ViewBag.HasReported = await _context.MessageComplaints.AnyAsync(x => x.MessageId == id && x.ReporterId == user.Id);

            return View(message);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Report(int id)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id);
            if (user == null || message == null || (message.SenderId != user.Id && message.ReceiverId != user.Id))
            {
                return RedirectToAction("Index");
            }
            if (message.SenderId == user.Id)
            {
                return RedirectToAction("MailDetail", new { id });
            }
            var exists = await _context.MessageComplaints.AnyAsync(x => x.MessageId == id && x.ReporterId == user.Id);
            if (!exists)
            {
                _context.MessageComplaints.Add(new MessageComplaint
                {
                    MessageId = id,
                    ReporterId = user.Id,
                    CreatedDate = DateTime.Now
                });
                await _context.SaveChangesAsync();
            }
            return RedirectToAction("MailDetail", new { id });
        }

        public async Task<IActionResult> Reported(int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            var mine = await _context.MessageComplaints
                .Where(x => x.ReporterId == user.Id)
                .ToListAsync();
            var messageIds = mine.Select(x => x.MessageId).ToList();
            var messages = await _context.UserMessages
                .Include(x => x.Sender)
                .Where(x => messageIds.Contains(x.Id)
                    && !(x.ReceiverId == user.Id && (x.IsDeletedByReceiver || x.IsPurgedByReceiver)))
                .OrderByDescending(x => x.SendDate)
                .ToListAsync();
            ViewBag.ReviewedMap = mine.GroupBy(x => x.MessageId).ToDictionary(g => g.Key, g => g.Any(x => x.IsReviewed));
            messages = ApplyPaging(messages, page);
            return View(messages);
        }

        public async Task<IActionResult> SetCategory(int id, int? categoryId, string? returnUrl)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages
                .FirstOrDefaultAsync(x => x.Id == id && x.ReceiverId == user.Id && !x.IsDeletedByReceiver && !x.IsPurgedByReceiver);
            if (user == null || message == null)
            {
                return RedirectToAction("Index");
            }
            if (categoryId.HasValue && categoryId.Value > 0)
            {
                var category = await _context.Categories
                    .FirstOrDefaultAsync(x => x.Id == categoryId.Value && x.UserId == user.Id);
                if (category == null)
                {
                    return RedirectToAction("Index");
                }
                message.CategoryId = category.Id;
            }
            else
            {
                message.CategoryId = null;
            }
            await _context.SaveChangesAsync();
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index");
        }


        // Yıldızı aç/kapa. Yalnızca alıcı. returnUrl: işlem bitince hangi listeye dönülecek.
        public async Task<IActionResult> ToggleImportant(int id, string? returnUrl)
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
            var threadKey = message.ThreadId ?? message.Id;
            var thread = await _context.UserMessages
                .Where(x => (x.ThreadId == threadKey || x.Id == threadKey)
                    && x.ReceiverId == user.Id
                    && !x.IsDraft
                    && !x.IsDeletedByReceiver
                    && !x.IsPurgedByReceiver)
                .ToListAsync();
            var makeImportant = !thread.Any(x => x.IsImportant);
            foreach (var item in thread)
            {
                item.IsImportant = makeImportant;
            }
            await _context.SaveChangesAsync();
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("MailDetail", new { id = message.Id });
        }

        // Tek maili çöpe taşır. Alıcıysa IsDeletedByReceiver, gönderense IsDeletedBySender.
        public async Task<IActionResult> MoveToTrash(int id, string? from)
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
            if (from == "Reported")
            {
                return RedirectToAction("Reported");
            }
            if (message.SenderId == user.Id && message.ReceiverId != user.Id)
            {
                return RedirectToAction("Sent");
            }
            return RedirectToAction("Index");
        }

        // Çöp kutusu listesi. Kalıcı silinenler (purge) burada durmaz.
        public async Task<IActionResult> Trash(int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var messages = await _context.UserMessages.Include(x => x.Sender).Include(x => x.Receiver)
                .Where(x => (x.ReceiverId == user.Id && x.IsDeletedByReceiver && !x.IsPurgedByReceiver)
                    || (x.SenderId == user.Id && x.IsDeletedBySender && !x.IsPurgedBySender))
                .OrderByDescending(x => x.SendDate)
                .ToListAsync();
            messages = ApplyPaging(messages, page);
            return View(messages);
        }
        // Yıldızlı mailler. Çöptekiler ve taslaklar yok.
        public async Task<IActionResult> Important(int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var inbox = await _context.UserMessages.Include(x => x.Sender)
                .Where(x => x.ReceiverId == user.Id && !x.IsDraft && !x.IsDeletedByReceiver)
                .ToListAsync();
            var messages = InMarkedThreads(inbox, x => x.IsImportant);
            var rows = GroupByThread(messages);
            ViewBag.RowUnread = UnreadRowIds(messages);
            ViewBag.RowStarred = MarkedRowIds(messages, rows, x => x.IsImportant);
            rows = ApplyPaging(rows, page);
            return View(rows);
        }

        // Gönderilenler: benim attığım, taslak olmayan, gönderen olarak çöpe atmadığım mailler.
        public async Task<IActionResult> Sent(int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var messages = await _context.UserMessages.Include(x => x.Receiver)
                .Where(x => x.SenderId == user.Id && !x.IsDraft && !x.IsDeletedBySender)
                .ToListAsync();
            ViewBag.RowUnread = UnreadRowIds(messages);
            var rows = GroupByThread(messages);
            rows = ApplyPaging(rows, page);
            return View(rows);
        }
        // Çöpteki tek maili geri yükler (çöp bayrağını kapatır).

        public async Task<IActionResult> Drafts(int? editId, int page = 1)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            if (user == null)
            {
                return RedirectToAction("Login", "Auth");
            }
            var drafts = await _context.UserMessages.Include(x => x.Receiver)
                .Where(x => x.SenderId == user.Id && x.IsDraft && !x.IsDeletedBySender)
                .OrderByDescending(x => x.SendDate)
                .ToListAsync();
            if (editId.HasValue)
            {
                var draft = drafts.FirstOrDefault(x => x.Id == editId.Value);
                if (draft != null)
                {
                    ViewBag.DraftId = draft.Id;
                    ViewBag.DraftMail = draft.Receiver != null ? draft.Receiver.Email : "";
                    ViewBag.DraftSubject = draft.Subject;
                    ViewBag.DraftBody = draft.Body;
                }
            }
            drafts = ApplyPaging(drafts, page);
            return View(drafts);
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
        // Üst araç çubuğundan seçilen mailleri çöpe taşır. returnAction: Index / Sent / Important.
        public async Task<IActionResult> MoveSelectedToTrash(int[] ids, string? returnAction)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var allowed = new[] { "Index", "Sent", "Important", "Trash", "Drafts" };
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
                if ((returnAction == "Sent" || returnAction == "Drafts") && message.SenderId == user.Id)
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
        // Çöpte kutusu tiklenen mailleri toplu geri yükler.
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

        // Çöpteki tek maili kalıcı siler (senin kutundan düşer; karşı tarafın kutusuna dokunmaz).
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
        // Çöpte seçilen mailleri toplu kalıcı siler.
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

        // Maili okunmadı yapar ve gelen kutusuna döner. Yalnızca alıcı.
        public async Task<IActionResult> MarkUnread(int id, string? from)
        {
            var user = await _userManager.FindByNameAsync(User.Identity.Name);
            var message = await _context.UserMessages.FirstOrDefaultAsync(x => x.Id == id && x.ReceiverId == user.Id && !x.IsDeletedByReceiver && !x.IsPurgedByReceiver);
            if (user == null || message == null)
            {
                return RedirectToAction("Index");
            }
            var threadKey = message.ThreadId ?? message.Id;
            var thread = await _context.UserMessages
                .Where(x => (x.ThreadId == threadKey || x.Id == threadKey)
                    && x.ReceiverId == user.Id
                    && !x.IsDraft
                    && !x.IsDeletedByReceiver
                    && !x.IsPurgedByReceiver)
                .ToListAsync();
            foreach (var item in thread)
            {
                item.IsRead = false;
            }
            await _context.SaveChangesAsync();
            if (from == "Reported")
            {
                return RedirectToAction("Reported");
            }
            return RedirectToAction("Index");
        }

        private static bool ContainsText(string text, string term)
        {
            return !string.IsNullOrEmpty(text)
                && text.Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private static List<UserMessage> InMarkedThreads(List<UserMessage> messages, Func<UserMessage, bool> mark)
        {
            var keys = messages.Where(mark).Select(x => x.ThreadId ?? x.Id).ToHashSet();
            return messages.Where(x => keys.Contains(x.ThreadId ?? x.Id)).ToList();
        }

        private static HashSet<int> MarkedRowIds(List<UserMessage> messages, List<UserMessage> rows, Func<UserMessage, bool> mark)
        {
            var keys = messages.Where(mark).Select(x => x.ThreadId ?? x.Id).ToHashSet();
            return rows.Where(x => keys.Contains(x.ThreadId ?? x.Id)).Select(x => x.Id).ToHashSet();
        }

        private static Dictionary<int, int> RowCategoryMap(List<UserMessage> messages, List<UserMessage> rows)
        {
            var map = new Dictionary<int, int>();
            foreach (var row in rows)
            {
                var key = row.ThreadId ?? row.Id;
                var marked = messages.FirstOrDefault(x => (x.ThreadId ?? x.Id) == key && x.CategoryId.HasValue);
                if (marked?.CategoryId != null)
                {
                    map[row.Id] = marked.CategoryId.Value;
                }
            }
            return map;
        }

        private IActionResult RedirectAfterCompose(string returnUrl, string fallbackAction)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction(fallbackAction);
        }

        private List<UserMessage> ApplyPaging(List<UserMessage> rows, int page)
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

        private static List<UserMessage> GroupByThread(List<UserMessage> messages)
        {
            return messages
                .GroupBy(x => x.ThreadId ?? x.Id)
                .Select(g => g.OrderByDescending(x => x.SendDate).First())
                .OrderByDescending(x => x.SendDate)
                .ToList();
        }

        private static HashSet<int> UnreadRowIds(List<UserMessage> messages)
        {
            return messages
                .GroupBy(x => x.ThreadId ?? x.Id)
                .Where(g => g.Any(x => !x.IsRead))
                .Select(g => g.OrderByDescending(x => x.SendDate).First().Id)
                .ToHashSet();
        }

        // Yardımcı: bu kullanıcı için çöp bayrağını kapatır (geri yükle).
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

        // Yardımcı: bu kullanıcı için kalıcı sil bayrağını açar (purge = kutudan tamamen düşür).
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

        // Yardımcı: taslaksa veya her iki taraf da kalıcı sildiyse satırı veritabanından siler.
        private void RemoveIfFullyPurged(UserMessage message)
        {
            if (message.IsDraft || (message.IsPurgedByReceiver && message.IsPurgedBySender))
            {
                var complaints = _context.MessageComplaints.Where(x => x.MessageId == message.Id);
                _context.MessageComplaints.RemoveRange(complaints);
                _context.UserMessages.Remove(message);
            }
        }
    }
}
