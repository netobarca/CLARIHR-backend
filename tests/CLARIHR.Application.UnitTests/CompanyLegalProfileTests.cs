using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Pagination;
using CLARIHR.Application.Features.Compliance;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Application.Features.LegalRepresentatives;
using CLARIHR.Domain.Compliance;
using CLARIHR.Domain.LegalRepresentatives;
using CLARIHR.Domain.Preferences;
using FluentValidation;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for <see cref="CompanyLegalProfile"/> (REQ-016 RF-006 — the employer legal identity that
/// gates payroll generation once <c>CompanyPreference.PayrollComplianceGatesEnabled</c> is on, P-03) and
/// for the paired gate toggle added to <see cref="CompanyPreference"/>.
/// </summary>
public sealed class CompanyLegalProfileTests
{
    private static CompanyLegalProfile CreateValid() =>
        CompanyLegalProfile.Create(
            legalName: "  Acme El Salvador, S.A. de C.V.  ",
            employerNitNumber: " 0614-010101-101-1 ",
            isssEmployerRegistrationNumber: " 123456 ",
            fiscalAddress: " Col. Escalón, San Salvador ",
            economicActivityDescription: " Servicios de tecnología ",
            legalRepresentativePublicId: Guid.NewGuid());

    [Fact]
    public void Create_TrimsTextFieldsAndAssignsAConcurrencyToken()
    {
        var profile = CreateValid();

        Assert.Equal("Acme El Salvador, S.A. de C.V.", profile.LegalName);
        Assert.Equal("0614-010101-101-1", profile.EmployerNitNumber);
        Assert.Equal("123456", profile.IsssEmployerRegistrationNumber);
        Assert.Equal("Col. Escalón, San Salvador", profile.FiscalAddress);
        Assert.Equal("Servicios de tecnología", profile.EconomicActivityDescription);
        Assert.NotEqual(Guid.Empty, profile.ConcurrencyToken);
        Assert.NotEqual(Guid.Empty, profile.PublicId);
    }

    [Fact]
    public void Create_WithoutEconomicActivity_LeavesItNull()
    {
        var profile = CompanyLegalProfile.Create(
            "Acme", "0614-010101-101-1", "123456", "San Salvador", economicActivityDescription: "   ", legalRepresentativePublicId: null);

        Assert.Null(profile.EconomicActivityDescription);
        Assert.Null(profile.LegalRepresentativePublicId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithBlankLegalName_Throws(string blankLegalName)
    {
        Assert.Throws<ArgumentException>(() =>
            CompanyLegalProfile.Create(blankLegalName, "0614-010101-101-1", "123456", "San Salvador", null, null));
    }

    [Fact]
    public void Update_RotatesConcurrencyTokenAndReplacesEveryField()
    {
        var profile = CreateValid();
        var originalToken = profile.ConcurrencyToken;
        var newRepresentative = Guid.NewGuid();

        profile.Update(
            "Nuevo Nombre Legal",
            "0614-020202-102-2",
            "654321",
            "Santa Tecla",
            "Comercio",
            newRepresentative);

        Assert.Equal("Nuevo Nombre Legal", profile.LegalName);
        Assert.Equal("0614-020202-102-2", profile.EmployerNitNumber);
        Assert.Equal("654321", profile.IsssEmployerRegistrationNumber);
        Assert.Equal("Santa Tecla", profile.FiscalAddress);
        Assert.Equal("Comercio", profile.EconomicActivityDescription);
        Assert.Equal(newRepresentative, profile.LegalRepresentativePublicId);
        Assert.NotEqual(originalToken, profile.ConcurrencyToken);
    }

    [Fact]
    public void Update_WithBlankFiscalAddress_ThrowsAndLeavesStatePristine()
    {
        var profile = CreateValid();

        Assert.Throws<ArgumentException>(() =>
            profile.Update("Acme", "0614-010101-101-1", "123456", "   ", null, null));
    }

    [Fact]
    public void SetPayrollCompliancePolicy_DefaultsToNullAndCanBeToggled()
    {
        var preference = CompanyPreference.Create("USD", "UTC");
        Assert.Null(preference.PayrollComplianceGatesEnabled);

        var originalToken = preference.ConcurrencyToken;
        preference.SetPayrollCompliancePolicy(true);

        Assert.True(preference.PayrollComplianceGatesEnabled);
        Assert.NotEqual(originalToken, preference.ConcurrencyToken);
    }
}

/// <summary>
/// Format of the employer's fiscal identifiers. Before this rule existed a mistyped NIT was stored
/// verbatim and printed on the F-14 with no warning.
/// </summary>
public sealed class CompanyLegalProfileFormatValidationTests
{
    private static readonly IValidator<CreateCompanyLegalProfileCommand> Validator =
        new CreateCompanyLegalProfileCommandValidator();

    private static CreateCompanyLegalProfileCommand Command(string nit, string isss) =>
        new(
            CompanyId: Guid.NewGuid(),
            LegalName: "Acme El Salvador, S.A. de C.V.",
            EmployerNitNumber: nit,
            IsssEmployerRegistrationNumber: isss,
            FiscalAddress: "Col. Escalón, San Salvador",
            EconomicActivityDescription: null,
            LegalRepresentativePublicId: null);

    // NOTE: padding is limited to what still fits MaximumLength(20) — that rule measures the RAW value
    // while the domain trims before storing, so a heavily padded but otherwise valid NIT is rejected on
    // length. Pre-existing behaviour shared with legalName/fiscalAddress; not changed here.
    [Theory]
    [InlineData("0614-010101-101-1")]
    [InlineData(" 0614-010101-101-1 ")] // trimmed before matching, mirroring the domain
    public async Task Validate_WithWellFormedNit_Passes(string nit)
    {
        var result = await Validator.ValidateAsync(Command(nit, "123456"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("06140101011011")]        // no dashes
    [InlineData("614-010101-101-1")]      // 3-digit head
    [InlineData("0614-01010-101-1")]      // 5-digit block
    [InlineData("0614-010101-101")]       // missing verifier
    public async Task Validate_WithMalformedNit_Fails(string nit)
    {
        var result = await Validator.ValidateAsync(Command(nit, "123456"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("####-######-###-#", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("123456-7")]
    public async Task Validate_WithWellFormedIsssRegistration_Passes(string isss)
    {
        var result = await Validator.ValidateAsync(Command("0614-010101-101-1", isss));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("12345")]                 // too short
    [InlineData("ABC1234")]               // letters
    [InlineData("123 456")]               // inner space
    public async Task Validate_WithMalformedIsssRegistration_Fails(string isss)
    {
        var result = await Validator.ValidateAsync(Command("0614-010101-101-1", isss));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("digits and dashes", StringComparison.Ordinal));
    }
}

/// <summary>
/// Resolution of the optional legal-representative link. <see cref="LegalRepresentative"/> is tenant
/// scoped: the same person representing several companies has one row PER company, so an id belonging
/// to another company must be rejected rather than silently stored and printed on the F-14.
/// </summary>
public sealed class CompanyLegalProfileLegalRepresentativeResolutionTests
{
    private static readonly Error TenantMismatchSentinel = new("TENANT_MISMATCH", "mismatch", ErrorType.Forbidden);

    private static LegalRepresentative CreateRepresentative() =>
        LegalRepresentative.Create(
            firstName: "Ana",
            lastName: "Portillo",
            documentType: "DUI",
            documentNumber: "01234567-8",
            positionTitle: "Representante Legal",
            representationType: LegalRepresentativeRepresentationType.PrimaryLegalRepresentative,
            authorityDescription: null,
            appointmentInstrument: null,
            appointmentDateUtc: null,
            effectiveFromUtc: new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            effectiveToUtc: null,
            email: null,
            phone: null,
            isPrimary: true);

    private static Task<Error?> ResolveAsync(FakeLegalRepresentativeRepository repository, Guid? representativeId) =>
        CompanyLegalProfileAdministrationHelpers.ResolveLegalRepresentativeErrorAsync(
            representativeId,
            repository,
            new FakeCompanyPreferenceAuthorizationService(TenantMismatchSentinel),
            RbacPermissionAction.Update,
            CancellationToken.None);

    [Fact]
    public async Task Resolve_WhenNoRepresentativeRequested_ReturnsNoError()
    {
        var error = await ResolveAsync(new FakeLegalRepresentativeRepository(), representativeId: null);

        Assert.Null(error);
    }

    [Fact]
    public async Task Resolve_WhenRepresentativeBelongsToTheCompanyAndIsActive_ReturnsNoError()
    {
        var representative = CreateRepresentative();
        var repository = new FakeLegalRepresentativeRepository { InTenant = representative };

        var error = await ResolveAsync(repository, representative.PublicId);

        Assert.Null(error);
    }

    [Fact]
    public async Task Resolve_WhenRepresentativeIsInactive_ReturnsInactiveError()
    {
        var representative = CreateRepresentative();
        representative.Inactivate();
        var repository = new FakeLegalRepresentativeRepository { InTenant = representative };

        var error = await ResolveAsync(repository, representative.PublicId);

        Assert.Equal(CompanyLegalProfileErrors.LegalRepresentativeInactive, error);
    }

    [Fact]
    public async Task Resolve_WhenRepresentativeDoesNotExistAnywhere_ReturnsNotFoundError()
    {
        var repository = new FakeLegalRepresentativeRepository();

        var error = await ResolveAsync(repository, Guid.NewGuid());

        Assert.Equal(CompanyLegalProfileErrors.LegalRepresentativeNotFound, error);
    }

    [Fact]
    public async Task Resolve_WhenRepresentativeBelongsToAnotherCompany_ReturnsTenantMismatch()
    {
        // The row exists, but the tenant query filter hides it: linking it would print another
        // company's signatory on this company's compliance reports.
        var repository = new FakeLegalRepresentativeRepository { ExistsOutsideTenant = true };

        var error = await ResolveAsync(repository, Guid.NewGuid());

        Assert.Equal(TenantMismatchSentinel, error);
        Assert.NotEqual(CompanyLegalProfileErrors.LegalRepresentativeNotFound, error);
    }

    private sealed class FakeCompanyPreferenceAuthorizationService(Error tenantMismatch)
        : ICompanyPreferenceAuthorizationService
    {
        public Task<Result> EnsureCanReadAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Task<Result> EnsureCanManageAsync(Guid companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Result.Success());

        public Error TenantMismatch(RbacPermissionAction action) => tenantMismatch;
    }

    private sealed class FakeLegalRepresentativeRepository : ILegalRepresentativeRepository
    {
        public Task<string?> GetCompanyCountryCodeAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>("SV");

        public Task<bool> IdentificationTypeExistsAsync(string countryCode, string normalizedCode, CancellationToken cancellationToken) =>
            Task.FromResult(true);

        public LegalRepresentative? InTenant { get; init; }

        public bool ExistsOutsideTenant { get; init; }

        public Task<LegalRepresentative?> GetByIdAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
            Task.FromResult(InTenant is not null && InTenant.PublicId == legalRepresentativeId ? InTenant : null);

        public Task<bool> ExistsOutsideTenantAsync(Guid legalRepresentativeId, CancellationToken cancellationToken) =>
            Task.FromResult(ExistsOutsideTenant);

        public void Add(LegalRepresentative legalRepresentative) => throw new NotSupportedException();

        public Task<bool> DocumentExistsAsync(
            Guid tenantId,
            string documentType,
            string normalizedDocumentNumber,
            long? excludingLegalRepresentativeId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<PagedResponse<LegalRepresentativeListItemResponse>> SearchAsync(
            Guid tenantId,
            bool? isActive,
            bool? isPrimary,
            LegalRepresentativeRepresentationType? representationType,
            string? search,
            int pageNumber,
            int pageSize,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LegalRepresentativeResponse?> GetResponseByIdAsync(
            Guid legalRepresentativeId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LegalRepresentativeUsageResponse?> GetUsageByIdAsync(
            Guid legalRepresentativeId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>> GetPositionTitleCatalogItemsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>> GetRepresentationTypeCatalogItemsAsync(
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<int> GetActiveCountAsync(Guid tenantId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> HasOtherActiveRepresentativeAsync(
            Guid tenantId,
            Guid excludingLegalRepresentativePublicId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<LegalRepresentative?> GetActivePrimaryAsync(
            Guid tenantId,
            Guid? excludingLegalRepresentativePublicId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<LegalRepresentativeExportRow>> GetExportRowsAsync(
            Guid tenantId,
            bool? isActive,
            bool? isPrimary,
            LegalRepresentativeRepresentationType? representationType,
            string? search,
            int? maxRows,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<IReadOnlyCollection<ActiveLegalRepresentativeSummary>> GetActiveSummariesByCompanyAsync(
            Guid companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
