using System.Reflection;
using Apolice.Domain.Apolices;
using Apolice.Domain.Comum;
using Apolice.Domain.Produtos;
using Apolice.Domain.Sinistros;

namespace Apolice.Domain.Tests.Comum;

// Contrato com o mapeamento EF Core da infraestrutura: materialização por construtor privado sem
// parâmetros, estado só alterável pelo próprio domínio e coleções expostas como somente leitura.
public sealed class ContratoMapeamentoTests
{
    private const BindingFlags Instancia = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static TheoryData<Type> TiposPersistidos { get; } = new(
    [
        .. typeof(Entity<>).Assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && HerdaDeEntidade(t)),
        typeof(CoberturaProduto),
        typeof(CoberturaApolice),
        typeof(Segurado),
        typeof(Vigencia),
        typeof(TriagemSinistro),
    ]);

    [Fact]
    public void Deve_listar_os_tipos_mapeados_quando_varre_o_assembly_do_dominio()
    {
        // Canário: entidade nova (ou removida) aparece pelo nome na falha, para revisar o mapeamento EF.
        TiposPersistidos.Select(t => t.Data.Name).Order(StringComparer.Ordinal).ShouldBe(
        [
            "ApoliceSeguro", "CoberturaApolice", "CoberturaProduto", "Cotacao", "Endosso", "Notificacao", "Produto",
            "RefreshToken", "Segurado", "Sinistro", "TriagemSinistro", "Usuario", "Vigencia",
        ]);
    }

    [Theory]
    [MemberData(nameof(TiposPersistidos))]
    public void Deve_ter_construtor_privado_sem_parametros_quando_tipo_e_persistido(Type tipo)
    {
        var construtor = tipo.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, Type.EmptyTypes);

        construtor.ShouldNotBeNull($"{tipo.Name} precisa de construtor privado sem parâmetros para o EF Core.");
        construtor.IsPrivate.ShouldBeTrue();
        construtor.Invoke(null).ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(TiposPersistidos))]
    public void Deve_ter_apenas_setters_privados_quando_tipo_e_persistido(Type tipo)
    {
        var comSetterPublico = tipo.GetProperties(Instancia)
            .Where(p => p.SetMethod is { IsPublic: true })
            .Select(p => p.Name);

        comSetterPublico.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(TiposPersistidos))]
    public void Deve_expor_colecoes_somente_leitura_quando_tipo_e_persistido(Type tipo)
    {
        var colecoesMutaveis = tipo.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.PropertyType != typeof(string) && p.PropertyType.IsAssignableTo(typeof(System.Collections.IEnumerable)))
            .Where(p => !EhSomenteLeitura(p.PropertyType))
            .Select(p => p.Name);

        colecoesMutaveis.ShouldBeEmpty();
    }

    private static bool HerdaDeEntidade(Type tipo)
    {
        for (var atual = tipo.BaseType; atual is not null; atual = atual.BaseType)
        {
            if (atual.IsGenericType && atual.GetGenericTypeDefinition() == typeof(Entity<>))
            {
                return true;
            }
        }

        return false;
    }

    private static bool EhSomenteLeitura(Type tipo) =>
        tipo.IsGenericType
        && (tipo.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) || tipo.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>));
}
