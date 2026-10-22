using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Sinistros;

// Análise, aprovação, negativa e pagamento só diferem na transição do domínio e no que vai para a auditoria.
public sealed class AcaoSinistro(
    ISinistroRepository sinistros,
    ISinistroQueries consultas,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider tempo)
{
    public async Task<Result<SinistroDto>> ExecutarAsync(
        Guid sinistroId,
        Func<Sinistro, Guid, DateTimeOffset, Result> acao,
        string acaoAuditoria,
        Func<Sinistro, string?>? detalhes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(acao);

        // Autenticação antes de carregar: senão o anônimo receberia 404 para id inexistente e 401 para id existente.
        var autenticado = usuario.ExigirAutenticado();
        if (autenticado.IsFailure)
        {
            return autenticado.Error;
        }

        var sinistro = await sinistros.ObterAsync(sinistroId, cancellationToken);
        if (sinistro is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Sinistro);
        }

        // Visibilidade primeiro (corretor de outra carteira recebe 404); depois o papel (o dono recebe 403).
        var analista = usuario.AutorizarAnalise(sinistro.CorretorId, Entidades.Sinistro);
        if (analista.IsFailure)
        {
            return analista.Error;
        }

        var resultado = acao(sinistro, analista.Value, tempo.GetUtcNow());
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        auditoria.Registrar(acaoAuditoria, EntidadesAuditoria.Sinistro, sinistro.Id.ToString(), detalhes?.Invoke(sinistro));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Releitura.Obrigatoria(await consultas.ObterAsync(sinistro.Id, null, cancellationToken), Entidades.Sinistro);
    }
}
