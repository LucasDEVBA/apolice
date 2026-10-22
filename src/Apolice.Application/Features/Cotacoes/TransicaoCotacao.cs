using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;

namespace Apolice.Application.Features.Cotacoes;

// Aceite e recusa só diferem na transição do domínio: carga, autorização, auditoria e releitura são iguais.
public sealed class TransicaoCotacao(
    ICotacaoRepository cotacoes,
    ICotacaoQueries consultas,
    IAuditoria auditoria,
    IUnitOfWork unitOfWork,
    ICurrentUser usuario,
    TimeProvider tempo)
{
    public async Task<Result<CotacaoDto>> ExecutarAsync(
        Guid cotacaoId,
        Func<Cotacao, DateTimeOffset, Result> transicao,
        string acaoAuditoria,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(transicao);

        // Autenticação antes de carregar: senão o anônimo receberia 404 para id inexistente e 401 para id existente.
        var autenticado = usuario.ExigirAutenticado();
        if (autenticado.IsFailure)
        {
            return autenticado.Error;
        }

        var cotacao = await cotacoes.ObterAsync(cotacaoId, cancellationToken);
        if (cotacao is null)
        {
            return ComumErros.NaoEncontrado(Entidades.Cotacao);
        }

        var acesso = usuario.AutorizarCarteira(cotacao.CorretorId, Entidades.Cotacao);
        if (acesso.IsFailure)
        {
            return acesso.Error;
        }

        var agora = tempo.GetUtcNow();
        var resultado = transicao(cotacao, agora);
        if (resultado.IsFailure)
        {
            return resultado.Error;
        }

        auditoria.Registrar(acaoAuditoria, EntidadesAuditoria.Cotacao, cotacao.Id.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Releitura.Obrigatoria(await consultas.ObterAsync(cotacao.Id, null, agora, cancellationToken), Entidades.Cotacao);
    }
}
