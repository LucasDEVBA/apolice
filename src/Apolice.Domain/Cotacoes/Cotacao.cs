using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Cotacoes;

public sealed class Cotacao : AggregateRoot<Guid>
{
    private const int DiasValidade = 30;

    private readonly List<ItemCotacao> _itens = [];
    private readonly List<FatorAplicado> _fatores = [];

    private Cotacao()
    {
        ProdutoCodigo = null!;
        Segurado = null!;
        DadosRisco = null!;
    }

    private Cotacao(Guid corretorId, Produto produto, Segurado segurado, DadosRisco dadosRisco, ResultadoTarifacao resultado, DateTimeOffset agora)
        : base(Guid.CreateVersion7(agora))
    {
        CorretorId = corretorId;
        ProdutoCodigo = produto.Codigo;
        Segurado = segurado;
        DadosRisco = dadosRisco;
        _itens.AddRange(resultado.Itens.Select(ItemCotacao.De));
        _fatores.AddRange(resultado.Fatores);
        PremioLiquido = resultado.PremioLiquido;
        AliquotaIof = resultado.AliquotaIof;
        Iof = resultado.Iof;
        PremioTotal = resultado.PremioTotal;
        MaxParcelas = resultado.MaxParcelas;
        Status = StatusCotacao.Calculada;
        CriadaEm = agora;
        ValidaAte = agora.AddDays(DiasValidade);
    }

    public Guid CorretorId { get; private set; }

    public string ProdutoCodigo { get; private set; }

    public Segurado Segurado { get; private set; }

    public DadosRisco DadosRisco { get; private set; }

    public IReadOnlyList<ItemCotacao> Itens => _itens.AsReadOnly();

    public IReadOnlyList<FatorAplicado> Fatores => _fatores.AsReadOnly();

    public decimal PremioLiquido { get; private set; }

    public decimal AliquotaIof { get; private set; }

    public decimal Iof { get; private set; }

    public decimal PremioTotal { get; private set; }

    public int MaxParcelas { get; private set; }

    public StatusCotacao Status { get; private set; }

    public DateTimeOffset ValidaAte { get; private set; }

    public DateTimeOffset CriadaEm { get; private set; }

    public Guid? ApoliceId { get; private set; }

    // Entradas incoerentes aqui são erro de programação (a tarifação já validou o pedido), não fluxo previsível.
    public static Cotacao Criar(
        Guid corretorId,
        Produto produto,
        Segurado segurado,
        DadosRisco dadosRisco,
        ResultadoTarifacao resultado,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(segurado);
        ArgumentNullException.ThrowIfNull(dadosRisco);
        ArgumentNullException.ThrowIfNull(resultado);

        if (corretorId == Guid.Empty)
        {
            throw new ArgumentException("A cotação exige um corretor.", nameof(corretorId));
        }

        if (dadosRisco.ProdutoCodigo != produto.Codigo || !TarifacaoCoerente(produto, resultado))
        {
            throw new ArgumentException("Dados de risco ou tarifação incompatíveis com o produto.", nameof(resultado));
        }

        return new Cotacao(corretorId, produto, segurado, dadosRisco, resultado, agora);
    }

    public bool EstaExpirada(DateTimeOffset agora) => agora > ValidaAte;

    public StatusEfetivoCotacao StatusEfetivo(DateTimeOffset agora)
    {
        if ((Status is StatusCotacao.Calculada or StatusCotacao.Aceita) && EstaExpirada(agora))
        {
            return StatusEfetivoCotacao.Expirada;
        }

        // Status fora do enum só chega por dado corrompido (o EF não passa pelas transições): falha em vez de chutar.
        return Status switch
        {
            StatusCotacao.Calculada => StatusEfetivoCotacao.Calculada,
            StatusCotacao.Aceita => StatusEfetivoCotacao.Aceita,
            StatusCotacao.Emitida => StatusEfetivoCotacao.Emitida,
            StatusCotacao.Recusada => StatusEfetivoCotacao.Recusada,
            _ => throw new DadoPersistidoInvalidoException($"Cotação com status desconhecido ({(int)Status})."),
        };
    }

    public Result Aceitar(DateTimeOffset agora)
    {
        var validacao = ExigirStatusVigente(StatusCotacao.Calculada, agora);
        if (validacao.IsSuccess)
        {
            Status = StatusCotacao.Aceita;
        }

        return validacao;
    }

    // Recusa vale mesmo para cotação expirada (o SPEC só barra expiração em aceite/emissão): o corretor
    // pode encerrar formalmente a proposta. "agora" fica no contrato por simetria com as demais transições.
    public Result Recusar(DateTimeOffset agora)
    {
        if (Status is not (StatusCotacao.Calculada or StatusCotacao.Aceita))
        {
            return CotacaoErros.StatusInvalido(Status);
        }

        Status = StatusCotacao.Recusada;
        return Result.Success();
    }

    // Exposto para a emissão checar a cotação antes de validar os próprios dados (mensagem mais útil primeiro).
    public Result ValidarEmissao(DateTimeOffset agora) => ExigirStatusVigente(StatusCotacao.Aceita, agora);

    public Result MarcarEmitida(Guid apoliceId, DateTimeOffset agora)
    {
        if (apoliceId == Guid.Empty)
        {
            throw new ArgumentException("Id de apólice inválido.", nameof(apoliceId));
        }

        var validacao = ValidarEmissao(agora);
        if (validacao.IsSuccess)
        {
            Status = StatusCotacao.Emitida;
            ApoliceId = apoliceId;
        }

        return validacao;
    }

    // ResultadoTarifacao.Criar é público (fakes de ITarifador precisam dele): sem esta guarda, itens repetidos
    // ou fora do produto só estourariam na PK de cotacao_itens/apolice_coberturas, longe da origem do bug.
    private static bool TarifacaoCoerente(Produto produto, ResultadoTarifacao resultado) =>
        resultado.Itens.Count > 0
        && resultado.AliquotaIof == produto.AliquotaIof
        && resultado.Itens.Select(i => i.Codigo).Distinct(StringComparer.Ordinal).Count() == resultado.Itens.Count
        && resultado.Itens.All(i => produto.ObterCobertura(i.Codigo) is not null);

    private Result ExigirStatusVigente(StatusCotacao esperado, DateTimeOffset agora)
    {
        if (Status != esperado)
        {
            return CotacaoErros.StatusInvalido(Status);
        }

        return EstaExpirada(agora) ? CotacaoErros.Expirada : Result.Success();
    }
}
