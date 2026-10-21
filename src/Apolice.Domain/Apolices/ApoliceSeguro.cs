using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Apolices;

public sealed partial class ApoliceSeguro : AggregateRoot<Guid>
{
    private const int DiasMaximosAteInicio = 30;
    private const int NumeroTamanhoMaximo = 30;

    private readonly List<CoberturaApolice> _coberturas = [];
    private readonly List<Endosso> _endossos = [];

    private ApoliceSeguro()
    {
        Numero = null!;
        ProdutoCodigo = null!;
        Segurado = null!;
        DadosRisco = null!;
        Vigencia = null!;
    }

    private ApoliceSeguro(Cotacao cotacao, string numero, Vigencia vigencia, int parcelas, DateTimeOffset agora)
        : base(Guid.CreateVersion7(agora))
    {
        Numero = numero;
        CotacaoId = cotacao.Id;
        CorretorId = cotacao.CorretorId;
        ProdutoCodigo = cotacao.ProdutoCodigo;
        Segurado = cotacao.Segurado;
        DadosRisco = cotacao.DadosRisco;
        Vigencia = vigencia;
        _coberturas.AddRange(cotacao.Itens.Select(CoberturaApolice.De));
        PremioLiquido = cotacao.PremioLiquido;
        Iof = cotacao.Iof;
        PremioTotal = cotacao.PremioTotal;
        AliquotaIof = cotacao.AliquotaIof;
        Parcelas = parcelas;
        Status = StatusApolice.Ativa;
        EmitidaEm = agora;
    }

    public string Numero { get; private set; }

    public Guid CotacaoId { get; private set; }

    public Guid CorretorId { get; private set; }

    public string ProdutoCodigo { get; private set; }

    public Segurado Segurado { get; private set; }

    public DadosRisco DadosRisco { get; private set; }

    public Vigencia Vigencia { get; private set; }

    public IReadOnlyList<CoberturaApolice> Coberturas => _coberturas.AsReadOnly();

    // Prêmios da emissão: os endossos ficam registrados à parte e somados em PremioLiquidoVigente.
    public decimal PremioLiquido { get; private set; }

    public decimal Iof { get; private set; }

    public decimal PremioTotal { get; private set; }

    public decimal AliquotaIof { get; private set; }

    public int Parcelas { get; private set; }

    public StatusApolice Status { get; private set; }

    public DateTimeOffset EmitidaEm { get; private set; }

    public DateTimeOffset? CanceladaEm { get; private set; }

    public MotivoCancelamento? MotivoCancelamento { get; private set; }

    public decimal? ValorRestituicao { get; private set; }

    public DateTimeOffset? BoasVindasEnviadasEm { get; private set; }

    public IReadOnlyList<Endosso> Endossos => _endossos.AsReadOnly();

    public decimal AjustesEndosso => _endossos.Sum(e => e.AjusteLiquido);

    public decimal PremioLiquidoVigente => PremioLiquido + AjustesEndosso;

    public DateOnly DataEmissao => Relogio.HojeEm(EmitidaEm);

    public DateOnly? DataCancelamento => CanceladaEm is { } canceladaEm ? Relogio.HojeEm(canceladaEm) : null;

    public static Result<ApoliceSeguro> Emitir(
        Cotacao cotacao,
        string numero,
        DateOnly inicioVigencia,
        int parcelas,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(cotacao);

        var cotacaoEmitivel = cotacao.ValidarEmissao(agora);
        var dados = cotacaoEmitivel.IsSuccess
            ? ValidarDadosEmissao(cotacao, numero, inicioVigencia, parcelas, Relogio.HojeEm(agora))
            : cotacaoEmitivel;
        if (dados.IsFailure)
        {
            return dados.Error;
        }

        var apolice = new ApoliceSeguro(cotacao, numero.Trim(), Vigencia.Anual(inicioVigencia), parcelas, agora);
        var marcacao = cotacao.MarcarEmitida(apolice.Id, agora);
        if (marcacao.IsFailure)
        {
            return marcacao.Error;
        }

        apolice.Raise(apolice.CriarEventoEmitida(agora));
        return apolice;
    }

    // Formato canônico (ex.: AUTO-2026-000042); o sequencial vem do numerador da infraestrutura.
    public static string FormatarNumero(string produtoCodigo, int ano, long sequencial) =>
        NumeroDocumento.Formatar(produtoCodigo, ano, sequencial);

    public StatusEfetivoApolice StatusEfetivo(DateOnly hoje)
    {
        if (Status == StatusApolice.Cancelada)
        {
            return StatusEfetivoApolice.Cancelada;
        }

        if (hoje < Vigencia.Inicio)
        {
            return StatusEfetivoApolice.Programada;
        }

        return hoje > Vigencia.Fim ? StatusEfetivoApolice.Vencida : StatusEfetivoApolice.Ativa;
    }

    public CoberturaApolice? ObterCobertura(string? codigo) => _coberturas.Find(c => c.Codigo == codigo);

    // "Cobertura existe na apólice" (SPEC 4.5) lido na data da OCORRÊNCIA, como as demais checagens do sinistro:
    // senão, incluir a cobertura ou subir o LMI depois do evento indenizaria um risco que não estava contratado.
    // Cada endosso guarda o estado anterior do item, então basta o primeiro com efeito posterior à data; o
    // endosso com efeito no próprio dia já vale (a granularidade da vigência é o dia).
    public CoberturaVigente? CoberturaEm(string? codigo, DateOnly data)
    {
        var primeiroPosterior = _endossos
            .Where(e => e.CoberturaCodigo == codigo && e.DataEfeito > data)
            .MinBy(e => e.Numero);
        if (primeiroPosterior is not null)
        {
            return primeiroPosterior.EstadoAnterior();
        }

        return ObterCobertura(codigo) is { } atual ? CoberturaVigente.De(atual) : null;
    }

    // Base truncada em centavos e a 1ª parcela absorve a sobra: a soma fecha exatamente no PremioTotal.
    // É o cronograma da EMISSÃO (SPEC 4.4): ajustes de endosso são cobrados ou devolvidos à parte, pelo
    // AjusteTotal do próprio endosso, e não redistribuídos nestas parcelas.
    public IReadOnlyList<ParcelaCronograma> CronogramaParcelas()
    {
        var valorBase = Dinheiro.ArredondarParaBaixo(PremioTotal / Parcelas);
        var primeira = PremioTotal - (valorBase * (Parcelas - 1));
        return
        [
            .. Enumerable.Range(1, Parcelas)
                .Select(n => new ParcelaCronograma(n, Vigencia.Inicio.AddMonths(n - 1), n == 1 ? primeira : valorBase)),
        ];
    }

    // Idempotente: reentregas da mensagem ApoliceEmitida não podem sobrescrever o primeiro envio.
    public void MarcarBoasVindasEnviadas(DateTimeOffset agora) => BoasVindasEnviadasEm ??= agora;

    private static Result ValidarDadosEmissao(Cotacao cotacao, string numero, DateOnly inicio, int parcelas, DateOnly hoje)
    {
        if (!NumeroValido(numero, cotacao.ProdutoCodigo))
        {
            return ApoliceErros.NumeroInvalido;
        }

        var inicioMaximo = hoje.AddDays(DiasMaximosAteInicio);
        if (inicio < hoje || inicio > inicioMaximo)
        {
            return ApoliceErros.InicioVigenciaInvalido(hoje, inicioMaximo);
        }

        return parcelas < ResultadoTarifacao.ParcelasMinimas || parcelas > cotacao.MaxParcelas
            ? ApoliceErros.ParcelasInvalidas(cotacao.MaxParcelas)
            : Result.Success();
    }

    // O prefixo tem de ser o produto da cotação: número de outra linha (ex.: RESID numa apólice AUTO) é bug do chamador.
    private static bool NumeroValido(string? numero, string produtoCodigo) =>
        !string.IsNullOrWhiteSpace(numero)
        && numero.Trim().Length <= NumeroTamanhoMaximo
        && NumeroDocumento.EhCanonico(numero.Trim(), produtoCodigo);

    private ApoliceEmitida CriarEventoEmitida(DateTimeOffset agora) =>
        new(Id, Numero, CorretorId, ProdutoCodigo, Segurado.Nome, Segurado.Email.Valor, PremioTotal, Vigencia.Inicio, Vigencia.Fim, agora);

    private Result ExigirStatusAlteravel(DateOnly hoje)
    {
        var status = StatusEfetivo(hoje);
        return status is StatusEfetivoApolice.Ativa or StatusEfetivoApolice.Programada
            ? Result.Success()
            : ApoliceErros.StatusInvalido(status);
    }
}
