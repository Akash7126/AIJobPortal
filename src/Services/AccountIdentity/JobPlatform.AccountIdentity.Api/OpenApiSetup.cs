using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace JobPlatform.AccountIdentity.Api;

/// <summary>
/// OpenAPI document of the service (foundation section 11): title/version, the JWT bearer scheme, a security requirement on every
/// operation that needs authentication, and the documented RFC 9457 problem responses (validation vs business rule vs auth).
/// </summary>
public static class OpenApiSetup
{
    public const string BearerScheme = "Bearer";

    public static IServiceCollection AddAccountIdentityOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = "JobPlatform - Account Identity API",
                    Version = "v1",
                    Description = "BC-03 Account Identity: registration, activation, authentication, sessions, RBAC, administrator account standing, "
                                  + "API credentials / OAuth client credentials and privacy consent. Errors are application/problem+json with a stable "
                                  + "`code`; malformed input is 400 with `errors`, business-rule violations are 409/422."
                };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Access token issued by POST /api/v1/auth/login (users) or POST /oauth/token (partners and services). "
                                  + "Signing keys: /.well-known/jwks.json."
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;
                var requiresAuth = metadata.OfType<IAuthorizeData>().Any() && !metadata.OfType<IAllowAnonymous>().Any();
                operation.Responses ??= new OpenApiResponses();
                if (requiresAuth)
                {
                    operation.Security ??= new List<OpenApiSecurityRequirement>();
                    operation.Security.Add(new OpenApiSecurityRequirement
                    {
                        [new OpenApiSecuritySchemeReference(BearerScheme, context.Document)] = new List<string>()
                    });
                    AddProblem(operation, "401", "Missing, expired or invalid access token (E-AAFR-UNAUTHORIZED).");
                    AddProblem(operation, "403", "The caller may not perform this action (E-AAFR-FORBIDDEN, E-AUM-FORBIDDEN).");
                }

                if (context.Description.HttpMethod is "POST" or "PUT" or "PATCH" or "DELETE")
                {
                    AddProblem(operation, "400", "Malformed request: `errors` lists the field codes (VAL.*).");
                    AddProblem(operation, "409", "The request conflicts with the current state (duplicates, banned account, concurrency).");
                    AddProblem(operation, "422", "A business rule refused the request (expired code, weak password, policy).");
                    AddProblem(operation, "429", "Rate limit or lockout (E-*-RATE-LIMITED); see Retry-After.");
                }

                if (context.Description.RelativePath?.StartsWith("api/v1/admin", StringComparison.Ordinal) == true
                    && context.Description.HttpMethod is "PUT" or "POST" or "DELETE")
                {
                    AddProblem(operation, "412", "If-Match no longer matches the resource ETag (E-PRECONDITION-FAILED).");
                }

                return Task.CompletedTask;
            });
        });
        return services;
    }

    private static void AddProblem(OpenApiOperation operation, string status, string description)
    {
        operation.Responses ??= new OpenApiResponses();
        if (operation.Responses.ContainsKey(status))
        {
            return;
        }

        operation.Responses[status] = new OpenApiResponse
        {
            Description = description,
            Content = new Dictionary<string, OpenApiMediaType> { ["application/problem+json"] = new() }
        };
    }
}
