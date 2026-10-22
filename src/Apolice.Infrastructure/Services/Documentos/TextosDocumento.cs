using System.Globalization;
using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Tarifacao;

namespace Apolice.Infrastructure.Services.Documentos;

// Textos pt-BR do documento montados à mão (sem ICU, como o Dinheiro do domínio): o Lambda roda com globalização invariante.
internal static class TextosDocumento
{
    private const int CasasPercentual = 2;
    private const decimal Cem = 100m;

    public static string Moeda(decimal valor) => Dinheiro.Formatar(valor);

    public static string MoedaOuTraco(decimal? valor) => valor is { } v ? Moeda(v) : "—";

    public static string Data(DateOnly data) => data.ToString("dd'/'MM'/'yyyy", CultureInfo.InvariantCulture);

    public static string Data(DateTimeOffset instante) => Data(Relogio.HojeEm(instante));

    public static string DataHora(DateTimeOffset instante) =>
        instante.ToOffset(Relogio.OffsetBrasilia).ToString("dd'/'MM'/'yyyy HH':'mm", CultureInfo.InvariantCulture);

    public static string Percentual(decimal fracao) =>
        Math.Round(fracao * Cem, CasasPercentual, MidpointRounding.AwayFromZero).ToString("0.##", CultureInfo.InvariantCulture).Replace('.', ',') + "%";

    public static string Inteiro(int valor) => valor.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.');

    public static string SimNao(bool valor) => valor ? "Sim" : "Não";

    public static string Status(StatusEfetivoApolice status) => status switch
    {
        StatusEfetivoApolice.Ativa => "Ativa",
        StatusEfetivoApolice.Programada => "Programada",
        StatusEfetivoApolice.Vencida => "Vencida",
        StatusEfetivoApolice.Cancelada => "Cancelada",
        _ => status.ToString(),
    };

    public static string Tipo(TipoEndosso tipo) => tipo switch
    {
        TipoEndosso.AlteracaoLmi => "Alteração de LMI",
        TipoEndosso.InclusaoCobertura => "Inclusão de cobertura",
        TipoEndosso.ExclusaoCobertura => "Exclusão de cobertura",
        _ => tipo.ToString(),
    };

    public static string Motivo(MotivoCancelamento motivo) => motivo switch
    {
        MotivoCancelamento.PedidoDoSegurado => "Pedido do segurado",
        MotivoCancelamento.IniciativaSeguradora => "Iniciativa da seguradora",
        MotivoCancelamento.Inadimplencia => "Inadimplência",
        _ => motivo.ToString(),
    };

    public static string Cpf(string cpf) => Mascaramento.Cpf(cpf);

    public static string Cep(string cep)
    {
        var digitos = new string([.. cep.Where(char.IsAsciiDigit)]);
        const int TamanhoCep = 8;
        const int PosicaoHifen = 5;
        return digitos.Length == TamanhoCep ? $"{digitos[..PosicaoHifen]}-{digitos[PosicaoHifen..]}" : cep;
    }

    // Linhas "rótulo: valor" dos dados de risco, na linguagem do corretor.
    public static IReadOnlyList<(string Rotulo, string Valor)> DadosRisco(DadosRisco dados) => dados switch
    {
        DadosRiscoAuto auto =>
        [
            ("Valor do veículo", Moeda(auto.ValorVeiculo)),
            ("Ano de fabricação", auto.AnoFabricacao.ToString(CultureInfo.InvariantCulture)),
            ("CEP de pernoite", Cep(auto.CepPernoite)),
            ("Idade do condutor", $"{auto.IdadeCondutor} anos"),
            ("Uso comercial", SimNao(auto.UsoComercial)),
            ("Classe de bônus", auto.ClasseBonus.ToString(CultureInfo.InvariantCulture)),
        ],
        DadosRiscoResidencial residencial =>
        [
            ("Valor do imóvel", Moeda(residencial.ValorImovel)),
            ("Tipo de imóvel", residencial.TipoImovel == TipoImovel.Casa ? "Casa" : "Apartamento"),
            ("CEP", Cep(residencial.Cep)),
            ("Residência habitual", SimNao(residencial.Habitual)),
        ],
        DadosRiscoVida vida => [("Fumante", SimNao(vida.Fumante))],
        _ => [],
    };
}
