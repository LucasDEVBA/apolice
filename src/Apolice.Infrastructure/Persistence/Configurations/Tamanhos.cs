namespace Apolice.Infrastructure.Persistence.Configurations;

// Limites de coluna espelhando as validações do domínio (que continua sendo quem decide): a coluna só não deixa um
// valor fora da regra entrar por outro caminho.
internal static class Tamanhos
{
    public const int CodigoProduto = 10;
    public const int CodigoCobertura = 30;
    public const int Nome = 120;
    public const int DescricaoCobertura = 500;
    public const int DescricaoProduto = 1_000;
    public const int NumeroDocumento = 30;
    public const int TextoLivre = 2_000;
    public const int DescricaoFator = 200;
    public const int SenhaHash = 512;
    public const int HashToken = 64;
    public const int AssuntoNotificacao = 200;
    public const int CorpoNotificacao = 10_000;
    public const int CorrelationId = 64;

    // numeric(12,6): taxas (fração ou ‰), alíquota de IOF e fatores de tarifação.
    public const int PrecisaoTaxa = 12;
    public const int EscalaTaxa = 6;
}
