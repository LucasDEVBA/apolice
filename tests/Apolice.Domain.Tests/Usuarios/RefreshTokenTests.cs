using Apolice.Domain.Comum;
using Apolice.Domain.Tests.Suporte;
using Apolice.Domain.Usuarios;
using Microsoft.Extensions.Time.Testing;

namespace Apolice.Domain.Tests.Usuarios;

public sealed class RefreshTokenTests
{
    private static readonly Guid UsuarioId = Cenarios.CorretorId;
    private static readonly Guid NovoId = Guid.Parse("0199a1b2-0000-7000-8000-0000000000dd");
    private static readonly string Hash = RefreshToken.CalcularHash("token-opaco-de-teste");

    [Fact]
    public void Deve_gerar_sha256_hex_minusculo_quando_calcula_hash()
    {
        // Vetor oficial do SHA-256 para "abc".
        RefreshToken.CalcularHash("abc").ShouldBe("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad");
        Should.Throw<ArgumentException>(() => RefreshToken.CalcularHash(""));
    }

    [Fact]
    public void Deve_expirar_em_7_dias_quando_criado_com_validade_padrao()
    {
        var token = RefreshToken.NovaFamilia(UsuarioId, Hash, Cenarios.Agora, RefreshToken.ValidadePadrao);

        token.Id.Version.ShouldBe(7);
        (token.UsuarioId, token.TokenHash, token.CriadoEm).ShouldBe((UsuarioId, Hash, Cenarios.Agora));
        token.FamiliaId.ShouldNotBe(Guid.Empty);
        token.FamiliaId.ShouldNotBe(token.Id);
        token.ExpiraEm.ShouldBe(Cenarios.Agora.AddDays(7));
        token.RevogadoEm.ShouldBeNull();
        token.SubstituidoPorId.ShouldBeNull();
    }

    [Fact]
    public void Deve_estar_inativo_quando_atinge_o_instante_de_expiracao()
    {
        var token = NovoToken();

        token.EstaAtivo(token.ExpiraEm.AddTicks(-1)).ShouldBeTrue();
        token.EstaAtivo(token.ExpiraEm).ShouldBeFalse();
    }

    [Fact]
    public void Deve_revogar_uma_vez_e_manter_a_primeira_data_quando_revogado_de_novo()
    {
        var token = NovoToken();
        var tempo = new FakeTimeProvider(Cenarios.Agora);

        tempo.Advance(TimeSpan.FromHours(1));
        token.Revogar(tempo.GetUtcNow());
        var primeiraRevogacao = tempo.GetUtcNow();
        tempo.Advance(TimeSpan.FromHours(1));
        token.Revogar(tempo.GetUtcNow());

        token.RevogadoEm.ShouldBe(primeiraRevogacao);
        token.EstaRevogado.ShouldBeTrue();
        token.EstaAtivo(tempo.GetUtcNow()).ShouldBeFalse();
    }

    [Fact]
    public void Deve_revogar_e_apontar_o_substituto_quando_rotacionado()
    {
        var token = NovoToken();
        var agora = Cenarios.Agora.AddMinutes(20);

        token.Rotacionar(NovoId, agora).DeveTerSucesso();

        token.RevogadoEm.ShouldBe(agora);
        token.SubstituidoPorId.ShouldBe(NovoId);
        token.EstaAtivo(agora).ShouldBeFalse();
    }

    [Fact]
    public void Deve_sinalizar_reuso_quando_token_ja_rotacionado_e_usado_de_novo()
    {
        var token = NovoToken();
        token.Rotacionar(NovoId, Cenarios.Agora.AddMinutes(20));

        token.Rotacionar(Guid.Parse("0199a1b2-0000-7000-8000-0000000000ee"), Cenarios.Agora.AddMinutes(30))
            .DeveFalharCom("RefreshToken.Revogado", ErrorType.NaoAutorizado);

        token.SubstituidoPorId.ShouldBe(NovoId); // o primeiro substituto continua registrado
    }

    [Fact]
    public void Deve_sinalizar_reuso_quando_token_rotacionado_e_reapresentado_apos_expirar()
    {
        // Revogado vem antes de expirado: é o que faz a aplicação revogar a família de um token roubado,
        // mesmo que o ladrão só o reapresente depois da expiração.
        var token = NovoToken();
        token.Rotacionar(NovoId, Cenarios.Agora.AddMinutes(20));

        token.Rotacionar(Guid.Parse("0199a1b2-0000-7000-8000-0000000000ee"), token.ExpiraEm.AddDays(1))
            .DeveFalharCom("RefreshToken.Revogado", ErrorType.NaoAutorizado);
    }

    [Fact]
    public void Deve_negar_rotacao_quando_expirado()
    {
        var token = NovoToken();

        token.Rotacionar(NovoId, token.ExpiraEm).DeveFalharCom("RefreshToken.Expirado", ErrorType.NaoAutorizado);
        token.RevogadoEm.ShouldBeNull();
    }

    [Fact]
    public void Deve_manter_a_familia_quando_criado_como_substituto()
    {
        var original = NovoToken();

        var substituto = RefreshToken.Criar(UsuarioId, Hash, original.FamiliaId, Cenarios.Agora, RefreshToken.ValidadePadrao);

        substituto.FamiliaId.ShouldBe(original.FamiliaId);
    }

    [Fact]
    public void Deve_lancar_quando_dados_gerados_pelo_servidor_sao_invalidos()
    {
        var familia = Guid.Parse("0199a1b2-0000-7000-8000-0000000000ff");

        Should.Throw<ArgumentException>(() => RefreshToken.Criar(Guid.Empty, Hash, familia, Cenarios.Agora, RefreshToken.ValidadePadrao));
        Should.Throw<ArgumentException>(() => RefreshToken.Criar(UsuarioId, Hash, Guid.Empty, Cenarios.Agora, RefreshToken.ValidadePadrao));
        Should.Throw<ArgumentException>(() => RefreshToken.Criar(UsuarioId, Hash.ToUpperInvariant(), familia, Cenarios.Agora, RefreshToken.ValidadePadrao));
        Should.Throw<ArgumentException>(() => RefreshToken.Criar(UsuarioId, "abc", familia, Cenarios.Agora, RefreshToken.ValidadePadrao));
        Should.Throw<ArgumentException>(() => RefreshToken.Criar(UsuarioId, null!, familia, Cenarios.Agora, RefreshToken.ValidadePadrao));
        Should.Throw<ArgumentOutOfRangeException>(() => RefreshToken.Criar(UsuarioId, Hash, familia, Cenarios.Agora, TimeSpan.Zero));

        var token = NovoToken();
        Should.Throw<ArgumentException>(() => token.Rotacionar(Guid.Empty, Cenarios.Agora));
        Should.Throw<ArgumentException>(() => token.Rotacionar(token.Id, Cenarios.Agora));
    }

    private static RefreshToken NovoToken() =>
        RefreshToken.NovaFamilia(UsuarioId, Hash, Cenarios.Agora, RefreshToken.ValidadePadrao);
}
