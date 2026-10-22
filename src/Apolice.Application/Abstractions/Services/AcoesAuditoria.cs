namespace Apolice.Application.Abstractions.Services;

public static class AcoesAuditoria
{
    public const string Login = "auth.login";
    public const string LoginDemo = "auth.login_demo";
    public const string LoginFalhou = "auth.login_falhou";

    // A tentativa que provocou o bloqueio; as tentativas seguintes, durante o bloqueio, são LoginBloqueado.
    public const string UsuarioBloqueado = "auth.usuario_bloqueado";
    public const string LoginBloqueado = "auth.login_bloqueado";
    public const string LoginInativo = "auth.login_inativo";
    public const string RefreshReutilizado = "auth.refresh_reutilizado";
    public const string SessaoRevogadaUsuarioInativo = "auth.sessao_revogada_inativo";
    public const string Logout = "auth.logout";
    public const string TaxaAtualizada = "produto.taxa_atualizada";
    public const string CotacaoCriada = "cotacao.criada";
    public const string CotacaoAceita = "cotacao.aceita";
    public const string CotacaoRecusada = "cotacao.recusada";
    public const string ApoliceEmitida = "apolice.emitida";
    public const string ApoliceEndossada = "apolice.endossada";
    public const string ApoliceCancelada = "apolice.cancelada";
    public const string SinistroAberto = "sinistro.aberto";
    public const string SinistroEmAnalise = "sinistro.em_analise";
    public const string SinistroAprovado = "sinistro.aprovado";
    public const string SinistroNegado = "sinistro.negado";
    public const string SinistroPago = "sinistro.pago";
    public const string SinistroTriado = "sinistro.triado";
}

// Identificadores de máquina gravados na coluna "entidade" da auditoria (Common/Entidades tem os nomes de exibição
// usados nas mensagens de erro: propósitos diferentes, por isso duas classes).
public static class EntidadesAuditoria
{
    public const string Usuario = "Usuario";
    public const string Produto = "Produto";
    public const string Cotacao = "Cotacao";
    public const string Apolice = "Apolice";
    public const string Sinistro = "Sinistro";
}
