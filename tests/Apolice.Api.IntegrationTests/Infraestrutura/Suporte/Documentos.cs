using System.Text;

namespace Apolice.Api.IntegrationTests.Infraestrutura.Suporte;

internal static class Documentos
{
    private const int TamanhoMinimoBytes = 10_000;

    // PDF válido o bastante para um leitor abrir: cabeçalho %PDF-, trailer %%EOF e tamanho de documento real.
    // Com APOLICE_PDF_SAIDA definido, o arquivo é gravado para conferência visual.
    public static void DeveParecerPdf(byte[] pdf, string nome = "apolice-teste.pdf")
    {
        pdf.Length.ShouldBeGreaterThan(TamanhoMinimoBytes);
        Encoding.ASCII.GetString(pdf, 0, 5).ShouldBe("%PDF-");
        Encoding.ASCII.GetString(pdf[^32..]).ShouldContain("%%EOF");
        if (Environment.GetEnvironmentVariable("APOLICE_PDF_SAIDA") is { Length: > 0 } pasta)
        {
            File.WriteAllBytes(Path.Combine(pasta, nome), pdf);
        }
    }
}
