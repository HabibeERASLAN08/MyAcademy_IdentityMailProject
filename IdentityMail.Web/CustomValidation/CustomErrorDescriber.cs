using Microsoft.AspNetCore.Identity;

namespace IdentityMail.Web.CustomValidation
{
    public class CustomErrorDescriber :IdentityErrorDescriber
    {
       public  override IdentityError PasswordRequiresDigit()
        {
            return new IdentityError
            {
                Code = "PasswordReguiresDigit",
                Description = "Şifre en az 1 rakam içermelidir."
            };
        }

        public override IdentityError PasswordRequiresUpper()
        {
            return new IdentityError
            {
                Code = "PasswordReguiresUpper",
                Description = "Şifre en az 1 büyük harf içermelidir.(A-Z)"
            };
        }
        public override IdentityError PasswordRequiresLower()
        {
            return new IdentityError
            {
                Code = "PasswordReguiresLower",
                Description = "Şifre en az 1 küçük harf içermelidir.(a-z)"
            };
        }
        public override IdentityError PasswordRequiresNonAlphanumeric()
        {
            return new IdentityError
            {
                Code = "PasswordReguiresNonAlphanumeric",
                Description = "Şifre en az 1 özel karakter içermelidir.(*,!,.,? vs.)"
            };
        }
        public override IdentityError PasswordTooShort(int length)
        {
            return new IdentityError
            {
                Code = "PasswordTooShort",
                Description = $"Şifre en az {length} karakterden oluşmalıdır."
            };
        }

        public override IdentityError DuplicateUserName(string userName)
        {
            return new IdentityError
            {
                Code = "DuplicateUserName",
                Description = $"{userName} kullanıcı adı daha önceden alınmış."
            };
        }

    }
}
