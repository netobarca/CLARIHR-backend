using Asp.Versioning;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using CLARIHR.Api.Common;
using CLARIHR.Api.Common.Authorization;
using CLARIHR.Api.Common.Conventions;
using CLARIHR.Api.Configuration;
using CLARIHR.Api.Middleware;
using CLARIHR.Application;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.Auth;
using CLARIHR.Infrastructure;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.Logging;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Microsoft.Extensions.Options;
using Serilog;
// Disambiguate from Asp.Versioning.ProblemDetailsDefaults introduced by `using Asp.Versioning;`.
using ProblemDetailsDefaults = CLARIHR.Api.Common.ProblemDetailsDefaults;

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
        options.ModelBinderProviders.Insert(0, new CLARIHR.Api.Common.Binders.PositionDescriptionCatalogTypeModelBinderProvider());
        options.ModelMetadataDetailsProviders.Add(new PublicContractBindingMetadataProvider());
        options.Conventions.Add(new PublicContractRouteConvention());
        options.Conventions.Add(new ProducesStandardErrorsConvention());
        options.Conventions.Add(new AuthorizationPolicyConvention());
        options.Filters.AddService<PersonnelFilePhotoUrlResultFilter>();
        options.Filters.AddService<ConditionalRequestResultFilter>();
        options.Filters.AddService<ValidateJsonPatchDocumentFilter>();
    })
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.TypeInfoResolver = JsonTypeInfoResolver.Combine(
            new PublicContractJsonTypeInfoResolver(),
            options.JsonSerializerOptions.TypeInfoResolver);
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddApiVersioning(options =>
    {
        options.DefaultApiVersion = new ApiVersion(1, 0);
        options.AssumeDefaultVersionWhenUnspecified = true;
        options.ReportApiVersions = true;
        options.ApiVersionReader = new UrlSegmentApiVersionReader();
    })
    .AddMvc()
    .AddApiExplorer(options =>
    {
        // Format the group name as "v1", "v2", ... so it matches the SwaggerDoc names.
        options.GroupNameFormat = "'v'VVV";
        // Replace the {version:apiVersion} route token with the resolved version in generated URLs.
        options.SubstituteApiVersionInUrl = true;
    });
builder.Services.AddScoped<PersonnelFilePhotoUrlResultFilter>();
builder.Services.AddScoped<ConditionalRequestResultFilter>();
builder.Services.AddScoped<ValidateJsonPatchDocumentFilter>();
builder.Services.AddScoped<ReportExportDeliveryService>();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    options.CustomSchemaIds(type =>
        (type.FullName ?? type.Name).Replace('+', '.'));
    options.SchemaFilter<PublicContractSchemaFilter>();
    options.OperationFilter<PublicContractOperationFilter>();
    options.OperationFilter<AuthorizationPolicyOperationFilter>();
    options.OperationFilter<CatalogTypeSlugOperationFilter>();

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
    options.OnRejected = async (context, cancellationToken) =>
    {
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            context.HttpContext.Response.Headers.RetryAfter = Math.Ceiling(retryAfter.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        var problemDetails = ProblemDetailsFactory.CreateProblemDetails(context.HttpContext, ErrorCatalog.TooManyRequests);
        await context.HttpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
    };
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
    options.AddPolicy("auth-register", httpContext =>
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
    options.AddPolicy("auth-login", httpContext =>
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
    options.AddPolicy("auth-invite-accept", httpContext =>
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
    options.AddPolicy(PersonnelFileRateLimitPolicies.Create, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:PersonnelFiles:Create:PermitLimit", 20));
    options.AddPolicy(PersonnelFileRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:PersonnelFiles:Search:PermitLimit", 120));
    options.AddPolicy(PersonnelFileRateLimitPolicies.Lifecycle, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:PersonnelFiles:Lifecycle:PermitLimit", 30));
    // Tight limiter for the unbounded-cost Personnel Files reads (row exports + full-tenant
    // analytics aggregation) — same abuse class as PositionSlots export; mirrors its 10/min default.
    options.AddPolicy(PersonnelFileRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:PersonnelFiles:Export:PermitLimit", 10));

    // Position Slots: tight limiter for the unbounded-cost generators (export /
    // diagram-export / full-tenant graph) — same abuse class as personnel-files-search,
    // sensitive HR data — plus a generous limiter for the paged search/list.
    options.AddPolicy(PositionSlotRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:PositionSlots:Export:PermitLimit", 10));
    options.AddPolicy(PositionSlotRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:PositionSlots:Search:PermitLimit", 120));
});
builder.Services.AddAuthorization(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim("client_type", AuthClientType.Core.ToClaimValue())
        .Build();

    options.DefaultPolicy = policy;
    options.FallbackPolicy = policy;

    options.AddPolicy(JobProfilePolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            JobProfilePermissionCodes.Read,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.CatalogAdmin,
            JobProfilePermissionCodes.ManageAdministration)));

    options.AddPolicy(JobProfilePolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            JobProfilePermissionCodes.Admin,
            JobProfilePermissionCodes.ManageAdministration)));

    // Catalog writes gate on the CatalogAdmin scope (mirrors EnsureCanManageCatalogsAsync),
    // NOT the generic JobProfilePolicies.Manage — keeping the declarative policy a superset
    // of the handler gate so a JobCatalogs.Admin-only admin is not falsely 403'd.
    options.AddPolicy(JobProfilePolicies.ManageCatalogs, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            JobProfilePermissionCodes.CatalogAdmin,
            JobProfilePermissionCodes.ManageAdministration)));

    options.AddPolicy(PositionDescriptionCatalogPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PositionDescriptionCatalogPermissionCodes.Read,
            PositionDescriptionCatalogPermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.ManageAdministration)));

    options.AddPolicy(PositionDescriptionCatalogPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PositionDescriptionCatalogPermissionCodes.Admin,
            PositionDescriptionCatalogPermissionCodes.ManageAdministration)));

    options.AddPolicy(PositionSlotPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PositionSlotPermissionCodes.Read,
            PositionSlotPermissionCodes.Admin,
            PositionSlotPermissionCodes.ManageAdministration)));

    options.AddPolicy(PositionSlotPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PositionSlotPermissionCodes.Admin,
            PositionSlotPermissionCodes.ManageAdministration)));

    // Personnel Files — declarative policies kept a superset of the precise
    // PersonnelFileAuthorizationService handler gate (EnsureCanReadAsync /
    // EnsureCanManageAsync) so a legitimate reader/manager is never falsely 403'd.
    options.AddPolicy(PersonnelFilePolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.Read,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    options.AddPolicy(PersonnelFilePolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Cost Centers — declarative policies kept a superset of the precise
    // CostCenterAuthorizationService handler gate (EnsureCanReadAsync /
    // EnsureCanManageAsync) so a legitimate reader/manager is never falsely 403'd.
    options.AddPolicy(CostCenterPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            CostCenterPermissionCodes.Read,
            CostCenterPermissionCodes.Admin,
            CostCenterPermissionCodes.ManageAdministration)));

    options.AddPolicy(CostCenterPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            CostCenterPermissionCodes.Admin,
            CostCenterPermissionCodes.ManageAdministration)));
});

// Emit the standard ProblemDetails contract (code/traceId/localized title) on policy-layer
// denials instead of the ASP.NET default empty body. Overrides the framework default
// (registered via TryAddSingleton), so a later AddSingleton always wins.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ProblemDetailsAuthorizationMiddlewareResultHandler>();

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
// §S2: endpoint-routing short-circuits (404 unmatched path, 405 unmapped verb)
// return with an EMPTY body — no framework or domain ProblemDetails factory runs,
// so an SDK client parsing `code` gets nothing. Re-emit the standard
// {status,code,traceId,title} envelope for those status codes, but only when the
// body really is empty and nothing has started writing (never overwrite a
// response a handler/filter already produced). Thrown exceptions still flow
// through UnhandledExceptionMiddleware above; this only covers the
// non-exception, empty-body status codes that bypass every other error path.
app.UseStatusCodePages(async statusCodeContext =>
{
    var context = statusCodeContext.HttpContext;
    var response = context.Response;

    if (response.HasStarted ||
        response.ContentLength is > 0 ||
        !string.IsNullOrEmpty(response.ContentType))
    {
        return;
    }

    Error? error = response.StatusCode switch
    {
        StatusCodes.Status404NotFound => ErrorCatalog.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCatalog.MethodNotAllowed,
        _ => null
    };

    if (error is null)
    {
        return;
    }

    var problemDetails = ProblemDetailsFactory.CreateProblemDetails(context, error);
    await response.WriteAsJsonAsync(
        problemDetails,
        problemDetails.GetType(),
        options: null,
        contentType: "application/problem+json",
        context.RequestAborted);
});
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

static RateLimitPartition<string> CreateUserTenantPartitionedLimiter(HttpContext httpContext, string configurationKey, int defaultPermitLimit)
{
    var permitLimit = httpContext.RequestServices.GetRequiredService<IConfiguration>().GetValue(configurationKey, defaultPermitLimit);
    var tenantId = httpContext.User.FindFirst("tid")?.Value;
    var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
        ?? httpContext.User.FindFirst("sub")?.Value;
    var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var partitionKey = !string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(userId)
        ? $"{tenantId}:{userId}"
        : remoteIp;

    return RateLimitPartition.GetFixedWindowLimiter(
        partitionKey,
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = permitLimit,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            AutoReplenishment = true
        });
}

public partial class Program;
