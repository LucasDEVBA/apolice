using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;

namespace Apolice.Domain.Tarifacao;

// Template Method: as validações comuns da seção 4.2 ficam aqui; cada produto só declara
// os seus dados de risco, os LMIs derivados do risco e os fatores.
public abstract class TarifacaoBase<TDados> : IEstrategiaTarifacao
    where TDados : DadosRisco
{
    protected const decimal FatorNeutro = 1m;
    private const decimal PorMil = 1000m;
    private const int DigitosCep = 8;
    private const int PosicaoHifenCep = 5;

    public abstract string ProdutoCodigo { get; }

    public Result<ResultadoTarifacao> Calcular(
        Produto produto,
        Segurado segurado,
        DadosRisco dadosRisco,
        IReadOnlyList<CoberturaSolicitada> coberturas,
        DateOnly dataReferencia)
    {
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(segurado);
        ArgumentNullException.ThrowIfNull(dadosRisco);
        ArgumentNullException.ThrowIfNull(coberturas);

        if (produto.Codigo != ProdutoCodigo || dadosRisco is not TDados dados)
        {
            return TarifacaoErros.ProdutoIncompativel;
        }

        var validacao = ValidarEntrada(dados, segurado, dataReferencia);
        var selecao = validacao.IsSuccess ? SelecionarCoberturas(produto, coberturas) : validacao.Error;
        var comLmi = selecao.IsSuccess ? ResolverLmis(selecao.Value, dados) : selecao.Error;
        return comLmi.IsSuccess
            ? Tarifar(produto, comLmi.Value, DefinirFatores(dados, segurado, dataReferencia))
            : comLmi.Error;
    }

    protected abstract Result ValidarDados(TDados dados, Segurado segurado, DateOnly dataReferencia);

    protected abstract IReadOnlyList<FatorTarifario> DefinirFatores(TDados dados, Segurado segurado, DateOnly dataReferencia);

    // Coberturas cujo LMI é ditado pelo próprio risco (ex.: CASCO = valor do veículo) ignoram o LMI enviado.
    protected virtual decimal? LmiDefinidoPeloRisco(CoberturaProduto cobertura, TDados dados) => null;

    // Restrições entre coberturas ou com os dados de risco (ex.: IPA ≤ MORTE, INCENDIO ≤ valor do imóvel).
    protected virtual Result ValidarLmis(IReadOnlyDictionary<string, decimal> lmis, TDados dados) => Result.Success();

    protected static bool CepValido(string? cep)
    {
        if (cep is null)
        {
            return false;
        }

        var semHifen = cep.Length == DigitosCep + 1 && cep[PosicaoHifenCep] == '-' ? cep.Remove(PosicaoHifenCep, 1) : cep;
        return semHifen.Length == DigitosCep && semHifen.All(char.IsAsciiDigit);
    }

    // Nascimento futuro é dado inválido em qualquer produto; já a idade mínima só existe onde o SPEC a define (VIDA).
    private Result ValidarEntrada(TDados dados, Segurado segurado, DateOnly dataReferencia) =>
        segurado.DataNascimento > dataReferencia
            ? SeguradoErros.DataNascimentoInvalida
            : ValidarDados(dados, segurado, dataReferencia);

    private static Result<IReadOnlyList<CoberturaSelecionada>> SelecionarCoberturas(
        Produto produto,
        IReadOnlyList<CoberturaSolicitada> solicitadas)
    {
        var indexadas = IndexarSolicitadas(produto, solicitadas);
        if (indexadas.IsFailure)
        {
            return indexadas.Error;
        }

        var porCodigo = indexadas.Value;
        var obrigatoriaAusente = produto.Coberturas.FirstOrDefault(c => c.Obrigatoria && !porCodigo.ContainsKey(c.Codigo));
        if (obrigatoriaAusente is not null)
        {
            return TarifacaoErros.CoberturaObrigatoriaAusente(obrigatoriaAusente.Codigo);
        }

        // A ordem do catálogo (e não a do pedido) deixa o resultado determinístico para tela e snapshot.
        return Result.Success<IReadOnlyList<CoberturaSelecionada>>(
        [
            .. produto.Coberturas
                .Where(c => porCodigo.ContainsKey(c.Codigo))
                .Select(c => new CoberturaSelecionada(c, porCodigo[c.Codigo].Lmi)),
        ]);
    }

    private static Result<Dictionary<string, CoberturaSolicitada>> IndexarSolicitadas(
        Produto produto,
        IReadOnlyList<CoberturaSolicitada> solicitadas)
    {
        var porCodigo = new Dictionary<string, CoberturaSolicitada>(StringComparer.Ordinal);
        foreach (var solicitada in solicitadas)
        {
            var codigo = solicitada?.Codigo;
            if (codigo is null || produto.ObterCobertura(codigo) is null)
            {
                return TarifacaoErros.CoberturaInexistente(codigo);
            }

            if (!porCodigo.TryAdd(codigo, solicitada!))
            {
                return TarifacaoErros.CoberturaDuplicada(codigo);
            }
        }

        return porCodigo;
    }

    private Result<IReadOnlyList<CoberturaComLmi>> ResolverLmis(IReadOnlyList<CoberturaSelecionada> selecao, TDados dados)
    {
        var resolvidas = new List<CoberturaComLmi>(selecao.Count);
        foreach (var (cobertura, lmiSolicitado) in selecao)
        {
            var lmi = ValidarLmi(cobertura, cobertura.LmiFixo ?? LmiDefinidoPeloRisco(cobertura, dados) ?? lmiSolicitado);
            if (lmi.IsFailure)
            {
                return lmi.Error;
            }

            resolvidas.Add(new CoberturaComLmi(cobertura, lmi.Value));
        }

        var restricoes = ValidarLmis(resolvidas.ToDictionary(r => r.Cobertura.Codigo, r => r.Lmi, StringComparer.Ordinal), dados);
        return restricoes.IsFailure ? restricoes.Error : Result.Success<IReadOnlyList<CoberturaComLmi>>(resolvidas);
    }

    // Centavos exatos: o LMI tarifado tem de ser o mesmo que o numeric(14,2) vai guardar na apólice e no sinistro.
    private static Result<decimal> ValidarLmi(CoberturaProduto cobertura, decimal? lmi)
    {
        if (lmi is not { } valor)
        {
            return TarifacaoErros.LmiObrigatorio(cobertura.Codigo);
        }

        if (!Dinheiro.EstaEmCentavos(valor))
        {
            return TarifacaoErros.ValorMonetarioInvalido($"LMI de {cobertura.Codigo}");
        }

        return valor < cobertura.LmiMinimo || valor > cobertura.LmiMaximo
            ? TarifacaoErros.LmiForaDoLimite(cobertura.Codigo, cobertura.LmiMinimo, cobertura.LmiMaximo)
            : valor;
    }

    private static ResultadoTarifacao Tarifar(
        Produto produto,
        IReadOnlyList<CoberturaComLmi> coberturas,
        IReadOnlyList<FatorTarifario> fatores)
    {
        var codigosComTaxa = coberturas.Where(c => c.Cobertura.CalculaPorTaxa).Select(c => c.Cobertura.Codigo).ToList();

        // Leitura de "só os ≠ 1" do SPEC 4.2: FatorAplicado lista o que de fato alterou algum item contratado;
        // um fator sem item-alvo na cotação (ex.: Casa sem ROUBO) não foi aplicado e confundiria a tela.
        var efetivos = fatores.Where(f => f.Fator != FatorNeutro && codigosComTaxa.Exists(f.AplicaA)).ToList();
        var itens = coberturas.Select(c => Precificar(c, efetivos)).ToList();
        var aplicados = efetivos.Select(f => new FatorAplicado(f.Descricao, f.Fator)).ToList();
        return ResultadoTarifacao.Criar(itens, aplicados, produto.AliquotaIof);
    }

    private static ItemTarifado Precificar(CoberturaComLmi item, IReadOnlyList<FatorTarifario> fatores)
    {
        var cobertura = item.Cobertura;
        var multiplicador = fatores.Where(f => f.AplicaA(cobertura.Codigo)).Aggregate(FatorNeutro, (produto, f) => produto * f.Fator);

        // O EF materializa a cobertura sem passar pelas fábricas: tipo desconhecido ou prêmio fixo ausente
        // é dado corrompido e não pode virar, em silêncio, um item de R$ 0,00.
        var premio = cobertura.TipoCalculo switch
        {
            TipoCalculo.TaxaSobreLmi => Dinheiro.Arredondar(item.Lmi * cobertura.Taxa * multiplicador),
            TipoCalculo.TaxaPorMil => Dinheiro.Arredondar(item.Lmi * cobertura.Taxa / PorMil * multiplicador),
            TipoCalculo.PremioFixo => cobertura.PremioFixo
                ?? throw new DadoPersistidoInvalidoException($"Cobertura '{cobertura.Codigo}' de prêmio fixo sem o valor do prêmio."),
            _ => throw new DadoPersistidoInvalidoException($"Cobertura '{cobertura.Codigo}' com tipo de cálculo desconhecido."),
        };

        return new ItemTarifado(cobertura.Codigo, cobertura.Nome, item.Lmi, premio, cobertura.Obrigatoria, cobertura.PermiteSinistro);
    }

    private sealed record CoberturaSelecionada(CoberturaProduto Cobertura, decimal? LmiSolicitado);

    private sealed record CoberturaComLmi(CoberturaProduto Cobertura, decimal Lmi);
}
