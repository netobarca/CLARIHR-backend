using CLARIHR.Application.Features.Reports;
using CLARIHR.Application.Features.Reports.Common;
using FluentValidation;

namespace CLARIHR.Application.UnitTests;

public sealed class CreateReportExportJobCommandValidatorTests
{
    private static readonly IValidator<CreateReportExportJobCommand> Validator = new CreateReportExportJobCommandValidator();

    [Fact]
    public async Task Validate_WhenJobProfilePdfWithPdfFormat_ShouldPass()
    {
        var command = new CreateReportExportJobCommand(
            CompanyId: Guid.NewGuid(),
            ResourceKey: ReportExportResources.JobProfilePdf,
            Format: ReportExportFormats.Pdf,
            ParametersJson: "{\"jobProfileId\":\"00000000-0000-0000-0000-000000000001\"}");

        var result = await Validator.ValidateAsync(command);

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public async Task Validate_WhenJobProfilePdfWithCsvFormat_ShouldFail()
    {
        var command = new CreateReportExportJobCommand(
            CompanyId: Guid.NewGuid(),
            ResourceKey: ReportExportResources.JobProfilePdf,
            Format: ReportExportFormats.Csv,
            ParametersJson: "{}");

        var result = await Validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == "Format" &&
            error.ErrorMessage.Contains("not compatible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_WhenTabularResourceWithPdfFormat_ShouldFail()
    {
        var command = new CreateReportExportJobCommand(
            CompanyId: Guid.NewGuid(),
            ResourceKey: ReportExportResources.PersonnelFiles,
            Format: ReportExportFormats.Pdf,
            ParametersJson: "{}");

        var result = await Validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error =>
            error.PropertyName == "Format" &&
            error.ErrorMessage.Contains("not compatible", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Validate_WhenTabularResourceWithXlsxFormat_ShouldPass()
    {
        var command = new CreateReportExportJobCommand(
            CompanyId: Guid.NewGuid(),
            ResourceKey: ReportExportResources.PersonnelFiles,
            Format: ReportExportFormats.Xlsx,
            ParametersJson: "{}");

        var result = await Validator.ValidateAsync(command);

        Assert.True(result.IsValid, FormatErrors(result));
    }

    [Fact]
    public async Task Validate_WhenUnsupportedResource_ShouldFailWithoutCrossRule()
    {
        var command = new CreateReportExportJobCommand(
            CompanyId: Guid.NewGuid(),
            ResourceKey: "UNKNOWN_RESOURCE",
            Format: ReportExportFormats.Csv,
            ParametersJson: "{}");

        var result = await Validator.ValidateAsync(command);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == "ResourceKey");
    }

    private static string FormatErrors(FluentValidation.Results.ValidationResult result) =>
        string.Join(", ", result.Errors.Select(error => $"{error.PropertyName}={error.ErrorMessage}"));
}
