using Apolice.Application.Common;
using FluentValidation;

namespace Apolice.Application.Features.Auth.Login;

// Sem checar formato do e-mail: o handler responde "credenciais inválidas" para qualquer combinação errada.
// No FluentValidation o WithMessage vale só para a regra imediatamente anterior: cada regra leva a sua mensagem.
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(r => r.Email)
            .NotEmpty()
            .WithMessage("Informe o e-mail.")
            .MaximumLength(LimitesEntrada.Email)
            .WithMessage($"O e-mail deve ter no máximo {LimitesEntrada.Email} caracteres.");
        RuleFor(r => r.Senha)
            .NotEmpty()
            .WithMessage("Informe a senha.")
            .MaximumLength(LimitesEntrada.Senha)
            .WithMessage($"A senha deve ter no máximo {LimitesEntrada.Senha} caracteres.");
    }
}
