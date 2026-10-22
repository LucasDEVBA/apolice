namespace Apolice.Infrastructure.Persistence.Registros;

// Resposta gravada para um Idempotency-Key (SPEC 7): mesma chave + mesmo usuário + mesmo corpo devolve a resposta
// original; corpo diferente é recusado. Só o hash da requisição é guardado, nunca o corpo de entrada.
internal sealed class IdempotenciaRegistro
{
    public const int TamanhoMaximoChave = 100;
    public const int TamanhoHash = 64;
    public const int TamanhoMaximoContentType = 100;

    private IdempotenciaRegistro()
    {
        Chave = null!;
        HashRequisicao = null!;
        Corpo = null!;
    }

    public IdempotenciaRegistro(Guid usuarioId, string chave, string hashRequisicao, int statusCode, string? contentType, string corpo, DateTimeOffset criadoEm, DateTimeOffset expiraEm)
    {
        UsuarioId = usuarioId;
        Chave = chave;
        HashRequisicao = hashRequisicao;
        StatusCode = statusCode;
        ContentType = contentType;
        Corpo = corpo;
        CriadoEm = criadoEm;
        ExpiraEm = expiraEm;
    }

    public Guid UsuarioId { get; private set; }

    public string Chave { get; private set; }

    public string HashRequisicao { get; private set; }

    public int StatusCode { get; private set; }

    public string? ContentType { get; private set; }

    public string Corpo { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public DateTimeOffset ExpiraEm { get; private set; }
}
