using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using ProductCatalog.Application.Abstractions;
using ProductCatalog.Infrastructure.Persistence;

namespace ProductCatalog.Api.IntegrationTests;

public sealed class CatalogApiFactory(IProductRepository? repository = null,
    IUnitOfWork? unitOfWork = null, string? connectionString = null) : WebApplicationFactory<Program>
{
    private const string Issuer = "https://identity.example.test";
    private const string Audience = "product-catalog-tests";
    // Test-only signing key. It is never registered or accepted by the production application.
    private static readonly SymmetricSecurityKey Key = new(
        System.Text.Encoding.UTF8.GetBytes("catalog-test-signing-key-64-bytes-long-for-hmac-sha256-tests-only!!!"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("Authentication:Authority", Issuer);
        builder.UseSetting("Authentication:Audience", Audience);
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Catalog"] = connectionString
                    ?? "Host=127.0.0.1;Port=1;Database=unused;Username=unused;Timeout=1",
                ["Authentication:Authority"] = Issuer,
                ["Authentication:Audience"] = Audience
            }));
        builder.ConfigureTestServices(services =>
        {
            if (connectionString is not null)
            {
                services.RemoveAll<CatalogDbContext>();
                services.RemoveAll<DbContextOptions<CatalogDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<CatalogDbContext>>();
                services.AddDbContext<CatalogDbContext>(options => options.UseNpgsql(connectionString));
            }

            if (repository is not null)
            {
                services.RemoveAll<IProductRepository>();
                services.AddSingleton(repository);
            }

            if (unitOfWork is not null)
            {
                services.RemoveAll<IUnitOfWork>();
                services.AddSingleton(unitOfWork);
            }

            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.MetadataAddress = null!;
                options.ConfigurationManager = null!;
                options.Configuration = new OpenIdConnectConfiguration { Issuer = Issuer };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = Issuer,
                    ValidateAudience = true,
                    ValidAudience = Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = Key
                };
            });
        });
    }

    public static string Token(string scope = "catalog.read catalog.write", bool expired = false,
        string? audience = null, bool wrongSignature = false)
    {
        var key = wrongSignature
            ? new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('x', 64)))
            : Key;
        var token = new JwtSecurityToken(Issuer, audience ?? Audience,
            [new Claim("sub", Guid.NewGuid().ToString()), new Claim("scope", scope)],
            notBefore: DateTime.UtcNow.AddMinutes(-10),
            expires: expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
