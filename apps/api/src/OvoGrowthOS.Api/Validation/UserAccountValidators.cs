using FluentValidation;
using OvoGrowthOS.Api.Features;

namespace OvoGrowthOS.Api.Validation;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.");
        RuleFor(x => x.Password).NotEmpty().MaximumLength(256).WithMessage("Şifrenizi girin (en fazla 256 karakter).");
    }
}

public sealed class UserAccountRequestValidator : AbstractValidator<UserAccountRequest>
{
    public UserAccountRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320).EmailAddress().WithMessage("Geçerli bir e-posta adresi girin (en fazla 320 karakter).");
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160).WithMessage("Ad soyad zorunludur ve 160 karakteri geçemez.");
        RuleFor(x => x.Role).Must(x => x is "Admin" or "Partner" or "Analyst").WithMessage("Geçerli bir kullanıcı rolü seçin.");
        RuleFor(x => x.Password).Must(x => x is null or "" || !string.IsNullOrWhiteSpace(x) && x.Length is >= 10 and <= 256)
            .WithMessage("Yeni şifre 10 ile 256 karakter arasında olmalıdır.");
    }
}
