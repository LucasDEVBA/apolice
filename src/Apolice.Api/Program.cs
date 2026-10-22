using Apolice.Api.Extensions;
using Apolice.Application;
using Apolice.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddConfiguracaoApolice();
builder.AddLogsApolice();
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration, builder.Environment)
    .AddSegurancaApi(builder.Configuration)
    .AddHttpApi()
    .AddDocumentacaoApi()
    .AddObservabilidadeApi(builder.Configuration);

var app = builder.Build();

app.UsePipelineApolice();
app.MapEndpointsApolice();

await app.RunAsync();

// Visível para o WebApplicationFactory<Program> dos testes de integração.
public partial class Program;
