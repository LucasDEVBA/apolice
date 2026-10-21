using Apolice.Domain.Comum;

namespace Apolice.Domain.Apolices;

public sealed class Endosso : Entity<Guid>
{
    private Endosso()
    {
        CoberturaCodigo = null!;
        CoberturaNome = null!;
    }

    private Endosso(Guid apoliceId, int numero, SimulacaoEndosso simulacao, bool coberturaPermiteSinistro, Guid criadoPorId, DateTimeOffset agora)
        : base(Guid.CreateVersion7(agora))
    {
        ApoliceId = apoliceId;
        Numero = numero;
        Tipo = simulacao.Tipo;
        CoberturaCodigo = simulacao.CoberturaCodigo;
        CoberturaNome = simulacao.CoberturaNome;
        CoberturaPermiteSinistro = coberturaPermiteSinistro;
        LmiAnterior = simulacao.LmiAnterior;
        LmiNovo = simulacao.LmiNovo;
        DataEfeito = simulacao.DataEfeito;
        PremioAnualAnterior = simulacao.PremioAnualAnterior;
        PremioAnualNovo = simulacao.PremioAnualNovo;
        AjusteLiquido = simulacao.AjusteLiquido;
        IofAjuste = simulacao.IofAjuste;
        AjusteTotal = simulacao.AjusteTotal;
        CriadoEm = agora;
        CriadoPorId = criadoPorId;
    }

    public Guid ApoliceId { get; private set; }

    public int Numero { get; private set; }

    public TipoEndosso Tipo { get; private set; }

    public string CoberturaCodigo { get; private set; }

    public string CoberturaNome { get; private set; }

    // Guardado para reconstituir o item numa data anterior ao endosso (sinistro de cobertura já excluída).
    public bool CoberturaPermiteSinistro { get; private set; }

    public decimal? LmiAnterior { get; private set; }

    public decimal? LmiNovo { get; private set; }

    public DateOnly DataEfeito { get; private set; }

    public decimal PremioAnualAnterior { get; private set; }

    public decimal PremioAnualNovo { get; private set; }

    public decimal AjusteLiquido { get; private set; }

    public decimal IofAjuste { get; private set; }

    public decimal AjusteTotal { get; private set; }

    public DateTimeOffset CriadoEm { get; private set; }

    public Guid CriadoPorId { get; private set; }

    internal static Endosso Criar(
        Guid apoliceId,
        int numero,
        SimulacaoEndosso simulacao,
        bool coberturaPermiteSinistro,
        Guid criadoPorId,
        DateTimeOffset agora) =>
        new(apoliceId, numero, simulacao, coberturaPermiteSinistro, criadoPorId, agora);

    // Antes de uma inclusão o item não existia; antes de alteração ou exclusão ele existia com o LMI anterior.
    internal CoberturaVigente? EstadoAnterior()
    {
        if (Tipo == TipoEndosso.InclusaoCobertura)
        {
            return null;
        }

        var lmiAnterior = LmiAnterior
            ?? throw new DadoPersistidoInvalidoException($"Endosso {Numero} de {Tipo} gravado sem o LMI anterior.");
        return new CoberturaVigente(CoberturaCodigo, CoberturaNome, lmiAnterior, CoberturaPermiteSinistro);
    }
}
