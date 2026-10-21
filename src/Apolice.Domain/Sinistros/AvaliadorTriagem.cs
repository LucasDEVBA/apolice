using Apolice.Domain.Comum;

namespace Apolice.Domain.Sinistros;

public static class AvaliadorTriagem
{
    private const int DiasVigenciaRecente = 30;
    private const int DiasComunicacaoTardia = 30;
    private const decimal FracaoProximaDoLmi = 0.80m;
    private const int PontosVigenciaRecente = 40;
    private const int PontosValorProximoLmi = 30;
    private const int PontosValorAcimaLmi = 20;
    private const int PontosComunicacaoTardia = 10;

    // A comunicação tardia é medida pela data de ABERTURA gravada (não por "hoje"): reprocessar a
    // mensagem SinistroAberto dias depois precisa produzir o mesmo score.
    // A assinatura do SPEC 4.5 só dá a data ("hoje"), então o TriadoEm devolvido é provisório (início do dia
    // em Brasília); o instante oficial é carimbado por Sinistro.RegistrarTriagem, que o substitui.
    public static TriagemSinistro Avaliar(Sinistro sinistro, DateOnly inicioVigencia, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(sinistro);

        var dataAbertura = Relogio.HojeEm(sinistro.AbertoEm);
        var regras = new (bool Disparou, int Pontos, string Alerta)[]
        {
            (sinistro.DataOcorrencia.DayNumber - inicioVigencia.DayNumber <= DiasVigenciaRecente, PontosVigenciaRecente, AlertasTriagem.VigenciaRecente),
            (sinistro.ValorReclamado > sinistro.LmiCobertura * FracaoProximaDoLmi, PontosValorProximoLmi, AlertasTriagem.ValorProximoLmi),
            (sinistro.ValorReclamado > sinistro.LmiCobertura, PontosValorAcimaLmi, AlertasTriagem.ValorAcimaLmi),
            (dataAbertura.DayNumber - sinistro.DataOcorrencia.DayNumber > DiasComunicacaoTardia, PontosComunicacaoTardia, AlertasTriagem.ComunicacaoTardia),
        };

        var disparadas = regras.Where(r => r.Disparou).ToList();
        var score = Math.Min(disparadas.Sum(r => r.Pontos), TriagemSinistro.ScoreMaximo);
        var avaliadoEm = new DateTimeOffset(hoje.ToDateTime(TimeOnly.MinValue), Relogio.OffsetBrasilia);

        // Pontuação e alertas são montados aqui dentro das faixas válidas: falha seria bug (Value lança).
        return TriagemSinistro.Criar(score, [.. disparadas.Select(r => r.Alerta)], avaliadoEm).Value;
    }
}
