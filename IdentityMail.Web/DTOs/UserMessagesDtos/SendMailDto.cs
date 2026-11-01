namespace IdentityMail.Web.DTOs.UserMessagesDtos
{
    public class SendMailDto
    {
        public int? Id { get; set; }
        public string? ReceiverMail { get; set; }
        public string? Subject { get; set; }
        public string? Body { get; set; }

        public int? ReplyToId { get; set; }
        public string? ReturnUrl { get; set; }

    }
}
