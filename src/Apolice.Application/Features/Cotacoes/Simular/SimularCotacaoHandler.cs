using Apolice.Application.Abstractions.Messaging;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Features.Cotacoes.Simular;

public sealed class SimularCotacaoHandler(TarifacaoCotacao tarifacao, ICurrentUser usuario, TimeProvider tempo)
    : ICommandHandler<SimularCotacaoCommand, TarifacaoDto>
{
    public async Task<Result<TarifacaoDto>> HandleAsync(SimularCotacaoCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var corretor = usuario.ExigirPapel(Papel.Corretor, Papel.Admin);
        if (corretor.IsFailure)
        {
            return corretor.Error;
        }

        var tarifada = await tarifacao.SimularAsync(command.Cotacao, Relogio.Hoje(tempo), cancellationToken);
        return tarifada.IsSuccess ? TarifacaoDto.De(tarifada.Value.Resultado) : tarifada.Error;
    }
}
