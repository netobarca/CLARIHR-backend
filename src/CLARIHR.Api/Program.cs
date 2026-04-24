using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CLARIHR.Api.Common;
using CLARIHR.Api.Configuration;
using CLARIHR.Api.Middleware;
using CLARIHR.Application;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Logging;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog logging
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
        options.Filters.AddService<PersonnelFilePhotoUrlResultFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            new PublicContractJsonTypeInfoResolver(),
            options.JsonSerializerOptions.TypeInfoResolver);
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddScoped<PersonnelFilePhotoUrlResultFilter>();
builder.Services.AddScoped<ReportExportDeliveryService>();
builder.Services.AddSwaggerGen(options =>
{
    options.CustomSchemaIds(type =>
        (type.FullName ?? type.Name).Replace('+', '.'));
    options.SchemaFilter<PublicContractSchemaFilter>();
    options.OperationFilter<PublicContractOperationFilter>();

    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "CLARIHR API",
        Version = "v1",
        Description = "Development-time API documentation for CLARIHR."
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
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth-password-reset-request", httpContext =>
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            remoteIp,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
});
builder.Services.AddAuthorization(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("client_type", AuthClientType.Core.ToClaimValue())
        .Build();

    options.DefaultPolicy = policy;
    options.FallbackPolicy = policy;
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

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "CLARIHR API v1");
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
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();

app.Run();

return;

static void ConfigureAuthentication(IServiceCollection services)
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
                        "JWT authentication failed for {Method} {Path}. Token issuer {TokenIssuer}, token audience {TokenAudience}, token client type {TokenClientType}, token valid from {TokenValidFromUtc}, token expires at {TokenExpiresAtUtc}, configured issuer {ConfiguredIssuer}, configured audience {ConfiguredAudience}, key fingerprint {KeyFingerprint}.",
                        context.Request.Method,
                        context.Request.Path,
                        tokenSummary?.Issuer,
                        tokenSummary?.Audience,
                        tokenSummary?.ClientType,
                        tokenSummary?.ValidFromUtc,
                        tokenSummary?.ExpiresAtUtc,
                        configuredOptions.Issuer,
                        configuredOptions.Audience,
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
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = JwtConfigurationDiagnostics.CreateSigningKey(jwtOptions.SigningKey!),
                ClockSkew = TimeSpan.Zero
            };
        });
}

static void LogJwtConfiguration(Microsoft.Extensions.Logging.ILogger logger, IConfiguration configuration)
{
    var jwtOptions = configuration.GetSection(JwtTokenOptions.SectionName).Get<JwtTokenOptions>();
    var keyFingerprint = JwtConfigurationDiagnostics.ComputeSigningKeyFingerprint(jwtOptions?.SigningKey);

    if (jwtOptions is not { IsConfigured: true })
    {
        logger.LogWarning(
            "JWT authentication is not fully configured for the core API. Issuer {Issuer}, audience {Audience}, platform audience {PlatformAudience}, key fingerprint {KeyFingerprint}.",
            jwtOptions?.Issuer,
            jwtOptions?.Audience,
            jwtOptions?.PlatformAudience,
            keyFingerprint);
        return;
    }

    logger.LogInformation(
        "Configured JWT authentication for the core API. Issuer {Issuer}, valid audience {Audience}, platform audience {PlatformAudience}, key fingerprint {KeyFingerprint}.",
        jwtOptions.Issuer,
        jwtOptions.Audience,
        jwtOptions.PlatformAudience,
        keyFingerprint);
}

public partial class Program;
