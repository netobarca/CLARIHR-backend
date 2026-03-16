using System.Text.RegularExpressions;
using CLARIHR.Application.Abstractions.Auth;
using CLARIHR.Application.Abstractions.Companies;
using CLARIHR.Application.Abstractions.Persistence;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.LegalRepresentatives.Common;
using CLARIHR.Application.Features.Provisioning.Common;
using FluentValidation;
using Microsoft.Extensions.Logging;

namespace CLARIHR.Application.Features.Provisioning;

public sealed record ProvisionCompanyForUserCommand(
    Guid UserId,
    string? CompanyName,
    string CountryCode,
    InitialLegalRepresentativeInput? InitialLegalRepresentative = null) : ICommand<ProvisionCompanyForUserResult>;

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
        RuleFor(command => command.CountryCode)
            .NotEmpty()
            .MaximumLength(3)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Country code must be a 2 or 3 letter ISO-style code.");

        RuleFor(command => command.InitialLegalRepresentative)
            .SetValidator(new InitialLegalRepresentativeInputValidator()!)
            .When(static command => command.InitialLegalRepresentative is not null);
    }

    private static bool BeValidCompanyName(string? companyName) =>
        string.IsNullOrWhiteSpace(companyName) || CompanyNameRegex().IsMatch(companyName.Trim());

    [GeneratedRegex(@"^[\p{L}\p{N}][\p{L}\p{N} '&().-]{0,149}$", RegexOptions.CultureInvariant)]
    private static partial Regex CompanyNameRegex();
}

internal sealed class ProvisionCompanyForUserCommandHandler(
    IUserRepository userRepository,
    IUserCompanyRepository userCompanyRepository,
    ICompanyProvisioningService companyProvisioningService,
    IUnitOfWork unitOfWork,
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

            if (command.InitialLegalRepresentative is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProvisionCompanyForUserResult>.Failure(ProvisioningErrors.InitialLegalRepresentativeRequired);
            }

            var provisioningResult = await companyProvisioningService.ProvisionAsync(
                new ProvisionCompanyRequest(
                    user.PublicId,
                    command.CompanyName,
                    command.CountryCode,
                    command.InitialLegalRepresentative,
                    MakePrimary: true,
                    ProvisioningConstants.FreePlanCode,
                    ProvisionAsInitialCompany: true,
                    CompanyTypeCatalogItemId: null),
                cancellationToken);
            if (provisioningResult.IsFailure)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Result<ProvisionCompanyForUserResult>.Failure(provisioningResult.Error);
            }

            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation(
                "ProvisioningSucceeded for user {UserPublicId} company {CompanyPublicId}",
                user.PublicId,
                provisioningResult.Value.CompanyId);

            return Result<ProvisionCompanyForUserResult>.Success(new ProvisionCompanyForUserResult(
                provisioningResult.Value.CompanyId,
                AlreadyProvisioned: false,
                provisioningResult.Value.PlanCode));
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
}
