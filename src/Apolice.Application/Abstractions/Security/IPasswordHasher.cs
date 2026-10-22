namespace Apolice.Application.Abstractions.Security;

public interface IPasswordHasher
{
    string Hash(string senha);

    bool Verificar(string senhaHash, string senha);

    // Para e-mail inexistente ou conta inativa: gasta o mesmo custo de CPU de uma verificação real (ex.: verifica a
    // senha contra um hash de referência calculado uma vez por instância, com os mesmos parâmetros do algoritmo).
    // Fica no hasher porque só ele conhece o próprio custo.
    void SimularVerificacao(string senha);
}
