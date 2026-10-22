using System.Text.RegularExpressions;
using Apolice.Domain.Comum;
using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using static ArchUnitNET.Fluent.ArchRuleDefinition;
using Assembly = System.Reflection.Assembly;

namespace Apolice.ArchitectureTests.Suporte;

// Carregada uma única vez: ler os assemblies (Mono.Cecil) é a parte cara de cada teste.
internal static class Solucao
{
    public static readonly Assembly Domain = typeof(AggregateRoot<>).Assembly;
    public static readonly Assembly Application = typeof(global::Apolice.Application.DependencyInjection).Assembly;
    public static readonly Assembly Infrastructure = typeof(global::Apolice.Infrastructure.DependencyInjection).Assembly;

    // O Program da Api usa top-level statements, por isso fica no namespace global.
    public static readonly Assembly Api = typeof(global::Program).Assembly;
    public static readonly Assembly Worker = typeof(global::Apolice.Worker.Program).Assembly;

    public static readonly Architecture Arquitetura = new ArchLoader()
        .LoadAssemblies(Domain, Application, Infrastructure, Api, Worker)
        .Build();

    // Dependência proibida, identificada pelo nome usado no SPEC §3 (assembly do projeto ou namespace raiz da biblioteca).
    private static readonly Dictionary<string, IObjectProvider<IType>> Alvos = new()
    {
        ["Apolice.Application"] = Camada(Application),
        ["Apolice.Infrastructure"] = Camada(Infrastructure),
        ["Apolice.Api"] = Camada(Api),
        ["Apolice.Worker"] = Camada(Worker),
        ["Microsoft.EntityFrameworkCore"] = Biblioteca("Microsoft.EntityFrameworkCore"),
        ["Microsoft.AspNetCore"] = Biblioteca("Microsoft.AspNetCore"),
        ["FluentValidation"] = Biblioteca("FluentValidation"),
        ["Npgsql"] = Biblioteca("Npgsql"),
        ["Dapper"] = Biblioteca("Dapper"),
        ["Amazon"] = Biblioteca("Amazon"),
        ["RabbitMQ"] = Biblioteca("RabbitMQ"),
    };

    public static IObjectProvider<IType> Camada(Assembly assembly) => Types().That().ResideInAssembly(assembly);

    public static IObjectProvider<IType> Alvo(string nome) => Alvos[nome];

    public static Assembly Projeto(string nome) => new[] { Domain, Application, Infrastructure, Api, Worker }
        .Single(assembly => assembly.GetName().Name == nome);

    // Bibliotecas externas não entram na arquitetura carregada: só existem como tipos referenciados
    // (includeReferenced), daí a busca pelo namespace raiz e por qualquer sub-namespace dele.
    private static IObjectProvider<IType> Biblioteca(string namespaceRaiz) =>
        Types(includeReferenced: true).That().ResideInNamespaceMatching($@"^{Regex.Escape(namespaceRaiz)}(\..+)?$");
}
