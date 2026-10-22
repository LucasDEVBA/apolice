using System.Text.Json;
using Apolice.Application.Common;
using Apolice.Domain.Comum;
using Apolice.Domain.Tarifacao;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Apolice.Infrastructure.Persistence.Conversores;

// Os VOs entram pela fábrica Reconstituir: valor gravado corrompido vira exceção na leitura, não VO inválido.
internal sealed class CpfConverter() : ValueConverter<Cpf, string>(cpf => cpf.Numero, numero => Cpf.Reconstituir(numero));

internal sealed class EmailConverter() : ValueConverter<Email, string>(email => email.Valor, valor => Email.Reconstituir(valor));

internal sealed class DinheiroConverter() : ValueConverter<Dinheiro, decimal>(dinheiro => dinheiro.Valor, valor => new Dinheiro(valor));

// O Npgsql só grava timestamptz com offset zero; o domínio às vezes carrega -03:00 (ex.: instante provisório da triagem).
internal sealed class UtcConverter() : ValueConverter<DateTimeOffset, DateTimeOffset>(instante => instante.ToUniversalTime(), instante => instante);

// Coluna text com o mesmo JSON polimórfico da API (discriminador "produto"): o DSQL não tem jsonb.
internal sealed class DadosRiscoConverter() : ValueConverter<DadosRisco, string>(dados => Serializar(dados), json => Desserializar(json))
{
    private static string Serializar(DadosRisco dados) => JsonSerializer.Serialize(dados, JsonOpcoes.Padrao);

    private static DadosRisco Desserializar(string json) =>
        JsonSerializer.Deserialize<DadosRisco>(json, JsonOpcoes.Padrao)
            ?? throw new DadoPersistidoInvalidoException("Dados de risco persistidos vazios.");
}

// Lista curta de códigos (alertas da triagem) numa coluna text com JSON: o DSQL não tem colunas de array.
internal sealed class ListaTextoConverter() : ValueConverter<IReadOnlyList<string>, string>(lista => Serializar(lista), json => Desserializar(json))
{
    private static string Serializar(IReadOnlyList<string> lista) => JsonSerializer.Serialize(lista, JsonOpcoes.Padrao);

    private static IReadOnlyList<string> Desserializar(string json) =>
        JsonSerializer.Deserialize<string[]>(json, JsonOpcoes.Padrao)
            ?? throw new DadoPersistidoInvalidoException("Lista persistida vazia.");
}

// Comparação por conteúdo: sem ele o EF compararia a referência da lista e não detectaria troca de alertas.
internal sealed class ListaTextoComparer() : ValueComparer<IReadOnlyList<string>>(
    (esquerda, direita) => esquerda == null ? direita == null : direita != null && esquerda.SequenceEqual(direita),
    lista => lista.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode(StringComparison.Ordinal))),
    lista => lista.ToArray());
