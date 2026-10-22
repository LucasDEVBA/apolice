using System.Buffers.Text;
using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;
using Apolice.Infrastructure;
using Apolice.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Apolice.Api.IntegrationTests.Infraestrutura;

// Sem banco: a composição é montada com uma connection string que nunca é aberta.
public sealed class SegurancaTests
{
    private const string SemBanco = "Host=localhost;Port=1;Database=nenhum;Username=ninguem";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Deve_emitir_access_token_hs256_com_os_claims_do_spec()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(SemBanco, migrar: false);
        var usuario = Usuario.Criar("Carla Souza", Email.Criar("carla.analista@apolice.dev").Value, "hash", Papel.Analista, ambiente.Tempo.GetUtcNow()).Value;
        var tokens = ambiente.Provedor.GetRequiredService<ITokenService>();

        var (token, expiraEm) = tokens.GerarAccessToken(usuario);

        expiraEm.ShouldBe(ambiente.Tempo.GetUtcNow().AddMinutes(15));
        var opcoes = ambiente.Provedor.GetRequiredService<IOptions<JwtOptions>>().Value;
        var validacao = await new JsonWebTokenHandler().ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidIssuer = "apolice-api",
            ValidAudience = "apolice-web",
            IssuerSigningKey = opcoes.ChaveAssinatura(),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // O relógio do teste é falso (FakeTimeProvider): a expiração é conferida pelo claim, abaixo.
            ValidateLifetime = false,
        });
        validacao.IsValid.ShouldBeTrue(validacao.Exception?.Message);
        var jwt = (JsonWebToken)validacao.SecurityToken;
        jwt.Subject.ShouldBe(usuario.Id.ToString());
        jwt.GetClaim("name").Value.ShouldBe("Carla Souza");
        jwt.GetClaim("email").Value.ShouldBe("carla.analista@apolice.dev");
        jwt.GetClaim(JwtOptions.ClaimPapel).Value.ShouldBe("Analista");
        jwt.Id.ShouldNotBeNullOrWhiteSpace();
        jwt.ValidTo.ShouldBe(expiraEm.UtcDateTime, TimeSpan.FromSeconds(1));
        tokens.ValidadeRefreshToken.ShouldBe(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task Deve_gerar_refresh_tokens_aleatorios_de_32_bytes_em_base64url()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(SemBanco, migrar: false);
        var tokens = ambiente.Provedor.GetRequiredService<ITokenService>();

        var gerados = Enumerable.Range(0, 50).Select(_ => tokens.GerarRefreshToken()).ToList();

        gerados.Distinct().Count().ShouldBe(50);
        gerados.ShouldAllBe(t => Base64Url.IsValid(t) && Base64Url.DecodeFromChars(t).Length == 32);
    }

    [Fact]
    public async Task Deve_recusar_chave_jwt_curta()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(SemBanco, new Dictionary<string, string?> { ["Jwt:SigningKey"] = "curta" }, migrar: false);

        var erro = Should.Throw<OptionsValidationException>(() => ambiente.Provedor.GetRequiredService<IOptions<JwtOptions>>().Value);
        erro.Message.ShouldContain("32 bytes");
    }

    [Fact]
    public async Task Deve_derrubar_a_subida_quando_a_api_exige_jwt_e_a_chave_e_curta()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(
            SemBanco,
            new Dictionary<string, string?> { ["Jwt:SigningKey"] = "curta" },
            servicos => servicos.ExigirJwtNaInicializacao(),
            migrar: false);

        Should.Throw<OptionsValidationException>(() => ambiente.Provedor.GetRequiredService<IStartupValidator>().Validate());
    }

    [Fact]
    public async Task Deve_respeitar_flag_desligada_na_configuracao_e_manter_as_demais_ligadas()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(SemBanco, new Dictionary<string, string?> { ["FeatureManagement:DocumentoPdf"] = "false" }, migrar: false);
        var flags = ambiente.Provedor.GetRequiredService<Application.Abstractions.Services.IFeatureFlags>();

        (await flags.IsEnabledAsync(Application.Abstractions.Services.FeatureFlags.DocumentoPdf, Ct)).ShouldBeFalse();
        (await flags.IsEnabledAsync(Application.Abstractions.Services.FeatureFlags.LoginDemo, Ct)).ShouldBeTrue();
    }

    [Fact]
    public void Deve_ler_o_envelope_sqs_com_os_mesmos_atributos_que_o_publicador_grava()
    {
        var id = Guid.CreateVersion7();
        var atributos = new Dictionary<string, string?>
        {
            [Apolice.Infrastructure.Messaging.CabecalhosMensagem.Tipo] = "SinistroAberto",
            [Apolice.Infrastructure.Messaging.CabecalhosMensagem.MensagemId] = id.ToString(),
            [Apolice.Infrastructure.Messaging.CabecalhosMensagem.CorrelationId] = "corr-sqs",
            [Apolice.Infrastructure.Messaging.CabecalhosMensagem.OcorridoEm] = "2026-10-15T13:00:00.0000000+00:00",
        };

        var mensagem = Apolice.Infrastructure.Messaging.Sqs.EnvelopeSqs.Ler("{\"a\":1}", atributos)!;

        mensagem.Id.ShouldBe(id);
        mensagem.Tipo.ShouldBe("SinistroAberto");
        mensagem.CorrelationId.ShouldBe("corr-sqs");
        mensagem.Traceparent.ShouldBeNull();
        mensagem.OcorridoEm.ShouldBe(new DateTimeOffset(2026, 10, 15, 13, 0, 0, TimeSpan.Zero));
        Apolice.Infrastructure.Messaging.Sqs.EnvelopeSqs.Ler("{}", new Dictionary<string, string?> { ["tipo"] = "X" }).ShouldBeNull();
    }

    [Theory]
    [InlineData("Development", true)]
    [InlineData("Production", false)]
    public async Task Deve_gerar_chave_efemera_so_em_development_quando_nao_ha_chave(string nomeAmbiente, bool aceita)
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(SemBanco, new Dictionary<string, string?> { ["Jwt:SigningKey"] = null }, migrar: false, ambiente: nomeAmbiente);

        JwtOptions Ler() => ambiente.Provedor.GetRequiredService<IOptions<JwtOptions>>().Value;
        if (aceita)
        {
            Ler().SigningKey!.Length.ShouldBeGreaterThanOrEqualTo(JwtOptions.TamanhoMinimoChaveBytes);
        }
        else
        {
            Should.Throw<OptionsValidationException>(Ler);
        }
    }

    [Fact]
    public async Task Deve_verificar_senha_pelo_pbkdf2_e_simular_custo_sem_aceitar_nada()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(SemBanco, migrar: false);
        var hasher = ambiente.Provedor.GetRequiredService<IPasswordHasher>();

        var hash = hasher.Hash("S3nha-forte!");

        hash.ShouldNotContain("S3nha-forte!");
        hasher.Verificar(hash, "S3nha-forte!").ShouldBeTrue();
        hasher.Verificar(hash, "s3nha-forte!").ShouldBeFalse();
        hasher.Verificar("isto-nao-e-base64-$$$", "S3nha-forte!").ShouldBeFalse();
        hasher.Verificar(string.Empty, "x").ShouldBeFalse();
        Should.NotThrow(() => hasher.SimularVerificacao("qualquer"));
    }

    [Fact]
    public async Task Deve_recusar_configuracao_de_dsql_e_sqs_incompleta_no_start()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(
            SemBanco,
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Dsql",
                ["Database:DsqlEndpoint"] = "nao-e-um-endpoint",
                ["Database:AppRole"] = "papel; DROP TABLE x",
                ["Database:AppRoleArns:0"] = "arn:aws:iam::123:role/curta",
            },
            migrar: false);

        var erro = Should.Throw<OptionsValidationException>(() => ambiente.Provedor.GetRequiredService<IOptions<Apolice.Infrastructure.Configuracao.DatabaseOptions>>().Value);
        erro.Failures.Count().ShouldBe(3);
    }

    [Fact]
    public async Task Deve_aceitar_configuracao_valida_de_dsql()
    {
        await using var ambiente = await AmbienteInfra.CriarAsync(
            SemBanco,
            new Dictionary<string, string?>
            {
                ["Database:Provider"] = "Dsql",
                ["Database:DsqlEndpoint"] = "abcdefghijklmnopqrstuvwxyz.dsql.us-east-1.on.aws",
                ["Database:DsqlUser"] = "apolice_app",
                ["Database:AppRoleArns:0"] = "arn:aws:iam::123456789012:role/apolice-api-role",
            },
            migrar: false);

        var opcoes = ambiente.Provedor.GetRequiredService<IOptions<Apolice.Infrastructure.Configuracao.DatabaseOptions>>().Value;
        opcoes.AppRoleArns.ShouldHaveSingleItem();
        opcoes.UsuarioAdmin.ShouldBeFalse();
        Apolice.Infrastructure.Persistence.Provedores.FonteDadosNpgsql.RegiaoDoEndpoint(opcoes.DsqlEndpoint!).ShouldBe("us-east-1");
    }
}
