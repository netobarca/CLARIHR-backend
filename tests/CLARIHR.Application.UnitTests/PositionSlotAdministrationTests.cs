using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.CostCenters;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.PositionSlots;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.CostCenters;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.IdentityAccess.Contracts;
using CLARIHR.Application.Features.PositionSlots;
using CLARIHR.Application.Features.PositionSlots.Common;
using CLARIHR.Domain.CostCenters;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.IdentityAccess;
using CLARIHR.Domain.PositionSlots;
using FluentValidation.TestHelper;
using System.Reflection;

namespace CLARIHR.Application.UnitTests;

public sealed class PositionSlotAdministrationTests
{
    [Fact]
    public void CreateValidator_WhenOccupiedEmployeesExceedsCapacity_ShouldAttachErrorToOccupiedEmployees()
    {
        var validator = new CreatePositionSlotCommandValidator();
        var command = new CreatePositionSlotCommand(
            CompanyId: Guid.Parse("10101010-1010-1010-1010-101010101010"),
            Code: "PS-VAL",
            Title: "Plaza",
            JobProfileId: Guid.Parse("20202020-2020-2020-2020-202020202020"),
            RoleId: null,
            WorkCenterId: null,
            DirectDependencyPositionSlotId: null,
            FunctionalDependencyPositionSlotId: null,
            Status: PositionSlotStatus.Occupied,
            MaxEmployees: 1,
            OccupiedEmployees: 20,
            EffectiveFromUtc: new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc: null,
            Notes: null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(command => command.OccupiedEmployees)
            .WithErrorMessage("OccupiedEmployees must be less than or equal to MaxEmployees.");
        Assert.DoesNotContain(result.Errors, static error => string.IsNullOrWhiteSpace(error.PropertyName));
    }

    [Fact]
    public void CreateValidator_WhenEffectiveToIsBeforeEffectiveFrom_ShouldAttachErrorToEffectiveToUtc()
    {
        var validator = new CreatePositionSlotCommandValidator();
        var command = new CreatePositionSlotCommand(
            CompanyId: Guid.Parse("30303030-3030-3030-3030-303030303030"),
            Code: "PS-DATE",
            Title: "Plaza",
            JobProfileId: Guid.Parse("40404040-4040-4040-4040-404040404040"),
            RoleId: null,
            WorkCenterId: null,
            DirectDependencyPositionSlotId: null,
            FunctionalDependencyPositionSlotId: null,
            Status: PositionSlotStatus.Vacant,
            MaxEmployees: 1,
            OccupiedEmployees: 0,
            EffectiveFromUtc: new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Utc),
            EffectiveToUtc: new DateTime(2026, 4, 7, 0, 0, 0, DateTimeKind.Utc),
            Notes: null);

        var result = validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(command => command.EffectiveToUtc)
            .WithErrorMessage("EffectiveToUtc must be greater than or equal to EffectiveFromUtc.");
        Assert.DoesNotContain(result.Errors, static error => string.IsNullOrWhiteSpace(error.PropertyName));
    }

    [Fact]
    public async Task Create_WhenJobProfileDoesNotResolveContractType_ShouldCreateIndefiniteSlot()
    {
        var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var jobProfileId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var orgUnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var repository = new TestPositionSlotRepository();
        repository.RegisterLookup(new PositionSlotJobProfileLookup(
            InternalJobProfileId: 10,
            JobProfileId: jobProfileId,
            OrgUnitId: orgUnitId,
            OrgUnitName: "Finanzas",
            CostCenterCode: null,
            PositionCategoryId: null,
            PositionCategoryClassificationId: null,
            ContractTypeId: null,
            ContractTypeCode: null,
            ContractTypeName: null));

        var unitOfWork = new TestUnitOfWork();
        var handler = new CreatePositionSlotCommandHandler(
            new AllowPositionSlotAuthorizationService(),
            repository,
            new TestIamAdministrationRepository(),
            new TestCostCenterRepository(),
            new NoOpAuditService(),
            unitOfWork);

        var result = await handler.Handle(
            new CreatePositionSlotCommand(
                companyId,
                "PS-001",
                "Plaza",
                jobProfileId,
                RoleId: null,
                WorkCenterId: null,
                DirectDependencyPositionSlotId: null,
                FunctionalDependencyPositionSlotId: null,
                Status: PositionSlotStatus.Vacant,
                MaxEmployees: 10,
                OccupiedEmployees: 0,
                EffectiveFromUtc: new DateTime(2026, 4, 8, 0, 0, 0, DateTimeKind.Utc),
                EffectiveToUtc: null,
                Notes: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(repository.LastAddedSlot);
        Assert.False(repository.LastAddedSlot!.IsFixedTerm);
        Assert.Null(result.Value.ContractTypeId);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Update_WhenJobProfileDoesNotResolveContractType_ShouldResetFixedTermFlag()
    {
        var companyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var originalJobProfileId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var replacementJobProfileId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var orgUnitId = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var initialContractTypeId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var repository = new TestPositionSlotRepository();
        var slot = PositionSlot.Create(
            "PS-UPD",
            "Plaza original",
            jobProfileId: 20,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 3,
            occupiedEmployees: 0,
            isFixedTerm: true,
            effectiveFromUtc: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveToUtc: null,
            notes: null);
        slot.SetTenantId(companyId);

        repository.AddExisting(slot);
        repository.RegisterLookup(new PositionSlotJobProfileLookup(
            InternalJobProfileId: 20,
            JobProfileId: originalJobProfileId,
            OrgUnitId: orgUnitId,
            OrgUnitName: "Operaciones",
            CostCenterCode: null,
            PositionCategoryId: null,
            PositionCategoryClassificationId: null,
            ContractTypeId: initialContractTypeId,
            ContractTypeCode: "TEMP-01",
            ContractTypeName: "Temporal"));
        repository.RegisterLookup(new PositionSlotJobProfileLookup(
            InternalJobProfileId: 30,
            JobProfileId: replacementJobProfileId,
            OrgUnitId: orgUnitId,
            OrgUnitName: "Operaciones",
            CostCenterCode: null,
            PositionCategoryId: null,
            PositionCategoryClassificationId: null,
            ContractTypeId: null,
            ContractTypeCode: null,
            ContractTypeName: null));

        var unitOfWork = new TestUnitOfWork();
        var handler = new UpdatePositionSlotCommandHandler(
            new AllowPositionSlotAuthorizationService(),
            repository,
            new TestIamAdministrationRepository(),
            new TestCompanyUserProvisioningService(),
            new TestCostCenterRepository(),
            new NoOpAuditService(),
            new FixedTenantContext(companyId),
            unitOfWork);

        var result = await handler.Handle(
            new UpdatePositionSlotCommand(
                slot.PublicId,
                "PS-UPD",
                "Plaza actualizada",
                replacementJobProfileId,
                RoleId: null,
                WorkCenterId: null,
                MaxEmployees: 5,
                EffectiveFromUtc: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                EffectiveToUtc: null,
                Notes: "Sin tipo de contrato",
                ConcurrencyToken: slot.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(30, slot.JobProfileId);
        Assert.False(slot.IsFixedTerm);
        Assert.Null(result.Value.ContractTypeId);
        Assert.True(unitOfWork.Transaction.CommitCalled);
    }

    [Fact]
    public async Task Update_WhenRoleChanges_ShouldSyncLinkedUsersForAssignedSlot()
    {
        var companyId = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var jobProfileId = Guid.Parse("23232323-2323-2323-2323-232323232323");
        var orgUnitId = Guid.Parse("34343434-3434-3434-3434-343434343434");
        var repository = new TestPositionSlotRepository();
        var slot = PositionSlot.Create(
            "PS-ROLE",
            "Plaza con rol",
            jobProfileId: 50,
            roleId: null,
            workCenterId: null,
            directDependencyPositionSlotId: null,
            functionalDependencyPositionSlotId: null,
            status: PositionSlotStatus.Vacant,
            maxEmployees: 1,
            occupiedEmployees: 0,
            isFixedTerm: false,
            effectiveFromUtc: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveToUtc: null,
            notes: null);
        slot.SetTenantId(companyId);
        repository.AddExisting(slot);
        repository.RegisterLookup(new PositionSlotJobProfileLookup(
            InternalJobProfileId: 50,
            JobProfileId: jobProfileId,
            OrgUnitId: orgUnitId,
            OrgUnitName: "Tecnologia",
            CostCenterCode: null,
            PositionCategoryId: null,
            PositionCategoryClassificationId: null,
            ContractTypeId: null,
            ContractTypeCode: null,
            ContractTypeName: null));

        var iamRepository = new TestIamAdministrationRepository();
        var role = IamRole.Create("Supervisor", "Supervisor role", isSystemRole: false);
        role.SetTenantId(companyId);
        iamRepository.AddRole(role);
        repository.RegisterRole(role.Id, role.PublicId, role.Name);

        var provisioningService = new TestCompanyUserProvisioningService();
        var handler = new UpdatePositionSlotCommandHandler(
            new AllowPositionSlotAuthorizationService(),
            repository,
            iamRepository,
            provisioningService,
            new TestCostCenterRepository(),
            new NoOpAuditService(),
            new FixedTenantContext(companyId),
            new TestUnitOfWork());

        var result = await handler.Handle(
            new UpdatePositionSlotCommand(
                slot.PublicId,
                "PS-ROLE",
                "Plaza con rol",
                jobProfileId,
                RoleId: role.PublicId,
                WorkCenterId: null,
                MaxEmployees: 1,
                EffectiveFromUtc: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc),
                EffectiveToUtc: null,
                Notes: null,
                ConcurrencyToken: slot.ConcurrencyToken),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(provisioningService.LastSyncRequest);
        Assert.Equal(companyId, provisioningService.LastSyncRequest!.Value.CompanyPublicId);
        Assert.Equal(slot.PublicId, provisioningService.LastSyncRequest.Value.AssignedPositionSlotId);
        Assert.Equal(role.PublicId, provisioningService.LastSyncRequest.Value.RoleId);
    }

    private sealed class AllowPositionSlotAuthorizationService : IPositionSlotAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result<PositionSlotAccess>> EvaluateAccessAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result<PositionSlotAccess>.Success(new PositionSlotAccess(CanRead: true, CanManage: true)));

        public Error TenantMismatch(RbacPermissionAction action) => PositionSlotErrors.Forbidden;
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class TestPositionSlotRepository : IPositionSlotRepository
    {
        private static readonly DateTime FixedUtcNow = new(2026, 4, 7, 12, 0, 0, DateTimeKind.Utc);
        private readonly Dictionary<Guid, PositionSlot> _slots = [];
        private readonly Dictionary<Guid, PositionSlotJobProfileLookup> _lookupsByPublicId = [];
        private readonly Dictionary<long, PositionSlotJobProfileLookup> _lookupsByInternalId = [];
        private readonly Dictionary<long, (Guid PublicId, string? Name)> _rolesByInternalId = [];

        public PositionSlot? LastAddedSlot { get; private set; }

        public void RegisterLookup(PositionSlotJobProfileLookup lookup)
        {
            _lookupsByPublicId[lookup.JobProfileId] = lookup;
            _lookupsByInternalId[lookup.InternalJobProfileId] = lookup;
        }

        public void RegisterRole(long internalRoleId, Guid rolePublicId, string? roleName) =>
            _rolesByInternalId[internalRoleId] = (rolePublicId, roleName);

        public void AddExisting(PositionSlot slot)
        {
            if (slot.CreatedUtc == default)
            {
                slot.MarkCreated(FixedUtcNow);
            }

            _slots[slot.PublicId] = slot;
        }

        public void Add(PositionSlot slot)
        {
            AddExisting(slot);
            LastAddedSlot = slot;
        }

        public Task<PositionSlot?> GetByIdAsync(Guid slotId, CancellationToken cancellationToken) =>
            Task.FromResult(_slots.TryGetValue(slotId, out var slot) ? slot : null);

        public Task<bool> ExistsOutsideTenantAsync(Guid slotId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingSlotId, CancellationToken cancellationToken) =>
            Task.FromResult(_slots.Values.Any(slot =>
                slot.TenantId == tenantId &&
                slot.NormalizedCode == normalizedCode &&
                (!excludingSlotId.HasValue || slot.Id != excludingSlotId.Value)));

        public Task<long?> ResolveJobProfileIdAsync(Guid tenantId, Guid jobProfileId, CancellationToken cancellationToken) =>
            Task.FromResult(_lookupsByPublicId.TryGetValue(jobProfileId, out var lookup) ? (long?)lookup.InternalJobProfileId : null);

        public Task<bool> JobProfileExistsOutsideTenantAsync(Guid jobProfileId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<long?> ResolveWorkCenterIdAsync(Guid tenantId, Guid workCenterId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);

        public Task<bool> WorkCenterExistsOutsideTenantAsync(Guid workCenterId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public Task<long?> ResolvePositionSlotIdAsync(Guid tenantId, Guid slotId, CancellationToken cancellationToken) =>
            Task.FromResult<long?>(null);

        public Task<PagedResponse<PositionSlotListItemResponse>> SearchAsync(
            Guid tenantId,
            PositionSlotStatus? status,
            Guid? jobProfileId,
            Guid? orgUnitId,
            Guid? workCenterId,
            Guid? contractTypeId,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PositionSlotResponse?> GetResponseByIdAsync(Guid slotId, CancellationToken cancellationToken)
        {
            if (!_slots.TryGetValue(slotId, out var slot))
            {
                return Task.FromResult<PositionSlotResponse?>(null);
            }

            _ = _lookupsByInternalId.TryGetValue(slot.JobProfileId, out var lookup);
            var role = slot.RoleId.HasValue && _rolesByInternalId.TryGetValue(slot.RoleId.Value, out var registeredRole)
                ? registeredRole
                : ((Guid PublicId, string? Name)?)null;
            var response = new PositionSlotResponse(
                Id: slot.PublicId,
                CompanyId: slot.TenantId,
                Code: slot.Code,
                Title: slot.Title,
                Status: slot.Status,
                JobProfileId: lookup?.JobProfileId ?? Guid.Empty,
                JobProfileCode: lookup?.JobProfileId.ToString("D") ?? "UNKNOWN",
                JobProfileTitle: "Perfil",
                RoleId: role?.PublicId,
                RoleName: role?.Name,
                OrgUnitId: lookup?.OrgUnitId ?? Guid.Empty,
                OrgUnitCode: "ORG",
                OrgUnitName: lookup?.OrgUnitName ?? "Sin unidad",
                WorkCenterId: null,
                WorkCenterCode: null,
                WorkCenterName: null,
                CostCenterCode: lookup?.CostCenterCode,
                DirectDependencyPositionSlotId: null,
                DirectDependencyPositionSlotCode: null,
                FunctionalDependencyPositionSlotId: null,
                FunctionalDependencyPositionSlotCode: null,
                PositionCategoryId: lookup?.PositionCategoryId,
                PositionCategoryCode: null,
                PositionCategoryName: null,
                PositionCategoryClassificationId: lookup?.PositionCategoryClassificationId,
                PositionCategoryClassificationCode: null,
                PositionCategoryClassificationName: null,
                ContractTypeId: lookup?.ContractTypeId,
                ContractTypeCode: lookup?.ContractTypeCode,
                ContractTypeName: lookup?.ContractTypeName,
                MaxEmployees: slot.MaxEmployees,
                OccupiedEmployees: slot.OccupiedEmployees,
                EffectiveFromUtc: slot.EffectiveFromUtc,
                EffectiveToUtc: slot.EffectiveToUtc,
                Notes: slot.Notes,
                IsActive: slot.IsActive,
                ConcurrencyToken: slot.ConcurrencyToken,
                CreatedAtUtc: slot.CreatedUtc,
                ModifiedAtUtc: slot.ModifiedUtc);

            return Task.FromResult<PositionSlotResponse?>(response);
        }

        public Task<int> CountSlotsAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(_slots.Values.Count(slot => slot.TenantId == tenantId));

        public Task<IReadOnlyCollection<PositionSlotGraphNodeData>> GetGraphNodesAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<PositionSlotExportRow>> GetExportRowsAsync(
            Guid tenantId,
            PositionSlotStatus? status,
            Guid? jobProfileId,
            Guid? orgUnitId,
            Guid? workCenterId,
            Guid? contractTypeId,
            string? search,
            int? maxRows,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PositionSlotJobProfileLookup?> GetJobProfileLookupAsync(
            Guid tenantId,
            Guid jobProfileId,
            CancellationToken cancellationToken) =>
            Task.FromResult(_lookupsByPublicId.TryGetValue(jobProfileId, out var lookup) ? lookup : null);
    }

    private sealed class TestIamAdministrationRepository : IIamAdministrationRepository
    {
        public void AddUser(IamUser user) => throw new NotSupportedException();
        private long _nextRoleId = 1;
        public List<IamRole> Roles { get; } = [];
        public void AddRole(IamRole role)
        {
            if (role.Id == 0)
            {
                SetEntityId(role, _nextRoleId++);
            }

            Roles.Add(role);
        }
        public void RemoveRole(IamRole role) => throw new NotSupportedException();
        public void AddPermission(IamPermission permission) => throw new NotSupportedException();
        public Task<bool> UserEmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RoleNameExistsAsync(string normalizedRoleName, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> UserPublicIdExistsAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> RolePublicIdExistsAsync(Guid roleId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamUser?> FindUserByPublicIdAsync(Guid userId, bool includeRoles, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamUser?> FindUserByTenantAndLinkedUserPublicIdAsync(Guid tenantId, Guid linkedUserPublicId, bool includeRoles, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamRole?> FindRoleByPublicIdAsync(Guid roleId, bool includePermissions, CancellationToken cancellationToken) =>
            Task.FromResult<IamRole?>(Roles.SingleOrDefault(role => role.PublicId == roleId));
        public Task<IamRole?> FindSystemRoleByTenantAndNormalizedNameAsync(Guid tenantId, string normalizedRoleName, bool includePermissions, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamRole>> GetRolesByPublicIdsAsync(IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<IamRole>>(Roles.Where(role => roleIds.Contains(role.PublicId)).ToArray());
        public Task<IReadOnlyList<IamUser>> GetUsersByPublicIdsAsync(IReadOnlyCollection<Guid> userIds, bool includeRoles, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamUser>> GetUsersAssignedToRoleAsync(Guid roleId, bool includeRoles, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamUser>> GetActiveUsersAsync(bool includeRoles, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<Guid>> GetActiveAdministratorUserIdsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamPermission>> GetPermissionsByNormalizedCodesAsync(IReadOnlyCollection<string> normalizedPermissionCodes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamPermission>> GetPermissionsByTenantAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamPermission>> GetPermissionsByTenantAndNormalizedCodesAsync(Guid tenantId, IReadOnlyCollection<string> normalizedPermissionCodes, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<IamPermission>> GetPermissionsByPublicIdsAsync(IReadOnlyCollection<Guid> permissionIds, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResponse<IamUserSummaryResponse>> GetUsersAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamUserResponse?> GetUserAsync(Guid userId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedResponse<IamRoleSummaryResponse>> GetRolesAsync(int pageNumber, int pageSize, string? search, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IamRoleResponse?> GetRoleAsync(Guid roleId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class TestCompanyUserProvisioningService : ICompanyUserProvisioningService
    {
        public (Guid CompanyPublicId, Guid AssignedPositionSlotId, Guid RoleId)? LastSyncRequest { get; private set; }

        public Task<Result<CompanyUserProvisioningResult>> ProvisionAsync(
            CompanyUserProvisioningRequest request,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Result<int>> SyncRoleAssignmentsForPositionSlotAsync(
            Guid companyPublicId,
            Guid assignedPositionSlotId,
            Guid roleId,
            CancellationToken cancellationToken)
        {
            LastSyncRequest = (companyPublicId, assignedPositionSlotId, roleId);
            return Task.FromResult(Result<int>.Success(0));
        }
    }

    private static void SetEntityId(Entity entity, long id)
    {
        typeof(Entity)
            .GetProperty(nameof(Entity.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetSetMethod(nonPublic: true)!
            .Invoke(entity, [id]);
    }

    private sealed class TestCostCenterRepository : ICostCenterRepository
    {
        public void Add(CostCenter costCenter) => throw new NotSupportedException();

        public Task<CostCenter?> GetByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsOutsideTenantAsync(Guid costCenterId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> CodeExistsAsync(Guid tenantId, string normalizedCode, long? excludingCostCenterId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> ExistsActiveByCodeAsync(Guid tenantId, string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public Task<PagedResponse<CostCenterListItemResponse>> SearchAsync(
            Guid tenantId,
            CostCenterType? type,
            bool? isActive,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CostCenterResponse?> GetResponseByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CostCenterUsageResponse?> GetUsageByIdAsync(Guid costCenterId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasActiveUsageAsync(long costCenterId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IReadOnlyCollection<CostCenterExportRow>> GetExportRowsAsync(
            Guid tenantId,
            CostCenterType? type,
            bool? isActive,
            string? search,
            int? maxRows,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
