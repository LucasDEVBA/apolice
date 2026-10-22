using Apolice.Application.Features.Notificacoes;
using Apolice.Application.Features.Sinistros;
using Apolice.Domain.Notificacoes;
using Apolice.Domain.Sinistros;
using Dapper;
using Npgsql;

namespace Apolice.Infrastructure.Persistence.Queries;

// Pedaços de leitura reaproveitados pelos detalhes (nomes, resumos de sinistro, notificações).
internal static class Leitura
{
    public const string UsuarioDesconhecido = "Usuário desconhecido";

    public const string ColunasResumoSinistro = """
        s.id AS Id, s.numero AS Numero, s.apolice_numero AS ApoliceNumero, s.segurado_nome AS SeguradoNome,
        s.cobertura_nome AS CoberturaNome, s.valor_reclamado AS ValorReclamado, s.status AS Status,
        s.triagem_score AS TriagemScore, s.aberto_em AS AbertoEm
        """;

    public static async Task<List<T>> ListarAsync<T>(this NpgsqlConnection conexao, string sql, object? parametros, CancellationToken cancellationToken)
    {
        DapperConfiguracao.Garantir();
        var linhas = await conexao.QueryAsync<T>(new CommandDefinition(sql, parametros, cancellationToken: cancellationToken));
        return linhas.AsList();
    }

    public static async Task<LinhaNomes> NomesAsync(this NpgsqlConnection conexao, string? produtoCodigo, Guid corretorId, CancellationToken cancellationToken)
    {
        const string Sql = """
            SELECT (SELECT p.nome FROM produtos p WHERE p.codigo = @Produto) AS ProdutoNome,
                   (SELECT u.nome FROM usuarios u WHERE u.id = @Corretor) AS CorretorNome
            """;
        var linhas = await conexao.ListarAsync<LinhaNomes>(Sql, new { Produto = produtoCodigo ?? string.Empty, Corretor = corretorId }, cancellationToken);
        return linhas[0];
    }

    // Subconsulta em vez de "= ANY(array)": a lista de ids sai do próprio banco e nenhum parâmetro de array é preciso.
    public static async Task<Dictionary<Guid, string>> NomesUsuariosAsync(this NpgsqlConnection conexao, string subconsultaIds, object parametros, CancellationToken cancellationToken)
    {
        var sql = $"SELECT u.id AS Id, u.nome AS Nome FROM usuarios u WHERE u.id IN ({subconsultaIds})";
        var linhas = await conexao.ListarAsync<LinhaNome>(sql, parametros, cancellationToken);
        return linhas.ToDictionary(l => l.Id, l => l.Nome);
    }

    public static SinistroResumoDto ParaDto(LinhaSinistroResumo linha) =>
        new(
            linha.Id,
            linha.Numero,
            linha.ApoliceNumero,
            linha.SeguradoNome,
            linha.CoberturaNome,
            linha.ValorReclamado,
            Enum.Parse<StatusSinistro>(linha.Status),
            linha.TriagemScore,
            linha.AbertoEm);

    public static NotificacaoDto ParaDto(LinhaNotificacao linha) =>
        new(linha.Id, Enum.Parse<TipoNotificacao>(linha.Tipo), linha.Destinatario, linha.Assunto, linha.Corpo, linha.CriadaEm);
}
