using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Sinistros;
using Apolice.Infrastructure.Outbox;
using Apolice.Infrastructure.Persistence;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

public sealed class SeedTests(PostgresFixture postgres) : IAsyncLifetime
{
    private AmbienteInfra _ambiente = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        _ambiente = await AmbienteInfra.CriarAsync(await postgres.CriarBancoAsync("seed"));
        await _ambiente.SemearBaseAsync();
    }

    public async ValueTask DisposeAsync() => await _ambiente.DisposeAsync();

    [Fact]
    public async Task Deve_manter_as_mesmas_contagens_quando_semear_de_novo()
    {
        var antes = await ContagensAsync();

        await _ambiente.SemearBaseAsync();

        (await ContagensAsync()).ShouldBe(antes);
        antes.Produtos.ShouldBe(3);
        antes.Usuarios.ShouldBe(PersonasDemo.Usuarios.Count);
    }

    [Fact]
    public async Task Deve_cobrir_todos_os_status_quando_semear_dados_de_demonstracao()
    {
        var agora = _ambiente.Tempo.GetUtcNow();
        var hoje = Relogio.HojeEm(agora);
        var (cotacoes, apolices, sinistros) = await _ambiente.ExecutarAsync<ApoliceDbContext, (List<Cotacao>, List<ApoliceSeguro>, List<Sinistro>)>(async db =>
            (await db.Cotacoes.AsNoTracking().Completas().ToListAsync(Ct),
             await db.Apolices.AsNoTracking().Completas().ToListAsync(Ct),
             await db.Sinistros.AsNoTracking().Completos().ToListAsync(Ct)));

        cotacoes.Select(c => c.StatusEfetivo(agora)).Distinct().ShouldBe(Enum.GetValues<StatusEfetivoCotacao>(), ignoreOrder: true);
        apolices.Select(a => a.StatusEfetivo(hoje)).Distinct().ShouldBe(Enum.GetValues<StatusEfetivoApolice>(), ignoreOrder: true);
        sinistros.Select(s => s.Status).Distinct().ShouldBe(Enum.GetValues<StatusSinistro>(), ignoreOrder: true);
        apolices.Sum(a => a.Endossos.Count).ShouldBe(2);
        apolices.Count(a => a.Status == StatusApolice.Cancelada).ShouldBe(1);

        var ana = await UsuarioAsync(PersonasDemo.Corretora);
        var daAna = apolices.Where(a => a.CorretorId == ana).ToList();
        daAna.Count.ShouldBe(10);
        daAna.Select(a => (a.EmitidaEm.Year, a.EmitidaEm.Month)).Distinct().Count().ShouldBeGreaterThanOrEqualTo(10);
        daAna.ShouldAllBe(a => a.EmitidaEm > agora.AddMonths(-12) && a.EmitidaEm < agora);
        apolices.Count(a => a.CorretorId != ana).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Deve_gerar_notificacoes_pelos_consumidores_quando_o_relay_publica_os_eventos_do_seed()
    {
        var publicadas = await _ambiente.ExecutarAsync<OutboxDispatcher, int>(d => d.DespacharPendentesAsync(200, Ct));

        publicadas.ShouldBeGreaterThan(0);
        var tipos = await _ambiente.ExecutarAsync<ApoliceDbContext, List<TipoNotificacao>>(db => db.Notificacoes.Select(n => n.Tipo).Distinct().ToListAsync(Ct));
        tipos.ShouldBe(Enum.GetValues<TipoNotificacao>(), ignoreOrder: true);
        var pendentes = await _ambiente.ExecutarAsync<ApoliceDbContext, int>(db => db.Outbox.CountAsync(m => m.ProcessadoEm == null, Ct));
        pendentes.ShouldBe(0);
    }

    [Fact]
    public async Task Deve_exigir_senha_quando_semear_fora_de_development()
    {
        await using var producao = await AmbienteInfra.CriarAsync(
            await postgres.CriarBancoAsync("seed_prod"),
            new Dictionary<string, string?> { ["Seed:SenhaUsuarios"] = null },
            ambiente: "Production");

        var erro = await Should.ThrowAsync<InvalidOperationException>(producao.SemearBaseAsync);
        erro.Message.ShouldContain("Seed:SenhaUsuarios");
    }

    [Fact]
    public async Task Deve_gerar_senha_aleatoria_quando_semear_em_development_sem_senha()
    {
        await using var desenvolvimento = await AmbienteInfra.CriarAsync(
            await postgres.CriarBancoAsync("seed_dev"),
            new Dictionary<string, string?> { ["Seed:SenhaUsuarios"] = null, ["Seed:DadosDemo"] = "false" },
            ambiente: "Development");

        await desenvolvimento.SemearBaseAsync();

        var usuarios = await desenvolvimento.ExecutarAsync<ApoliceDbContext, int>(db => db.Usuarios.CountAsync(Ct));
        usuarios.ShouldBe(PersonasDemo.Usuarios.Count);
        var cotacoes = await desenvolvimento.ExecutarAsync<ApoliceDbContext, int>(db => db.Cotacoes.CountAsync(Ct));
        cotacoes.ShouldBe(0);
    }

    private async Task<Guid> UsuarioAsync(string persona)
    {
        var email = Email.Criar(PersonasDemo.EmailDe(persona)).Value;
        return await _ambiente.ExecutarAsync<ApoliceDbContext, Guid>(db => db.Usuarios.Where(u => u.Email == email).Select(u => u.Id).SingleAsync(Ct));
    }

    private Task<(int Produtos, int Usuarios, int Cotacoes, int Apolices, int Sinistros, int Outbox)> ContagensAsync() =>
        _ambiente.ExecutarAsync<ApoliceDbContext, (int, int, int, int, int, int)>(async db =>
            (await db.Produtos.CountAsync(Ct), await db.Usuarios.CountAsync(Ct), await db.Cotacoes.CountAsync(Ct),
             await db.Apolices.CountAsync(Ct), await db.Sinistros.CountAsync(Ct), await db.Outbox.CountAsync(Ct)));
}
