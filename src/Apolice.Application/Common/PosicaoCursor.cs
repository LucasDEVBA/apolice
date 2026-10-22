namespace Apolice.Application.Common;

// Última linha vista: a próxima página traz o que vem estritamente depois de (Instante, Id) em ordem DESC.
public sealed record PosicaoCursor(DateTimeOffset Instante, Guid Id);
