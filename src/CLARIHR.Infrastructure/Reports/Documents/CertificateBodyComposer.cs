using System.Globalization;
using CLARIHR.Application.Abstractions.PersonnelFiles;
using CLARIHR.Domain.PersonnelFiles;

namespace CLARIHR.Infrastructure.Reports.Documents;

/// <summary>The title and body paragraphs of a certificate, composed in code per type/language (D-15).</summary>
internal sealed record CertificateBody(string Title, IReadOnlyList<string> Paragraphs);

/// <summary>
/// Composes the certificate letter body per canonical type and language (es/en). The wording is structural
/// (in code, not user-editable — D-15); the company letterhead/signatory/footer are configurable (D-17) and are
/// laid out by the renderer. Custom (user-added) types fall back to a generic body without salary.
/// </summary>
internal static class CertificateBodyComposer
{
    private static readonly CultureInfo Spanish = CultureInfo.GetCultureInfo("es-ES");

    public static CertificateBody Compose(CertificatePrintPayload p)
    {
        var en = string.Equals(p.LanguageCode, "en", StringComparison.OrdinalIgnoreCase);
        var type = p.CertificateTypeCode.Trim().ToUpperInvariant();
        var who = Who(p, en);
        var hire = FormatDate(p.HireDate, en);
        var salary = FormatSalary(p, en);
        var tenure = en
            ? $"{p.SeniorityYears} year(s) and {p.SeniorityMonths} month(s)"
            : $"{p.SeniorityYears} año(s) y {p.SeniorityMonths} mes(es)";
        var closing = en
            ? "This certificate is issued at the request of the interested party for the purposes deemed appropriate."
            : "Y para los fines que al(la) interesado(a) convengan, se extiende la presente constancia.";

        return type switch
        {
            CertificateTypes.Salario => new(
                en ? "SALARY CERTIFICATE" : "CONSTANCIA DE SALARIO",
                [
                    en
                        ? $"This is to certify that {who} has been employed by this institution as {p.JobTitle} since {hire}, earning a monthly salary of {salary}."
                        : $"Por este medio se hace constar que {who}, labora en esta institución desempeñando el cargo de {p.JobTitle} desde el {hire}, devengando un salario mensual de {salary}.",
                    closing,
                ]),

            CertificateTypes.Embajada => new(
                en ? "EMPLOYMENT CERTIFICATE" : "CONSTANCIA LABORAL",
                [
                    en
                        ? $"This is to certify that {who} has been employed by this institution as {p.JobTitle} since {hire}, with a length of service of {tenure}, earning a monthly salary of {salary}."
                        : $"Por este medio se hace constar que {who}, labora en esta institución desempeñando el cargo de {p.JobTitle} desde el {hire}, con una antigüedad de {tenure}, devengando un salario mensual de {salary}.",
                    en
                        ? "This institution confirms the employment relationship and the employee's job stability."
                        : "Esta institución avala la relación laboral y hace constar la estabilidad laboral del(la) empleado(a).",
                    closing,
                ]),

            CertificateTypes.TiempoLaborado => new(
                en ? "LENGTH-OF-SERVICE CERTIFICATE" : "CONSTANCIA DE TIEMPO LABORADO",
                [
                    en
                        ? $"This is to certify that {who} has worked for this institution as {p.JobTitle} since {hire}, accumulating a length of service of {tenure}."
                        : $"Por este medio se hace constar que {who}, ha laborado en esta institución desempeñando el cargo de {p.JobTitle} desde el {hire}, acumulando una antigüedad de {tenure}.",
                    closing,
                ]),

            CertificateTypes.NoDescuento => new(
                en ? "NO-DEDUCTION CERTIFICATE" : "CONSTANCIA DE NO DESCUENTO",
                [
                    en
                        ? $"This is to certify that {who} has been employed by this institution as {p.JobTitle} since {hire}, and that as of this date no loan or obligation deductions are recorded by this institution."
                        : $"Por este medio se hace constar que {who}, labora en esta institución desempeñando el cargo de {p.JobTitle} desde el {hire}, y que a la fecha no posee descuentos por préstamos u obligaciones registrados ante esta institución.",
                    closing,
                ]),

            CertificateTypes.Recomendacion => new(
                en ? "LETTER OF RECOMMENDATION" : "CARTA DE RECOMENDACIÓN LABORAL",
                [
                    en
                        ? $"This is to certify that {who} has been employed by this institution as {p.JobTitle} since {hire}, performing their duties with responsibility and professionalism."
                        : $"Por este medio se hace constar que {who}, labora en esta institución desempeñando el cargo de {p.JobTitle} desde el {hire}, desempeñando sus funciones con responsabilidad y profesionalismo.",
                    closing,
                ]),

            _ => new(
                en ? "EMPLOYMENT CERTIFICATE" : (type == CertificateTypes.Laboral ? "CONSTANCIA LABORAL" : "CONSTANCIA"),
                [
                    en
                        ? $"This is to certify that {who} has been employed by this institution as {p.JobTitle} since {hire}."
                        : $"Por este medio se hace constar que {who}, labora en esta institución desempeñando el cargo de {p.JobTitle} desde el {hire}.",
                    closing,
                ]),
        };
    }

    private static string Who(CertificatePrintPayload p, bool en)
    {
        var idClause = !string.IsNullOrWhiteSpace(p.IdentificationNumber)
            ? (en
                ? $", holder of {p.IdentificationType} No. {p.IdentificationNumber}"
                : $", portador(a) de {p.IdentificationType} N° {p.IdentificationNumber}")
            : string.Empty;
        return en ? $"Mr./Ms. {p.FullName}{idClause}" : $"el(la) señor(a) {p.FullName}{idClause}";
    }

    private static string FormatSalary(CertificatePrintPayload p, bool en) =>
        p.MonthlySalary is { } salary
            ? $"{salary.ToString("N2", en ? CultureInfo.InvariantCulture : Spanish)} {p.CurrencyCode}".Trim()
            : (en ? "(not available)" : "(no disponible)");

    private static string FormatDate(DateTime date, bool en) =>
        en
            ? date.ToString("MMMM d, yyyy", CultureInfo.InvariantCulture)
            : date.ToString("d 'de' MMMM 'de' yyyy", Spanish);
}
