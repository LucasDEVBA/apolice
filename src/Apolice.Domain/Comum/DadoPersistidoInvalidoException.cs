namespace Apolice.Domain.Comum;

// Valor gravado que não passa na validação do VO indica corrupção ou migração errada:
// é falha de infraestrutura/bug, não um fluxo previsível — por isso exceção, e não Result.
public sealed class DadoPersistidoInvalidoException(string mensagem) : Exception(mensagem);
