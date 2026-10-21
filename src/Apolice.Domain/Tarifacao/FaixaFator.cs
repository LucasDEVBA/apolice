namespace Apolice.Domain.Tarifacao;

internal sealed record FaixaFator(int Minimo, int Maximo, decimal Fator)
{
    public bool Contem(int valor) => valor >= Minimo && valor <= Maximo;
}
