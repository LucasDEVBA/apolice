using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Apolice.Api.OpenApi;

// Info do documento + esquema Bearer. Sem "servers": o Scalar e o cliente gerado usam a origem de onde o documento
// foi servido (atrás do rewrite da Vercel, a origem do próprio site), e o arquivo gerado no build fica estável.
internal sealed class InformacoesDocumentoTransformer : IOpenApiDocumentTransformer
{
    public const string EsquemaBearer = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info = new OpenApiInfo
        {
            Title = "Apólice API",
            Version = "v1",
            Description = "Cotação, emissão e gestão de apólices de seguro (Auto, Residencial e Vida), com endosso, " +
                "cancelamento e sinistro. Erros seguem a RFC 9457 (ProblemDetails) com type urn:apolice:problema:{codigo}. " +
                "Demonstração pública: o login por persona dá acesso a qualquer perfil, inclusive Admin; não use dados reais.",
        };
        document.Servers?.Clear();
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[EsquemaBearer] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Access token devolvido por /api/v1/auth/login, /auth/demo ou /auth/refresh (validade de 15 minutos).",
        };
        return Task.CompletedTask;
    }
}
