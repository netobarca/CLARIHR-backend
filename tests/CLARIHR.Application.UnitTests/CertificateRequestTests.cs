using CLARIHR.Application.Features.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Unit coverage for the employee certificate ("constancia") feature: the pure rules (embassy addressee D-06,
/// salary-printing types D-20), the linear domain transition guards on
/// <see cref="PersonnelFileCertificateRequest"/> (process/issue/deliver/reject/cancel — D-04) and the input
/// validator.
/// </summary>
public sealed class CertificateRequestTests
{
    private static readonly DateTime RequestDate = new(2026, 6, 25, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime IssueDate = new(2026, 6, 28, 0, 0, 0, DateTimeKind.Utc);

    private static PersonnelFileCertificateRequest NewRequest(string typeCode = "CONSTANCIA_LABORAL") =>
        PersonnelFileCertificateRequest.Create(
            typeCode,
            "Constancia de trabajo (laboral)",
            "TRAMITE_BANCARIO",
            null,
            "PRESENCIAL",
            "es",
            1,
            RequestDate,
            null,
            Guid.NewGuid());

    private static CertificateRequestInput ValidInput() =>
        new("CONSTANCIA_LABORAL", "TRAMITE_BANCARIO", null, "PRESENCIAL", "es", 1, RequestDate, null);

    private static bool IsValid(CertificateRequestInput input) =>
        new CertificateRequestInputValidator().Validate(input).IsValid;

    // ── Pure rules ──────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("CONSTANCIA_EMBAJADA", true)]
    [InlineData("constancia_embajada", true)]
    [InlineData("CONSTANCIA_LABORAL", false)]
    [InlineData("CONSTANCIA_SALARIO", false)]
    public void RequiresAddressee_OnlyEmbassy(string typeCode, bool expected) =>
        Assert.Equal(expected, CertificateRequestRules.RequiresAddressee(typeCode));

    [Theory]
    [InlineData("CONSTANCIA_SALARIO", true)]
    [InlineData("CONSTANCIA_EMBAJADA", true)]
    [InlineData("constancia_salario", true)]
    [InlineData("CONSTANCIA_LABORAL", false)]
    [InlineData("CARTA_RECOMENDACION", false)]
    [InlineData("TIPO_PERSONALIZADO", false)]
    public void PrintsSalary_OnlySalaryAndEmbassy(string typeCode, bool expected) =>
        Assert.Equal(expected, CertificateRequestRules.PrintsSalary(typeCode));

    [Fact]
    public void DeriveResponseTimeDays_IssuedAfterRequest_ReturnsDays() =>
        Assert.Equal(3, PersonnelFileCertificateRequest.DeriveResponseTimeDays(RequestDate, IssueDate));

    [Fact]
    public void DeriveResponseTimeDays_Unissued_ReturnsNull() =>
        Assert.Null(PersonnelFileCertificateRequest.DeriveResponseTimeDays(RequestDate, null));

    [Fact]
    public void DeriveResponseTimeDays_IssuedBeforeRequest_ReturnsNull() =>
        Assert.Null(PersonnelFileCertificateRequest.DeriveResponseTimeDays(IssueDate, RequestDate));

    // ── Linear lifecycle (D-04) ──────────────────────────────────────────────────

    [Fact]
    public void Create_StartsSolicitada()
    {
        var request = NewRequest();
        Assert.Equal(CertificateRequestStatuses.Solicitada, request.RequestStatusCode);
        Assert.True(request.IsActive);
    }

    [Fact]
    public void StartProcessing_FromSolicitada_MovesToEnProceso()
    {
        var request = NewRequest();
        request.StartProcessing();
        Assert.Equal(CertificateRequestStatuses.EnProceso, request.RequestStatusCode);
    }

    [Fact]
    public void Issue_FromPending_SetsIssuerDateAndResponseTime()
    {
        var request = NewRequest();
        var issuer = Guid.NewGuid();

        request.Issue(issuer, IssueDate, "Listo.");

        Assert.Equal(CertificateRequestStatuses.Emitida, request.RequestStatusCode);
        Assert.Equal(issuer, request.IssuedByUserId);
        Assert.Equal(IssueDate.Date, request.IssuedDateUtc!.Value.Date);
        Assert.Equal(3, request.ResponseTimeDays);
    }

    [Fact]
    public void Deliver_FromEmitida_MovesToEntregada()
    {
        var request = NewRequest();
        request.Issue(Guid.NewGuid(), IssueDate, null);
        request.Deliver(new DateTime(2026, 6, 29, 0, 0, 0, DateTimeKind.Utc));
        Assert.Equal(CertificateRequestStatuses.Entregada, request.RequestStatusCode);
    }

    [Fact]
    public void Reject_FromPending_MovesToRechazada()
    {
        var request = NewRequest();
        request.Reject("No procede.");
        Assert.Equal(CertificateRequestStatuses.Rechazada, request.RequestStatusCode);
        Assert.Equal("No procede.", request.ResolutionNotes);
    }

    [Fact]
    public void Cancel_FromPending_MovesToAnulada()
    {
        var request = NewRequest();
        request.Cancel();
        Assert.Equal(CertificateRequestStatuses.Anulada, request.RequestStatusCode);
    }

    [Fact]
    public void Issue_FromIssued_Throws()
    {
        var request = NewRequest();
        request.Issue(Guid.NewGuid(), IssueDate, null);
        Assert.Throws<InvalidOperationException>(() => request.Issue(Guid.NewGuid(), IssueDate, null));
    }

    [Fact]
    public void Deliver_FromSolicitada_Throws() =>
        Assert.Throws<InvalidOperationException>(() => NewRequest().Deliver(IssueDate));

    [Fact]
    public void Cancel_FromIssued_Throws()
    {
        var request = NewRequest();
        request.Issue(Guid.NewGuid(), IssueDate, null);
        Assert.Throws<InvalidOperationException>(request.Cancel);
    }

    [Fact]
    public void Reject_FromIssued_Throws()
    {
        var request = NewRequest();
        request.Issue(Guid.NewGuid(), IssueDate, null);
        Assert.Throws<InvalidOperationException>(() => request.Reject(null));
    }

    // ── Input validator ──────────────────────────────────────────────────────────

    [Fact]
    public void Validator_ValidInput_Passes() => Assert.True(IsValid(ValidInput()));

    [Fact]
    public void Validator_ZeroCopies_Fails() =>
        Assert.False(IsValid(ValidInput() with { Copies = 0 }));

    [Fact]
    public void Validator_FutureRequestDate_Fails() =>
        Assert.False(IsValid(ValidInput() with { RequestDateUtc = DateTime.UtcNow.AddDays(5) }));

    [Fact]
    public void Validator_NeededByBeforeRequest_Fails() =>
        Assert.False(IsValid(ValidInput() with { NeededByDateUtc = RequestDate.AddDays(-2) }));

    [Fact]
    public void Validator_MissingType_Fails() =>
        Assert.False(IsValid(ValidInput() with { TypeCode = "" }));
}
