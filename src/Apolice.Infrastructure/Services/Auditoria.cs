using Apolice.Application.Abstractions.Security;
using Apolice.Application.Abstractions.Services;
using Apolice.Infrastructure.Correlacao;
using Apolice.Infrastructure.Persistence;
using Apolice.Infrastructure.Persistence.Registros;

namespace Apolice.Infrastructure.Services;

// Só enfileira no DbContext do escopo: grava junto com o SaveChanges do caso de uso (ou é descartada com ele).
internal sealed class Auditoria(ApoliceDbContext db, ICurrentUser usuario, ContextoCorrelacao correlacao, TimeProvider tempo) : IAuditoria
{
    public void Registrar(string acao, string entidade, string? entidadeId = null, string? detalhes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(acao);
        ArgumentException.ThrowIfNullOrWhiteSpace(entidade);

        var agora = tempo.GetUtcNow();
        db.Auditoria.Add(new AuditoriaRegistro(
            Guid.CreateVersion7(agora),
            usuario.Autenticado ? usuario.UsuarioId : null,
            acao,
            entidade,
            entidadeId,
            Limitar(detalhes),
            correlacao.CorrelationId,
            agora));
    }

    // Detalhes longos são cortados em vez de derrubar o caso de uso: a auditoria nunca deve impedir a operação.
    private static string? Limitar(string? detalhes) =>
        detalhes is { Length: > AuditoriaRegistro.TamanhoMaximoDetalhes } ? detalhes[..AuditoriaRegistro.TamanhoMaximoDetalhes] : detalhes;
}
