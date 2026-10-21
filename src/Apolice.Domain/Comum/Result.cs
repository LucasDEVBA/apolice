namespace Apolice.Domain.Comum;

public class Result
{
    private static readonly Result Sucesso = new(null);
    private readonly Error? _error;

    // private protected: só Result<T>, no próprio domínio, herda; ninguém cria uma variante de Result com semântica própria.
    private protected Result(Error? error)
    {
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public bool IsFailure => !IsSuccess;

    // Ler o erro de um sucesso é bug de quem chamou: falha rápido em vez de devolver um sentinela vazio.
    public Error Error => _error ?? throw new InvalidOperationException("Um resultado de sucesso não possui erro.");

    public static Result Success() => Sucesso;

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(error);
    }

    public static Result<T> Success<T>(T value) => Result<T>.Success(value);

    public static Result<T> Failure<T>(Error error) => Result<T>.Failure(error);

    public static implicit operator Result(Error error) => Failure(error);
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(null)
    {
        _value = value;
    }

    private Result(Error error)
        : base(error)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"O resultado falhou com '{Error.Codigo}' e não possui valor.");

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value);
    }

    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);
}
