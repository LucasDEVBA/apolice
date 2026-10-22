using Apolice.Application.Abstractions.Queries;
using Apolice.Application.Common;
using Apolice.Application.Features.Apolices;
using Apolice.Application.Features.Cotacoes;
using Apolice.Application.Features.Notificacoes;
using Apolice.Application.Features.Sinistros;
using Apolice.Domain.Apolices;
using Apolice.Domain.Sinistros;

namespace Apolice.Application.Tests.Suporte;

// Lado de leitura em memória: lê o estado GRAVADO (como o Dapper) e segue o contrato documentado nas portas (filtro
// por corretor, status efetivo, busca, keyset). É um dublê para exercitar os handlers — a fidelidade do SQL real
// (filtros, keyset, junções do detalhe) só é provada pelos testes de integração da infra.
internal sealed class ConsultasEmMemoria(BancoEmMemoria banco) : ICotacaoQueries, IApoliceQueries, ISinistroQueries
{
    public List<Guid?> CorretoresConsultados { get; } = [];

    public FiltroCotacoes? UltimoFiltroCotacoes { get; private set; }

    public FiltroApolices? UltimoFiltroApolices { get; private set; }

    public FiltroSinistros? UltimoFiltroSinistros { get; private set; }

    // Simula um WHERE esquecido no SQL da infra: o detalhe devolve o registro de qualquer carteira.
    public bool IgnorarFiltroDeCorretor { get; set; }

    public Task<Pagina<CotacaoResumoDto>> ListarAsync(FiltroCotacoes filtro, CancellationToken cancellationToken)
    {
        UltimoFiltroCotacoes = filtro;
        var linhas = banco.Cotacoes
            .Where(c => filtro.CorretorId is null || c.CorretorId == filtro.CorretorId)
            .Where(c => filtro.Status is null || c.StatusEfetivo(filtro.Agora) == filtro.Status)
            .Where(c => filtro.ProdutoCodigo is null || c.ProdutoCodigo == filtro.ProdutoCodigo)
            .Where(c => filtro.Busca is null || Contem(c.Segurado.Nome, filtro.Busca) || c.Segurado.Cpf.Numero.Contains(filtro.Busca, StringComparison.Ordinal))
            .Where(c => Depois(c.CriadaEm, c.Id, filtro.Apos))
            .OrderByDescending(c => c.CriadaEm).ThenByDescending(c => c.Id)
            .Take(filtro.Limite + 1)
            .Select(c => new CotacaoResumoDto(c.Id, c.ProdutoCodigo, c.Segurado.Nome, c.PremioTotal, c.StatusEfetivo(filtro.Agora), c.ValidaAte, c.CriadaEm, Nome(c.CorretorId)))
            .ToList();
        return Task.FromResult(Pagina.Montar(linhas, filtro.Limite, r => new PosicaoCursor(r.CriadaEm, r.Id)));
    }

    public Task<CotacaoDto?> ObterAsync(Guid id, Guid? corretorId, DateTimeOffset agora, CancellationToken cancellationToken)
    {
        CorretoresConsultados.Add(corretorId);
        var cotacao = banco.Cotacoes.Find(c => c.Id == id && DaCarteira(c.CorretorId, corretorId));
        return Task.FromResult(cotacao is null ? null : CotacaoMapeamento.ParaDto(cotacao, NomeProduto(cotacao.ProdutoCodigo), Nome(cotacao.CorretorId), agora));
    }

    public Task<Pagina<ApoliceResumoDto>> ListarAsync(FiltroApolices filtro, CancellationToken cancellationToken)
    {
        UltimoFiltroApolices = filtro;
        var linhas = banco.Apolices
            .Where(a => filtro.CorretorId is null || a.CorretorId == filtro.CorretorId)
            .Where(a => filtro.Status is null || a.StatusEfetivo(filtro.Hoje) == filtro.Status)
            .Where(a => filtro.ProdutoCodigo is null || a.ProdutoCodigo == filtro.ProdutoCodigo)
            .Where(a => filtro.Busca is null || Contem(a.Numero, filtro.Busca) || Contem(a.Segurado.Nome, filtro.Busca) || a.Segurado.Cpf.Numero.Contains(filtro.Busca, StringComparison.Ordinal))
            .Where(a => Depois(a.EmitidaEm, a.Id, filtro.Apos))
            .OrderByDescending(a => a.EmitidaEm).ThenByDescending(a => a.Id)
            .Take(filtro.Limite + 1)
            .Select(a => ResumoApolice(a, filtro.Hoje))
            .ToList();
        return Task.FromResult(Pagina.Montar(linhas, filtro.Limite, r => new PosicaoCursor(r.EmitidaEm, r.Id)));
    }

    public Task<ApoliceDto?> ObterAsync(Guid id, Guid? corretorId, DateOnly hoje, CancellationToken cancellationToken)
    {
        CorretoresConsultados.Add(corretorId);
        var apolice = banco.Apolices.Find(a => a.Id == id && DaCarteira(a.CorretorId, corretorId));
        return Task.FromResult(apolice is null ? null : ApoliceMapeamento.ParaDto(apolice, hoje, Complemento(apolice)));
    }

    public Task<Pagina<SinistroResumoDto>> ListarAsync(FiltroSinistros filtro, CancellationToken cancellationToken)
    {
        UltimoFiltroSinistros = filtro;
        var linhas = banco.Sinistros
            .Where(s => filtro.CorretorId is null || s.CorretorId == filtro.CorretorId)
            .Where(s => filtro.Status is null || s.Status == filtro.Status)
            .Where(s => filtro.Busca is null || Contem(s.Numero, filtro.Busca) || Contem(s.ApoliceNumero, filtro.Busca) || Contem(s.SeguradoNome, filtro.Busca))
            .Where(s => Depois(s.AbertoEm, s.Id, filtro.Apos))
            .OrderByDescending(s => s.AbertoEm).ThenByDescending(s => s.Id)
            .Take(filtro.Limite + 1)
            .Select(ResumoSinistro)
            .ToList();
        return Task.FromResult(Pagina.Montar(linhas, filtro.Limite, r => new PosicaoCursor(r.AbertoEm, r.Id)));
    }

    public Task<SinistroDto?> ObterAsync(Guid id, Guid? corretorId, CancellationToken cancellationToken)
    {
        CorretoresConsultados.Add(corretorId);
        var sinistro = banco.Sinistros.Find(s => s.Id == id && DaCarteira(s.CorretorId, corretorId));
        return Task.FromResult(sinistro is null ? null : SinistroMapeamento.ParaDto(sinistro, Nome(sinistro.CorretorId), NomesUsuarios()));
    }

    private static bool Contem(string texto, string busca) => texto.Contains(busca, StringComparison.OrdinalIgnoreCase);

    private static bool Depois(DateTimeOffset instante, Guid id, PosicaoCursor? apos) =>
        apos is null || instante < apos.Instante || (instante == apos.Instante && id.CompareTo(apos.Id) < 0);

    private static SinistroResumoDto ResumoSinistro(Sinistro s) =>
        new(s.Id, s.Numero, s.ApoliceNumero, s.SeguradoNome, s.CoberturaNome, s.ValorReclamado, s.Status, s.Triagem?.Score, s.AbertoEm);

    private bool DaCarteira(Guid dono, Guid? corretorId) => IgnorarFiltroDeCorretor || corretorId is null || dono == corretorId;

    private ApoliceResumoDto ResumoApolice(ApoliceSeguro a, DateOnly hoje) =>
        new(a.Id, a.Numero, a.ProdutoCodigo, a.Segurado.Nome, a.Segurado.Cpf.Mascarado, a.PremioTotal, a.Vigencia.Inicio, a.Vigencia.Fim, a.StatusEfetivo(hoje), Nome(a.CorretorId), a.EmitidaEm);

    private ComplementoApolice Complemento(ApoliceSeguro apolice)
    {
        var sinistros = banco.Sinistros.Where(s => s.ApoliceId == apolice.Id).ToList();
        var referencias = sinistros.Select(s => s.Id).Append(apolice.Id).ToHashSet();
        return new ComplementoApolice(
            NomeProduto(apolice.ProdutoCodigo),
            Nome(apolice.CorretorId),
            NomesUsuarios(),
            [.. sinistros.Select(ResumoSinistro)],
            [.. banco.Notificacoes.Where(n => referencias.Contains(n.ReferenciaId)).OrderBy(n => n.CriadaEm).Select(NotificacaoDto.De)]);
    }

    private Dictionary<Guid, string> NomesUsuarios() => banco.Usuarios.ToDictionary(u => u.Id, u => u.Nome);

    private string Nome(Guid usuarioId) => banco.Usuarios.Find(u => u.Id == usuarioId)?.Nome ?? "?";

    private string NomeProduto(string codigo) => banco.Produtos.Find(p => p.Codigo == codigo)?.Nome ?? codigo;
}
