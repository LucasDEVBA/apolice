using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Tarifacao;

namespace Apolice.Domain.Apolices;

public sealed partial class ApoliceSeguro
{
    public Result<SimulacaoEndosso> SimularEndosso(
        SolicitacaoEndosso solicitacao,
        Produto produto,
        ITarifador tarifador,
        DateOnly hoje)
    {
        var calculo = CalcularEndosso(solicitacao, produto, tarifador, hoje);
        return calculo.IsSuccess ? calculo.Value.Simulacao : calculo.Error;
    }

    public Result<Endosso> Endossar(
        SolicitacaoEndosso solicitacao,
        Produto produto,
        ITarifador tarifador,
        Guid usuarioId,
        DateTimeOffset agora)
    {
        if (usuarioId == Guid.Empty)
        {
            throw new ArgumentException("O endosso exige o usuário responsável.", nameof(usuarioId));
        }

        var calculo = CalcularEndosso(solicitacao, produto, tarifador, Relogio.HojeEm(agora));
        if (calculo.IsFailure)
        {
            return calculo.Error;
        }

        var (simulacao, itemNovo, permiteSinistro) = calculo.Value;
        AplicarAlteracao(solicitacao, itemNovo);
        var endosso = Endosso.Criar(Id, _endossos.Count + 1, simulacao, permiteSinistro, usuarioId, agora);
        _endossos.Add(endosso);
        Raise(CriarEventoEndossada(endosso, agora));
        return endosso;
    }

    private Result<CalculoEndosso> CalcularEndosso(SolicitacaoEndosso solicitacao, Produto produto, ITarifador tarifador, DateOnly hoje)
    {
        ArgumentNullException.ThrowIfNull(solicitacao);
        ArgumentNullException.ThrowIfNull(produto);
        ArgumentNullException.ThrowIfNull(tarifador);
        if (produto.Codigo != ProdutoCodigo)
        {
            throw new ArgumentException("O produto informado não é o produto da apólice.", nameof(produto));
        }

        var dataEfeito = ValidarDataEfeito(hoje);
        if (dataEfeito.IsFailure)
        {
            return dataEfeito.Error;
        }

        var atual = ObterCobertura(solicitacao.CoberturaCodigo);
        var regra = ValidarRegraEndosso(solicitacao, atual);
        if (regra.IsFailure)
        {
            return regra.Error;
        }

        // Nova tarifação do conjunto inteiro (com o produto ATUAL e a data de emissão, como manda o SPEC 4.4)
        // para revalidar as dependências entre coberturas, como IPA ≤ MORTE.
        var tarifacao = tarifador.Calcular(produto, Segurado, DadosRisco, MontarCoberturasAlteradas(solicitacao), DataEmissao);
        return tarifacao.IsSuccess ? MontarCalculo(solicitacao, atual, tarifacao.Value, dataEfeito.Value) : tarifacao.Error;
    }

    private Result<DateOnly> ValidarDataEfeito(DateOnly hoje)
    {
        var status = ExigirStatusAlteravel(hoje);
        if (status.IsFailure)
        {
            return status.Error;
        }

        var dataEfeito = hoje > Vigencia.Inicio ? hoje : Vigencia.Inicio;
        return dataEfeito < Vigencia.Fim ? dataEfeito : ApoliceErros.EndossoForaDaVigencia;
    }

    private static Result ValidarRegraEndosso(SolicitacaoEndosso solicitacao, CoberturaApolice? atual)
    {
        if (!Enum.IsDefined(solicitacao.Tipo))
        {
            return ApoliceErros.TipoEndossoInvalido;
        }

        if (solicitacao.Tipo == TipoEndosso.InclusaoCobertura)
        {
            return atual is null ? Result.Success() : ApoliceErros.CoberturaJaContratada(solicitacao.CoberturaCodigo);
        }

        if (atual is null)
        {
            return ApoliceErros.CoberturaNaoContratada(solicitacao.CoberturaCodigo);
        }

        return solicitacao.Tipo == TipoEndosso.ExclusaoCobertura && atual.Obrigatoria
            ? ApoliceErros.CoberturaObrigatoria(solicitacao.CoberturaCodigo)
            : Result.Success();
    }

    private List<CoberturaSolicitada> MontarCoberturasAlteradas(SolicitacaoEndosso solicitacao)
    {
        var coberturas = _coberturas
            .Where(c => c.Codigo != solicitacao.CoberturaCodigo)
            .Select(c => new CoberturaSolicitada(c.Codigo, c.Lmi))
            .ToList();

        if (solicitacao.Tipo != TipoEndosso.ExclusaoCobertura)
        {
            coberturas.Add(new CoberturaSolicitada(solicitacao.CoberturaCodigo, solicitacao.Lmi));
        }

        return coberturas;
    }

    // Invariantes: ValidarRegraEndosso garante o item ATUAL em alteração/exclusão, e ItemRetarifado garante
    // o item NOVO em inclusão/alteração — por isso "novo ?? atual!" nunca encontra os dois nulos.
    private Result<CalculoEndosso> MontarCalculo(
        SolicitacaoEndosso solicitacao,
        CoberturaApolice? atual,
        ResultadoTarifacao tarifacao,
        DateOnly dataEfeito)
    {
        var novo = ItemRetarifado(solicitacao, tarifacao);

        // LMI fixo ou derivado do risco (ex.: CASCO) faz a tarifação ignorar o valor pedido.
        if (solicitacao.Tipo == TipoEndosso.AlteracaoLmi && novo?.Lmi == atual?.Lmi)
        {
            return ApoliceErros.LmiInalterado;
        }

        var anualAnterior = atual?.PremioLiquido ?? 0m;
        var anualNovo = novo?.PremioLiquido ?? 0m;
        var (ajusteLiquido, iofAjuste) = CalcularAjuste(anualNovo - anualAnterior, dataEfeito);
        var simulacao = new SimulacaoEndosso(
            solicitacao.Tipo,
            solicitacao.CoberturaCodigo,
            novo?.Nome ?? atual!.Nome,
            atual?.Lmi,
            novo?.Lmi,
            dataEfeito,
            Vigencia.DiasRestantes(dataEfeito),
            Vigencia.DiasTotais,
            anualAnterior,
            anualNovo,
            ajusteLiquido,
            iofAjuste,
            ajusteLiquido + iofAjuste);
        return new CalculoEndosso(simulacao, novo, novo?.PermiteSinistro ?? atual!.PermiteSinistro);
    }

    // O tarifador recebe o conjunto já alterado: o item endossado só falta no resultado quando é exclusão.
    // Divergência é bug do tarifador (ex.: um fake mal montado), não regra de negócio — por isso exceção.
    private static ItemTarifado? ItemRetarifado(SolicitacaoEndosso solicitacao, ResultadoTarifacao tarifacao)
    {
        var novo = tarifacao.Itens.FirstOrDefault(i => i.Codigo == solicitacao.CoberturaCodigo);
        var esperaItem = solicitacao.Tipo != TipoEndosso.ExclusaoCobertura;
        return (novo is not null) == esperaItem
            ? novo
            : throw new InvalidOperationException("A tarifação do endosso não corresponde ao conjunto de coberturas pedido.");
    }

    private (decimal Liquido, decimal Iof) CalcularAjuste(decimal diferencaAnual, DateOnly dataEfeito)
    {
        var liquido = Dinheiro.Arredondar(diferencaAnual * Vigencia.DiasRestantes(dataEfeito) / Vigencia.DiasTotais);
        return (liquido, Dinheiro.Arredondar(liquido * AliquotaIof));
    }

    private ApoliceEndossada CriarEventoEndossada(Endosso endosso, DateTimeOffset agora) =>
        new(Id, Numero, endosso.Numero, endosso.Tipo, endosso.CoberturaCodigo, endosso.AjusteTotal, Segurado.Nome, Segurado.Email.Valor, agora);

    private void AplicarAlteracao(SolicitacaoEndosso solicitacao, ItemTarifado? novo)
    {
        switch (solicitacao.Tipo)
        {
            case TipoEndosso.InclusaoCobertura:
                _coberturas.Add(CoberturaApolice.De(novo!));
                break;
            case TipoEndosso.ExclusaoCobertura:
                _coberturas.RemoveAll(c => c.Codigo == solicitacao.CoberturaCodigo);
                break;
            default:
                ObterCobertura(solicitacao.CoberturaCodigo)!.Atualizar(novo!.Lmi, novo.PremioLiquido);
                break;
        }
    }

    private sealed record CalculoEndosso(SimulacaoEndosso Simulacao, ItemTarifado? ItemNovo, bool PermiteSinistro);
}
