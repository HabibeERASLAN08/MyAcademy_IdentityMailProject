namespace IdentityMail.Web.DTOs.AdminDtos
{
    public class AdminUserRow
    {
        public int Id { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool IsAdmin { get; set; }
        public bool IsPassive { get; set; }
    }
}
