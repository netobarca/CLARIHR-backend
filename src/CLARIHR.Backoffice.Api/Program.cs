using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CLARIHR.Application;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Features.PlatformOperators;
using CLARIHR.Backoffice.Api.Common;
using CLARIHR.Backoffice.Api.Configuration;
using CLARIHR.Backoffice.Api.Middleware;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.Platform;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Serilog;

namespace CLARIHR.Backoffice.Api;

public partial class Program
{
    public static async Task<int> Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        Log.Logger = LoggingConfigurationExtensions.CreateLoggingConfiguration(builder.Environment).CreateLogger();

        builder.Logging.ClearProviders();
        builder.Logging.AddSerilog(Log.Logger);

        builder.Services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = ProblemDetailsDefaults.Apply;
        });
        builder.Services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = ModelStateProblemDetailsFactory.Create;
        });
        builder.Services
            .AddControllers(options =>
            {
                options.ModelMetadataDetailsProviders.Add(new PublicContractBindingMetadataProvider());
                options.Conventions.Add(new PublicContractRouteConvention());
                options.Conventions.Add(new CLARIHR.Api.Common.Conventions.ProducesStandardErrorsConvention());
                options.Filters.AddService<ValidateJsonPatchDocumentFilter>();
            })
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
                    new PublicContractJsonTypeInfoResolver(),
                    options.JsonSerializerOptions.TypeInfoResolver);
            });
        builder.Services.AddScoped<ValidateJsonPatchDocumentFilter>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.CustomSchemaIds(type =>
                (type.FullName ?? type.Name).Replace('+', '.'));
            options.SchemaFilter<PublicContractSchemaFilter>();
            options.OperationFilter<PublicContractOperationFilter>();

            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "CLARIHR Backoffice API",
                Version = "v1",
                Description = "Platform backoffice API documentation for CLARIHR."
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Paste only the raw accessToken JWT value. Do not include the word Bearer, quotes, commas, or any other JSON characters."
            });

            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                [
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    }
                ] = Array.Empty<string>()
            });
        });
        builder.Services.AddApplication();
        builder.Services.AddInfrastructure(builder.Configuration);
        builder.Services.AddAuthorization(options =>
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim("client_type", AuthClientType.Platform.ToClaimValue())
                .Build();

            options.DefaultPolicy = policy;
            options.FallbackPolicy = policy;

            // Explicit registration for the named policy that the Backoffice catalog
            // controllers reference via [Authorize(Policy = "PlatformOperator")].
            // Equivalent to the default policy; fixes a latent "policy not found" gap.
            options.AddPolicy("PlatformOperator", policy);
        });

        ConfigureAuthentication(builder.Services);

        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
        });

        var app = builder.Build();

        LogJwtConfiguration(app.Logger, app.Configuration);

        await app.Services.InitializeInfrastructureAsync(app.Logger, app.Environment.IsDevelopment());

        var commandExitCode = await TryExecuteCommandAsync(args, app.Services, app.Logger);
        if (commandExitCode.HasValue)
        {
            return commandExitCode.Value;
        }

        if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "CLARIHR Backoffice API v1");
                options.RoutePrefix = "swagger";
                options.DisplayRequestDuration();
            });
        }

        app.UseForwardedHeaders();
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<RequestLoggingMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();
        app.UseMiddleware<UnhandledExceptionMiddleware>();
        if (!app.Environment.IsDevelopment())
        {
            app.UseHttpsRedirection();
        }

        app.UseAuthentication();
        app.UseMiddleware<RequestLanguageMiddleware>();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
        return 0;
    }

    private static async Task<int?> TryExecuteCommandAsync(
        IReadOnlyList<string> args,
        IServiceProvider services,
        Microsoft.Extensions.Logging.ILogger logger)
    {
        if (args.Count == 0 || !args[0].Equals("bootstrap-platform-operator", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (args.Count < 2 || string.IsNullOrWhiteSpace(args[1]))
        {
            logger.LogError("Usage: bootstrap-platform-operator <email> [Admin|ReadOnly]");
            return 1;
        }

        var role = PlatformOperatorRole.Admin;
        if (args.Count >= 3 &&
            !Enum.TryParse(args[2], ignoreCase: true, out role))
        {
            logger.LogError("Invalid platform operator role '{Role}'. Supported values: Admin, ReadOnly.", args[2]);
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var commandDispatcher = scope.ServiceProvider.GetRequiredService<ICommandDispatcher>();
        var result = await commandDispatcher.SendAsync(
            new BootstrapPlatformOperatorCommand(args[1], role),
            CancellationToken.None);

        if (result.IsFailure)
        {
            logger.LogError(
                "Failed to bootstrap platform operator for {Email}. Error {Code}: {Message}",
                args[1],
                result.Error.Code,
                result.Error.Message);
            return 1;
        }

        logger.LogInformation(
            "Bootstrapped platform operator {PlatformOperatorPublicId} for {Email} with role {Role}.",
            result.Value.PlatformOperatorId,
            result.Value.Email,
            result.Value.Role);

        return 0;
    }

    private static void ConfigureAuthentication(IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtTokenOptions>>((options, jwtTokenOptionsAccessor) =>
            {
                var jwtOptions = jwtTokenOptionsAccessor.Value;
                if (!jwtOptions.IsConfigured)
                {
                    throw new InvalidOperationException(
                        $"JWT authentication is not fully configured. Section '{JwtTokenOptions.SectionName}' must provide issuer, audience, platform audience, and signing key.");
                }

                options.RequireHttpsMetadata = true;
                options.SaveToken = false;
                options.Events = new JwtBearerEvents
                {
                    OnAuthenticationFailed = context =>
                    {
                        var loggerFactory = context.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>();
                        var logger = loggerFactory.CreateLogger("JwtAuthentication");
                        var configuredOptions = context.HttpContext.RequestServices
                            .GetRequiredService<IOptions<JwtTokenOptions>>()
                            .Value;
                        var rawAuthorization = context.Request.Headers.Authorization.ToString();
                        var rawToken = rawAuthorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                            ? rawAuthorization["Bearer ".Length..].Trim()
                            : rawAuthorization;
                        var tokenSummary = JwtConfigurationDiagnostics.TryReadSummary(rawToken);

                        logger.LogWarning(
                            context.Exception,
                            "JWT authentication failed for {Method} {Path}. Token issuer {TokenIssuer}, token audience {TokenAudience}, token client type {TokenClientType}, token valid from {TokenValidFromUtc}, token expires at {TokenExpiresAtUtc}, configured issuer {ConfiguredIssuer}, configured platform audience {ConfiguredAudience}, key fingerprint {KeyFingerprint}.",
                            context.Request.Method,
                            context.Request.Path,
                            tokenSummary?.Issuer,
                            tokenSummary?.Audience,
                            tokenSummary?.ClientType,
                            tokenSummary?.ValidFromUtc,
                            tokenSummary?.ExpiresAtUtc,
                            configuredOptions.Issuer,
                            configuredOptions.PlatformAudience,
                            JwtConfigurationDiagnostics.ComputeSigningKeyFingerprint(configuredOptions.SigningKey));

                        return Task.CompletedTask;
                    }
                };
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidAudience = jwtOptions.PlatformAudience,
                    IssuerSigningKey = JwtConfigurationDiagnostics.CreateSigningKey(jwtOptions.SigningKey!),
                    ClockSkew = TimeSpan.Zero
                };
            });
    }

    private static void LogJwtConfiguration(Microsoft.Extensions.Logging.ILogger logger, IConfiguration configuration)
    {
        var jwtOptions = configuration.GetSection(JwtTokenOptions.SectionName).Get<JwtTokenOptions>();
        var keyFingerprint = JwtConfigurationDiagnostics.ComputeSigningKeyFingerprint(jwtOptions?.SigningKey);

        if (jwtOptions is not { IsConfigured: true })
        {
            logger.LogWarning(
                "JWT authentication is not fully configured for the backoffice API. Issuer {Issuer}, audience {Audience}, platform audience {PlatformAudience}, key fingerprint {KeyFingerprint}.",
                jwtOptions?.Issuer,
                jwtOptions?.Audience,
                jwtOptions?.PlatformAudience,
                keyFingerprint);
            return;
        }

        logger.LogInformation(
            "Configured JWT authentication for the backoffice API. Issuer {Issuer}, core audience {Audience}, valid platform audience {PlatformAudience}, key fingerprint {KeyFingerprint}.",
            jwtOptions.Issuer,
            jwtOptions.Audience,
            jwtOptions.PlatformAudience,
            keyFingerprint);
    }
}
