using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public sealed class Sinistro : AggregateRoot<Guid>
{
    private const int DescricaoTamanhoMinimo = 20;
    private const int DescricaoTamanhoMaximo = 2000;
    private const int MotivoNegativaTamanhoMinimo = 10;
    private const int MotivoNegativaTamanhoMaximo = 2000;
    private const int ObservacaoTamanhoMaximo = 2000;
    private const int NumeroTamanhoMaximo = 30;
    private const string PrefixoNumero = "SIN";

    private readonly List<HistoricoSinistro> _historico = [];

    private Sinistro()
    {
        Numero = null!;
        ApoliceNumero = null!;
        ProdutoCodigo = null!;
        SeguradoNome = null!;
        SeguradoEmail = null!;
        CoberturaCodigo = null!;
        CoberturaNome = null!;
        Descricao = null!;
    }

    private Sinistro(
        ApoliceSeguro apolice,
        CoberturaVigente cobertura,
        string numero,
        DateOnly dataOcorrencia,
        string descricao,
        decimal valorReclamado,
        Guid? usuarioId,
        DateTimeOffset agora)
        : base(Guid.CreateVersion7(agora))
    {
        Numero = numero;
        ApoliceId = apolice.Id;
        ApoliceNumero = apolice.Numero;
        CorretorId = apolice.CorretorId;
        ProdutoCodigo = apolice.ProdutoCodigo;
        SeguradoNome = apolice.Segurado.Nome;
        SeguradoEmail = apolice.Segurado.Email;
        CoberturaCodigo = cobertura.Codigo;
        CoberturaNome = cobertura.Nome;
        LmiCobertura = cobertura.Lmi;
        DataOcorrencia = dataOcorrencia;
        Descricao = descricao;
        ValorReclamado = valorReclamado;
        Status = StatusSinistro.Aberto;
        AbertoEm = agora;
        _historico.Add(new HistoricoSinistro(StatusSinistro.Aberto, null, usuarioId, agora));
    }

    public string Numero { get; private set; }

    public Guid ApoliceId { get; private set; }

    public string ApoliceNumero { get; private set; }

    public Guid CorretorId { get; private set; }

    public string ProdutoCodigo { get; private set; }

    public string SeguradoNome { get; private set; }

    // Copiado da apólice para os eventos/notificações não dependerem de carregar outro agregado.
    public Email SeguradoEmail { get; private set; }

    public string CoberturaCodigo { get; private set; }

    public string CoberturaNome { get; private set; }

    public decimal LmiCobertura { get; private set; }

    public DateOnly DataOcorrencia { get; private set; }

    public string Descricao { get; private set; }

    public decimal ValorReclamado { get; private set; }

    public decimal? ValorIndenizacao { get; private set; }

    public StatusSinistro Status { get; private set; }

    public string? MotivoNegativa { get; private set; }

    public TriagemSinistro? Triagem { get; private set; }

    public DateTimeOffset AbertoEm { get; private set; }

    public IReadOnlyList<HistoricoSinistro> Historico => _historico.AsReadOnly();

    public static Result<Sinistro> Abrir(
        ApoliceSeguro apolice,
        string numero,
        string coberturaCodigo,
        DateOnly dataOcorrencia,
        string descricao,
        decimal valorReclamado,
        Guid? usuarioId,
        DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(apolice);
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("Usuário vazio: use null quando a abertura não tem autor humano.", nameof(usuarioId));
        }

        var dados = ValidarDados(numero, descricao, valorReclamado);
        var ocorrencia = dados.IsSuccess ? ValidarOcorrencia(apolice, dataOcorrencia, Relogio.HojeEm(agora)) : dados;
        var cobertura = ocorrencia.IsSuccess
            ? ValidarCobertura(apolice.CoberturaEm(coberturaCodigo, dataOcorrencia), coberturaCodigo)
            : ocorrencia.Error;
        if (cobertura.IsFailure)
        {
            return cobertura.Error;
        }

        var sinistro = new Sinistro(apolice, cobertura.Value, numero.Trim(), dataOcorrencia, descricao.Trim(), Dinheiro.Arredondar(valorReclamado), usuarioId, agora);
        sinistro.Raise(sinistro.CriarEventoAberto(agora));
        return sinistro;
    }

    public static string FormatarNumero(int ano, long sequencial) => NumeroDocumento.Formatar(PrefixoNumero, ano, sequencial);

    public Result IniciarAnalise(Guid usuarioId, DateTimeOffset agora)
    {
        ExigirUsuario(usuarioId);
        return Transicionar(StatusSinistro.Aberto, StatusSinistro.EmAnalise, usuarioId, agora);
    }

    public Result Aprovar(decimal valor, string? observacao, Guid usuarioId, DateTimeOffset agora)
    {
        ExigirUsuario(usuarioId);
        if (Status != StatusSinistro.EmAnalise)
        {
            return SinistroErros.TransicaoInvalida(Status, StatusSinistro.Aprovado);
        }

        var indenizacao = Dinheiro.Arredondar(valor);
        var validacao = ValidarIndenizacao(indenizacao, observacao);
        if (validacao.IsFailure)
        {
            return validacao;
        }

        ValorIndenizacao = indenizacao;
        RegistrarStatus(StatusSinistro.Aprovado, string.IsNullOrWhiteSpace(observacao) ? null : observacao.Trim(), usuarioId, agora);
        Raise(new SinistroAprovado(Id, Numero, indenizacao, SeguradoEmail.Valor, agora));
        return Result.Success();
    }

    public Result Negar(string motivo, Guid usuarioId, DateTimeOffset agora)
    {
        ExigirUsuario(usuarioId);
        if (Status != StatusSinistro.EmAnalise)
        {
            return SinistroErros.TransicaoInvalida(Status, StatusSinistro.Negado);
        }

        var motivoNormalizado = motivo?.Trim();
        if (motivoNormalizado is null || motivoNormalizado.Length is < MotivoNegativaTamanhoMinimo or > MotivoNegativaTamanhoMaximo)
        {
            return SinistroErros.MotivoNegativaInvalido(MotivoNegativaTamanhoMinimo, MotivoNegativaTamanhoMaximo);
        }

        MotivoNegativa = motivoNormalizado;
        RegistrarStatus(StatusSinistro.Negado, motivoNormalizado, usuarioId, agora);
        Raise(new SinistroNegado(Id, Numero, motivoNormalizado, SeguradoEmail.Valor, agora));
        return Result.Success();
    }

    public Result RegistrarPagamento(Guid usuarioId, DateTimeOffset agora)
    {
        ExigirUsuario(usuarioId);
        var transicao = Transicionar(StatusSinistro.Aprovado, StatusSinistro.Pago, usuarioId, agora);
        if (transicao.IsSuccess)
        {
            Raise(new SinistroPago(Id, Numero, SeguradoEmail.Valor, agora));
        }

        return transicao;
    }

    // Só a primeira triagem vale: reprocessar a mensagem SinistroAberto não pode trocar o score já visto
    // pelo analista. Quem carimba o instante oficial é este registro: o TriadoEm vindo do avaliador é
    // provisório (o SPEC só lhe dá a data) e é substituído por "agora".
    public void RegistrarTriagem(TriagemSinistro triagem, DateTimeOffset agora)
    {
        ArgumentNullException.ThrowIfNull(triagem);
        Triagem ??= triagem.RegistradaEm(agora);
    }

    // Ações de análise vêm sempre de um analista autenticado: Guid vazio é bug do chamador, não entrada do usuário.
    private static void ExigirUsuario(Guid usuarioId)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("A transição do sinistro exige o usuário responsável.", nameof(usuarioId));
        }
    }

    private static Result ValidarDados(string numero, string descricao, decimal valorReclamado)
    {
        if (string.IsNullOrWhiteSpace(numero)
            || numero.Trim().Length > NumeroTamanhoMaximo
            || !NumeroDocumento.EhCanonico(numero.Trim(), PrefixoNumero))
        {
            return SinistroErros.NumeroInvalido;
        }

        var tamanhoDescricao = descricao?.Trim().Length ?? 0;
        if (tamanhoDescricao is < DescricaoTamanhoMinimo or > DescricaoTamanhoMaximo)
        {
            return SinistroErros.DescricaoInvalida(DescricaoTamanhoMinimo, DescricaoTamanhoMaximo);
        }

        return Dinheiro.Arredondar(valorReclamado) > 0 ? Result.Success() : SinistroErros.ValorReclamadoInvalido;
    }

    private static Result ValidarOcorrencia(ApoliceSeguro apolice, DateOnly dataOcorrencia, DateOnly hoje)
    {
        if (dataOcorrencia > hoje)
        {
            return SinistroErros.OcorrenciaFutura;
        }

        if (!apolice.Vigencia.Contem(dataOcorrencia))
        {
            return SinistroErros.ForaDaVigencia;
        }

        return apolice.DataCancelamento is { } cancelamento && dataOcorrencia >= cancelamento
            ? SinistroErros.AposCancelamento
            : Result.Success();
    }

    private static Result<CoberturaVigente> ValidarCobertura(CoberturaVigente? cobertura, string coberturaCodigo)
    {
        if (cobertura is null)
        {
            return SinistroErros.CoberturaNaoContratada(coberturaCodigo);
        }

        return cobertura.PermiteSinistro ? cobertura : SinistroErros.CoberturaSemIndenizacao(cobertura.Codigo);
    }

    // Levantado na fábrica (e não no construtor), como em ApoliceSeguro.Emitir: o construtor não carrega efeito colateral.
    private SinistroAberto CriarEventoAberto(DateTimeOffset agora) =>
        new(Id, Numero, ApoliceId, CoberturaCodigo, ValorReclamado, SeguradoEmail.Valor, agora);

    private Result ValidarIndenizacao(decimal indenizacao, string? observacao)
    {
        if (indenizacao <= 0)
        {
            return SinistroErros.ValorIndenizacaoInvalido;
        }

        var limite = Math.Min(ValorReclamado, LmiCobertura);
        if (indenizacao > limite)
        {
            return SinistroErros.IndenizacaoAcimaDoLimite(limite);
        }

        return observacao?.Trim().Length > ObservacaoTamanhoMaximo
            ? SinistroErros.ObservacaoInvalida(ObservacaoTamanhoMaximo)
            : Result.Success();
    }

    private Result Transicionar(StatusSinistro esperado, StatusSinistro destino, Guid usuarioId, DateTimeOffset agora)
    {
        if (Status != esperado)
        {
            return SinistroErros.TransicaoInvalida(Status, destino);
        }

        RegistrarStatus(destino, null, usuarioId, agora);
        return Result.Success();
    }

    private void RegistrarStatus(StatusSinistro destino, string? observacao, Guid usuarioId, DateTimeOffset agora)
    {
        Status = destino;
        _historico.Add(new HistoricoSinistro(destino, observacao, usuarioId, agora));
    }
}
