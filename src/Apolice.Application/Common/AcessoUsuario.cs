using Apolice.Application.Abstractions.Security;
using Apolice.Domain.Comum;
using Apolice.Domain.Usuarios;

namespace Apolice.Application.Common;

// Autorização por recurso (anti-BOLA/IDOR) centralizada: os handlers não repetem as regras de papel.
// É política da aplicação, não contrato com a infraestrutura — por isso fica em Common, e não em Abstractions.
internal static class AcessoUsuario
{
    // Fonte única da visão global: o Papel, a mesma informação que ExigirPapel usa. Uma implementação de ICurrentUser
    // que derivasse PodeVerTudo por outra regra (ex.: "não é corretor", verdadeiro com papel nulo) não abre a carteira toda.
    public static bool TemVisaoGlobal(Papel? papel) => papel is Papel.Analista or Papel.Admin;

    public static Result<Guid> ExigirAutenticado(this ICurrentUser usuario) =>
        usuario.Autenticado && usuario.UsuarioId is { } id ? id : ComumErros.NaoAutenticado;

    // Sem filtro só para Analista/Admin. Usuário sem id nunca vira "sem filtro": isso abriria a carteira toda.
    public static Result<EscopoCorretor> Escopo(this ICurrentUser usuario)
    {
        var id = usuario.ExigirAutenticado();
        if (id.IsFailure)
        {
            return id.Error;
        }

        return TemVisaoGlobal(usuario.Papel) ? EscopoCorretor.Global : new EscopoCorretor(id.Value);
    }

    public static Result<Guid> ExigirPapel(this ICurrentUser usuario, params Papel[] papeis)
    {
        var id = usuario.ExigirAutenticado();
        if (id.IsFailure)
        {
            return id;
        }

        return usuario.Papel is { } papel && papeis.Contains(papel) ? id : ComumErros.Proibido;
    }

    // Operações da carteira (cotação, apólice, abertura de sinistro): o corretor dono ou o Admin.
    // Recurso de outro corretor responde "não encontrado"; o Analista vê, mas não opera (Proibido).
    public static Result<Guid> AutorizarCarteira(this ICurrentUser usuario, Guid corretorId, string entidade) =>
        Autorizar(usuario, corretorId, entidade, Papel.Corretor, Papel.Admin);

    // Análise, decisão e pagamento de sinistro: só Analista ou Admin (o corretor dono recebe Proibido).
    public static Result<Guid> AutorizarAnalise(this ICurrentUser usuario, Guid corretorId, string entidade) =>
        Autorizar(usuario, corretorId, entidade, Papel.Analista, Papel.Admin);

    private static Result<Guid> Autorizar(ICurrentUser usuario, Guid corretorId, string entidade, params Papel[] papeis)
    {
        var escopo = usuario.Escopo();
        if (escopo.IsFailure)
        {
            return escopo.Error;
        }

        return escopo.Value.Alcanca(corretorId) ? usuario.ExigirPapel(papeis) : ComumErros.NaoEncontrado(entidade);
    }
}

public sealed record EscopoCorretor(Guid? CorretorId)
{
    public static EscopoCorretor Global { get; } = new((Guid?)null);

    public bool EhGlobal => CorretorId is null;

    // Também conferido sobre o que a leitura devolve (defesa em profundidade): um filtro esquecido no SQL
    // não pode virar BOLA, porque o handler recusa o registro de outra carteira do mesmo jeito.
    public bool Alcanca(Guid corretorId) => EhGlobal || CorretorId == corretorId;
}
