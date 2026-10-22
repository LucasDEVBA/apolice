using Apolice.Application.Common;
using Apolice.Domain.Apolices;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Features.Eventos;

// E-mails simulados (caixa de saída na UI): texto pt-BR com valores e datas no formato brasileiro.
internal static class ModelosNotificacao
{
    private const string Assinatura = "Atenciosamente,\nEquipe Apólice";
    private const string Rodape = "Este é um e-mail automático; em caso de dúvidas, fale com o seu corretor.";

    public static ModeloNotificacao BoasVindas(ApoliceEmitida evento, string produtoNome) => new(
        $"Bem-vindo(a)! Sua apólice {evento.Numero} foi emitida",
        Montar(
            evento.SeguradoNome,
            $"É um prazer ter você conosco. Sua apólice {evento.Numero} ({produtoNome}) foi emitida com sucesso.",
            $"Vigência: de {Formatacao.Data(evento.VigenciaInicio)} a {Formatacao.Data(evento.VigenciaFim)}.\nPrêmio total: {Formatacao.Moeda(evento.PremioTotal)}.",
            "Guarde este e-mail para consulta."));

    public static ModeloNotificacao Endosso(ApoliceEndossada evento, string coberturaNome, DateOnly? dataEfeito) => new(
        $"Endosso nº {evento.EndossoNumero} da apólice {evento.Numero}",
        Montar(
            evento.SeguradoNome,
            $"Registramos o endosso nº {evento.EndossoNumero} na sua apólice {evento.Numero}: {DescreverTipo(evento.Tipo)} da cobertura {coberturaNome}"
                + (dataEfeito is { } data ? $", com efeito a partir de {Formatacao.Data(data)}." : "."),
            DescreverAjuste(evento.AjusteTotal)));

    public static ModeloNotificacao Cancelamento(ApoliceCancelada evento) => new(
        $"Cancelamento da apólice {evento.Numero}",
        Montar(
            evento.SeguradoNome,
            $"Confirmamos o cancelamento da sua apólice {evento.Numero}, por {DescreverMotivo(evento.Motivo)}.",
            evento.ValorRestituicao > 0
                ? $"Valor a restituir: {Formatacao.Moeda(evento.ValorRestituicao)} (o IOF não é restituível)."
                : "Não há valor de prêmio a restituir.",
            "Agradecemos a confiança e seguimos à disposição."));

    public static ModeloNotificacao SinistroAberto(Sinistro sinistro) => new(
        $"Recebemos o aviso do sinistro {sinistro.Numero}",
        Montar(
            sinistro.SeguradoNome,
            $"Recebemos o aviso do sinistro {sinistro.Numero}, referente à apólice {sinistro.ApoliceNumero} (cobertura {sinistro.CoberturaNome}), ocorrido em {Formatacao.Data(sinistro.DataOcorrencia)}.",
            $"Valor reclamado: {Formatacao.Moeda(sinistro.ValorReclamado)}.",
            "Nossa equipe vai analisar o pedido, e você será informado(a) a cada etapa."));

    public static ModeloNotificacao SinistroAprovado(SinistroAprovado evento, Sinistro sinistro) => new(
        $"Sinistro {evento.Numero} aprovado",
        Montar(
            sinistro.SeguradoNome,
            $"Temos uma boa notícia: o sinistro {evento.Numero}, da apólice {sinistro.ApoliceNumero}, foi aprovado.",
            $"Valor da indenização: {Formatacao.Moeda(evento.ValorIndenizacao)}.",
            "Avisaremos assim que o pagamento for realizado."));

    public static ModeloNotificacao SinistroNegado(SinistroNegado evento, Sinistro sinistro) => new(
        $"Sinistro {evento.Numero}: resultado da análise",
        Montar(
            sinistro.SeguradoNome,
            $"Após análise cuidadosa, o sinistro {evento.Numero}, da apólice {sinistro.ApoliceNumero}, não pôde ser aprovado.",
            $"Motivo: {evento.Motivo}",
            "Se tiver novas informações ou discordar da decisão, fale com o seu corretor."));

    public static ModeloNotificacao SinistroPago(SinistroPago evento, Sinistro sinistro) => new(
        $"Pagamento do sinistro {evento.Numero}",
        Montar(
            sinistro.SeguradoNome,
            $"Informamos que o pagamento da indenização do sinistro {evento.Numero}, da apólice {sinistro.ApoliceNumero}, foi realizado.",
            $"Valor pago: {Formatacao.Moeda(sinistro.ValorIndenizacao ?? 0m)}.",
            "Esperamos que tudo volte ao normal o quanto antes."));

    internal static string DescreverAjuste(decimal ajusteTotal) => ajusteTotal switch
    {
        > 0 => $"Ajuste de prêmio a pagar: {Formatacao.Moeda(ajusteTotal)}.",
        < 0 => $"Valor a restituir: {Formatacao.Moeda(-ajusteTotal)}.",
        _ => "Não houve alteração no valor do prêmio.",
    };

    internal static string DescreverTipo(TipoEndosso tipo) => tipo switch
    {
        TipoEndosso.AlteracaoLmi => "alteração do limite máximo de indenização (LMI)",
        TipoEndosso.InclusaoCobertura => "inclusão",
        TipoEndosso.ExclusaoCobertura => "exclusão",
        _ => "alteração",
    };

    internal static string DescreverMotivo(MotivoCancelamento motivo) => motivo switch
    {
        MotivoCancelamento.PedidoDoSegurado => "pedido do segurado",
        MotivoCancelamento.IniciativaSeguradora => "iniciativa da seguradora",
        MotivoCancelamento.Inadimplencia => "inadimplência",
        _ => "motivo administrativo",
    };

    private static string Montar(string nome, params string[] paragrafos) =>
        string.Join("\n\n", [$"Olá, {nome}!", .. paragrafos, Rodape, Assinatura]);
}
