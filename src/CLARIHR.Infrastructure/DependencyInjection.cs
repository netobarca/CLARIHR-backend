using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.CompetencyFramework;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.JobProfiles;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Locations;
using CLARIHR.Application.Abstractions.OrgUnits;
using CLARIHR.Application.Abstractions.OrgStructureCatalogs;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.PositionDescriptionCatalogs;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Policies;
using CLARIHR.Application.Abstractions.Reports;
using CLARIHR.Application.Abstractions.SalaryTabulator;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Infrastructure.Auth;
using CLARIHR.Infrastructure.Auditing;
using CLARIHR.Infrastructure.Authentication;
using CLARIHR.Infrastructure.Companies;
using CLARIHR.Infrastructure.CompetencyFramework;
using CLARIHR.Infrastructure.Configuration;
using CLARIHR.Infrastructure.CostCenters;
using CLARIHR.Infrastructure.IdentityAccess;
using CLARIHR.Infrastructure.JobProfiles;
using CLARIHR.Infrastructure.LegalRepresentatives;
using CLARIHR.Infrastructure.Locations;
using CLARIHR.Infrastructure.OrgUnits;
using CLARIHR.Infrastructure.OrgStructureCatalogs;
using CLARIHR.Infrastructure.PersonnelFiles;
using CLARIHR.Infrastructure.PositionDescriptionCatalogs;
using CLARIHR.Infrastructure.PositionSlots;
using CLARIHR.Infrastructure.Policies;
using CLARIHR.Infrastructure.Persistence;
using CLARIHR.Infrastructure.Reports;
using CLARIHR.Infrastructure.SalaryTabulator;
using CLARIHR.Infrastructure.Tenancy;
using CLARIHR.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CLARIHR.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
        services.Configure<JwtTokenOptions>(configuration.GetSection(JwtTokenOptions.SectionName));
        services.Configure<GoogleAuthOptions>(configuration.GetSection(GoogleAuthOptions.SectionName));
        services.Configure<FieldPermissionCacheOptions>(configuration.GetSection(FieldPermissionCacheOptions.SectionName));
        services.Configure<CompanyOwnershipOptions>(configuration.GetSection(CompanyOwnershipOptions.SectionName));
        services.AddHttpContextAccessor();

        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IPasswordHasher, BuiltInPasswordHasher>();
        services.AddMemoryCache();
        services.AddSingleton<RefreshTokenHasher>();
        services.AddSingleton<IRefreshTokenHasher>(serviceProvider => serviceProvider.GetRequiredService<RefreshTokenHasher>());
        services.AddSingleton<IInvitationTokenHasher>(serviceProvider => serviceProvider.GetRequiredService<RefreshTokenHasher>());
        services.AddSingleton<IGoogleIdTokenValidator, GoogleIdTokenValidator>();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        services.AddScoped<ICurrentUserService, HttpCurrentUserService>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IAuditService, AuditService>();
        services.AddSingleton<IAuditSanitizer, AuditSanitizer>();
        services.AddScoped<ICompanyRepository, CompanyRepository>();
        services.AddScoped<ICompanySubscriptionRepository, CompanySubscriptionRepository>();
        services.AddScoped<IUserCompanyRepository, UserCompanyRepository>();
        services.AddScoped<ICompanyOwnershipPolicy, CompanyOwnershipPolicy>();
        services.AddScoped<IInvitationTokenRepository, InvitationTokenRepository>();
        services.AddScoped<IEmailService, LoggingEmailService>();
        services.AddScoped<ICompanyUserAuthorizationService, CompanyUserAuthorizationService>();
        services.AddScoped<IPlanEntitlementService, PlanEntitlementService>();
        services.AddScoped<ILocationHierarchyRepository, LocationHierarchyRepository>();
        services.AddScoped<ILocationGroupRepository, LocationGroupRepository>();
        services.AddScoped<IWorkCenterTypeRepository, WorkCenterTypeRepository>();
        services.AddScoped<IWorkCenterRepository, WorkCenterRepository>();
        services.AddScoped<ILocationDependencyPolicy, LocationDependencyPolicy>();
        services.AddScoped<ILocationSeedService, LocationSeedService>();
        services.AddScoped<ILocationAuthorizationService, LocationAuthorizationService>();
        services.AddScoped<IOrgUnitRepository, OrgUnitRepository>();
        services.AddScoped<IOrgUnitAuthorizationService, OrgUnitAuthorizationService>();
        services.AddScoped<IOrgStructureCatalogRepository, OrgStructureCatalogRepository>();
        services.AddScoped<IOrgStructureCatalogAuthorizationService, OrgStructureCatalogAuthorizationService>();
        services.AddScoped<IJobProfileRepository, JobProfileRepository>();
        services.AddScoped<IJobCatalogRepository, JobCatalogRepository>();
        services.AddScoped<IJobProfileAuthorizationService, JobProfileAuthorizationService>();
        services.AddScoped<IPositionDescriptionCatalogRepository, PositionDescriptionCatalogRepository>();
        services.AddScoped<IPositionDescriptionCatalogAuthorizationService, PositionDescriptionCatalogAuthorizationService>();
        services.AddScoped<IPositionSlotRepository, PositionSlotRepository>();
        services.AddScoped<IPositionSlotAuthorizationService, PositionSlotAuthorizationService>();
        services.AddScoped<ICostCenterRepository, CostCenterRepository>();
        services.AddScoped<ICostCenterAuthorizationService, CostCenterAuthorizationService>();
        services.AddScoped<ICompetencyFrameworkRepository, CompetencyFrameworkRepository>();
        services.AddScoped<ICompetencyFrameworkAuthorizationService, CompetencyFrameworkAuthorizationService>();
        services.AddScoped<ILegalRepresentativeRepository, LegalRepresentativeRepository>();
        services.AddScoped<ILegalRepresentativeAuthorizationService, LegalRepresentativeAuthorizationService>();
        services.AddScoped<IPersonnelFileRepository, PersonnelFileRepository>();
        services.AddScoped<IPersonnelFileAuthorizationService, PersonnelFileAuthorizationService>();
        services.AddScoped<ISalaryTabulatorRepository, SalaryTabulatorRepository>();
        services.AddScoped<ISalaryTabulatorAuthorizationService, SalaryTabulatorAuthorizationService>();
        services.AddSingleton<IReportCapabilityRegistry, ReportCapabilityRegistry>();
        services.AddScoped<IResourceActionPolicyService, ResourceActionPolicyService>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IExternalAuthProviderService, GoogleExternalAuthProviderService>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddScoped<IRbacAuthorizationService, RbacAuthorizationService>();
        services.AddScoped<IIamAdministrationRepository, IamAdministrationRepository>();
        services.AddScoped<IIamAdministrationAuthorizationService, IamAdministrationAuthorizationService>();
        services.AddSingleton<IFieldPermissionOverrideCache, FieldPermissionOverrideCache>();
        services.AddScoped<IFieldAccessProfileService, FieldAccessProfileService>();
        services.AddScoped<IFieldPermissionService, FieldPermissionService>();
        services.AddSingleton<IFieldSerializationService, FieldSerializationService>();

        services.AddDbContext<ApplicationDbContext>((serviceProvider, optionsBuilder) =>
        {
            var databaseOptions = serviceProvider.GetRequiredService<IOptions<DatabaseOptions>>().Value;

            optionsBuilder.EnableDetailedErrors();
            optionsBuilder.EnableSensitiveDataLogging(false);

            PostgreSqlOptionsConfigurator.Configure(optionsBuilder, databaseOptions.ConnectionString);
        });

        services.AddScoped<IApplicationDbContext>(serviceProvider => serviceProvider.GetRequiredService<ApplicationDbContext>());

        return services;
    }
}
