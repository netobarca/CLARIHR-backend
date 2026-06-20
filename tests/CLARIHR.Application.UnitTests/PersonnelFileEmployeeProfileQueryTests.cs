using System.Reflection;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Coverage for <see cref="GetPersonnelFileEmployeeProfileQueryHandler"/>. The employee-profile row is
/// created lazily on the first PUT upsert, so a finalized employee that has not had its employment data
/// saved yet legitimately has no profile row. "Not created yet" is a normal empty state, so the read
/// returns a successful result with a null body (HTTP 200, aligned with the sibling list sub-resources
/// that return an empty array) — never an unhandled exception that the API translates into a 500
/// "common.unexpected".
/// </summary>
public sealed class PersonnelFileEmployeeProfileQueryTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Get_WhenCompletedEmployeeHasNoProfileRow_ReturnsSuccessWithNullBody()
    {
        var personnelFile = CreateCompletedEmployee();
        var handler = CreateHandler(personnelFile, employeeProfile: null);

        var result = await handler.Handle(
            new GetPersonnelFileEmployeeProfileQuery(personnelFile.PublicId),
            CancellationToken.None);

        // "Section not created yet" is a normal empty state: 200 OK with a null body, not a 404.
        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Get_WhenProfileExists_ReturnsIt()
    {
        var personnelFile = CreateCompletedEmployee();
        var profile = SampleProfile();
        var handler = CreateHandler(personnelFile, profile);

        var result = await handler.Handle(
            new GetPersonnelFileEmployeeProfileQuery(personnelFile.PublicId),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Same(profile, result.Value);
    }

    [Fact]
    public async Task Get_WhenFileIsNotACompletedEmployee_ReturnsStateRuleViolation()
    {
        // Draft (not finalized) employee: the read precondition must reject it before touching the profile.
        var draftEmployee = CreateEmployee();
        var handler = CreateHandler(draftEmployee, employeeProfile: null);

        var result = await handler.Handle(
            new GetPersonnelFileEmployeeProfileQuery(draftEmployee.PublicId),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.UnprocessableEntity, result.Error.Type);
        Assert.Equal("PERSONNEL_FILE_STATE_RULE_VIOLATION", result.Error.Code);
    }

    private static GetPersonnelFileEmployeeProfileQueryHandler CreateHandler(
        PersonnelFile personnelFile,
        PersonnelFileEmployeeProfileResponse? employeeProfile)
    {
        var personnelFileRepository = ThrowingProxy<IPersonnelFileRepository>.Create(handlers =>
            handlers[nameof(IPersonnelFileRepository.GetForAccessCheckAsync)] =
                _ => Task.FromResult<PersonnelFile?>(personnelFile));

        var employeeRepository = ThrowingProxy<IPersonnelFileEmployeeRepository>.Create(handlers =>
            handlers[nameof(IPersonnelFileEmployeeRepository.GetEmployeeProfileAsync)] =
                _ => Task.FromResult(employeeProfile));

        return new GetPersonnelFileEmployeeProfileQueryHandler(
            new AllowPersonnelFileAuthorizationService(),
            personnelFileRepository,
            employeeRepository,
            new FixedTenantContext(TenantId));
    }

    private static PersonnelFile CreateCompletedEmployee()
    {
        var file = CreateEmployee();
        file.CompleteWithoutLinkedUser();
        return file;
    }

    private static PersonnelFile CreateEmployee()
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Owner",
            new DateTime(1990, 1, 1),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: "ana.owner@corp.test",
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(TenantId);
        return file;
    }

    private static PersonnelFileEmployeeProfileResponse SampleProfile() =>
        new(
            Id: Guid.NewGuid(),
            EmployeeCode: "EMP-0001",
            EmploymentStatusCode: "ACTIVE",
            IsEmploymentActive: true,
            ContractTypeCode: "PERMANENT",
            HireDate: new DateTime(2026, 1, 1),
            RetirementCategoryCode: null,
            RetirementReasonCode: null,
            RetirementNotes: null,
            RetirementDate: null,
            WorkdayCode: null,
            PayrollTypeCode: null,
            OrgUnitId: null,
            WorkCenterId: null,
            CostCenterId: null,
            ContractStartDate: null,
            ContractEndDate: null,
            VacationConfigurationJson: null,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: new DateTime(2026, 1, 1),
            ModifiedAtUtc: null);

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class AllowPersonnelFileAuthorizationService : IPersonnelFileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TENANT_MISMATCH", "Tenant mismatch.", ErrorType.Forbidden);
    }

    /// <summary>
    /// Minimal dynamic stub for a wide repository interface: every member throws unless a handler is
    /// registered by method name. This keeps the test focused on the single method the handler calls
    /// instead of hand-writing ~90 throwing stubs (and re-touching them whenever the interface grows).
    /// Not sealed: <see cref="DispatchProxy"/> generates a runtime subclass of this type.
    /// </summary>
    private class ThrowingProxy<T> : DispatchProxy
        where T : class
    {
        private Dictionary<string, Func<object?[]?, object?>> _handlers = new(StringComparer.Ordinal);

        public static T Create(Action<IDictionary<string, Func<object?[]?, object?>>> configure)
        {
            var proxy = DispatchProxy.Create<T, ThrowingProxy<T>>();
            var handlers = new Dictionary<string, Func<object?[]?, object?>>(StringComparer.Ordinal);
            configure(handlers);
            ((ThrowingProxy<T>)(object)proxy!)._handlers = handlers;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) =>
            targetMethod is not null && _handlers.TryGetValue(targetMethod.Name, out var handler)
                ? handler(args)
                : throw new NotSupportedException(
                    $"{typeof(T).Name}.{targetMethod?.Name} is not configured on this test stub.");
    }
}
