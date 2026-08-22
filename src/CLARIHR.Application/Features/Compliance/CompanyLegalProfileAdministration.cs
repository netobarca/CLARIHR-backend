using CLARIHR.Application.Abstractions.Auditing;
using CLARIHR.Application.Abstractions.Compliance;
using CLARIHR.Application.Abstractions.LegalRepresentatives;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Preferences;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Common.Policies;
using CLARIHR.Application.Features.Audit.Common;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Compliance;
using System.Linq.Expressions;
using FluentValidation;
using System.Text.RegularExpressions;

namespace CLARIHR.Application.Features.Compliance;

/// <summary>
/// 00001 / B-01 — expone <c>allowedActions</c> para que el cliente no tenga que replicar los códigos de
/// permiso a mano. Sin esto, el frontend mostraba <i>Guardar cambios</i> habilitado a quien sólo tiene
/// <c>CompanyPreferences.Read</c>, y el guard de la ruta pedía un permiso equivocado (F-01).
/// </summary>
public sealed record CompanyLegalProfileResponse(
    Guid Id,
    string LegalName,
    string EmployerNitNumber,
    string IsssEmployerRegistrationNumber,
    string FiscalAddress,
    string? EconomicActivityDescription,
    Guid? LegalRepresentativePublicId,
    Guid ConcurrencyToken,
    DateTime CreatedAtUtc,
    DateTime? ModifiedAtUtc,
    AllowedActionsResponse? AllowedActions = null) : ISupportsAllowedActions;

public sealed record GetCompanyLegalProfileQuery(Guid CompanyId) : IQuery<CompanyLegalProfileResponse>;

public sealed record CreateCompanyLegalProfileCommand(
    Guid CompanyId,
    string LegalName,
    string EmployerNitNumber,
    string IsssEmployerRegistrationNumber,
    string FiscalAddress,
    string? EconomicActivityDescription,
    Guid? LegalRepresentativePublicId) : ICommand<CompanyLegalProfileResponse>;

public sealed record UpdateCompanyLegalProfileCommand(
    Guid CompanyId,
    string LegalName,
    string EmployerNitNumber,
    string IsssEmployerRegistrationNumber,
    string FiscalAddress,
    string? EconomicActivityDescription,
    Guid? LegalRepresentativePublicId,
    Guid ConcurrencyToken) : ICommand<CompanyLegalProfileResponse>;

/// <summary>
/// Format of the employer's fiscal identifiers (El Salvador). Public so the API contract and the tests
/// state the same rule; the frontend integration guide quotes these patterns verbatim.
/// </summary>
public static partial class CompanyLegalProfileValidation
{
    /// <summary>NIT of a juridical person: <c>####-######-###-#</c>.</summary>
    public const string EmployerNitPattern = @"^\d{4}-\d{6}-\d{3}-\d$";

    /// <summary>
    /// ISSS employer registration number. Deliberately loose — the layout varies with the year of
    /// registration, so only the alphabet (digits and dashes) and a sane length are enforced.
    /// </summary>
    public const string IsssEmployerRegistrationPattern = "^[0-9-]{6,20}$";

    // Both matched against the TRIMMED value: the domain trims before storing, so rejecting a pasted
    // trailing space here would be a 400 the caller cannot explain from the stored result.
    public static bool IsEmployerNit(string? value) =>
        value is not null && EmployerNitRegex().IsMatch(value.Trim());

    public static bool IsIsssEmployerRegistration(string? value) =>
        value is not null && IsssEmployerRegistrationRegex().IsMatch(value.Trim());

    [GeneratedRegex(EmployerNitPattern, RegexOptions.CultureInvariant)]
    private static partial Regex EmployerNitRegex();

    [GeneratedRegex(IsssEmployerRegistrationPattern, RegexOptions.CultureInvariant)]
    private static partial Regex IsssEmployerRegistrationRegex();
}

public static class CompanyLegalProfileErrors
{
    public static readonly Error NotFound = new(
        "COMPANY_LEGAL_PROFILE_NOT_FOUND",
        "The company does not have a legal profile configured yet.",
        ErrorType.NotFound);

    public static readonly Error AlreadyExists = new(
        "COMPANY_LEGAL_PROFILE_ALREADY_EXISTS",
        "The company already has a legal profile; use the update endpoint instead.",
        ErrorType.Conflict);

    public static readonly Error ConcurrencyConflict = new(
        "COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT",
        "The company legal profile was modified by someone else; reload and try again.",
        ErrorType.Conflict);

    public static readonly Error LegalRepresentativeNotFound = new(
        "COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_NOT_FOUND",
        "The referenced legal representative does not exist in this company.",
        ErrorType.UnprocessableEntity);

    public static readonly Error LegalRepresentativeInactive = new(
        "COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_INACTIVE",
        "The referenced legal representative is inactive and cannot sign the compliance reports.",
        ErrorType.UnprocessableEntity);
}

/// <summary>
/// 00001 / B-02 — las reglas se declaran <b>por comando y con expresiones directas</b>, no a través de
/// accesores lambda compartidos.
/// <para>
/// La versión anterior compartía las seis reglas con una clase base que recibía <c>Func&lt;TCommand, string&gt;</c>
/// y las aplicaba como <c>RuleFor(command =&gt; legalName(command))</c>. FluentValidation deriva el nombre del
/// campo <b>analizando la expresión</b> del <c>RuleFor</c>, y de una lambda <i>invocada</i> no hay propiedad que
/// derivar: emitía todos los mensajes bajo la clave <c>""</c>. El cliente recibía una lista de textos sin saber
/// a qué input pertenecía cada uno.
/// </para>
/// <para>
/// Se eligió duplicar las reglas en vez de conservar la base con <c>.WithName("legalName")</c> a mano: son dos
/// comandos con los mismos campos, y un nombre escrito como literal se desincroniza en silencio el día que la
/// propiedad se renombre. Con la expresión directa el nombre sale del compilador.
/// </para>
/// </summary>
internal static class CompanyLegalProfileRules
{
    internal static void ApplyEmployerNitRule<TCommand>(
        AbstractValidator<TCommand> validator,
        Expression<Func<TCommand, string>> selector) =>
        validator.RuleFor(selector)
            .NotEmpty()
            .MaximumLength(20)
            .Must(CompanyLegalProfileValidation.IsEmployerNit)
            .WithMessage("Employer NIT must follow the ####-######-###-# format.");

    internal static void ApplyIsssRule<TCommand>(
        AbstractValidator<TCommand> validator,
        Expression<Func<TCommand, string>> selector) =>
        validator.RuleFor(selector)
            .NotEmpty()
            .MaximumLength(20)
            .Must(CompanyLegalProfileValidation.IsIsssEmployerRegistration)
            .WithMessage("ISSS employer registration number accepts digits and dashes only.");
}

internal sealed class GetCompanyLegalProfileQueryValidator : AbstractValidator<GetCompanyLegalProfileQuery>
{
    public GetCompanyLegalProfileQueryValidator() => RuleFor(query => query.CompanyId).NotEmpty();
}

internal sealed class CreateCompanyLegalProfileCommandValidator
    : AbstractValidator<CreateCompanyLegalProfileCommand>
{
    public CreateCompanyLegalProfileCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.LegalName).NotEmpty().MaximumLength(200);
        CompanyLegalProfileRules.ApplyEmployerNitRule(this, command => command.EmployerNitNumber);
        CompanyLegalProfileRules.ApplyIsssRule(this, command => command.IsssEmployerRegistrationNumber);
        RuleFor(command => command.FiscalAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.EconomicActivityDescription).MaximumLength(200);
    }
}

internal sealed class UpdateCompanyLegalProfileCommandValidator
    : AbstractValidator<UpdateCompanyLegalProfileCommand>
{
    public UpdateCompanyLegalProfileCommandValidator()
    {
        RuleFor(command => command.CompanyId).NotEmpty();
        RuleFor(command => command.LegalName).NotEmpty().MaximumLength(200);
        CompanyLegalProfileRules.ApplyEmployerNitRule(this, command => command.EmployerNitNumber);
        CompanyLegalProfileRules.ApplyIsssRule(this, command => command.IsssEmployerRegistrationNumber);
        RuleFor(command => command.FiscalAddress).NotEmpty().MaximumLength(500);
        RuleFor(command => command.EconomicActivityDescription).MaximumLength(200);
        RuleFor(command => command.ConcurrencyToken).NotEmpty();
    }
}

internal sealed class GetCompanyLegalProfileQueryHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyLegalProfileRepository repository)
    : IQueryHandler<GetCompanyLegalProfileQuery, CompanyLegalProfileResponse>
{
    public async Task<Result<CompanyLegalProfileResponse>> Handle(
        GetCompanyLegalProfileQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanReadAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyLegalProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByTenantIdAsync(query.CompanyId, cancellationToken);
        return profile is null
            ? Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.NotFound)
            : Result<CompanyLegalProfileResponse>.Success(CompanyLegalProfileAdministrationHelpers.Map(profile));
    }
}

internal sealed class CreateCompanyLegalProfileCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyLegalProfileRepository repository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<CreateCompanyLegalProfileCommand, CompanyLegalProfileResponse>
{
    public async Task<Result<CompanyLegalProfileResponse>> Handle(
        CreateCompanyLegalProfileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyLegalProfileResponse>.Failure(authorizationResult.Error);
        }

        var existing = await repository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (existing is not null)
        {
            return Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.AlreadyExists);
        }

        var representativeError = await CompanyLegalProfileAdministrationHelpers.ResolveLegalRepresentativeErrorAsync(
            command.LegalRepresentativePublicId,
            legalRepresentativeRepository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (representativeError is not null)
        {
            return Result<CompanyLegalProfileResponse>.Failure(representativeError);
        }

        var profile = CompanyLegalProfile.Create(
            command.LegalName,
            command.EmployerNitNumber,
            command.IsssEmployerRegistrationNumber,
            command.FiscalAddress,
            command.EconomicActivityDescription,
            command.LegalRepresentativePublicId);
        profile.SetTenantId(command.CompanyId);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            repository.Add(profile);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CompanyLegalProfileAdministrationHelpers.Map(profile);
            await auditService.LogForTenantAsync(
                command.CompanyId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyLegalProfileSaved,
                    AuditEntityTypes.CompanyLegalProfile,
                    profile.PublicId,
                    after.LegalName,
                    AuditActions.Create,
                    $"Created the company legal profile ({after.LegalName}, NIT {after.EmployerNitNumber}).",
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyLegalProfileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal sealed class UpdateCompanyLegalProfileCommandHandler(
    ICompanyPreferenceAuthorizationService authorizationService,
    ICompanyLegalProfileRepository repository,
    ILegalRepresentativeRepository legalRepresentativeRepository,
    IAuditService auditService,
    IUnitOfWork unitOfWork)
    : ICommandHandler<UpdateCompanyLegalProfileCommand, CompanyLegalProfileResponse>
{
    public async Task<Result<CompanyLegalProfileResponse>> Handle(
        UpdateCompanyLegalProfileCommand command,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanManageAsync(command.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<CompanyLegalProfileResponse>.Failure(authorizationResult.Error);
        }

        var profile = await repository.GetByTenantIdAsync(command.CompanyId, cancellationToken);
        if (profile is null)
        {
            return Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.NotFound);
        }

        if (profile.ConcurrencyToken != command.ConcurrencyToken)
        {
            return Result<CompanyLegalProfileResponse>.Failure(CompanyLegalProfileErrors.ConcurrencyConflict);
        }

        var representativeError = await CompanyLegalProfileAdministrationHelpers.ResolveLegalRepresentativeErrorAsync(
            command.LegalRepresentativePublicId,
            legalRepresentativeRepository,
            authorizationService,
            RbacPermissionAction.Update,
            cancellationToken);
        if (representativeError is not null)
        {
            return Result<CompanyLegalProfileResponse>.Failure(representativeError);
        }

        var before = CompanyLegalProfileAdministrationHelpers.Map(profile);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            profile.Update(
                command.LegalName,
                command.EmployerNitNumber,
                command.IsssEmployerRegistrationNumber,
                command.FiscalAddress,
                command.EconomicActivityDescription,
                command.LegalRepresentativePublicId);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var after = CompanyLegalProfileAdministrationHelpers.Map(profile);
            await auditService.LogForTenantAsync(
                command.CompanyId,
                new AuditLogEntry(
                    AuditEventTypes.CompanyLegalProfileSaved,
                    AuditEntityTypes.CompanyLegalProfile,
                    profile.PublicId,
                    after.LegalName,
                    AuditActions.Update,
                    $"Updated the company legal profile ({after.LegalName}, NIT {after.EmployerNitNumber}).",
                    Before: before,
                    After: after),
                cancellationToken);
            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return Result<CompanyLegalProfileResponse>.Success(after);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}

internal static class CompanyLegalProfileAdministrationHelpers
{
    /// <summary>
    /// Resolves the optional legal representative link. <see cref="LegalRepresentative"/> is a
    /// <c>TenantEntity</c>: the same person representing several companies exists as one row PER company
    /// (the document uniqueness index is scoped by tenant), so a representative of another company must
    /// never be linkable here — it would print a foreign signatory on the F-14. <c>GetByIdAsync</c> is
    /// subject to the global tenant filter; <c>ExistsOutsideTenantAsync</c> bypasses it on purpose so a
    /// cross-tenant id reports a tenant mismatch instead of a plain not-found (same shape as
    /// <c>DisciplinaryActionCauses</c> and <c>PositionSlotAdministration</c>).
    /// </summary>
    public static async Task<Error?> ResolveLegalRepresentativeErrorAsync(
        Guid? legalRepresentativePublicId,
        ILegalRepresentativeRepository legalRepresentativeRepository,
        ICompanyPreferenceAuthorizationService authorizationService,
        RbacPermissionAction action,
        CancellationToken cancellationToken)
    {
        if (legalRepresentativePublicId is not { } representativeId)
        {
            return null;
        }

        var representative = await legalRepresentativeRepository.GetByIdAsync(representativeId, cancellationToken);
        if (representative is null)
        {
            return await legalRepresentativeRepository.ExistsOutsideTenantAsync(representativeId, cancellationToken)
                ? authorizationService.TenantMismatch(action)
                : CompanyLegalProfileErrors.LegalRepresentativeNotFound;
        }

        // An explicitly deactivated representative would still be printed on the compliance reports.
        // Effective dates are deliberately NOT checked: a future appointment date and a backdated record
        // are both legitimate.
        return representative.IsActive ? null : CompanyLegalProfileErrors.LegalRepresentativeInactive;
    }

    public static CompanyLegalProfileResponse Map(CompanyLegalProfile profile) =>
        new(
            profile.PublicId,
            profile.LegalName,
            profile.EmployerNitNumber,
            profile.IsssEmployerRegistrationNumber,
            profile.FiscalAddress,
            profile.EconomicActivityDescription,
            profile.LegalRepresentativePublicId,
            profile.ConcurrencyToken,
            profile.CreatedUtc,
            profile.ModifiedUtc);
}
