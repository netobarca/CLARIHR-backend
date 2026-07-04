using System.Reflection;
using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Tenancy;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Auth;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Coverage for the institutional-email edit on <see cref="UpdatePersonnelFileEmployeeProfileCommandHandler"/>.
/// The institutional email is the employee's sign-in account identifier, so editing it from the
/// employment-information section is applied to BOTH the personnel file and the linked login account, is
/// rejected when the email already belongs to another account, and is a no-op when omitted.
/// </summary>
public sealed class PersonnelFileEmployeeProfileEmailChangeTests
{
    private static readonly Guid TenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid LinkedUserPublicId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

    [Fact]
    public async Task Update_WithNewInstitutionalEmail_ChangesFileAndResyncsLinkedAccount()
    {
        var personnelFile = CreateCompletedEmployee("old.email@corp.test", LinkedUserPublicId);
        var linkedUser = User.RegisterLocal("Ana", "Owner", "old.email@corp.test", "hash", "SV", "test");

        var handler = CreateHandler(personnelFile, userByEmail: null, linkedUser: linkedUser);

        var result = await handler.Handle(
            UpdateCommand(personnelFile.PublicId, institutionalEmail: "new.email@corp.test"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Record side: the file (and the enriched response) carry the new email.
        Assert.Equal("new.email@corp.test", result.Value!.InstitutionalEmail);
        Assert.Equal("new.email@corp.test", personnelFile.InstitutionalEmail);
        // Login side: the linked account's sign-in email is re-synced so the employee keeps signing in.
        Assert.Equal("new.email@corp.test", linkedUser.Email);
        Assert.Equal("new.email@corp.test", linkedUser.NormalizedEmail);
    }

    [Fact]
    public async Task Update_WhenInstitutionalEmailBelongsToAnotherAccount_ReturnsConflictAndLeavesEmailUnchanged()
    {
        var personnelFile = CreateCompletedEmployee("old.email@corp.test", LinkedUserPublicId);
        // A different account already owns the requested email (its PublicId is not the file's linked user).
        var otherAccount = User.RegisterLocal("Other", "Person", "taken@corp.test", "hash", "SV", "test");

        var handler = CreateHandler(personnelFile, userByEmail: otherAccount, linkedUser: null);

        var result = await handler.Handle(
            UpdateCommand(personnelFile.PublicId, institutionalEmail: "taken@corp.test"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(ErrorType.Conflict, result.Error.Type);
        Assert.Equal("PERSONNEL_FILE_LINKED_USER_CONFLICT", result.Error.Code);
        // The conflict is detected before any mutation: the file keeps its original email.
        Assert.Equal("old.email@corp.test", personnelFile.InstitutionalEmail);
    }

    [Fact]
    public async Task Update_WhenInstitutionalEmailOmitted_LeavesItUnchangedWithoutTouchingAccounts()
    {
        var personnelFile = CreateCompletedEmployee("old.email@corp.test", LinkedUserPublicId);

        // The user repository is left unconfigured: any call to it throws (ThrowingProxy), so a successful
        // result proves the email path was skipped entirely when no email was supplied.
        var handler = CreateHandler(personnelFile, userByEmail: null, linkedUser: null, configureUserRepository: false);

        var result = await handler.Handle(
            UpdateCommand(personnelFile.PublicId, institutionalEmail: null),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("old.email@corp.test", result.Value!.InstitutionalEmail);
        Assert.Equal("old.email@corp.test", personnelFile.InstitutionalEmail);
    }

    private static UpdatePersonnelFileEmployeeProfileCommandHandler CreateHandler(
        PersonnelFile personnelFile,
        User? userByEmail,
        User? linkedUser,
        bool configureUserRepository = true)
    {
        var personnelFileRepository = ThrowingProxy<IPersonnelFileRepository>.Create(handlers =>
        {
            handlers[nameof(IPersonnelFileRepository.GetForAccessCheckAsync)] =
                _ => Task.FromResult<PersonnelFile?>(personnelFile);
            handlers[nameof(IPersonnelFileRepository.CatalogCodeIsActiveAsync)] =
                _ => Task.FromResult(true);
        });

        var employeeRepository = ThrowingProxy<IPersonnelFileEmployeeRepository>.Create(handlers =>
        {
            // No persisted profile yet: the 1:1 section is created on this first PUT (no concurrency check).
            handlers[nameof(IPersonnelFileEmployeeRepository.GetEmployeeProfileAsync)] =
                _ => Task.FromResult<PersonnelFileEmployeeProfileResponse?>(null);
            handlers[nameof(IPersonnelFileEmployeeRepository.UpsertEmployeeProfileAsync)] =
                _ => Task.FromResult(SampleProfile());
        });

        var userRepository = ThrowingProxy<IUserRepository>.Create(handlers =>
        {
            if (!configureUserRepository)
            {
                return;
            }

            handlers[nameof(IUserRepository.GetByEmailAsync)] =
                _ => Task.FromResult(userByEmail);
            handlers[nameof(IUserRepository.GetByPublicIdAsync)] =
                _ => Task.FromResult(linkedUser);
        });

        return new UpdatePersonnelFileEmployeeProfileCommandHandler(
            new AllowPersonnelFileAuthorizationService(),
            personnelFileRepository,
            employeeRepository,
            userRepository,
            new NoOpAuditService(),
            new FixedTenantContext(TenantId),
            new FixedDateTimeProvider(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)),
            new TestUnitOfWork());
    }

    private static UpdatePersonnelFileEmployeeProfileCommand UpdateCommand(Guid personnelFileId, string? institutionalEmail) =>
        new(
            PersonnelFileId: personnelFileId,
            EmployeeCode: "EMP-0001",
            EmploymentStatusCode: "ACTIVO",
            HireDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ConcurrencyToken: Guid.NewGuid(),
            InstitutionalEmail: institutionalEmail);

    private static PersonnelFile CreateCompletedEmployee(string institutionalEmail, Guid linkedUserPublicId)
    {
        var file = PersonnelFile.Create(
            PersonnelFileRecordType.Employee,
            "Ana",
            "Owner",
            new DateTime(1990, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            maritalStatus: null,
            profession: null,
            nationality: null,
            personalEmail: null,
            institutionalEmail: institutionalEmail,
            personalPhone: null,
            institutionalPhone: null,
            birthCountry: null,
            birthDepartment: null,
            birthMunicipality: null,
            photoFilePublicId: null,
            orgUnitPublicId: null);
        file.SetTenantId(TenantId);
        file.Complete(linkedUserPublicId);
        return file;
    }

    private static PersonnelFileEmployeeProfileResponse SampleProfile() =>
        new(
            Id: Guid.NewGuid(),
            EmployeeCode: "EMP-0001",
            EmploymentStatusCode: "ACTIVO",
            // Overwritten by the handler's enricher with the parent file's institutional email.
            InstitutionalEmail: null,
            HireDate: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            Seniority: EmployeeSeniority.None,
            RetirementCategoryCode: null,
            RetirementReasonCode: null,
            RetirementNotes: null,
            RetirementDate: null,
            VacationDaysAvailable: null,
            DisabilityDaysAvailable: null,
            ConcurrencyToken: Guid.NewGuid(),
            CreatedAtUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ModifiedAtUtc: null);

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = utcNow;
    }

    private sealed class NoOpAuditService : IAuditService
    {
        public Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task LogForTenantAsync(Guid tenantId, AuditLogEntry entry, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AllowPersonnelFileAuthorizationService : IPersonnelFileAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(Result.Success());

        public Task<bool> HasRehireAuthorizationAsync(Guid companyId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Error TenantMismatch(RbacPermissionAction action) =>
            new("TENANT_MISMATCH", "Tenant mismatch.", ErrorType.Forbidden);
    }

    /// <summary>
    /// Minimal dynamic stub for a wide repository interface: every member throws unless a handler is
    /// registered by method name, keeping each test focused on the handful of methods the handler calls.
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
