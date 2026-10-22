using FluentValidation;

namespace Apolice.Application.Features.Auth.LoginDemo;

public sealed class LoginDemoRequestValidator : AbstractValidator<LoginDemoRequest>
{
    public LoginDemoRequestValidator()
    {
        RuleFor(r => r.Persona)
            .Must(persona => PersonasDemo.EmailDe(persona) is not null)
            .WithMessage("Persona inválida (use corretora, corretor, analista ou admin).");
    }
}
