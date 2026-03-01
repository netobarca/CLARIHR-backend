using System.Text;
using System.Text.RegularExpressions;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.IdentityAccess;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Abstractions.Time;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.Provisioning.Common;
using CLARIHR.Domain.Companies;
using CLARIHR.Domain.IdentityAccess;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.Provisioning;

public sealed record ProvisionCompanyForUserCommand(
    Guid UserId,
    string? CompanyName) : ICommand<ProvisionCompanyForUserResult>;

public sealed record ProvisionCompanyForUserResult(
    Guid CompanyId,
    bool AlreadyProvisioned,
    string PlanCode);

internal sealed partial class ProvisionCompanyForUserCommandValidator : AbstractValidator<ProvisionCompanyForUserCommand>
{
    public ProvisionCompanyForUserCommandValidator()
    {
        RuleFor(command => command.UserId)
            .NotEmpty();

        RuleFor(command => command.CompanyName)
            .MaximumLength(150)
            .Must(BeValidCompanyName)
            .WithMessage("Company name contains invalid characters.")
            .When(static command => !string.IsNullOrWhiteSpace(command.CompanyName));
    }

    private static bool BeValidCompanyName(string? companyName) =>
        string.IsNullOrWhiteSpace(companyName) || CompanyNameRegex().IsMatch(companyName.Trim());

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} '&().-]{0,149}$", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyNameRegex();
}

internal sealed class ProvisionCompanyForUserCommandHandler(
    IUserRepository userRepository,
    ICompanyRepository companyRepository,
    ICompanySubscriptionRepository subscriptionRepository,
    IUserCompanyRepository userCompanyRepository,
    IIamAdministrationRepository iamRepository,
    IPlanEntitlementService planEntitlementService,
    IUnitOfWork unitOfWork,
    IDateTimeProvider dateTimeProvider,
    ILogger<ProvisionCompanyForUserCommandHandler> logger) : ICommandHandler<ProvisionCompanyForUserCommand, ProvisionCompanyForUserResult>
{
    public async Task<Result<ProvisionCompanyForUserResult>> Handle(
        ProvisionCompanyForUserCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await userRepository.GetByPublicIdAsync(command.UserId, cancellationToken);
            if (user is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProvisionCompanyForUserResult>.Failure(ProvisioningErrors.UserNotFound);
            }

            logger.LogInformation(
                "ProvisioningStarted for user {UserPublicId}",
                user.PublicId);

            if (await userCompanyRepository.HasPrimaryCompanyAsync(user.Id, cancellationToken))
            {
                var existingCompanyId = await userCompanyRepository.GetPrimaryCompanyPublicIdAsync(user.Id, cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                logger.LogInformation(
                    "ProvisioningSkippedAlreadyProvisioned for user {UserPublicId} company {CompanyPublicId}",
                    user.PublicId,
                    existingCompanyId);

                return Result<ProvisionCompanyForUserResult>.Success(new ProvisionCompanyForUserResult(
                    existingCompanyId ?? Guid.Empty,
                    AlreadyProvisioned: true,
                    ProvisioningConstants.FreePlanCode));
            }

            await planEntitlementService.EnsureFreePlanDefaultsAsync(cancellationToken);

            var companyName = DeriveCompanyName(command.CompanyName, user.FirstName, user.Email);
            var company = Company.Create(companyName, await GenerateUniqueSlugAsync(companyName, cancellationToken), user.PublicId);
            companyRepository.Add(company);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            subscriptionRepository.Add(CompanySubscription.Activate(
                company.Id,
                ProvisioningConstants.FreePlanCode,
                dateTimeProvider.UtcNow));

            var permissions = CreateAdminPermissions(company.PublicId);
            foreach (var permission in permissions)
            {
                iamRepository.AddPermission(permission);
            }

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var adminRole = IamRole.Create(
                ProvisioningConstants.CompanyAdminRoleName,
                "Administrador inicial del tenant.",
                isSystemRole: true);
            adminRole.SetTenantId(company.PublicId);
            adminRole.SyncPermissions(permissions);
            StampTenant(adminRole.PermissionAssignments, company.PublicId);

            var standardRole = IamRole.Create(
                ProvisioningConstants.StandardUserRoleName,
                "Rol base para usuarios del tenant.",
                isSystemRole: true);
            standardRole.SetTenantId(company.PublicId);

            iamRepository.AddRole(adminRole);
            iamRepository.AddRole(standardRole);

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);

            var iamUser = IamUser.CreateLinked(
                user.PublicId,
                user.FirstName,
                user.LastName,
                user.Email,
                isActive: true);
            iamUser.SetTenantId(company.PublicId);
            iamUser.SyncRoles([adminRole]);
            StampTenant(iamUser.RoleAssignments, company.PublicId);

            iamRepository.AddUser(iamUser);
            userCompanyRepository.Add(UserCompanyMembership.Create(user.Id, company.Id, adminRole.Id, isPrimary: true));

            _ = await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "ProvisioningSucceeded for user {UserPublicId} company {CompanyPublicId}",
                user.PublicId,
                company.PublicId);

            return Result<ProvisionCompanyForUserResult>.Success(new ProvisionCompanyForUserResult(
                company.PublicId,
                AlreadyProvisioned: false,
                ProvisioningConstants.FreePlanCode));
        }
        catch (Exception exception)
        {
            await transaction.RollbackAsync(cancellationToken);

            logger.LogError(
                exception,
                "ProvisioningFailed for user {UserPublicId}",
                command.UserId);

            return Result<ProvisionCompanyForUserResult>.Failure(ProvisioningErrors.ProvisioningFailed);
        }
    }

    private async Task<string> GenerateUniqueSlugAsync(string companyName, CancellationToken cancellationToken)
    {
        var baseSlug = CreateSlug(companyName);
        var candidate = baseSlug;

        for (var suffix = 2; suffix <= 100; suffix++)
        {
            if (!await companyRepository.SlugExistsAsync(candidate, cancellationToken))
            {
                return candidate;
            }

            candidate = $"{baseSlug}-{suffix}";
        }

        return $"{baseSlug}-{Guid.NewGuid():N}"[..Math.Min(120, baseSlug.Length + 9)];
    }

    private static string DeriveCompanyName(string? requestedCompanyName, string firstName, string email)
    {
        if (!string.IsNullOrWhiteSpace(requestedCompanyName))
        {
            return requestedCompanyName.Trim();
        }

        var emailDomain = email.Split('@').Skip(1).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(emailDomain) &&
            !emailDomain.Equals("gmail.com", StringComparison.OrdinalIgnoreCase) &&
            !emailDomain.Equals("outlook.com", StringComparison.OrdinalIgnoreCase) &&
            !emailDomain.Equals("hotmail.com", StringComparison.OrdinalIgnoreCase))
        {
            var companySegment = emailDomain.Split('.').FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(companySegment))
            {
                return Capitalize(companySegment);
            }
        }

        return $"Empresa de {firstName.Trim()}";
    }

    private static IReadOnlyList<IamPermission> CreateAdminPermissions(Guid tenantId)
    {
        var permissions = ProvisioningConstants.CompanyAdminPermissions
            .Select(definition =>
            {
                var permission = IamPermission.CreateScreenAction(
                    definition.Code,
                    definition.Name,
                    definition.Description,
                    definition.Module,
                    definition.Screen,
                    definition.Action);
                permission.SetTenantId(tenantId);
                return permission;
            })
            .ToArray();

        return permissions;
    }

    private static void StampTenant<TTenantEntity>(IEnumerable<TTenantEntity> entities, Guid tenantId)
        where TTenantEntity : CLARIHR.Domain.Common.TenantEntity
    {
        foreach (var entity in entities)
        {
            entity.SetTenantId(tenantId);
        }
    }

    private static string Capitalize(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0
            ? "Empresa"
            : char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
    }

    private static string CreateSlug(string value)
    {
        var trimmed = value.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        var previousWasSeparator = false;

        foreach (var character in trimmed)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSeparator = false;
                continue;
            }

            if (builder.Length == 0 || previousWasSeparator)
            {
                continue;
            }

            builder.Append('-');
            previousWasSeparator = true;
        }

        var slug = builder.ToString().Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? "company" : slug;
    }
}
