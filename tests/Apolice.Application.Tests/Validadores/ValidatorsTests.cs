using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Apolices.Emitir;
using Apolice.Application.Features.Apolices.Listar;
using Apolice.Application.Features.Auditoria.Listar;
using Apolice.Application.Features.Auth.Login;
using Apolice.Application.Features.Auth.LoginDemo;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Cotacoes.Listar;
using Apolice.Application.Features.Produtos.AtualizarTaxa;
using Apolice.Application.Features.Sinistros.Abrir;
using Apolice.Application.Features.Sinistros.Aprovar;
using Apolice.Application.Features.Sinistros.Listar;
using Apolice.Application.Features.Sinistros.Negar;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Apolices;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using FluentValidation;

namespace Apolice.Application.Tests.Validadores;

public sealed class ValidatorsTests
{
    private static readonly string CursorValido = Cursor.Codificar(DateTimeOffset.UnixEpoch, Guid.NewGuid());

    [Fact]
    public void Deve_aceitar_login_valido_e_recusar_campos_vazios_ou_longos()
    {
        var validator = new LoginRequestValidator();

        validator.Validate(new LoginRequest("ana@apolice.dev", "x")).IsValid.ShouldBeTrue();
        validator.Validate(new LoginRequest(new string('a', 254), new string('b', 128))).IsValid.ShouldBeTrue();
        Mensagens(validator, new LoginRequest("", "")).ShouldBe([("Email", "Informe o e-mail."), ("Senha", "Informe a senha.")]);
        Mensagens(validator, new LoginRequest(new string('a', 255), new string('b', 129)))
            .ShouldBe([("Email", "O e-mail deve ter no máximo 254 caracteres."), ("Senha", "A senha deve ter no máximo 128 caracteres.")]);
    }

    // No FluentValidation o WithMessage vale só para a regra anterior: sem mensagem própria, o campo vazio cairia na
    // mensagem padrão em inglês ("'X' must not be empty.") e o campo longo herdaria o texto de "informe".
    [Fact]
    public void Deve_dar_a_cada_regra_a_propria_mensagem_quando_codigo_ou_texto_vazio_ou_longo()
    {
        var longo = new string('x', 31);
        var textoLongo = new string('x', 2001);

        Mensagens(new CotacaoRequestValidator(), Dados.CotacaoAuto(new CoberturaRequest("", null)))
            .ShouldBe([("Coberturas[2].Codigo", "Informe o código da cobertura.")]);
        Mensagens(new CotacaoRequestValidator(), Dados.CotacaoAuto(new CoberturaRequest(longo, null)))
            .ShouldBe([("Coberturas[2].Codigo", "O código da cobertura deve ter no máximo 30 caracteres.")]);
        Mensagens(new EndossoRequestValidator(), new EndossoRequest(TipoEndosso.InclusaoCobertura, "", null))
            .ShouldBe([("CoberturaCodigo", "Informe o código da cobertura.")]);
        Mensagens(new EndossoRequestValidator(), new EndossoRequest(TipoEndosso.InclusaoCobertura, longo, null))
            .ShouldBe([("CoberturaCodigo", "O código da cobertura deve ter no máximo 30 caracteres.")]);
        Mensagens(new AbrirSinistroRequestValidator(), new AbrirSinistroRequest(Guid.NewGuid(), "", new DateOnly(2026, 3, 1), "", 10m))
            .ShouldBe([("CoberturaCodigo", "Informe o código da cobertura."), ("Descricao", "Descreva a ocorrência.")]);
        Mensagens(new AbrirSinistroRequestValidator(), new AbrirSinistroRequest(Guid.NewGuid(), longo, new DateOnly(2026, 3, 1), textoLongo, 10m))
            .ShouldBe([("CoberturaCodigo", "O código da cobertura deve ter no máximo 30 caracteres."), ("Descricao", "A descrição deve ter no máximo 2000 caracteres.")]);
        Mensagens(new NegarSinistroRequestValidator(), new NegarSinistroRequest("")).ShouldBe([("Motivo", "Informe o motivo da negativa.")]);
        Mensagens(new NegarSinistroRequestValidator(), new NegarSinistroRequest(textoLongo)).ShouldBe([("Motivo", "O motivo da negativa deve ter no máximo 2000 caracteres.")]);
        Mensagens(new AprovarSinistroRequestValidator(), new AprovarSinistroRequest(1m, textoLongo)).ShouldBe([("Observacao", "A observação deve ter no máximo 2000 caracteres.")]);
    }

    [Theory]
    [InlineData("corretora", true)]
    [InlineData("ADMIN", true)]
    [InlineData("gerente", false)]
    [InlineData(null, false)]
    public void Deve_validar_persona_quando_login_demo(string? persona, bool valido)
    {
        new LoginDemoRequestValidator().Validate(new LoginDemoRequest(persona!)).IsValid.ShouldBe(valido);
    }

    [Fact]
    public void Deve_aceitar_cotacao_completa_quando_campos_presentes()
    {
        new CotacaoRequestValidator().Validate(Dados.CotacaoAuto()).IsValid.ShouldBeTrue();
        new CotacaoRequestValidator().Validate(Dados.CotacaoVida()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(CotacoesInvalidas))]
    public void Deve_apontar_o_campo_quando_cotacao_invalida(CotacaoRequest request, string propriedade)
    {
        PropriedadesInvalidas(new CotacaoRequestValidator(), request).ShouldContain(propriedade);
    }

    public static TheoryData<CotacaoRequest, string> CotacoesInvalidas() => new()
    {
        { Dados.CotacaoAuto() with { ProdutoCodigo = "MOTO" }, "ProdutoCodigo" },
        { Dados.CotacaoAuto() with { Segurado = null! }, "Segurado" },
        { Dados.CotacaoAuto() with { Segurado = Dados.Segurado(nome: "") }, "Segurado.Nome" },
        { Dados.CotacaoAuto() with { Segurado = Dados.Segurado(cpf: "123.456.789-00") }, "Segurado.Cpf" },
        { Dados.CotacaoAuto() with { Segurado = new SeguradoRequest("Maria", Dados.CpfValido, "maria@", new DateOnly(1985, 5, 20)) }, "Segurado.Email" },
        { Dados.CotacaoAuto() with { Segurado = new SeguradoRequest("Maria", Dados.CpfValido, Dados.EmailSegurado, default) }, "Segurado.DataNascimento" },
        { Dados.CotacaoAuto() with { DadosRisco = null! }, "DadosRisco" },
        { Dados.CotacaoAuto() with { Coberturas = [] }, "Coberturas" },
        { Dados.CotacaoAuto(new CoberturaRequest(CodigosCobertura.Casco, null)), "Coberturas" },
        { Dados.CotacaoAuto(new CoberturaRequest(" casco ", null)), "Coberturas" },
        { Dados.CotacaoAuto() with { Coberturas = [null!] }, "Coberturas[0]" },
        { Dados.CotacaoAuto(new CoberturaRequest("", null)), "Coberturas[2].Codigo" },
        { Dados.CotacaoAuto(new CoberturaRequest(CodigosCobertura.RcfDanosCorporais, -1m)), "Coberturas[2].Lmi" },
    };

    // Mesma regra de normalização da tarifação: o que o validator aceita, o domínio encontra.
    [Fact]
    public void Deve_aceitar_codigos_em_qualquer_caixa_quando_cotacao_usa_minusculas()
    {
        var request = Dados.CotacaoAuto() with
        {
            ProdutoCodigo = " auto ",
            Coberturas = [new("casco", null), new("rcf_dm", 100_000m)],
        };

        new CotacaoRequestValidator().Validate(request).IsValid.ShouldBeTrue();
    }

    // Produto e dados de risco de tipos diferentes é regra da tarifação (Tarifacao.ProdutoIncompativel), não repetida aqui.
    [Fact]
    public void Deve_deixar_a_coerencia_entre_produto_e_dados_de_risco_para_o_dominio()
    {
        new CotacaoRequestValidator().Validate(Dados.CotacaoAuto() with { DadosRisco = new DadosRiscoVida(true) }).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Deve_acusar_so_o_item_nulo_e_nao_repeticao_quando_lista_tem_cobertura_nula()
    {
        var erros = new CotacaoRequestValidator().Validate(Dados.CotacaoAuto() with { Coberturas = [new("CASCO", null), null!] }).Errors;

        erros.ShouldHaveSingleItem().PropertyName.ShouldBe("Coberturas[1]");
    }

    [Fact]
    public void Deve_esconder_senha_e_tokens_quando_dtos_de_autenticacao_viram_texto()
    {
        var resposta = new Features.Auth.AuthResponse("token-secreto", DateTimeOffset.UnixEpoch, new Features.Auth.UsuarioDto(Guid.Empty, "Ana", "ana@x.com", Domain.Usuarios.Papel.Corretor));

        new LoginRequest("ana@x.com", "senha-secreta").ToString().ShouldNotContain("senha-secreta");
        resposta.ToString().ShouldNotContain("token-secreto");
        new Features.Auth.ResultadoAutenticacao(resposta, "refresh-secreto", DateTimeOffset.UnixEpoch).ToString().ShouldNotContain("secreto");
    }

    [Fact]
    public void Deve_validar_emissao_quando_cotacao_inicio_e_parcelas()
    {
        var validator = new EmitirApoliceRequestValidator();

        validator.Validate(new EmitirApoliceRequest(Guid.NewGuid(), new DateOnly(2026, 3, 10), 1)).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(validator, new EmitirApoliceRequest(Guid.Empty, default, 0)).ShouldBe(["CotacaoId", "InicioVigencia", "Parcelas"]);
    }

    [Fact]
    public void Deve_exigir_lmi_positivo_quando_endosso_altera_limite()
    {
        var validator = new EndossoRequestValidator();

        validator.Validate(new EndossoRequest(TipoEndosso.InclusaoCobertura, "VIDROS", null)).IsValid.ShouldBeTrue();
        validator.Validate(new EndossoRequest(TipoEndosso.AlteracaoLmi, "RCF_DM", 200_000m)).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(validator, new EndossoRequest(TipoEndosso.AlteracaoLmi, "RCF_DM", null)).ShouldBe(["Lmi"]);
        PropriedadesInvalidas(validator, new EndossoRequest((TipoEndosso)9, "", -5m)).ShouldBe(["Tipo", "CoberturaCodigo", "Lmi"]);
    }

    [Fact]
    public void Deve_validar_motivo_de_cancelamento_quando_fora_do_enum()
    {
        var validator = new CancelamentoRequestValidator();

        validator.Validate(new CancelamentoRequest(MotivoCancelamento.Inadimplencia)).IsValid.ShouldBeTrue();
        validator.Validate(new CancelamentoRequest((MotivoCancelamento)7)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Deve_validar_aviso_de_sinistro_quando_campos_obrigatorios_ausentes()
    {
        var validator = new AbrirSinistroRequestValidator();

        validator.Validate(new AbrirSinistroRequest(Guid.NewGuid(), "CASCO", new DateOnly(2026, 3, 1), Ambiente.DescricaoSinistro, 10m)).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(validator, new AbrirSinistroRequest(Guid.Empty, "", default, new string('x', 2001), 0m))
            .ShouldBe(["ApoliceId", "CoberturaCodigo", "DataOcorrencia", "Descricao", "ValorReclamado"]);
    }

    [Fact]
    public void Deve_validar_decisoes_do_analista_quando_valor_ou_texto_invalidos()
    {
        new AprovarSinistroRequestValidator().Validate(new AprovarSinistroRequest(10m, null)).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(new AprovarSinistroRequestValidator(), new AprovarSinistroRequest(0m, new string('o', 2001))).ShouldBe(["ValorIndenizacao", "Observacao"]);
        new NegarSinistroRequestValidator().Validate(new NegarSinistroRequest("Motivo detalhado.")).IsValid.ShouldBeTrue();
        new NegarSinistroRequestValidator().Validate(new NegarSinistroRequest(" ")).IsValid.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0.035, true)]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(1.01, false)]
    public void Deve_usar_a_faixa_do_dominio_quando_valida_nova_taxa(double taxa, bool valida)
    {
        new AtualizarTaxaRequestValidator().Validate(new AtualizarTaxaRequest((decimal)taxa)).IsValid.ShouldBe(valida);
    }

    [Fact]
    public void Deve_validar_filtros_e_paginacao_quando_lista()
    {
        new ListarCotacoesQueryValidator().Validate(new ListarCotacoesQuery(StatusEfetivoCotacao.Expirada, "AUTO", "maria", CursorValido, 100)).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(new ListarCotacoesQueryValidator(), new ListarCotacoesQuery((StatusEfetivoCotacao)9, "MOTO", new string('b', 101), "x", 0))
            .ShouldBe(["Status", "Produto", "Busca", "Cursor", "Limite"]);
        new ListarCotacoesQueryValidator().Validate(new ListarCotacoesQuery(Produto: " auto ")).IsValid.ShouldBeTrue();
        new ListarApolicesQueryValidator().Validate(new ListarApolicesQuery()).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(new ListarApolicesQueryValidator(), new ListarApolicesQuery((StatusEfetivoApolice)9, "MOTO", new string('b', 101), "x", 101))
            .ShouldBe(["Status", "Produto", "Busca", "Cursor", "Limite"]);
        new ListarSinistrosQueryValidator().Validate(new ListarSinistrosQuery(StatusSinistro.Pago, null, CursorValido, 1)).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(new ListarSinistrosQueryValidator(), new ListarSinistrosQuery((StatusSinistro)9, new string('b', 101), "x", 0))
            .ShouldBe(["Status", "Busca", "Cursor", "Limite"]);
        new ListarAuditoriaQueryValidator().Validate(new ListarAuditoriaQuery()).IsValid.ShouldBeTrue();
        PropriedadesInvalidas(new ListarAuditoriaQueryValidator(), new ListarAuditoriaQuery("x", 0)).ShouldBe(["Cursor", "Limite"]);
    }

    [Fact]
    public void Deve_trazer_mensagens_em_portugues_quando_valida()
    {
        var erros = new CotacaoRequestValidator().Validate(Dados.CotacaoAuto() with { Segurado = Dados.Segurado(cpf: "1") }).Errors;

        erros.ShouldHaveSingleItem().ErrorMessage.ShouldBe("CPF inválido.");
    }

    private static string[] PropriedadesInvalidas<T>(AbstractValidator<T> validator, T instancia) =>
        [.. validator.Validate(instancia).Errors.Select(e => e.PropertyName).Distinct()];

    private static (string Propriedade, string Mensagem)[] Mensagens<T>(AbstractValidator<T> validator, T instancia) =>
        [.. validator.Validate(instancia).Errors.Select(e => (e.PropertyName, e.ErrorMessage))];
}
