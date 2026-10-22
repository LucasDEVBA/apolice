using Apolice.Application.Abstractions.Services;
using Apolice.Application.Features.Apolices;
using Apolice.Domain.Apolices;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using T = Apolice.Infrastructure.Services.Documentos.TextosDocumento;

namespace Apolice.Infrastructure.Services.Documentos;

// Documento da apólice em PDF (QuestPDF, licença Community). Recebe o mesmo ApoliceDto da tela: o PDF nunca mostra
// algo diferente do detalhe. Identidade do guia de design: face marinho com fio dourado, número como protagonista.
// Fonte Lato embutida no pacote do QuestPDF (copiada para a saída): fontes do sistema ficam desligadas, então o
// documento sai igual no Mac, no container e no Lambda.
internal sealed class QuestPdfGeradorDocumentoApolice(TimeProvider tempo) : IGeradorDocumentoApolice
{
    private const string Fonte = "Lato";
    private const float TamanhoTexto = 9f;
    private const float Margem = 36f;
    private const float Espaco = 12f;

    private static readonly Color Marinho = Color.FromHex("#0B1F3A");
    private static readonly Color MarinhoClaro = Color.FromHex("#16305A");
    private static readonly Color Dourado = Color.FromHex("#C9A86A");
    private static readonly Color DouradoTexto = Color.FromHex("#7A5C2C");
    private static readonly Color DouradoSobreMarinho = Color.FromHex("#D9BF8C");
    private static readonly Color Tinta = Color.FromHex("#0E1A2C");
    private static readonly Color Tinta2 = Color.FromHex("#44526A");
    private static readonly Color Tinta3 = Color.FromHex("#5D697D");
    private static readonly Color SobreMarinho = Color.FromHex("#F4F6FA");
    private static readonly Color SobreMarinho2 = Color.FromHex("#B8C3D6");
    private static readonly Color Linha = Color.FromHex("#DFE4EC");
    private static readonly Color Superficie2 = Color.FromHex("#F7F8FB");

    static QuestPdfGeradorDocumentoApolice()
    {
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = false;
    }

    public Task<byte[]> GerarAsync(ApoliceDto apolice, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(apolice);
        cancellationToken.ThrowIfCancellationRequested();

        var geradoEm = tempo.GetUtcNow();
        var documento = Document.Create(container => container.Page(page => Pagina(page, apolice, geradoEm)))
            .WithMetadata(new DocumentMetadata
            {
                Title = $"Apólice {apolice.Numero}",
                Subject = $"{apolice.ProdutoNome} — documento da apólice",
                Author = "Apólice",
                Creator = "Apólice",
                Language = "pt-BR",
                CreationDate = geradoEm,
                ModifiedDate = geradoEm,
            });
        return Task.FromResult(documento.GeneratePdf());
    }

    private static void Pagina(PageDescriptor page, ApoliceDto apolice, DateTimeOffset geradoEm)
    {
        page.Size(PageSizes.A4);
        page.Margin(Margem);
        page.PageColor(Colors.White);
        page.DefaultTextStyle(x => x.FontFamily(Fonte).FontSize(TamanhoTexto).FontColor(Tinta).LineHeight(1.3f));
        // A face (marinho) só na primeira página: nas seguintes o conteúdo continua sem repetir o "certificado".
        page.Header().ShowOnce().Element(c => Face(c, apolice));
        page.Content().PaddingTop(Espaco).Element(c => Conteudo(c, apolice));
        page.Footer().Element(c => Rodape(c, apolice, geradoEm));
    }

    // Face da apólice: o "certificado" (marinho, faixa dourada no topo, número e prêmio total em destaque).
    private static void Face(IContainer container, ApoliceDto apolice) =>
        container.Background(Marinho).Column(face =>
        {
            face.Item().Height(4).Background(Dourado);
            face.Item().Padding(18).Row(linha =>
            {
                linha.RelativeItem().Column(esquerda =>
                {
                    esquerda.Item().Text("APÓLICE").FontSize(8).LetterSpacing(0.2f).FontColor(DouradoSobreMarinho).SemiBold();
                    esquerda.Item().PaddingTop(2).Text(apolice.ProdutoNome).FontSize(18).FontColor(SobreMarinho).Bold();
                    esquerda.Item().PaddingTop(6).Text(apolice.Numero).FontSize(12).FontColor(SobreMarinho).SemiBold();
                    esquerda.Item().PaddingTop(2).Text($"Emitida em {T.Data(apolice.EmitidaEm)} · Corretor(a) {apolice.CorretorNome}")
                        .FontSize(8.5f).FontColor(SobreMarinho2);
                });
                linha.ConstantItem(170).AlignRight().Column(direita =>
                {
                    direita.Item().AlignRight().Text("PRÊMIO TOTAL").FontSize(8).LetterSpacing(0.2f).FontColor(DouradoSobreMarinho).SemiBold();
                    direita.Item().AlignRight().PaddingTop(2).Text(T.Moeda(apolice.PremioTotal)).FontSize(20).FontColor(SobreMarinho).Bold();
                    direita.Item().AlignRight().PaddingTop(4).Text($"em {apolice.Parcelas}x · {T.Status(apolice.Status)}").FontSize(8.5f).FontColor(SobreMarinho2);
                });
            });
        });

    private static void Conteudo(IContainer container, ApoliceDto apolice) =>
        container.Column(coluna =>
        {
            coluna.Spacing(Espaco);
            coluna.Item().Row(linha =>
            {
                linha.Spacing(Espaco);
                linha.RelativeItem().Element(c => Segurado(c, apolice));
                linha.RelativeItem().Element(c => Vigencia(c, apolice));
            });
            coluna.Item().Element(c => DadosRisco(c, apolice));
            coluna.Item().Element(c => Coberturas(c, apolice));
            coluna.Item().Row(linha =>
            {
                linha.Spacing(Espaco);
                linha.RelativeItem(5).Element(c => Premio(c, apolice));
                linha.RelativeItem(4).Element(c => Cronograma(c, apolice));
            });
            // ShowEntire: seção curta não se parte entre páginas (vai inteira para a próxima se não couber).
            coluna.Item().ShowEntire().Element(c => Endossos(c, apolice));
            if (apolice.Status == StatusEfetivoApolice.Cancelada)
            {
                coluna.Item().ShowEntire().Element(c => Cancelamento(c, apolice));
            }
        });

    private static void Segurado(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Segurado", corpo =>
        {
            Campo(corpo, "Nome", apolice.Segurado.Nome);
            Campo(corpo, "CPF", T.Cpf(apolice.Segurado.Cpf));
            Campo(corpo, "E-mail", apolice.Segurado.Email);
            Campo(corpo, "Data de nascimento", T.Data(apolice.Segurado.DataNascimento));
        });

    private static void Vigencia(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Vigência", corpo =>
        {
            Campo(corpo, "Início", $"{T.Data(apolice.VigenciaInicio)} às 00h00");
            Campo(corpo, "Fim", $"{T.Data(apolice.VigenciaFim)} às 24h00");
            Campo(corpo, "Duração", $"{T.Inteiro(apolice.VigenciaFim.DayNumber - apolice.VigenciaInicio.DayNumber)} dias");
            Campo(corpo, "Situação", T.Status(apolice.Status));
        });

    private static void DadosRisco(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Dados do risco", corpo => corpo.Item().Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn();
                colunas.RelativeColumn();
                colunas.RelativeColumn();
            });
            foreach (var (rotulo, valor) in T.DadosRisco(apolice.DadosRisco))
            {
                tabela.Cell().PaddingBottom(6).Column(c =>
                {
                    c.Item().Text(rotulo).FontSize(7.5f).FontColor(Tinta3);
                    c.Item().Text(valor).SemiBold();
                });
            }
        }));

    private static void Coberturas(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Coberturas contratadas", corpo => corpo.Item().Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.RelativeColumn(5);
                colunas.RelativeColumn(2);
                colunas.RelativeColumn(2);
            });
            tabela.Header(cabecalho =>
            {
                Cabecalho(cabecalho.Cell(), "Cobertura");
                Cabecalho(cabecalho.Cell().AlignRight(), "LMI");
                Cabecalho(cabecalho.Cell().AlignRight(), "Prêmio anual");
            });
            foreach (var cobertura in apolice.Coberturas)
            {
                Celula(tabela.Cell()).Text(texto =>
                {
                    texto.Span(cobertura.Nome).SemiBold();
                    texto.Span(cobertura.Obrigatoria ? "  · obrigatória" : string.Empty).FontSize(7.5f).FontColor(DouradoTexto);
                    texto.Span(cobertura.PermiteSinistro ? string.Empty : "  · serviço").FontSize(7.5f).FontColor(Tinta3);
                });
                Celula(tabela.Cell()).AlignRight().Text(cobertura.Lmi > 0 ? T.Moeda(cobertura.Lmi) : "—");
                Celula(tabela.Cell()).AlignRight().Text(T.Moeda(cobertura.PremioLiquido));
            }
        }));

    private static void Premio(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Prêmio", corpo =>
        {
            Valor(corpo, "Prêmio líquido da emissão", T.Moeda(apolice.PremioLiquido));
            Valor(corpo, "Ajustes de endosso", T.Moeda(apolice.AjustesEndosso));
            Valor(corpo, "Prêmio líquido vigente", T.Moeda(apolice.PremioLiquidoVigente));
            Valor(corpo, $"IOF ({T.Percentual(apolice.AliquotaIof)})", T.Moeda(apolice.Iof));
            corpo.Item().PaddingTop(4).BorderTop(1).BorderColor(Dourado).PaddingTop(6).Row(linha =>
            {
                linha.RelativeItem().Text("Prêmio total da emissão").SemiBold();
                linha.AutoItem().Text(T.Moeda(apolice.PremioTotal)).FontSize(12).Bold().FontColor(Marinho);
            });
            corpo.Item().PaddingTop(4).Text("Ajustes de endosso são cobrados ou restituídos à parte, pelo valor de cada endosso.")
                .FontSize(7.5f).FontColor(Tinta3);
        });

    private static void Cronograma(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Cronograma de parcelas", corpo => corpo.Item().Table(tabela =>
        {
            tabela.ColumnsDefinition(colunas =>
            {
                colunas.ConstantColumn(28);
                colunas.RelativeColumn();
                colunas.RelativeColumn();
            });
            tabela.Header(cabecalho =>
            {
                Cabecalho(cabecalho.Cell(), "Nº");
                Cabecalho(cabecalho.Cell(), "Vencimento");
                Cabecalho(cabecalho.Cell().AlignRight(), "Valor");
            });
            foreach (var parcela in apolice.CronogramaParcelas)
            {
                Celula(tabela.Cell()).Text(parcela.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture));
                Celula(tabela.Cell()).Text(T.Data(parcela.Vencimento));
                Celula(tabela.Cell()).AlignRight().Text(T.Moeda(parcela.Valor));
            }
        }));

    private static void Endossos(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Endossos", corpo =>
        {
            if (apolice.Endossos.Count == 0)
            {
                corpo.Item().Text("Nenhum endosso registrado.").FontColor(Tinta2);
                return;
            }

            corpo.Item().Table(tabela =>
            {
                tabela.ColumnsDefinition(colunas =>
                {
                    colunas.ConstantColumn(24);
                    colunas.RelativeColumn(4);
                    colunas.RelativeColumn(2);
                    colunas.RelativeColumn(3);
                    colunas.RelativeColumn(2);
                });
                tabela.Header(cabecalho =>
                {
                    Cabecalho(cabecalho.Cell(), "Nº");
                    Cabecalho(cabecalho.Cell(), "Endosso");
                    Cabecalho(cabecalho.Cell(), "Efeito");
                    Cabecalho(cabecalho.Cell(), "LMI");
                    Cabecalho(cabecalho.Cell().AlignRight(), "Ajuste total");
                });
                foreach (var endosso in apolice.Endossos)
                {
                    Celula(tabela.Cell()).Text(endosso.Numero.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    Celula(tabela.Cell()).Column(c =>
                    {
                        c.Item().Text(T.Tipo(endosso.Tipo)).SemiBold();
                        c.Item().Text($"{endosso.CoberturaNome} · por {endosso.CriadoPorNome}").FontSize(7.5f).FontColor(Tinta2);
                    });
                    Celula(tabela.Cell()).Text(T.Data(endosso.DataEfeito));
                    Celula(tabela.Cell()).Column(c =>
                    {
                        c.Item().Text(endosso.LmiAnterior is { } anterior ? $"de {T.Moeda(anterior)}" : "nova cobertura").FontColor(Tinta2);
                        c.Item().Text(endosso.LmiNovo is { } novo ? $"para {T.Moeda(novo)}" : "excluída");
                    });
                    Celula(tabela.Cell()).AlignRight().Text(T.Moeda(endosso.AjusteTotal)).SemiBold();
                }
            });
        });

    private static void Cancelamento(IContainer container, ApoliceDto apolice) =>
        Secao(container, "Cancelamento", corpo =>
        {
            Campo(corpo, "Cancelada em", apolice.CanceladaEm is { } em ? T.DataHora(em) : "—");
            Campo(corpo, "Motivo", apolice.MotivoCancelamento is { } motivo ? T.Motivo(motivo) : "—");
            Campo(corpo, "Valor de restituição", T.MoedaOuTraco(apolice.ValorRestituicao));
        });

    private static void Rodape(IContainer container, ApoliceDto apolice, DateTimeOffset geradoEm) =>
        container.PaddingTop(8).BorderTop(0.5f).BorderColor(Linha).PaddingTop(6).Row(linha =>
        {
            linha.RelativeItem().Text($"Apólice {apolice.Numero} · gerado em {T.DataHora(geradoEm)} (Brasília) · documento de demonstração, sem valor contratual.")
                .FontSize(7.5f).FontColor(Tinta3);
            linha.AutoItem().Text(texto =>
            {
                texto.DefaultTextStyle(x => x.FontSize(7.5f).FontColor(Tinta3));
                texto.Span("Página ");
                texto.CurrentPageNumber();
                texto.Span(" de ");
                texto.TotalPages();
            });
        });

    // Bloco com título em dourado e fio fino: estrutura comum a todas as seções.
    private static void Secao(IContainer container, string titulo, Action<ColumnDescriptor> corpo) =>
        container.Border(0.5f).BorderColor(Linha).Column(coluna =>
        {
            coluna.Item().Background(Superficie2).BorderBottom(0.5f).BorderColor(Linha).PaddingVertical(6).PaddingHorizontal(10)
                .Text(titulo).FontSize(9.5f).SemiBold().FontColor(MarinhoClaro);
            coluna.Item().PaddingVertical(8).PaddingHorizontal(10).Column(c =>
            {
                c.Spacing(4);
                corpo(c);
            });
        });

    private static void Campo(ColumnDescriptor coluna, string rotulo, string valor) =>
        coluna.Item().Row(linha =>
        {
            linha.ConstantItem(105).Text(rotulo).FontColor(Tinta3);
            linha.RelativeItem().Text(valor).SemiBold();
        });

    private static void Valor(ColumnDescriptor coluna, string rotulo, string valor) =>
        coluna.Item().Row(linha =>
        {
            linha.RelativeItem().Text(rotulo).FontColor(Tinta2);
            linha.AutoItem().Text(valor);
        });

    private static void Cabecalho(IContainer celula, string texto) =>
        celula.BorderBottom(0.75f).BorderColor(Dourado).PaddingBottom(4).Text(texto).FontSize(7.5f).SemiBold().FontColor(Tinta2);

    private static IContainer Celula(IContainer celula) =>
        celula.BorderBottom(0.5f).BorderColor(Linha).PaddingVertical(4);
}
