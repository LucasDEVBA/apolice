using Apolice.Application.Common;
using Apolice.Application.Tests.Suporte;
using Apolice.Domain.Comum;

namespace Apolice.Application.Tests.Common;

public sealed class CursorTests
{
    [Fact]
    public void Deve_recuperar_instante_e_id_quando_decodifica_o_que_codificou()
    {
        var instante = new DateTimeOffset(2026, 3, 10, 10, 30, 15, 123, Relogio.OffsetBrasilia);
        var id = Guid.CreateVersion7(instante);

        var cursor = Cursor.Codificar(instante, id);
        var posicao = Cursor.Decodificar(cursor).DeveTerSucesso();

        posicao.Instante.ShouldBe(instante);
        posicao.Instante.Offset.ShouldBe(TimeSpan.Zero);
        posicao.Id.ShouldBe(id);
        cursor.ShouldNotContain("=");
        cursor.ShouldNotContain("+");
        cursor.ShouldNotContain("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("não é base64!")]
    [InlineData("AAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void Deve_retornar_erro_de_validacao_quando_cursor_invalido(string? cursor)
    {
        Cursor.Decodificar(cursor).DeveFalharCom(ComumErros.CursorInvalido.Codigo, ErrorType.Validacao);
    }

    [Fact]
    public void Deve_rejeitar_quando_ticks_fora_do_intervalo_de_datas()
    {
        // 24 bytes com ticks = long.MaxValue (fora de DateTimeOffset.MaxValue).
        var bytes = new byte[24];
        bytes[0] = 0x7F;
        for (var i = 1; i < 8; i++)
        {
            bytes[i] = 0xFF;
        }

        var cursor = System.Buffers.Text.Base64Url.EncodeToString(bytes);

        Cursor.Decodificar(cursor).DeveFalharCom(ComumErros.CursorInvalido.Codigo);
        Cursor.EhValido(cursor).ShouldBeFalse();
    }

    [Fact]
    public void Deve_rejeitar_quando_ticks_negativos()
    {
        var bytes = new byte[24];
        bytes[0] = 0x80;

        Cursor.Decodificar(System.Buffers.Text.Base64Url.EncodeToString(bytes)).DeveFalharCom(ComumErros.CursorInvalido.Codigo);
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("lixo", false)]
    public void Deve_considerar_ausente_valido_quando_valida_cursor(string? cursor, bool valido)
    {
        Cursor.EhValido(cursor).ShouldBe(valido);
    }
}
