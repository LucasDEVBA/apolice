namespace Apolice.Infrastructure.Persistence.Queries;

// Linhas cruas do Dapper (setters tolerantes a conversão, ex.: COUNT bigint → long). O mapeamento para os DTOs da
// aplicação é explícito nas consultas: máscara de CPF, enums e regras de exibição ficam visíveis em C#.
internal sealed class LinhaCotacaoResumo
{
    public Guid Id { get; init; }

    public string ProdutoCodigo { get; init; } = string.Empty;

    public string SeguradoNome { get; init; } = string.Empty;

    public decimal PremioTotal { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTimeOffset ValidaAte { get; init; }

    public DateTimeOffset CriadaEm { get; init; }

    public string CorretorNome { get; init; } = string.Empty;
}

internal sealed class LinhaApoliceResumo
{
    public Guid Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public string ProdutoCodigo { get; init; } = string.Empty;

    public string SeguradoNome { get; init; } = string.Empty;

    public string SeguradoCpf { get; init; } = string.Empty;

    public decimal PremioTotal { get; init; }

    public DateOnly VigenciaInicio { get; init; }

    public DateOnly VigenciaFim { get; init; }

    public string Status { get; init; } = string.Empty;

    public string CorretorNome { get; init; } = string.Empty;

    public DateTimeOffset EmitidaEm { get; init; }
}

internal sealed class LinhaSinistroResumo
{
    public Guid Id { get; init; }

    public string Numero { get; init; } = string.Empty;

    public string ApoliceNumero { get; init; } = string.Empty;

    public string SeguradoNome { get; init; } = string.Empty;

    public string CoberturaNome { get; init; } = string.Empty;

    public decimal ValorReclamado { get; init; }

    public string Status { get; init; } = string.Empty;

    public int? TriagemScore { get; init; }

    public DateTimeOffset AbertoEm { get; init; }
}

internal sealed class LinhaNotificacao
{
    public Guid Id { get; init; }

    public string Tipo { get; init; } = string.Empty;

    public string Destinatario { get; init; } = string.Empty;

    public string Assunto { get; init; } = string.Empty;

    public string Corpo { get; init; } = string.Empty;

    public DateTimeOffset CriadaEm { get; init; }
}

internal sealed class LinhaAuditoria
{
    public Guid Id { get; init; }

    public string? UsuarioNome { get; init; }

    public string Acao { get; init; } = string.Empty;

    public string Entidade { get; init; } = string.Empty;

    public string? EntidadeId { get; init; }

    public string? Detalhes { get; init; }

    public string? CorrelationId { get; init; }

    public DateTimeOffset OcorridoEm { get; init; }
}

internal sealed class LinhaNome
{
    public Guid Id { get; init; }

    public string Nome { get; init; } = string.Empty;
}

internal sealed class LinhaNomes
{
    public string? ProdutoNome { get; init; }

    public string? CorretorNome { get; init; }
}
