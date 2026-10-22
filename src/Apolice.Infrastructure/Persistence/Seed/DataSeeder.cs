using System.Security.Cryptography;
using Apolice.Application.Abstractions.Data;
using Apolice.Application.Abstractions.Security;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure.Configuracao;
using Apolice.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apolice.Infrastructure.Persistence.Seed;

// Verbo "seed" do Worker. Idempotente por partes: produtos e usuários que já existem são mantidos como estão (uma taxa
// alterada pelo Admin não volta ao padrão), e os dados de demonstração só entram num banco sem nenhuma cotação, numa
// transação única — ou o cenário inteiro existe, ou nada dele. Os eventos do cenário ficam no outbox: o relay os
// publica e os consumidores reais geram notificações e triagem, pelo mesmo caminho da produção.
public sealed partial class DataSeeder(
    ApoliceDbContext db,
    IPasswordHasher hasher,
    ITarifador tarifador,
    INumerador numerador,
    IOptions<SeedOptions> opcoes,
    IHostEnvironment ambiente,
    TimeProvider tempo,
    ILogger<DataSeeder> logger)
{
    private const int TamanhoSenhaAleatoriaBytes = 24;
    private const int TamanhoMinimoSenha = 8;
    private const int TamanhoMaximoSenha = 128;

    public async Task SemearAsync(CancellationToken cancellationToken)
    {
        await SemearProdutosAsync(cancellationToken);
        var usuarios = await SemearUsuariosAsync(cancellationToken);
        if (opcoes.Value.DadosDemo)
        {
            await SemearDemonstracaoAsync(usuarios, cancellationToken);
        }
    }

    private async Task SemearProdutosAsync(CancellationToken cancellationToken)
    {
        var existentes = await db.Produtos.Select(p => p.Id).ToListAsync(cancellationToken);
        var novos = CatalogoPadrao.Produtos().Where(p => !existentes.Contains(p.Codigo)).ToList();
        if (novos.Count == 0)
        {
            return;
        }

        db.Produtos.AddRange(novos);
        await db.SaveChangesAsync(cancellationToken);
        LogProdutos(logger, novos.Count);
    }

    private async Task<Dictionary<string, Usuario>> SemearUsuariosAsync(CancellationToken cancellationToken)
    {
        var existentes = new List<Usuario>();
        foreach (var demo in PersonasDemo.Usuarios)
        {
            var email = Email.Criar(demo.Email).Value;
            if (await db.Usuarios.FirstOrDefaultAsync(u => u.Email == email, cancellationToken) is { } usuario)
            {
                existentes.Add(usuario);
            }
        }

        var faltantes = PersonasDemo.Usuarios.Where(d => existentes.TrueForAll(u => u.Email.Valor != d.Email)).ToList();
        if (faltantes.Count > 0)
        {
            var senhaHash = hasher.Hash(ResolverSenha());
            var agora = tempo.GetUtcNow();
            var criados = faltantes.Select(d => Usuario.Criar(d.Nome, Email.Criar(d.Email).Value, senhaHash, d.Papel, agora).Value).ToList();
            db.Usuarios.AddRange(criados);
            await db.SaveChangesAsync(cancellationToken);
            existentes.AddRange(criados);
            LogUsuarios(logger, criados.Count);
        }

        return PersonasDemo.Usuarios.ToDictionary(d => d.Persona, d => existentes.First(u => u.Email.Valor == d.Email), StringComparer.Ordinal);
    }

    // Fora de Development a senha é obrigatória (vem do SSM no deploy). Em Development, sem ela, gera uma aleatória
    // e registra só o aviso: a senha nunca vai para o log (o login por persona demo dispensa senha).
    private string ResolverSenha()
    {
        var senha = opcoes.Value.SenhaUsuarios;
        if (!string.IsNullOrWhiteSpace(senha))
        {
            return senha.Length is >= TamanhoMinimoSenha and <= TamanhoMaximoSenha
                ? senha
                : throw new InvalidOperationException($"Seed:SenhaUsuarios deve ter entre {TamanhoMinimoSenha} e {TamanhoMaximoSenha} caracteres.");
        }

        if (!ambiente.IsDevelopment())
        {
            throw new InvalidOperationException("Seed:SenhaUsuarios é obrigatória fora de Development.");
        }

        LogSenhaAleatoria(logger);
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(TamanhoSenhaAleatoriaBytes));
    }

    private async Task SemearDemonstracaoAsync(Dictionary<string, Usuario> usuarios, CancellationToken cancellationToken)
    {
        if (await db.Cotacoes.AnyAsync(cancellationToken))
        {
            LogDemonstracaoExistente(logger);
            return;
        }

        var produtos = await db.Produtos.ComCoberturas().ToDictionaryAsync(p => p.Codigo, StringComparer.Ordinal, cancellationToken);
        var cenario = new CenarioDemonstracao(db, tarifador, numerador, produtos, usuarios, tempo.GetUtcNow());
        var resumo = await cenario.MontarAsync(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        LogDemonstracao(logger, resumo.Cotacoes, resumo.Apolices, resumo.Sinistros);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: {Quantidade} produto(s) do catálogo padrão criado(s).")]
    private static partial void LogProdutos(ILogger logger, int quantidade);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: {Quantidade} usuário(s) de demonstração criado(s).")]
    private static partial void LogUsuarios(ILogger logger, int quantidade);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Seed: Seed:SenhaUsuarios ausente em Development; os usuários demo receberam uma senha aleatória (use o login por persona).")]
    private static partial void LogSenhaAleatoria(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: o banco já tem cotações; dados de demonstração não foram recriados.")]
    private static partial void LogDemonstracaoExistente(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Seed: demonstração criada com {Cotacoes} cotações, {Apolices} apólices e {Sinistros} sinistros.")]
    private static partial void LogDemonstracao(ILogger logger, int cotacoes, int apolices, int sinistros);
}
