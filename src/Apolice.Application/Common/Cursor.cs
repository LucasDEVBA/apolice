using System.Buffers.Binary;
using System.Buffers.Text;
using Apolice.Domain.Comum;

namespace Apolice.Application.Common;

// Cursor opaco para o cliente: base64url de 8 bytes (ticks UTC) + 16 bytes (Guid), ambos big-endian.
public static class Cursor
{
    private const int TamanhoTicks = sizeof(long);
    private const int TamanhoGuid = 16;
    private const int TamanhoBytes = TamanhoTicks + TamanhoGuid;
    private const int TamanhoMaximoTexto = 64;

    public static string Codificar(DateTimeOffset instante, Guid id)
    {
        Span<byte> bytes = stackalloc byte[TamanhoBytes];
        BinaryPrimitives.WriteInt64BigEndian(bytes, instante.UtcTicks);
        id.TryWriteBytes(bytes[TamanhoTicks..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    public static Result<PosicaoCursor> Decodificar(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor) || cursor.Length > TamanhoMaximoTexto)
        {
            return ComumErros.CursorInvalido;
        }

        // IsValid antes de decodificar: o TryDecode só protege o tamanho do destino e lança com caractere inválido.
        if (!Base64Url.IsValid(cursor, out var tamanhoDecodificado) || tamanhoDecodificado != TamanhoBytes)
        {
            return ComumErros.CursorInvalido;
        }

        Span<byte> bytes = stackalloc byte[TamanhoBytes];
        Base64Url.DecodeFromChars(cursor, bytes);

        var ticks = BinaryPrimitives.ReadInt64BigEndian(bytes);
        if (ticks < DateTimeOffset.MinValue.UtcTicks || ticks > DateTimeOffset.MaxValue.UtcTicks)
        {
            return ComumErros.CursorInvalido;
        }

        var id = new Guid(bytes[TamanhoTicks..TamanhoBytes], bigEndian: true);
        return new PosicaoCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    public static bool EhValido(string? cursor) => string.IsNullOrWhiteSpace(cursor) || Decodificar(cursor).IsSuccess;
}
