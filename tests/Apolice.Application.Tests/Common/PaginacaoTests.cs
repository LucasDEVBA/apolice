using Apolice.Application.Common;
using Apolice.Application.Tests.Suporte;

namespace Apolice.Application.Tests.Common;

public sealed class PaginacaoTests
{
    private static readonly DateTimeOffset Base = new(2026, 3, 10, 13, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Deve_usar_limite_padrao_e_sem_cursor_quando_parametros_ausentes()
    {
        var parametros = Paginacao.Resolver(null, null).DeveTerSucesso();

        parametros.ShouldBe(new ParametrosPagina(null, Paginacao.LimitePadrao));
        Paginacao.Resolver("  ", 5).DeveTerSucesso().ShouldBe(new ParametrosPagina(null, 5));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    [InlineData(-1)]
    public void Deve_rejeitar_quando_limite_fora_da_faixa(int limite)
    {
        Paginacao.Resolver(null, limite).DeveFalharCom("Comum.LimiteInvalido");
    }

    [Fact]
    public void Deve_decodificar_o_cursor_quando_informado()
    {
        var id = Guid.NewGuid();
        var parametros = Paginacao.Resolver(Cursor.Codificar(Base, id), 10).DeveTerSucesso();

        parametros.Apos.ShouldBe(new PosicaoCursor(Base, id));
        Paginacao.Resolver("lixo", 10).DeveFalharCom(ComumErros.CursorInvalido.Codigo);
    }

    [Fact]
    public void Deve_montar_proximo_cursor_pela_ultima_linha_devolvida_quando_ha_linha_extra()
    {
        var linhas = Enumerable.Range(0, 4).Select(i => (Instante: Base.AddMinutes(-i), Id: Guid.NewGuid())).ToList();

        var pagina = Pagina.Montar(linhas, 3, l => new PosicaoCursor(l.Instante, l.Id));

        pagina.Itens.Count.ShouldBe(3);
        Cursor.Decodificar(pagina.ProximoCursor).DeveTerSucesso().ShouldBe(new PosicaoCursor(linhas[2].Instante, linhas[2].Id));
    }

    [Fact]
    public void Deve_encerrar_sem_cursor_quando_nao_ha_mais_linhas()
    {
        var pagina = Pagina.Montar(new[] { 1, 2 }, 3, _ => new PosicaoCursor(Base, Guid.Empty));

        pagina.Itens.ShouldBe([1, 2]);
        pagina.ProximoCursor.ShouldBeNull();
        Pagina.Vazia<int>().Itens.ShouldBeEmpty();
        Should.Throw<ArgumentOutOfRangeException>(() => Pagina.Montar(new[] { 1 }, 0, _ => new PosicaoCursor(Base, Guid.Empty)));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("   ", null)]
    [InlineData("  maria ", "maria")]
    public void Deve_normalizar_filtro_textual_quando_vazio_ou_com_espacos(string? entrada, string? esperado)
    {
        Paginacao.NormalizarFiltro(entrada).ShouldBe(esperado);
    }
}
