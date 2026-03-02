using System.Text.Json;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Authentication;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;

namespace CLARIHR.Infrastructure.IdentityAccess;

internal sealed partial class FieldPermissionService : IFieldPermissionService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _dbContext;
    private readonly IIamAdministrationAuthorizationService _authorizationService;
    private readonly IFieldAccessProfileService _fieldAccessProfileService;
    private readonly IFieldPermissionOverrideCache _fieldPermissionOverrideCache;
    private readonly ICurrentUserService _currentUserService;
    private readonly ITenantContext _tenantContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuditService _auditService;

    public FieldPermissionService(
        ApplicationDbContext dbContext,
        IIamAdministrationAuthorizationService authorizationService,
        IFieldAccessProfileService fieldAccessProfileService,
        IFieldPermissionOverrideCache fieldPermissionOverrideCache,
        ICurrentUserService currentUserService,
        ITenantContext tenantContext,
        IDateTimeProvider dateTimeProvider,
        IHttpContextAccessor httpContextAccessor,
        IAuditService auditService)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _fieldAccessProfileService = fieldAccessProfileService;
        _fieldPermissionOverrideCache = fieldPermissionOverrideCache;
        _currentUserService = currentUserService;
        _tenantContext = tenantContext;
        _dateTimeProvider = dateTimeProvider;
        _httpContextAccessor = httpContextAccessor;
        _auditService = auditService;
    }

    public Task<Result<FieldAccessProfile>> GetCurrentUserAccessProfileAsync(
        string resourceKey,
        CancellationToken cancellationToken) =>
        _fieldAccessProfileService.GetCurrentUserAccessProfileAsync(resourceKey, cancellationToken);
}
