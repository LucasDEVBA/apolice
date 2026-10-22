using Apolice.Application.Abstractions.Data;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Persistence.Registros;
using Microsoft.EntityFrameworkCore;

namespace Apolice.Infrastructure.Persistence.Repositories;

// Repositórios com tracking: devolvem o agregado completo, pronto para mutação, e só enfileiram no UoW (o
// SaveChanges é do caso de uso).
internal sealed class CotacaoRepository(ApoliceDbContext db) : ICotacaoRepository
{
    public Task<Cotacao?> ObterAsync(Guid id, CancellationToken cancellationToken) =>
        db.Cotacoes.Completas().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public void Adicionar(Cotacao cotacao) => db.Cotacoes.Add(cotacao);
}

internal sealed class ApoliceRepository(ApoliceDbContext db) : IApoliceRepository
{
    public Task<ApoliceSeguro?> ObterAsync(Guid id, CancellationToken cancellationToken) =>
        db.Apolices.Completas().FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public void Adicionar(ApoliceSeguro apolice) => db.Apolices.Add(apolice);
}

internal sealed class SinistroRepository(ApoliceDbContext db) : ISinistroRepository
{
    public Task<Sinistro?> ObterAsync(Guid id, CancellationToken cancellationToken) =>
        db.Sinistros.Completos().FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Adicionar(Sinistro sinistro) => db.Sinistros.Add(sinistro);
}

internal sealed class ProdutoRepository(ApoliceDbContext db) : IProdutoRepository
{
    public Task<Produto?> ObterAsync(string codigo, CancellationToken cancellationToken) =>
        db.Produtos.ComCoberturas().FirstOrDefaultAsync(p => p.Id == codigo, cancellationToken);

    public async Task<IReadOnlyList<Produto>> ListarAsync(CancellationToken cancellationToken) =>
        await db.Produtos.ComCoberturas().OrderBy(p => p.Id).ToListAsync(cancellationToken);
}

internal sealed class UsuarioRepository(ApoliceDbContext db) : IUsuarioRepository
{
    public Task<Usuario?> ObterPorEmailAsync(Email email, CancellationToken cancellationToken) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

    public Task<Usuario?> ObterAsync(Guid id, CancellationToken cancellationToken) =>
        db.Usuarios.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
}

internal sealed class RefreshTokenRepository(ApoliceDbContext db) : IRefreshTokenRepository
{
    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

    public async Task<IReadOnlyList<RefreshToken>> ListarNaoRevogadosDaFamiliaAsync(Guid familiaId, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(t => t.FamiliaId == familiaId && t.RevogadoEm == null).ToListAsync(cancellationToken);

    public void Adicionar(RefreshToken token) => db.RefreshTokens.Add(token);
}

internal sealed class NotificacaoRepository(ApoliceDbContext db) : INotificacaoRepository
{
    public void Adicionar(Notificacao notificacao) => db.Notificacoes.Add(notificacao);
}

internal sealed class MensagensProcessadas(ApoliceDbContext db) : IMensagensProcessadas
{
    // O registro ainda não salvo (mesmo UoW) também conta: o dispatcher não pode rodar o mesmo consumidor duas vezes.
    public async Task<bool> JaProcessadaAsync(Guid mensagemId, string consumidor, CancellationToken cancellationToken) =>
        db.MensagensProcessadas.Local.Any(m => m.MensagemId == mensagemId && m.Consumidor == consumidor)
        || await db.MensagensProcessadas.AnyAsync(m => m.MensagemId == mensagemId && m.Consumidor == consumidor, cancellationToken);

    public void Registrar(Guid mensagemId, string consumidor, DateTimeOffset agora) =>
        db.MensagensProcessadas.Add(new MensagemProcessada(mensagemId, consumidor, agora));
}
