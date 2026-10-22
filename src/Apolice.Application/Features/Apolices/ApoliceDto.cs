using Apolice.Application.Features.Comum;
using Apolice.Application.Features.Notificacoes;
using Apolice.Application.Features.Sinistros;
using Apolice.Domain.Apolices;
using Apolice.Domain.Tarifacao;

namespace Apolice.Application.Features.Apolices;

public sealed record ApoliceDto(
    Guid Id,
    string Numero,
    Guid CotacaoId,
    string ProdutoCodigo,
    string ProdutoNome,
    SeguradoDto Segurado,
    DadosRisco DadosRisco,
    DateOnly VigenciaInicio,
    DateOnly VigenciaFim,
    IReadOnlyList<CoberturaApoliceDto> Coberturas,
    decimal PremioLiquido,
    decimal AjustesEndosso,
    decimal PremioLiquidoVigente,
    decimal AliquotaIof,
    decimal Iof,
    decimal PremioTotal,
    int Parcelas,
    IReadOnlyList<ParcelaDto> CronogramaParcelas,
    StatusEfetivoApolice Status,
    DateTimeOffset EmitidaEm,
    DateTimeOffset? CanceladaEm,
    MotivoCancelamento? MotivoCancelamento,
    decimal? ValorRestituicao,
    DateTimeOffset? BoasVindasEnviadasEm,
    Guid CorretorId,
    string CorretorNome,
    IReadOnlyList<EndossoDto> Endossos,
    IReadOnlyList<SinistroResumoDto> Sinistros,
    IReadOnlyList<NotificacaoDto> Notificacoes);

public sealed record CoberturaApoliceDto(string Codigo, string Nome, decimal Lmi, decimal PremioLiquido, bool Obrigatoria, bool PermiteSinistro);

public sealed record ParcelaDto(int Numero, DateOnly Vencimento, decimal Valor);

public sealed record EndossoDto(
    Guid Id,
    int Numero,
    TipoEndosso Tipo,
    string CoberturaCodigo,
    string CoberturaNome,
    decimal? LmiAnterior,
    decimal? LmiNovo,
    DateOnly DataEfeito,
    decimal AjusteLiquido,
    decimal IofAjuste,
    decimal AjusteTotal,
    DateTimeOffset CriadoEm,
    string CriadoPorNome);

public sealed record ApoliceResumoDto(
    Guid Id,
    string Numero,
    string ProdutoCodigo,
    string SeguradoNome,
    string SeguradoCpfMascarado,
    decimal PremioTotal,
    DateOnly VigenciaInicio,
    DateOnly VigenciaFim,
    StatusEfetivoApolice Status,
    string CorretorNome,
    DateTimeOffset EmitidaEm);

// O que o agregado não sabe: nomes (produto, corretor, autores dos endossos) e os outros agregados exibidos
// no detalhe. Notificações incluem as dos sinistros da apólice (referência = id do sinistro).
public sealed record ComplementoApolice(
    string ProdutoNome,
    string CorretorNome,
    IReadOnlyDictionary<Guid, string> NomesUsuarios,
    IReadOnlyList<SinistroResumoDto> Sinistros,
    IReadOnlyList<NotificacaoDto> Notificacoes);

// Mapeamento explícito a partir do agregado. A leitura da infra (IApoliceQueries) deve usá-lo no detalhe para que
// cronograma de parcelas e status efetivo saiam das regras do domínio, nunca de uma cópia em SQL. A listagem não tem
// equivalente aqui de propósito: ela sai do SQL (Dapper), sem materializar agregados.
public static class ApoliceMapeamento
{
    public static ApoliceDto ParaDto(ApoliceSeguro apolice, DateOnly hoje, ComplementoApolice complemento)
    {
        ArgumentNullException.ThrowIfNull(apolice);
        ArgumentNullException.ThrowIfNull(complemento);
        return new ApoliceDto(
            apolice.Id,
            apolice.Numero,
            apolice.CotacaoId,
            apolice.ProdutoCodigo,
            complemento.ProdutoNome,
            SeguradoDto.De(apolice.Segurado),
            apolice.DadosRisco,
            apolice.Vigencia.Inicio,
            apolice.Vigencia.Fim,
            [.. apolice.Coberturas.Select(ParaDto)],
            apolice.PremioLiquido,
            apolice.AjustesEndosso,
            apolice.PremioLiquidoVigente,
            apolice.AliquotaIof,
            apolice.Iof,
            apolice.PremioTotal,
            apolice.Parcelas,
            Cronograma(apolice),
            apolice.StatusEfetivo(hoje),
            apolice.EmitidaEm,
            apolice.CanceladaEm,
            apolice.MotivoCancelamento,
            apolice.ValorRestituicao,
            apolice.BoasVindasEnviadasEm,
            apolice.CorretorId,
            complemento.CorretorNome,
            [.. apolice.Endossos.OrderBy(e => e.Numero).Select(e => ParaDto(e, complemento.NomesUsuarios))],
            complemento.Sinistros,
            complemento.Notificacoes);
    }

    public static IReadOnlyList<ParcelaDto> Cronograma(ApoliceSeguro apolice)
    {
        ArgumentNullException.ThrowIfNull(apolice);
        return [.. apolice.CronogramaParcelas().Select(p => new ParcelaDto(p.Numero, p.Vencimento, p.Valor))];
    }

    private static CoberturaApoliceDto ParaDto(CoberturaApolice cobertura) =>
        new(cobertura.Codigo, cobertura.Nome, cobertura.Lmi, cobertura.PremioLiquido, cobertura.Obrigatoria, cobertura.PermiteSinistro);

    private static EndossoDto ParaDto(Endosso endosso, IReadOnlyDictionary<Guid, string> nomesUsuarios) =>
        new(
            endosso.Id,
            endosso.Numero,
            endosso.Tipo,
            endosso.CoberturaCodigo,
            endosso.CoberturaNome,
            endosso.LmiAnterior,
            endosso.LmiNovo,
            endosso.DataEfeito,
            endosso.AjusteLiquido,
            endosso.IofAjuste,
            endosso.AjusteTotal,
            endosso.CriadoEm,
            nomesUsuarios.GetValueOrDefault(endosso.CriadoPorId, NomesPadrao.UsuarioDesconhecido));
}
