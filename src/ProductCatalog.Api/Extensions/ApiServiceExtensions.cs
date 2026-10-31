using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

namespace ProductCatalog.Api.Extensions;

public static class ApiServiceExtensions
{
    public static IServiceCollection AddApiSecurity(this IServiceCollection services,
        IConfiguration configuration, IHostEnvironment environment)
    {
        var authority = configuration["Authentication:Authority"];
        var audience = configuration["Authentication:Audience"];
        if (!Uri.TryCreate(authority, UriKind.Absolute, out var issuer)
            || (issuer.Scheme != Uri.UriSchemeHttps && !(environment.IsDevelopment() && issuer.IsLoopback))
            || string.IsNullOrWhiteSpace(audience))
        {
            throw new InvalidOperationException("Configure Authentication:Authority (HTTPS) e Authentication:Audience.");
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.Authority = authority;
            if (configuration["Authentication:MetadataAddress"] is { Length: > 0 } metadataAddress)
            {
                options.MetadataAddress = metadataAddress;
            }
            options.Audience = audience;
            options.RequireHttpsMetadata = !environment.IsDevelopment();
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
        });
        services.AddAuthorization(options =>
        {
            foreach (var scope in new[] { "catalog.read", "catalog.write" })
            {
                options.AddPolicy(scope, policy => policy.RequireAuthenticatedUser().RequireAssertion(context =>
                    context.User.FindAll("scope").Concat(context.User.FindAll("scp"))
                        .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Contains(scope, StringComparer.Ordinal)));
            }
        });
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("api", context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 120,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));
        });
        return services;
    }

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Product Catalog API",
                Version = "v1",
                Description = "Catálogo de produtos. PUT e DELETE exigem If-Match com o ETag atual. Preços em BRL."
            });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Token OAuth2/OIDC com scopes catalog.read e/ou catalog.write."
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = []
            });
        });
        services.Configure<ApiBehaviorOptions>(options => options.InvalidModelStateResponseFactory = context =>
        {
            var problem = new ValidationProblemDetails(context.ModelState)
            {
                Status = 400,
                Title = "Requisição inválida",
                Instance = context.HttpContext.Request.Path
            };
            problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;
            var result = new BadRequestObjectResult(problem);
            result.ContentTypes.Add("application/problem+json");
            return result;
        });
        return services;
    }
}
