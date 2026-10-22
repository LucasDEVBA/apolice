using Apolice.Api.IntegrationTests.Infraestrutura.Suporte;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Apolice.Api.IntegrationTests.Api.Suporte;

// A API real (Program) sobre um banco do container, transporte InMemory com despacho imediato (os consumidores rodam
// no próprio request, então notificações e triagem já estão gravadas quando a resposta volta).
// UseSetting, e não ConfigureAppConfiguration: o Program lê a configuração antes do Build (transporte, CORS, SSM), e só
// as host settings chegam a tempo (viram argumentos de linha de comando do WebApplicationBuilder).
public sealed class ApiFactory(string connectionString, Action<IServiceCollection>? servicos = null) : WebApplicationFactory<Program>
{
    public const string Ambiente = "Testing";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.UseEnvironment(Ambiente);
        builder.UseSetting("Database:Provider", "Postgres");
        builder.UseSetting("ConnectionStrings:Apolice", connectionString);
        builder.UseSetting("Messaging:Transport", "InMemory");
        builder.UseSetting("Outbox:DispatchImediato", "true");
        builder.UseSetting("Jwt:SigningKey", AmbienteInfra.ChaveJwtTeste);
        builder.UseSetting("Seed:SenhaUsuarios", AmbienteInfra.SenhaSeed);
        builder.UseSetting("Seed:DadosDemo", "false");
        builder.UseSetting("Serilog:MinimumLevel:Default", "Warning");
        if (servicos is not null)
        {
            builder.ConfigureTestServices(servicos);
        }
    }
}
