using System.Globalization;
using CLARIHR.Application.Abstractions.Payroll;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Application.Abstractions.Reports.Documents;
using CLARIHR.Application.Common.CQRS;
using CLARIHR.Application.Common.Errors;
using CLARIHR.Application.Features.IdentityAccess.Common;
using CLARIHR.Domain.Payroll;
using FluentValidation;

namespace CLARIHR.Application.Features.Payroll;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// REQ-012 §3.7 (PR-8) — payslips ("boletas de pago"): the per-employee document of an AUTHORIZED or
// CLOSED run, rendered through the shared DocumentModel seam (molde SettlementDocumentMapper — the PDF
// engine stays swappable). A GENERADA/ANULADA run has NO slips (the figures are not final — 422); the
// batch download zips the individual PDFs (decision №2). Slips carry ONLY the employee's own lines.
// ─────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>The data of ONE employee's slip: the run header + that employee's lines and sums.</summary>
public sealed record PayrollSlipData(PayrollRunResponse Run, PayrollRunEmployeeLinesResponse Employee);

public sealed record GetPayrollRunEmployeeSlipDataQuery(
    Guid CompanyId,
    Guid PayrollRunId,
    Guid PersonnelFilePublicId) : IQuery<PayrollSlipData>;

/// <summary>The batch: the run header + every employee's lines (the zip renders one PDF per employee).</summary>
public sealed record PayrollRunSlipsData(
    PayrollRunResponse Run,
    IReadOnlyList<PayrollRunEmployeeLinesResponse> Employees);

public sealed record GetPayrollRunSlipsDataQuery(Guid CompanyId, Guid PayrollRunId) : IQuery<PayrollRunSlipsData>;

internal sealed class GetPayrollRunEmployeeSlipDataQueryValidator : AbstractValidator<GetPayrollRunEmployeeSlipDataQuery>
{
    public GetPayrollRunEmployeeSlipDataQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
        RuleFor(query => query.PersonnelFilePublicId).NotEmpty();
    }
}

internal sealed class GetPayrollRunSlipsDataQueryValidator : AbstractValidator<GetPayrollRunSlipsDataQuery>
{
    public GetPayrollRunSlipsDataQueryValidator()
    {
        RuleFor(query => query.CompanyId).NotEmpty();
        RuleFor(query => query.PayrollRunId).NotEmpty();
    }
}

internal sealed class GetPayrollRunEmployeeSlipDataQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<GetPayrollRunEmployeeSlipDataQuery, PayrollSlipData>
{
    public async Task<Result<PayrollSlipData>> Handle(
        GetPayrollRunEmployeeSlipDataQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollSlipData>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollSlipSupport.LoadSlipReadyRunAsync(runRepository, query.CompanyId, query.PayrollRunId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollSlipData>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        var employee = PayrollSlipSupport.ComposeEmployee(run, query.PersonnelFilePublicId);
        if (employee is null)
        {
            return Result<PayrollSlipData>.Failure(PayrollRunReviewErrors.LineNotFound);
        }

        return Result<PayrollSlipData>.Success(new PayrollSlipData(
            await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken),
            employee));
    }
}

internal sealed class GetPayrollRunSlipsDataQueryHandler(
    IPersonnelFileAuthorizationService authorizationService,
    IPayrollRunRepository runRepository)
    : IQueryHandler<GetPayrollRunSlipsDataQuery, PayrollRunSlipsData>
{
    public async Task<Result<PayrollRunSlipsData>> Handle(
        GetPayrollRunSlipsDataQuery query,
        CancellationToken cancellationToken)
    {
        var authorizationResult = await authorizationService.EnsureCanViewPayrollRunsAsync(query.CompanyId, cancellationToken);
        if (authorizationResult.IsFailure)
        {
            return Result<PayrollRunSlipsData>.Failure(authorizationResult.Error);
        }

        var loaded = await PayrollSlipSupport.LoadSlipReadyRunAsync(runRepository, query.CompanyId, query.PayrollRunId, cancellationToken);
        if (loaded.IsFailure)
        {
            return Result<PayrollRunSlipsData>.Failure(loaded.Error);
        }

        var run = loaded.Value;
        var employees = run.Lines
            .Select(line => line.EmployeePublicId)
            .Distinct()
            .Select(filePublicId => PayrollSlipSupport.ComposeEmployee(run, filePublicId)!)
            .OrderBy(employee => employee.EmployeeName, StringComparer.Ordinal)
            .ToList();

        return Result<PayrollRunSlipsData>.Success(new PayrollRunSlipsData(
            await PayrollRunReviewSupport.ToResponseAsync(runRepository, run, cancellationToken),
            employees));
    }
}

internal static class PayrollSlipSupport
{
    /// <summary>Slips exist only for FINAL figures: an AUTORIZADA or CERRADA run (422 otherwise).</summary>
    public static async Task<Result<PayrollRun>> LoadSlipReadyRunAsync(
        IPayrollRunRepository runRepository,
        Guid companyId,
        Guid payrollRunId,
        CancellationToken cancellationToken)
    {
        var loaded = await PayrollRunReviewSupport.LoadAsync(
            runRepository, companyId, payrollRunId, RbacPermissionAction.Read, cancellationToken);
        if (loaded.IsFailure)
        {
            return loaded;
        }

        return loaded.Value.StatusCode is PayrollRunStatuses.Autorizada or PayrollRunStatuses.Cerrada
            ? loaded
            : Result<PayrollRun>.Failure(PayrollRunErrors.StateRuleViolation);
    }

    public static PayrollRunEmployeeLinesResponse? ComposeEmployee(PayrollRun run, Guid personnelFilePublicId)
    {
        var lines = run.Lines
            .Where(line => line.EmployeePublicId == personnelFilePublicId)
            .OrderBy(line => line.SortOrder)
            .ThenBy(line => line.PublicId)
            .ToArray();
        if (lines.Length == 0)
        {
            return null;
        }

        var income = lines.Where(line => line is { LineClass: PayrollLineClasses.Ingreso, IsIncluded: true }).Sum(line => line.FinalAmount);
        var deductions = lines.Where(line => line is { LineClass: PayrollLineClasses.Descuento, IsIncluded: true }).Sum(line => line.FinalAmount);

        return new PayrollRunEmployeeLinesResponse(
            run.PublicId,
            personnelFilePublicId,
            lines[0].EmployeeName,
            income,
            deductions,
            income - deductions,
            lines.Select(PayrollRunReviewSupport.ToLineResponse).ToArray());
    }
}

/// <summary>
/// Maps one employee's slice of a run to the format-agnostic <see cref="DocumentModel"/> — the boleta de
/// pago (REQ-012 §3.7). Same seam as the settlement boleta: the PDF engine stays swappable and the slip
/// NEVER carries another employee's data.
/// </summary>
public static class PayrollSlipDocumentMapper
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    public static DocumentModel Map(PayrollRunResponse run, PayrollRunEmployeeLinesResponse employee)
    {
        var employeeCode = employee.Lines.Count > 0 ? employee.Lines[0].EmployeeCode : null;
        var headerFields = new List<DocumentField>
        {
            new("Empleado", employee.EmployeeName),
            new("Código", employeeCode ?? "—"),
            new("Nómina", $"{run.PayrollDefinitionCode} — {run.PayrollDefinitionName}"),
            new("Tipo de planilla", run.PayrollTypeCode),
            new("Periodo", $"{run.PeriodLabel} ({Date(run.PeriodStartDate)} a {Date(run.PeriodEndDate)})"),
            new("Fecha de pago", run.PaymentDate is { } paymentDate ? Date(paymentDate) : "—"),
            new("Estado de la planilla", run.StatusCode),
            new("Moneda", run.CurrencyCode),
        };

        var sections = new List<DocumentSection>
        {
            LinesSection("Ingresos", employee, PayrollLineClasses.Ingreso),
            LinesSection("Descuentos", employee, PayrollLineClasses.Descuento),
            LinesSection("Pagos patronales (informativos)", employee, PayrollLineClasses.PagoPatronal),
            new("Resumen", [new KeyValueBlock(
            [
                new DocumentField("Total ingresos", Money(employee.TotalIncome, run.CurrencyCode)),
                new DocumentField("Total descuentos", Money(employee.TotalDeductions, run.CurrencyCode)),
                new DocumentField("NETO A PAGAR", Money(employee.TotalNet, run.CurrencyCode)),
            ])]),
        };

        return new DocumentModel(
            "Boleta de pago",
            headerFields,
            "Boleta de pago de planilla generada por el motor interno. Los pagos patronales son informativos "
            + "y no descuentan al empleado; el pago material se gestiona por la vía bancaria de la empresa.",
            sections);
    }

    private static DocumentSection LinesSection(
        string title,
        PayrollRunEmployeeLinesResponse employee,
        string lineClass)
    {
        var lines = employee.Lines.Where(line => line.LineClass == lineClass).ToArray();
        if (lines.Length == 0)
        {
            return new DocumentSection(title, [new MutedTextBlock("Sin líneas en esta sección.")]);
        }

        var rows = lines
            .Select(line => (IReadOnlyList<string>)
            [
                line.ConceptName + (line.IsIncluded ? string.Empty : " (excluida)"),
                line.SourceModule ?? string.Empty,
                line.Units is { } units ? Amount(units) : string.Empty,
                line.BaseAmount is { } baseAmount ? Amount(baseAmount) : string.Empty,
                Amount(line.CalculatedAmount),
                line.OverrideAmount is { } overrideAmount ? Amount(overrideAmount) : string.Empty,
                Amount(line.FinalAmount),
            ])
            .ToArray();

        return new DocumentSection(title,
        [
            new TableBlock(
                [
                    DocumentTableColumn.Relative("Concepto", 2.4f),
                    DocumentTableColumn.Relative("Fuente", 1.4f),
                    DocumentTableColumn.Relative("Unidades", 0.9f),
                    DocumentTableColumn.Relative("Base", 1f),
                    DocumentTableColumn.Relative("Calculado", 1f),
                    DocumentTableColumn.Relative("Ajustado", 1f),
                    DocumentTableColumn.Relative("Final", 1f),
                ],
                rows),
        ]);
    }

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", Culture);

    private static string Amount(decimal value) => value.ToString("0.00##", Culture);

    private static string Money(decimal value, string currencyCode) => $"{value.ToString("0.00", Culture)} {currencyCode}";
}
