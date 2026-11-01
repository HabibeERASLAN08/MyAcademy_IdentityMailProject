namespace IdentityMail.Web.Entities
{
    public class MessageComplaint
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public UserMessage Message { get; set; }
        public int ReporterId { get; set; }
        public AppUser Reporter { get; set; }
        public DateTime CreatedDate { get; set; }
        public bool IsReviewed { get; set; }
    }
}
