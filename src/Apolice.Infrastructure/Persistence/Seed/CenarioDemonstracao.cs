using Apolice.Application.Abstractions.Data;
using Apolice.Application.Features.Auth;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Cotacoes;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;
using Apolice.Domain.Tarifacao;
using Apolice.Domain.Usuarios;

namespace Apolice.Infrastructure.Persistence.Seed;

// Carteira de demonstração montada PELOS MÉTODOS DO DOMÍNIO (Cotacao.Criar → Aceitar → ApoliceSeguro.Emitir →
// Endossar/Cancelar → Sinistro.Abrir → análise), com instantes no passado relativos a "agora": as regras de datas
// (início da vigência ≤ 30 dias, validade da cotação, ocorrência dentro da vigência) valem como em produção.
// Cobre todos os status: cotação calculada/aceita/emitida/recusada/expirada, apólice ativa/programada/vencida/cancelada,
// sinistro aberto/em análise/aprovado/negado/pago, 2 endossos e 1 cancelamento. Só enfileira no DbContext.
internal sealed class CenarioDemonstracao(
    ApoliceDbContext db,
    ITarifador tarifador,
    INumerador numerador,
    IReadOnlyDictionary<string, Produto> produtos,
    IReadOnlyDictionary<string, Usuario> usuarios,
    DateTimeOffset agora)
{
    private const int HoraCotacao = 10;
    private const int HoraAceite = 15;
    private const int HoraEmissao = 11;
    private const int HoraAnalise = 14;

    private readonly DateOnly _hoje = Relogio.HojeEm(agora);
    private int _cotacoes;
    private int _apolices;
    private int _sinistros;

    private Usuario Ana => usuarios[PersonasDemo.Corretora];

    private Usuario Bruno => usuarios[PersonasDemo.Corretor];

    private Usuario Carla => usuarios[PersonasDemo.Analista];

    public async Task<(int Cotacoes, int Apolices, int Sinistros)> MontarAsync(CancellationToken cancellationToken)
    {
        var carteira = await EmitirCarteiraAsync(cancellationToken);
        Endossar(carteira);
        Exigir(carteira.AutoLucas.Cancelar(MotivoCancelamento.PedidoDoSegurado, Instante(_hoje.AddMonths(-1), HoraAnalise)));
        await AbrirSinistrosAsync(carteira, cancellationToken);
        CotarSemEmissao();
        return (_cotacoes, _apolices, _sinistros);
    }

    // ≈10 apólices da Ana espalhadas pelos últimos 12 meses (uma programada) e 3 do Bruno (uma já vencida).
    private async Task<Carteira> EmitirCarteiraAsync(CancellationToken ct)
    {
        var autoMariana = await EmitirAsync(Ana, Auto("Mariana Alves Costa", "529982247", 92_000m, 2021, "01310100", 34, 4, rcfDc: true, vidros: true), Mes(11, 5), ct);
        var residRafael = await EmitirAsync(Ana, Residencial("Rafael Souza Lima", "153509460", 680_000m, TipoImovel.Apartamento, "40140110", true, eletricos: 15_000m, roubo: 30_000m, rc: 50_000m), Mes(10, 12), ct);
        await EmitirAsync(Ana, Vida("Juliana Pereira Santos", "248438034", new DateOnly(1988, 3, 14), false, 400_000m, ipa: 200_000m), Mes(9, 3), ct);
        var autoPedro = await EmitirAsync(Ana, Auto("Pedro Henrique Rocha", "071834551", 145_000m, 2023, "22041001", 27, 2, carroReserva: true), Mes(8, 20), ct);
        var residCamila = await EmitirAsync(Ana, Residencial("Camila Ferreira Gomes", "386112905", 950_000m, TipoImovel.Casa, "13083970", true, roubo: 60_000m, vendaval: 80_000m), Mes(7, 8), ct);
        var autoLucas = await EmitirAsync(Ana, Auto("Lucas Martins Ribeiro", "614023878", 68_000m, _hoje.Year - 9, "30140071", 45, 6), Mes(6, 15), ct);
        await EmitirAsync(Ana, Vida("Fernanda Oliveira Dias", "902617345", new DateOnly(1975, 8, 2), true, 250_000m), Mes(5, 2), ct);
        await EmitirAsync(Ana, Residencial("Gustavo Almeida Nunes", "457290136", 520_000m, TipoImovel.Apartamento, "04538132", false, eletricos: 10_000m), Mes(4, 10), ct);
        var autoBeatriz = await EmitirAsync(Ana, Auto("Beatriz Carvalho Mendes", "830156729", 115_000m, 2024, "41820020", 52, 8, rcfDc: true, vidros: true), Mes(2, 6), ct);
        await EmitirAsync(Ana, Auto("Thiago Barbosa Freitas", "275019463", 74_500m, 2022, "40301110", 38, 1), _hoje.AddDays(-2), ct, diasAteInicio: 10);

        await EmitirAsync(Bruno, Auto("Ricardo Teixeira Moura", "690341257", 58_000m, 2019, "60160230", 41, 5), Mes(14, 10), ct);
        var residPatricia = await EmitirAsync(Bruno, Residencial("Patrícia Lopes Araújo", "318764029", 430_000m, TipoImovel.Casa, "80240210", true, eletricos: 8_000m, roubo: 20_000m), Mes(3, 2), ct);
        await EmitirAsync(Bruno, Vida("Eduardo Ramos Pinto", "104857362", new DateOnly(1969, 11, 23), false, 150_000m, ipa: 150_000m), Mes(6, 25), ct);

        return new Carteira(autoMariana, residRafael, autoPedro, residCamila, autoLucas, autoBeatriz, residPatricia);
    }

    private void Endossar(Carteira carteira)
    {
        Exigir(carteira.AutoMariana.Endossar(
            new SolicitacaoEndosso(TipoEndosso.AlteracaoLmi, CodigosCobertura.RcfDanosMateriais, 200_000m),
            produtos[CodigosProduto.Auto],
            tarifador,
            Ana.Id,
            Instante(_hoje.AddMonths(-6), HoraAnalise)));

        Exigir(carteira.ResidRafael.Endossar(
            new SolicitacaoEndosso(TipoEndosso.InclusaoCobertura, CodigosCobertura.Vendaval, 50_000m),
            produtos[CodigosProduto.Residencial],
            tarifador,
            Ana.Id,
            Instante(_hoje.AddMonths(-3), HoraAnalise)));
    }

    private async Task AbrirSinistrosAsync(Carteira carteira, CancellationToken ct)
    {
        var pago = await AbrirAsync(carteira.AutoPedro, CodigosCobertura.Casco, _hoje.AddMonths(-6), "Colisão traseira em semáforo com danos no para-choque, tampa do porta-malas e lanternas.", 8_400m, Ana, ct);
        Analisar(pago, dias: 1);
        Exigir(pago.Aprovar(7_900m, "Orçamento da oficina referenciada abaixo do valor reclamado.", Carla.Id, Depois(pago, dias: 3)));
        Exigir(pago.RegistrarPagamento(Carla.Id, Depois(pago, dias: 7)));

        var aprovado = await AbrirAsync(carteira.ResidRafael, CodigosCobertura.DanosEletricos, _hoje.AddMonths(-2), "Oscilação na rede elétrica danificou a geladeira e o televisor da sala durante a tempestade.", 4_800m, Ana, ct);
        Analisar(aprovado, dias: 1);
        Exigir(aprovado.Aprovar(4_500m, null, Carla.Id, Depois(aprovado, dias: 2)));

        var negado = await AbrirAsync(carteira.ResidCamila, CodigosCobertura.Roubo, _hoje.AddMonths(-3), "Bicicleta deixada na área externa desapareceu durante a noite, sem sinais de arrombamento.", 12_000m, Ana, ct);
        Analisar(negado, dias: 1);
        Exigir(negado.Negar("Furto simples, sem vestígios de arrombamento, não é coberto pela garantia de roubo e furto qualificado.", Carla.Id, Depois(negado, dias: 4)));

        var emAnalise = await AbrirAsync(carteira.AutoBeatriz, CodigosCobertura.Vidros, _hoje.AddDays(-20), "Trinca no para-brisa causada por pedra lançada por caminhão na rodovia BR-324.", 1_850m, Ana, ct);
        Analisar(emAnalise, dias: 2);

        await AbrirAsync(carteira.ResidPatricia, CodigosCobertura.DanosEletricos, _hoje.AddDays(-5), "Queda de raio próxima à residência queimou o portão eletrônico e o roteador.", 2_300m, Bruno, ct, diasAteAbertura: 2);
    }

    // Cotações que não viraram apólice, em todos os status restantes (inclusive a "Expirada", derivada do relógio).
    private void CotarSemEmissao()
    {
        Cotar(Ana, Auto("Sofia Cardoso Moreira", "736205148", 88_000m, 2022, "01415000", 31, 3), Instante(_hoje.AddDays(-2), HoraCotacao));
        Cotar(Bruno, Vida("Marcelo Duarte Vieira", "582640317", new DateOnly(1983, 6, 9), false, 300_000m, ipa: 100_000m), Instante(_hoje.AddDays(-5), HoraCotacao));

        var aceita = Cotar(Ana, Residencial("Larissa Monteiro Castro", "419087263", 610_000m, TipoImovel.Apartamento, "05407002", true, eletricos: 12_000m), Instante(_hoje.AddDays(-6), HoraCotacao));
        Exigir(aceita.Aceitar(Instante(_hoje.AddDays(-5), HoraAceite)));

        var recusadaAna = Cotar(Ana, Vida("Rodrigo Azevedo Campos", "967312840", new DateOnly(1979, 1, 30), true, 500_000m), Instante(_hoje.AddDays(-40), HoraCotacao));
        Exigir(recusadaAna.Recusar(Instante(_hoje.AddDays(-38), HoraAceite)));

        var recusadaBruno = Cotar(Bruno, Auto("Aline Batista Rezende", "245973016", 132_000m, 2023, "20040002", 23, 0), Instante(_hoje.AddDays(-20), HoraCotacao));
        Exigir(recusadaBruno.Recusar(Instante(_hoje.AddDays(-19), HoraAceite)));

        Cotar(Ana, Auto("Vinícius Correia Prado", "803461297", 54_000m, 2018, "41100050", 63, 7), Instante(_hoje.AddDays(-45), HoraCotacao));
        var aceitaExpirada = Cotar(Bruno, Residencial("Natália Freire Antunes", "136829574", 390_000m, TipoImovel.Casa, "88015200", true, roubo: 15_000m), Instante(_hoje.AddDays(-60), HoraCotacao));
        Exigir(aceitaExpirada.Aceitar(Instante(_hoje.AddDays(-59), HoraAceite)));
    }

    // Cotação no dia anterior, aceite à tarde e emissão na manhã seguinte, com início da vigência poucos dias depois.
    private async Task<ApoliceSeguro> EmitirAsync(Usuario corretor, Pedido pedido, DateOnly diaEmissao, CancellationToken ct, int diasAteInicio = 2)
    {
        var cotacao = Cotar(corretor, pedido, Instante(diaEmissao.AddDays(-1), HoraCotacao));
        Exigir(cotacao.Aceitar(Instante(diaEmissao.AddDays(-1), HoraAceite)));

        var emissao = Instante(diaEmissao, HoraEmissao);
        var ano = diaEmissao.Year;
        var sequencial = await numerador.ProximoAsync(ChavesNumerador.Apolice(cotacao.ProdutoCodigo, ano), ct);
        var parcelas = Math.Min(pedido.Parcelas, cotacao.MaxParcelas);
        var apolice = Exigir(ApoliceSeguro.Emitir(cotacao, ApoliceSeguro.FormatarNumero(cotacao.ProdutoCodigo, ano, sequencial), diaEmissao.AddDays(diasAteInicio), parcelas, emissao));
        db.Apolices.Add(apolice);
        _apolices++;
        return apolice;
    }

    private Cotacao Cotar(Usuario corretor, Pedido pedido, DateTimeOffset em)
    {
        var produto = produtos[pedido.DadosRisco.ProdutoCodigo];
        var tarifacao = Exigir(tarifador.Calcular(produto, pedido.Segurado, pedido.DadosRisco, pedido.Coberturas, Relogio.HojeEm(em)));
        var cotacao = Cotacao.Criar(corretor.Id, produto, pedido.Segurado, pedido.DadosRisco, tarifacao, em);
        db.Cotacoes.Add(cotacao);
        _cotacoes++;
        return cotacao;
    }

    private async Task<Sinistro> AbrirAsync(
        ApoliceSeguro apolice,
        string cobertura,
        DateOnly ocorrencia,
        string descricao,
        decimal valor,
        Usuario corretor,
        CancellationToken ct,
        int diasAteAbertura = 1)
    {
        var abertura = Instante(ocorrencia.AddDays(diasAteAbertura), HoraCotacao);
        var ano = Relogio.HojeEm(abertura).Year;
        var sequencial = await numerador.ProximoAsync(ChavesNumerador.Sinistro(ano), ct);
        var sinistro = Exigir(Sinistro.Abrir(apolice, Sinistro.FormatarNumero(ano, sequencial), cobertura, ocorrencia, descricao, valor, corretor.Id, abertura));
        db.Sinistros.Add(sinistro);
        _sinistros++;
        return sinistro;
    }

    private void Analisar(Sinistro sinistro, int dias) => Exigir(sinistro.IniciarAnalise(Carla.Id, Depois(sinistro, dias)));

    private static DateTimeOffset Depois(Sinistro sinistro, int dias) => sinistro.AbertoEm.AddDays(dias).AddHours(HoraAnalise - HoraCotacao);

    // Dia fixo de um mês passado (meses ≥ 1): uma apólice por mês, qualquer que seja o dia de hoje.
    private DateOnly Mes(int meses, int dia)
    {
        var mes = _hoje.AddMonths(-meses);
        return new DateOnly(mes.Year, mes.Month, dia);
    }

    // Instantes em Brasília convertidos para UTC (o timestamptz do PostgreSQL só aceita offset zero).
    private static DateTimeOffset Instante(DateOnly dia, int hora) =>
        new DateTimeOffset(dia.ToDateTime(new TimeOnly(hora, 0)), Relogio.OffsetBrasilia).ToUniversalTime();

    private static Pedido Auto(
        string nome,
        string baseCpf,
        decimal valorVeiculo,
        int anoFabricacao,
        string cep,
        int idadeCondutor,
        int classeBonus,
        bool rcfDc = false,
        bool vidros = false,
        bool carroReserva = false)
    {
        var coberturas = new List<CoberturaSolicitada>
        {
            new(CodigosCobertura.Casco, null),
            new(CodigosCobertura.RcfDanosMateriais, 100_000m),
            new(CodigosCobertura.Assistencia24h, null),
        };
        AdicionarSe(coberturas, rcfDc, CodigosCobertura.RcfDanosCorporais, 50_000m);
        AdicionarSe(coberturas, vidros, CodigosCobertura.Vidros, null);
        AdicionarSe(coberturas, carroReserva, CodigosCobertura.CarroReserva, null);
        var dados = new DadosRiscoAuto(valorVeiculo, anoFabricacao, cep, idadeCondutor, UsoComercial: false, classeBonus);
        return new Pedido(NovoSegurado(nome, baseCpf, new DateOnly(1980, 1, 1).AddDays(baseCpf[0] * 97)), dados, coberturas, Parcelas: 10);
    }

    private static Pedido Residencial(
        string nome,
        string baseCpf,
        decimal valorImovel,
        TipoImovel tipo,
        string cep,
        bool habitual,
        decimal? eletricos = null,
        decimal? roubo = null,
        decimal? rc = null,
        decimal? vendaval = null)
    {
        var coberturas = new List<CoberturaSolicitada> { new(CodigosCobertura.Incendio, valorImovel) };
        AdicionarSe(coberturas, eletricos is not null, CodigosCobertura.DanosEletricos, eletricos);
        AdicionarSe(coberturas, roubo is not null, CodigosCobertura.Roubo, roubo);
        AdicionarSe(coberturas, rc is not null, CodigosCobertura.RcFamiliar, rc);
        AdicionarSe(coberturas, vendaval is not null, CodigosCobertura.Vendaval, vendaval);
        var dados = new DadosRiscoResidencial(valorImovel, tipo, cep, habitual);
        return new Pedido(NovoSegurado(nome, baseCpf, new DateOnly(1978, 5, 10).AddDays(baseCpf[1] * 113)), dados, coberturas, Parcelas: 6);
    }

    private static Pedido Vida(string nome, string baseCpf, DateOnly nascimento, bool fumante, decimal capitalMorte, decimal? ipa = null)
    {
        var coberturas = new List<CoberturaSolicitada> { new(CodigosCobertura.Morte, capitalMorte), new(CodigosCobertura.Funeral, null) };
        AdicionarSe(coberturas, ipa is not null, CodigosCobertura.InvalidezPermanenteAcidente, ipa);
        return new Pedido(NovoSegurado(nome, baseCpf, nascimento), new DadosRiscoVida(fumante), coberturas, Parcelas: 12);
    }

    private static void AdicionarSe(List<CoberturaSolicitada> coberturas, bool condicao, string codigo, decimal? lmi)
    {
        if (condicao)
        {
            coberturas.Add(new CoberturaSolicitada(codigo, lmi));
        }
    }

    // Segurados fictícios: CPF com dígitos verificadores calculados a partir de uma base inventada e e-mail no
    // domínio reservado .test (RFC 2606), que nunca entrega nada.
    private static Segurado NovoSegurado(string nome, string baseCpf, DateOnly nascimento)
    {
        var usuario = string.Join('.', nome.Split(' ')[0], nome.Split(' ')[^1]).ToLowerInvariant();
        var email = new string([.. usuario.Normalize(System.Text.NormalizationForm.FormD).Where(c => c is '.' || char.IsAsciiLetter(c))]) + "@exemplo.test";
        return Exigir(Segurado.Criar(nome, CpfValido(baseCpf), email, nascimento));
    }

    private static string CpfValido(string base9)
    {
        var primeiro = DigitoVerificador(base9);
        return base9 + primeiro + DigitoVerificador(base9 + primeiro);
    }

    private static int DigitoVerificador(string digitos)
    {
        const int Modulo = 11;
        var soma = digitos.Select((d, i) => (d - '0') * (digitos.Length + 1 - i)).Sum();
        var resto = soma % Modulo;
        return resto < 2 ? 0 : Modulo - resto;
    }

    // O cenário é fixo em código: uma regra de domínio recusando-o é bug do seed, não entrada do usuário.
    private static T Exigir<T>(Result<T> resultado) =>
        resultado.IsSuccess ? resultado.Value : throw new InvalidOperationException($"Seed de demonstração inconsistente: {resultado.Error.Codigo} — {resultado.Error.Mensagem}");

    private static void Exigir(Result resultado)
    {
        if (resultado.IsFailure)
        {
            throw new InvalidOperationException($"Seed de demonstração inconsistente: {resultado.Error.Codigo} — {resultado.Error.Mensagem}");
        }
    }

    private sealed record Pedido(Segurado Segurado, DadosRisco DadosRisco, IReadOnlyList<CoberturaSolicitada> Coberturas, int Parcelas);

    private sealed record Carteira(
        ApoliceSeguro AutoMariana,
        ApoliceSeguro ResidRafael,
        ApoliceSeguro AutoPedro,
        ApoliceSeguro ResidCamila,
        ApoliceSeguro AutoLucas,
        ApoliceSeguro AutoBeatriz,
        ApoliceSeguro ResidPatricia);
}
