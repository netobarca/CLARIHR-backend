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
using CLARIHR.Application.Features.CompanyUsers.Common;
using CLARIHR.Application.Features.CompetencyFramework.Common;
using CLARIHR.Application.Features.CostCenters.Common;
using CLARIHR.Application.Features.EmployeeRelations.Common;
using CLARIHR.Application.Features.Leave.Common;
using CLARIHR.Application.Features.AccountCompanies.Common;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.Auth.Common;
using CLARIHR.Application.Features.Files.Common;
using CLARIHR.Application.Features.JobProfiles.Common;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Locations.Common;
using CLARIHR.Application.Features.OrgStructureCatalogs.Common;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Application.Features.PersonnelFiles.Common;
using CLARIHR.Application.Features.PersonnelFiles.Overtime.Common;
using CLARIHR.Application.Features.PositionDescriptionCatalogs.Common;
using CLARIHR.Application.Features.Reports.Common;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Application.Features.SalaryTabulator.Common;
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
        options.Filters.AddService<AllowedActionsResultFilter>();
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
builder.Services.AddScoped<AllowedActionsResultFilter>();
builder.Services.AddScoped<ConditionalRequestResultFilter>();
builder.Services.AddScoped<ValidateJsonPatchDocumentFilter>();
builder.Services.AddScoped<ReportExportDeliveryService>();
builder.Services.AddSingleton<OrgUnitDiagramWriter>();
builder.Services.AddSingleton<PositionSlotDiagramWriter>();
builder.Services.AddSwaggerGen(options =>
{
    options.EnableAnnotations();
    // Reflect C# nullable-reference-type annotations into the schema: a non-nullable reference type (e.g.
    // `string AssignmentTypeCode`) is emitted as non-nullable AND added to the schema's `required` set, while
    // `string?` stays optional/nullable. This aligns the published contract with the runtime — ASP.NET already
    // rejects null for non-nullable reference types (implicit [Required]) and FluentValidation enforces the rest —
    // fixing the systemic drift where required request fields advertised themselves as `nullable`/optional.
    options.SupportNonNullableReferenceTypes();
    // Schema ids: rely on Swashbuckle's default scheme (short type name; generics rendered as
    // "<Outer>Of<Inner>", e.g. PagedResponseOfAccountCompanySummaryResponse). This keeps the
    // published schema names clean and stable for client codegen and matches the canonical
    // contract in docs/technical/api/openapi.yaml. A fully-qualified id (type.FullName) leaks
    // internal namespaces (CLARIHR.Api.Contracts.*) into every schema name and silently drifts
    // the live contract away from the documented one — do not reintroduce it.
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
    options.AddPolicy(AuthRateLimitPolicies.PasswordResetRequest, httpContext =>
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
    options.AddPolicy(AuthRateLimitPolicies.Register, httpContext =>
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
    options.AddPolicy(AuthRateLimitPolicies.Login, httpContext =>
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
    options.AddPolicy(AuthRateLimitPolicies.InviteAccept, httpContext =>
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
    options.AddPolicy(AuthRateLimitPolicies.PasswordResetSubmit, httpContext =>
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
    options.AddPolicy(AuthRateLimitPolicies.Refresh, httpContext =>
    {
        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(
            remoteIp,
            // Deliberately generous: refresh is a frequent legitimate operation and the limiter is
            // IP-partitioned, so a tight cap would clip users behind shared NAT. The real anti-abuse is the
            // 512-bit token + rotation/reuse-detection; this is a per-IP DoS backstop (AU-2).
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true
            });
    });
    options.AddPolicy(AuthRateLimitPolicies.EmailVerificationSubmit, httpContext =>
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
    options.AddPolicy(AuthRateLimitPolicies.EmailVerificationResend, httpContext =>
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

    // Report Export Jobs: tight limiter for the artifact download (streams a stored blob), plus a
    // generous limiter for the paged export-jobs search/list. Same read abuse class as the other
    // export surfaces; mirrors their 10/min + 120/min defaults.
    options.AddPolicy(ReportExportJobRateLimitPolicies.Download, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:ReportExportJobs:Download:PermitLimit", 10));
    options.AddPolicy(ReportExportJobRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:ReportExportJobs:Search:PermitLimit", 120));

    // Competency Framework: tight limiter for the unbounded-cost competency-matrix export,
    // plus a generous limiter for the paged occupational-pyramid-levels / competency-conducts search.
    options.AddPolicy(CompetencyFrameworkRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:CompetencyFramework:Export:PermitLimit", 10));
    options.AddPolicy(CompetencyFrameworkRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:CompetencyFramework:Search:PermitLimit", 120));

    // Company Users: tight limiter for the invitation e-mail senders (invite + reset-invitation) —
    // same abuse class as auth-invite-accept (e-mail bomb / cross-tenant enumeration) — plus a
    // generous limiter for the paged company-users search/list.
    options.AddPolicy(CompanyUserRateLimitPolicies.Invite, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:CompanyUsers:Invite:PermitLimit", 10));
    options.AddPolicy(CompanyUserRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:CompanyUsers:Search:PermitLimit", 120));

    // Legal Representatives: tight limiter for the unbounded-cost report export (synchronous,
    // scan + LIKE '%x%'), plus a generous limiter for the paged free-text search/list — same
    // abuse class as competency-framework-export/search; mirrors its 10/min + 120/min defaults.
    options.AddPolicy(LegalRepresentativeRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:LegalRepresentatives:Export:PermitLimit", 10));
    options.AddPolicy(LegalRepresentativeRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:LegalRepresentatives:Search:PermitLimit", 120));

    // Locations: tighter limiter for the unpaginated full-hierarchy /tree (graph read that returns the
    // whole company hierarchy in one shot), plus a generous limiter for the paged free-text
    // location-group search/list. Same read abuse class as competency-framework/legal-representatives.
    options.AddPolicy(LocationRateLimitPolicies.Tree, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:Locations:Tree:PermitLimit", 60));
    options.AddPolicy(LocationRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:Locations:Search:PermitLimit", 120));

    // Cost Centers: tight limiter for the unbounded-cost report export (synchronous, scan +
    // LIKE '%x%'), plus a generous limiter for the paged free-text search/list — same abuse class
    // as legal-representatives-export/search; mirrors its 10/min + 120/min defaults.
    options.AddPolicy(CostCenterRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:CostCenters:Export:PermitLimit", 10));
    options.AddPolicy(CostCenterRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:CostCenters:Search:PermitLimit", 120));

    // Org Units: same read abuse class as Locations — a generous limiter for the paged free-text
    // search/list, a tighter one for the unpaginated full-hierarchy projections (/tree + /graph load
    // the whole company hierarchy in one shot), and the tightest for the downloadable export artifacts
    // (tabular /export + /diagram-export GraphML/DOT/JSON). Mirrors the 120/60/10 family defaults.
    options.AddPolicy(OrgUnitRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:OrgUnits:Search:PermitLimit", 120));
    options.AddPolicy(OrgUnitRateLimitPolicies.Tree, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:OrgUnits:Tree:PermitLimit", 60));
    options.AddPolicy(OrgUnitRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:OrgUnits:Export:PermitLimit", 10));

    // Organization Structure Catalogs: generous per-user+tenant limiter for the paged free-text
    // unit-types / functional-areas search (same paged-search abuse class as OrgUnits, mirrors its 120).
    options.AddPolicy(OrgStructureCatalogRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:OrgStructureCatalogs:Search:PermitLimit", 120));

    // Files: per-user+tenant limiters for the direct-upload surface — upload-session reserves a row
    // and mints a write SAS, read-url mints a read SAS, and complete/delete mutate the stored object.
    // Same abuse class as personnel-files-create; mirrors its 20/120/30 defaults.
    options.AddPolicy(FileRateLimitPolicies.Upload, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:Files:Upload:PermitLimit", 20));
    options.AddPolicy(FileRateLimitPolicies.Read, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:Files:Read:PermitLimit", 120));
    options.AddPolicy(FileRateLimitPolicies.Lifecycle, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:Files:Lifecycle:PermitLimit", 30));

    // Salary Tabulator (ST-A): the export surfaces salary data (PII) — a tight limiter on the
    // unbounded-cost synchronous export blocks scraping of the most sensitive data in the system, plus a
    // generous limiter for the paged free-text line/change-request search. Same abuse class as
    // cost-centers-export/search; mirrors its 10/min + 120/min defaults.
    options.AddPolicy(SalaryTabulatorRateLimitPolicies.Export, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:SalaryTabulator:Export:PermitLimit", 10));
    options.AddPolicy(SalaryTabulatorRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:SalaryTabulator:Search:PermitLimit", 120));

    // Audit Logs: generous per-user+tenant limiter for the paged free-text audit-log search/list
    // (scan + LIKE '%x%' over the append-only audit trail). Same paged-search abuse class as the
    // other read surfaces; mirrors the 120/min family default.
    options.AddPolicy(AuditRateLimitPolicies.Search, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:Audit:Search:PermitLimit", 120));

    // AC-8: POST account/companies/{id}/switch mints a fresh access+refresh token pair (functional
    // equivalent of login, which is limited at 5/min), so it gets an auth-style per-user+tenant limiter.
    options.AddPolicy(AccountCompanyRateLimitPolicies.Switch, httpContext => CreateUserTenantPartitionedLimiter(httpContext, "RateLimiting:AccountCompanies:Switch:PermitLimit", 10));
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

    // ViewCompensation is an authn-only SUPERSET: the precise gate (the ViewCompensation permission, or
    // the employee reading their own file) lives in the compensation read handlers, which also support
    // self-service that a declarative permission policy cannot express (D-16).
    options.AddPolicy(PersonnelFilePolicies.ViewCompensation, policyBuilder => policyBuilder
        .Combine(policy));

    // ViewInsurance is an authn-only SUPERSET: the precise gate (the ViewInsurance permission, or Admin)
    // lives in the insurance read handlers. No self-service in this phase.
    options.AddPolicy(PersonnelFilePolicies.ViewInsurance, policyBuilder => policyBuilder
        .Combine(policy));

    // ManageSubstitutions (D-09) — write policy for authorization substitutions, kept a superset of the
    // precise EnsureCanManageSubstitutionsAsync handler gate (the dedicated permission, or Admin / IAM
    // super-admin) so a legitimate manager is never falsely 403'd; reads stay on the Read policy.
    options.AddPolicy(PersonnelFilePolicies.ManageSubstitutions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageSubstitutions,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Medical claims (D-08/D-09) — authn-only SUPERSETS for BOTH read and write. The precise gate (the
    // ViewMedicalClaims/ManageMedicalClaims permission or Admin, OR the employee acting on their own claims)
    // lives in the medical-claim handlers. Kept authn-only — NOT a RequireAssertion — so a self-service
    // employee reading or creating their own claim is never blocked at the API layer (a RequireAssertion
    // would 403 them before the handler's self-service branch runs).
    options.AddPolicy(PersonnelFilePolicies.ViewMedicalClaims, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageMedicalClaims, policyBuilder => policyBuilder
        .Combine(policy));

    // Position competencies ("Competencias del puesto", D-08/D-09). Read is an authn-only SUPERSET so the
    // self-service branch (the employee reading their own competencies) is never blocked at the API layer;
    // the precise gate (ViewCompetencies / Admin, or self) lives in the competency read handlers. Writes are
    // HR-only (CLARIHR is the source of truth — D-01), so ManageCompetencies uses a RequireAssertion like
    // ManageSubstitutions, kept a superset of the precise EnsureCanManageCompetenciesAsync handler gate.
    options.AddPolicy(PersonnelFilePolicies.ViewCompetencies, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageCompetencies, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageCompetencies,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Off-payroll transactions ("transacciones fuera de nómina", D-06) — authn-only SUPERSETS for BOTH read and
    // write. The precise gate (the ViewOffPayrollTransactions / ManageOffPayrollTransactions permission, or Admin)
    // lives in the off-payroll handlers, which are HR-only (no self-service). Kept authn-only — NOT a
    // RequireAssertion — so a legitimate manager whose grant is not yet reflected in the token is never falsely
    // 403'd before the handler's precise gate runs (matches the medical-claims policy treatment).
    options.AddPolicy(PersonnelFilePolicies.ViewOffPayrollTransactions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageOffPayrollTransactions, policyBuilder => policyBuilder
        .Combine(policy));

    // Economic-aid requests (D-02/D-03/D-10) — authn-only SUPERSETS for BOTH read and write, like medical
    // claims: the employee creates/reads/cancels their OWN request (self-service) and HR validates. The precise
    // gate (View/ManageEconomicAidRequests permission or Admin, OR the employee acting on their own request)
    // lives in the economic-aid handlers; kept authn-only — NOT a RequireAssertion — so a self-service employee
    // is never 403'd at the API layer before the handler's self-service / manage-only gate runs.
    options.AddPolicy(PersonnelFilePolicies.ViewEconomicAidRequests, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageEconomicAidRequests, policyBuilder => policyBuilder
        .Combine(policy));

    // Certificate requests ("constancias", D-02/D-04/D-20) — authn-only SUPERSETS for BOTH read and write, like
    // economic aid: the employee creates/reads/cancels their OWN request (self-service) and HR processes/issues.
    // The precise gate (View/ManageCertificateRequests permission or Admin, OR the employee acting on their own
    // request; plus ViewCompensation at issuance of a salary-printing certificate, D-20) lives in the certificate
    // handlers; kept authn-only so a self-service employee is never 403'd at the API layer.
    options.AddPolicy(PersonnelFilePolicies.ViewCertificateRequests, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageCertificateRequests, policyBuilder => policyBuilder
        .Combine(policy));

    // Exit-interview form builder (D-01) — HR-only: designing/publishing/associating exit-interview forms is
    // not self-service. RequireAssertion like ManageCompetencies, kept a superset of the precise
    // EnsureCanManageExitInterviewFormsAsync handler gate.
    options.AddPolicy(PersonnelFilePolicies.ManageExitInterviewForms, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageExitInterviewForms,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Exit-interview submissions (D-04/D-14) — authn-only SUPERSETS for read and write. The precise gate
    // (ViewExitInterviews / ManageExitInterviews permission or Admin — RRHH; the employee on their own file
    // for fill) lives in the submission handlers, so a self-service employee is never blocked at the API layer.
    options.AddPolicy(PersonnelFilePolicies.ViewExitInterviews, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageExitInterviews, policyBuilder => policyBuilder
        .Combine(policy));

    // Retirement requests ("retiro definitivo", D-12/D-13). No self-service in Fase 1 (D-03), so the write
    // policies are RequireAssertion supersets of the precise handler gates. ViewRetirements stays authn-only
    // (the bandeja/tray gate per-handler, like the reporting controllers). AuthorizeRetirement and
    // RevertRetirement deliberately EXCLUDE PersonnelFiles.Admin (separation of duties — mirrors the
    // AuthorizeRehire handler gate); the IAM super-admin remains the universal fallback.
    options.AddPolicy(PersonnelFilePolicies.ViewRetirements, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageRetirements, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageRetirements,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeRetirement, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeRetirement,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.RevertRetirement, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.RevertRetirement,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Settlements ("liquidación de personal", settlement module D-20). HR-only, no self-service in
    // Fase 1, so the write policy is a RequireAssertion superset of the precise handler gate;
    // ViewSettlements stays authn-only (bandeja/detail gate per-handler, like the retirement module).
    options.AddPolicy(PersonnelFilePolicies.ViewSettlements, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageSettlements, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageSettlements,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Leave module (incapacidades/lactancia/vacaciones — leave module D-17/D-18). All four stay
    // authn-only supersets — NOT RequireAssertion — because both families have self-service branches
    // (an employee registers their own incapacity EN_REVISION and creates/cancels their own vacation
    // request); the precise View*/Manage*/isSelf/anti-self checks live in the handler gates.
    options.AddPolicy(PersonnelFilePolicies.ViewIncapacities, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageIncapacities, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ViewVacations, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageVacations, policyBuilder => policyBuilder
        .Combine(policy));

    // Compensatory time (REQ-002 D-01/D-13). Read is an authn-only SUPERSET so the self-service branch
    // (the employee reading their own fund/statement, PR-3/PR-4) is never blocked at the API layer; the
    // precise gate (ViewCompensatoryTime / Admin, or self) lives in the compensatory-time read handlers.
    // Writes are HR-only in Fase 1 (D-01), so ManageCompensatoryTime uses a RequireAssertion like
    // ManageSettlements, kept a superset of the precise EnsureCanManageCompensatoryTimeAsync handler gate.
    options.AddPolicy(PersonnelFilePolicies.ViewCompensatoryTime, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageCompensatoryTime, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageCompensatoryTime,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Otras transacciones de personal — recognitions + disciplinary actions (REQ-003 D-05). View* and
    // Manage* stay authn-only supersets because both families have a self-service read branch (an
    // employee reading their OWN applied records, D-13) and Manage is not blocked at the API layer; the
    // precise View*/Manage*/isSelf/anti-self checks live in the handler gates (PR-3/PR-4). Authorize*
    // deliberately EXCLUDE PersonnelFiles.Admin (separation of duties + double anti-self — mirrors
    // AuthorizeRetirement); the IAM super-admin remains the universal fallback. ViewTimeAvailability is a
    // RequireAssertion (corporate read, no self-service) like the compensatory-time read superset would
    // be if it had no self branch.
    options.AddPolicy(PersonnelFilePolicies.ViewRecognitions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageRecognitions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeRecognitions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeRecognitions,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.ViewDisciplinaryActions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageDisciplinaryActions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeDisciplinaryActions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeDisciplinaryActions,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.ViewTimeAvailability, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ViewTimeAvailability,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Recurring incomes ("planilla ingresos cíclicos" — REQ-005 D-06/P-14). HR-only, no self-service in
    // Fase 1 (P-11), so — mirroring the settlement policies — the write policy is a RequireAssertion
    // superset of the precise handler gate while ViewRecurringIncomes stays authn-only (the per-file detail
    // and the company bandeja gate per-handler via EnsureCanViewRecurringIncomesAsync).
    // AuthorizeRecurringIncomes deliberately EXCLUDES PersonnelFiles.Admin (separation of duties + double
    // anti-self — mirrors AuthorizeRetirement); the IAM super-admin (ManageAdministration) remains the
    // universal fallback. The record controllers that carry these are added in PR-3/PR-4/PR-5.
    options.AddPolicy(PersonnelFilePolicies.ViewRecurringIncomes, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageRecurringIncomes, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageRecurringIncomes,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeRecurringIncomes, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeRecurringIncomes,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // One-time deductions ("planilla descuentos eventuales" — REQ-009). Exact mirror of the one-time-income
    // policies: HR-only, the write policy is a RequireAssertion superset of the precise handler gate while
    // ViewOneTimeDeductions stays authn-only. AuthorizeOneTimeDeductions deliberately EXCLUDES
    // PersonnelFiles.Admin (separation of duties + TRIPLE anti-self).
    options.AddPolicy(PersonnelFilePolicies.ViewOneTimeDeductions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageOneTimeDeductions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageOneTimeDeductions,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeOneTimeDeductions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeOneTimeDeductions,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Recurring deductions ("planilla descuentos cíclicos" — REQ-008 D-06). Exact mirror of the
    // recurring-income policies: HR-only (no self-service in Fase 1), the write policy is a RequireAssertion
    // superset of the precise handler gate while ViewRecurringDeductions stays authn-only (the per-file
    // detail and the company bandeja gate per-handler via EnsureCanViewRecurringDeductionsAsync).
    // AuthorizeRecurringDeductions deliberately EXCLUDES PersonnelFiles.Admin (separation of duties + double
    // anti-self); the IAM super-admin (ManageAdministration) remains the universal fallback. The controllers
    // that carry these are added in PR-3/PR-4/PR-5.
    options.AddPolicy(PersonnelFilePolicies.ViewRecurringDeductions, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageRecurringDeductions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageRecurringDeductions,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeRecurringDeductions, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeRecurringDeductions,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Indebtedness (REQ-010 D-16/D-17). NOT an Authorize* grant: PersonnelFiles.Admin IS a superset of both.
    // ViewIndebtedness stays authn-only at the policy level (the query and the simulation gate per handler via
    // EnsureCanViewIndebtednessAsync); the parameter writes carry the precise RequireAssertion superset.
    // Registered here BEFORE the controllers land (PR-1/PR-3) — the governance test demands it.
    options.AddPolicy(PersonnelFilePolicies.ViewIndebtedness, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageIndebtednessParameters, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageIndebtednessParameters,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Not-worked time (REQ-011 D-18/P-16). NO Authorize* grant: the flow has no decision step — the absence already
    // happened (same reasoning as an incapacity), so PersonnelFiles.Admin IS a superset of all three.
    // Registered BEFORE the controllers land (PR-1/PR-3) — the governance test demands it.
    options.AddPolicy(PersonnelFilePolicies.ViewNotWorkedTimes, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageNotWorkedTimes, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageNotWorkedTimes,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.ManageNotWorkedTimeTypes, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageNotWorkedTimeTypes,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // One-time incomes ("planilla ingresos eventuales" — REQ-006 P-01). HR-only, no self-service in Fase 1
    // (P-11), so — mirroring the recurring-income policies — the write policy is a RequireAssertion superset
    // of the precise handler gate while ViewOneTimeIncomes stays authn-only (the per-file detail and the
    // company bandeja gate per-handler via EnsureCanViewOneTimeIncomesAsync). AuthorizeOneTimeIncomes
    // deliberately EXCLUDES PersonnelFiles.Admin (separation of duties + triple anti-self — mirrors
    // AuthorizeRetirement); the IAM super-admin (ManageAdministration) remains the universal fallback. The
    // record controllers that carry these are added in PR-3/PR-4/PR-5.
    options.AddPolicy(PersonnelFilePolicies.ViewOneTimeIncomes, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageOneTimeIncomes, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageOneTimeIncomes,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeOneTimeIncomes, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeOneTimeIncomes,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Overtime records ("horas extras del empleado" — REQ-007 P-01). Dual channel: HR + employee portal
    // self-service (preference default-off) + self-read (P-12). Unlike the recurring/one-time income
    // modules, BOTH the read AND the write RECORD policies stay authn-only, because the write policy is the
    // mechanism that enables the portal channel — the precise gate (View/ManageOvertimeRecords permission,
    // Admin, or the employee acting on their own record when the preference is enabled) lives in the handler
    // (EnsureCanView/ManageOvertimeRecordsAsync + the self-service load helpers, PR-3/PR-4). Mirrors the
    // medical-claims / leave read+write policies. AuthorizeOvertimeRecords deliberately EXCLUDES
    // PersonnelFiles.Admin (separation of duties + triple anti-self — mirrors AuthorizeRetirement); the IAM
    // super-admin (ManageAdministration) remains the universal fallback. The record controllers that carry
    // these are added in PR-3/PR-4/PR-5.
    options.AddPolicy(PersonnelFilePolicies.ViewOvertimeRecords, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.ManageOvertimeRecords, policyBuilder => policyBuilder
        .Combine(policy));
    options.AddPolicy(PersonnelFilePolicies.AuthorizeOvertimeRecords, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.AuthorizeOvertimeRecords,
            PersonnelFilePermissionCodes.ManageAdministration)));

    // Overtime configuration masters (overtime types, overtime justification types — REQ-007). Unlike the
    // authn-only record policies above, the masters have NO self-service channel, so these are STRICT
    // (RequireAssertion) declarative policies over the SAME View/ManageOvertimeRecords permission codes,
    // kept a superset of the precise EnsureCanView/ManageOvertimeRecordsAsync handler gate (mirrors
    // CostCenterPolicies / EmployeeRelationsConfigurationPolicies). The master controllers carry these in
    // PR-1.
    options.AddPolicy(OvertimeConfigurationPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ViewOvertimeRecords,
            PersonnelFilePermissionCodes.Admin,
            PersonnelFilePermissionCodes.ManageAdministration)));
    options.AddPolicy(OvertimeConfigurationPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            PersonnelFilePermissionCodes.ManageOvertimeRecords,
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

    // Leave configuration masters (medical clinics, incapacity risks/types, company holidays,
    // payroll periods) — declarative policies kept a superset of the precise
    // LeaveConfigurationAuthorizationService handler gate (EnsureCanReadAsync /
    // EnsureCanManageAsync) so a legitimate reader/manager is never falsely 403'd.
    options.AddPolicy(LeaveConfigurationPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            LeaveConfigurationPermissionCodes.Read,
            LeaveConfigurationPermissionCodes.Admin,
            LeaveConfigurationPermissionCodes.ManageAdministration)));

    options.AddPolicy(LeaveConfigurationPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            LeaveConfigurationPermissionCodes.Admin,
            LeaveConfigurationPermissionCodes.ManageAdministration)));

    // Employee-relations configuration masters (recognition types, disciplinary-action types/causes —
    // REQ-003) — declarative policies kept a superset of the precise
    // EmployeeRelationsConfigurationAuthorizationService handler gate (EnsureCanReadAsync /
    // EnsureCanManageAsync) so a legitimate reader/manager is never falsely 403'd.
    options.AddPolicy(EmployeeRelationsConfigurationPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            EmployeeRelationsConfigurationPermissionCodes.Read,
            EmployeeRelationsConfigurationPermissionCodes.Admin,
            EmployeeRelationsConfigurationPermissionCodes.ManageAdministration)));

    options.AddPolicy(EmployeeRelationsConfigurationPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            EmployeeRelationsConfigurationPermissionCodes.Admin,
            EmployeeRelationsConfigurationPermissionCodes.ManageAdministration)));

    // Locations (work centers, work center types, location groups/levels/hierarchy) — declarative
    // policies kept a superset of the precise ILocationAuthorizationService handler gate
    // (EnsureCanReadAsync / EnsureCanManageAsync) so a legitimate reader/manager is never falsely
    // 403'd. Shared across every Locations controller.
    options.AddPolicy(LocationPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            LocationPermissionCodes.Read,
            LocationPermissionCodes.Admin,
            LocationPermissionCodes.ManageAdministration)));

    options.AddPolicy(LocationPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            LocationPermissionCodes.Admin,
            LocationPermissionCodes.ManageAdministration)));

    // Legal Representatives — declarative policies kept a superset of the precise
    // LegalRepresentativeAuthorizationService handler gate (EnsureCanReadAsync /
    // EnsureCanManageAsync) so a legitimate reader/manager is never falsely 403'd.
    options.AddPolicy(LegalRepresentativePolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            LegalRepresentativePermissionCodes.Read,
            LegalRepresentativePermissionCodes.Admin,
            LegalRepresentativePermissionCodes.ManageAdministration)));

    options.AddPolicy(LegalRepresentativePolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            LegalRepresentativePermissionCodes.Admin,
            LegalRepresentativePermissionCodes.ManageAdministration)));

    // Org Units — declarative policies kept a superset of the precise OrgUnitAuthorizationService
    // handler gate (EnsureCanReadAsync / EnsureCanManageAsync) so a legitimate reader/manager is
    // never falsely 403'd.
    options.AddPolicy(OrgUnitPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            OrgUnitPermissionCodes.Read,
            OrgUnitPermissionCodes.Admin,
            OrgUnitPermissionCodes.ManageAdministration)));

    options.AddPolicy(OrgUnitPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            OrgUnitPermissionCodes.Admin,
            OrgUnitPermissionCodes.ManageAdministration)));

    // Organization Structure Catalogs — declarative policies kept a superset of the precise
    // OrgStructureCatalogAuthorizationService handler gate (EnsureCanReadTenantAsync /
    // EnsureCanManageTenantAsync), including the OrgUnits.* fallback (whoever administers org units
    // administers their catalogs), so a legitimate reader/manager is never falsely 403'd.
    options.AddPolicy(OrgStructureCatalogPolicies.Read, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            OrgStructureCatalogPermissionCodes.Read,
            OrgStructureCatalogPermissionCodes.Admin,
            OrgStructureCatalogPermissionCodes.OrgUnitsRead,
            OrgStructureCatalogPermissionCodes.OrgUnitsAdmin,
            OrgStructureCatalogPermissionCodes.ManageAdministration)));

    options.AddPolicy(OrgStructureCatalogPolicies.Manage, policyBuilder => policyBuilder
        .Combine(policy)
        .RequireAssertion(static context => PermissionClaimEvaluator.HasAnyPermission(
            context,
            OrgStructureCatalogPermissionCodes.Admin,
            OrgStructureCatalogPermissionCodes.OrgUnitsAdmin,
            OrgStructureCatalogPermissionCodes.ManageAdministration)));
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
    app.UseHsts();
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
                // AU-10: pin the accepted signing algorithm so an attacker cannot coerce a different alg
                // (alg-confusion defense-in-depth; tokens are HS256-signed).
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
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
