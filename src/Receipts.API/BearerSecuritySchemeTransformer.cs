using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Receipts.API;

// Documents the JWT bearer scheme in the OpenAPI spec so Scalar/Swagger render an
// "Authorize" box. Without this the auth works but is invisible to the docs UI.
public sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public const string SchemeId = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeId] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT from POST /auth/login. Paste only the token — the 'Bearer ' prefix is added automatically.",
        };

        return Task.CompletedTask;
    }
}

// Marks each endpoint that requires authorization as secured in the spec, so the
// Authorize token is sent only where it's needed (not on register/login/health).
public sealed class AuthorizationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        var requiresAuth = context.Description.ActionDescriptor.EndpointMetadata
            .OfType<IAuthorizeData>().Any();

        if (requiresAuth)
        {
            operation.Security ??= new List<OpenApiSecurityRequirement>();
            operation.Security.Add(new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSecuritySchemeTransformer.SchemeId, null)] = new List<string>(),
            });
        }

        return Task.CompletedTask;
    }
}
