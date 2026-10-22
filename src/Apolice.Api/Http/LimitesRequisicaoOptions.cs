namespace Apolice.Api.Http;

// Os padrões são os de produção (SPEC 7). Só o ambiente de E2E (docker compose) os aumenta: lá dezenas de testes
// paralelos entram pela mesma origem e estourariam o limite de login, que existe contra força bruta, não contra CI.
internal sealed class LimitesRequisicaoOptions
{
    public const string Secao = "RateLimiting";

    public int AuthPorMinuto { get; set; } = 10;
    public int RefreshPorMinuto { get; set; } = 30;
    public int GlobalPorMinuto { get; set; } = 120;
    public int FichasPorSegundo { get; set; } = 2;
}
