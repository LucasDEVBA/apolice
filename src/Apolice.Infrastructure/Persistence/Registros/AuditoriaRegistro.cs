namespace Apolice.Infrastructure.Persistence.Registros;

// Trilha de auditoria (append-only): gravada no mesmo SaveChanges do caso de uso, nunca alterada depois.
internal sealed class AuditoriaRegistro
{
    public const int TamanhoMaximoAcao = 60;
    public const int TamanhoMaximoEntidade = 60;
    public const int TamanhoMaximoEntidadeId = 100;
    public const int TamanhoMaximoDetalhes = 4_000;

    private AuditoriaRegistro()
    {
        Acao = null!;
        Entidade = null!;
    }

    public AuditoriaRegistro(Guid id, Guid? usuarioId, string acao, string entidade, string? entidadeId, string? detalhes, string? correlationId, DateTimeOffset ocorridoEm)
    {
        Id = id;
        UsuarioId = usuarioId;
        Acao = acao;
        Entidade = entidade;
        EntidadeId = entidadeId;
        Detalhes = detalhes;
        CorrelationId = correlationId;
        OcorridoEm = ocorridoEm;
    }

    public Guid Id { get; private set; }

    public Guid? UsuarioId { get; private set; }

    public string Acao { get; private set; }

    public string Entidade { get; private set; }

    public string? EntidadeId { get; private set; }

    public string? Detalhes { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OcorridoEm { get; private set; }
}
