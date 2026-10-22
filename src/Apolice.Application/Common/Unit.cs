namespace Apolice.Application.Common;

// Resposta vazia de comando (ex.: logout → 204): Result<T> não aceita valor nulo.
public readonly record struct Unit
{
    public static Unit Value { get; }
}
