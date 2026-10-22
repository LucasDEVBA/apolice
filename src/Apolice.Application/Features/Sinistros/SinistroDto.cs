using Apolice.Application.Features.Comum;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Sinistros;

public sealed record SinistroDto(
    Guid Id,
    string Numero,
    Guid ApoliceId,
    string ApoliceNumero,
    string ProdutoCodigo,
    string SeguradoNome,
    string CoberturaCodigo,
    string CoberturaNome,
    decimal LmiCobertura,
    DateOnly DataOcorrencia,
    string Descricao,
    decimal ValorReclamado,
    decimal? ValorIndenizacao,
    StatusSinistro Status,
    string? MotivoNegativa,
    TriagemDto? Triagem,
    DateTimeOffset AbertoEm,
    string CorretorNome,
    IReadOnlyList<HistoricoSinistroDto> Historico);

public sealed record TriagemDto(int Score, IReadOnlyList<string> Alertas, DateTimeOffset TriadoEm);

public sealed record HistoricoSinistroDto(StatusSinistro Status, string? Observacao, string? UsuarioNome, DateTimeOffset OcorridoEm);

public sealed record SinistroResumoDto(
    Guid Id,
    string Numero,
    string ApoliceNumero,
    string SeguradoNome,
    string CoberturaNome,
    decimal ValorReclamado,
    StatusSinistro Status,
    int? TriagemScore,
    DateTimeOffset AbertoEm);

public static class SinistroMapeamento
{
    // nomesUsuarios: id → nome de quem aparece no histórico (UsuarioId nulo = ação automática, sem nome).
    public static SinistroDto ParaDto(Sinistro sinistro, string corretorNome, IReadOnlyDictionary<Guid, string> nomesUsuarios)
    {
        ArgumentNullException.ThrowIfNull(sinistro);
        ArgumentNullException.ThrowIfNull(nomesUsuarios);
        return new SinistroDto(
            sinistro.Id,
            sinistro.Numero,
            sinistro.ApoliceId,
            sinistro.ApoliceNumero,
            sinistro.ProdutoCodigo,
            sinistro.SeguradoNome,
            sinistro.CoberturaCodigo,
            sinistro.CoberturaNome,
            sinistro.LmiCobertura,
            sinistro.DataOcorrencia,
            sinistro.Descricao,
            sinistro.ValorReclamado,
            sinistro.ValorIndenizacao,
            sinistro.Status,
            sinistro.MotivoNegativa,
            sinistro.Triagem is { } triagem ? new TriagemDto(triagem.Score, triagem.Alertas, triagem.TriadoEm) : null,
            sinistro.AbertoEm,
            corretorNome,
            [.. sinistro.Historico.Select(h => ParaDto(h, nomesUsuarios))]);
    }

    private static HistoricoSinistroDto ParaDto(HistoricoSinistro historico, IReadOnlyDictionary<Guid, string> nomesUsuarios)
    {
        var nome = historico.UsuarioId is { } id ? nomesUsuarios.GetValueOrDefault(id, NomesPadrao.UsuarioDesconhecido) : null;
        return new HistoricoSinistroDto(historico.Status, historico.Observacao, nome, historico.OcorridoEm);
    }
}
