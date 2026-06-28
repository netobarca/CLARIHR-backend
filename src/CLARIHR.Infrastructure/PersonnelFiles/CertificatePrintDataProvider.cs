using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.Common;
using CLARIHR.Domain.JobProfiles;
using CLARIHR.Domain.PersonnelFiles;
using CLARIHR.Domain.PositionSlots;
using CLARIHR.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CLARIHR.Infrastructure.PersonnelFiles;

/// <summary>
/// Reads the certificate merge data from the expediente (D-16): full name + primary identification, the job
/// title of the active+primary assignment, hire date + computed seniority, and — for salary-printing types
/// (D-20) — the active fixed income (Ingreso/Fijo) concept. Plus the company certificate settings (D-17).
/// Returns null when a required piece is missing (E-17). Salary is never accepted from the client.
/// </summary>
internal sealed class CertificatePrintDataProvider(ApplicationDbContext dbContext) : ICertificatePrintDataProvider
{
    public async Task<CertificatePrintPayload?> BuildAsync(
        Guid personnelFilePublicId,
        Guid tenantId,
        PersonnelFileCertificateRequestResponse request,
        DateTime generatedAtUtc,
        CancellationToken cancellationToken)
    {
        // Materialize the file (FullName is a computed property, not a mapped column — it cannot be projected in SQL).
        var file = await dbContext.Set<PersonnelFile>()
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.PublicId == personnelFilePublicId, cancellationToken);
        if (file is null)
        {
            return null;
        }

        var personnelFileInternalId = file.Id;

        // Active + primary assignment → the plaza/position slot.
        var assignment = await dbContext.Set<PersonnelFileEmploymentAssignment>()
            .AsNoTracking()
            .Where(a => a.PersonnelFileId == personnelFileInternalId && a.IsActive && a.IsPrimary && a.PositionSlotPublicId != null)
            .Select(a => new { a.PositionSlotPublicId })
            .FirstOrDefaultAsync(cancellationToken);
        if (assignment?.PositionSlotPublicId is null)
        {
            return null;
        }

        // Job title: the plaza-specific override, else the job profile title.
        var slot = await dbContext.Set<PositionSlot>()
            .AsNoTracking()
            .Where(s => s.PublicId == assignment.PositionSlotPublicId)
            .Select(s => new { s.Title, s.JobProfileId })
            .FirstOrDefaultAsync(cancellationToken);
        if (slot is null)
        {
            return null;
        }

        var jobTitle = slot.Title;
        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            jobTitle = await dbContext.Set<JobProfile>()
                .AsNoTracking()
                .Where(jp => jp.Id == slot.JobProfileId)
                .Select(jp => jp.Title)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(jobTitle))
        {
            return null;
        }

        var hireDate = await dbContext.Set<PersonnelFileEmployeeProfile>()
            .AsNoTracking()
            .Where(p => p.PersonnelFileId == file.Id)
            .Select(p => (DateTime?)p.HireDate)
            .FirstOrDefaultAsync(cancellationToken);
        if (hireDate is null)
        {
            return null;
        }

        var seniority = EmployeeSeniority.Between(hireDate.Value, generatedAtUtc);

        var identification = await dbContext.Set<PersonnelFileIdentification>()
            .AsNoTracking()
            .Where(i => i.PersonnelFileId == file.Id && i.IsPrimary)
            .Select(i => new { i.IdentificationType, i.IdentificationNumber })
            .FirstOrDefaultAsync(cancellationToken);

        decimal? monthlySalary = null;
        string? currencyCode = null;
        if (CLARIHR.Domain.PersonnelFiles.CertificateTypes.PrintsSalary.Contains(request.CertificateTypeCode.Trim().ToUpperInvariant()))
        {
            // The current monthly base salary = the highest active fixed income (Ingreso/Fijo) concept.
            var salaryConcept = await dbContext.Set<PersonnelFileCompensationConcept>()
                .AsNoTracking()
                .Where(c => c.PersonnelFileId == file.Id
                    && c.IsActive
                    && c.Nature == CompensationNature.Ingreso
                    && c.CalculationType == CompensationCalculationType.Fixed)
                .OrderByDescending(c => c.Value)
                .Select(c => new { c.Value, c.CurrencyCode })
                .FirstOrDefaultAsync(cancellationToken);
            if (salaryConcept is null)
            {
                // A salary-printing certificate (salario/embajada) requires a salary value (E-17).
                return null;
            }

            monthlySalary = salaryConcept.Value;
            currencyCode = salaryConcept.CurrencyCode;
        }

        var settings = await dbContext.Set<CompanyCertificateSettings>()
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new { s.LogoFilePublicId, s.IssuingCity, s.SignatoryName, s.SignatoryTitle, s.FooterText })
            .FirstOrDefaultAsync(cancellationToken);

        return new CertificatePrintPayload(
            request.CertificateTypeCode,
            request.LanguageCode,
            request.AddressedTo,
            request.Copies,
            file.FullName,
            identification?.IdentificationType,
            identification?.IdentificationNumber,
            jobTitle!,
            hireDate.Value,
            seniority.Years,
            seniority.Months,
            monthlySalary,
            currencyCode,
            settings?.LogoFilePublicId,
            settings?.IssuingCity,
            settings?.SignatoryName,
            settings?.SignatoryTitle,
            settings?.FooterText,
            generatedAtUtc);
    }
}
