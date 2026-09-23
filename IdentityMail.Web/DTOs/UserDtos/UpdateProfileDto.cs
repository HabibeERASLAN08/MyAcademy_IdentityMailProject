namespace IdentityMail.Web.DTOs.UserDtos
{
    public class UpdateProfileDto
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string? ExistingProfileImageUrl { get; set; }
        public IFormFile? ProfileImage { get; set; }
    }
}
