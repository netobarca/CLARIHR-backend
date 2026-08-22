using System.Reflection;
using System.Text.RegularExpressions;
using CLARIHR.Domain.LegalRepresentatives;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Guardrail de §9 «TIPO DE FECHA», que enforcea las dos invariantes de §1.2 y §4.5 de
/// `definiciones-tecnicas-backend.md`:
/// <list type="number">
/// <item>una propiedad de dominio cuyo nombre denota un <b>día</b> se modela como <c>DateOnly</c> sobre
/// columna <c>date</c>, no como <c>DateTime</c> sobre <c>timestamptz</c>;</item>
/// <item>ningún lector de JSON Patch usa <c>JsonElement.TryGetDateTime()</c> en crudo, porque el
/// <c>JsonElement</c> no pasa por ningún converter y el <c>Kind</c> sale sin normalizar.</item>
/// </list>
/// <para>
/// Origen: hallazgos <c>00000 / B-01</c>, <c>B-02</c> y <c>B-03</c> de `ComentariosPruebasBackend`. La regla
/// existía desde H-26 pero no estaba escrita ni vigilada, así que `LegalRepresentative` la incumplió sin que
/// nada fallara. Este test es la parte que impide que vuelva a pasar.
/// </para>
/// </summary>
public sealed class CalendarDateTypeGuardrailsTests
{
    /// <summary>
    /// Familia por regex, no lista a mano (§9). Un nombre denota un DÍA cuando termina en <c>Date</c>/
    /// <c>DateUtc</c>, cuando es una ventana de vigencia <c>EffectiveFrom</c>/<c>EffectiveTo</c>, o cuando
    /// marca un «desde» de calendario (<c>…SinceUtc</c>).
    /// <para>
    /// Los instantes NO caen aquí por construcción: ninguno termina en <c>Date</c>/<c>DateUtc</c>
    /// (<c>CreatedUtc</c>, <c>AnnulledUtc</c>, <c>RequestedAtUtc</c>, <c>LockoutEndUtc</c>,
    /// <c>AccessFailedWindowStartUtc</c>…). Es lo que permite que el filtro sea un regex y no un inventario.
    /// </para>
    /// </summary>
    private static readonly Regex DayNamePattern = new(
        @"(?:Date|DateUtc)$|^Effective(?:From|To)(?:Utc)?$|SinceUtc$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Inventario de lo que HOY incumple la regla. Congelado el 2026-08-15 con 82 entradas (hallazgo
    /// `00000 / B-02` §3.8); B-02 convirtió las 3 de `LegalRepresentative` el 2026-08-16 y quedan **79**.
    /// <para>
    /// ⚠️ <b>Vaciar esta lista es el avance, no un efecto colateral.</b> Cada conversión a <c>DateOnly</c>
    /// borra su línea; el test verifica además que no queden líneas muertas, para que la lista no se
    /// convierta en un cementerio que oculta el progreso.
    /// </para>
    /// <para>
    /// Las entradas marcadas <c>[H-26]</c> NO son deuda: son instantes deliberados, decididos al cerrar
    /// H-26/H-28, y se quedan. El resto está pendiente de clasificar y convertir.
    /// </para>
    /// </summary>
    private static readonly HashSet<string> KnownDayFieldsStillTypedAsInstant = new(StringComparer.Ordinal)
    {
        // ── [H-26] Instantes deliberados. No convertir sin revertir aquella decisión. ──────────────────
        "PositionSlot.EffectiveFromUtc",
        "PositionSlot.EffectiveToUtc",
        "PersonnelFileOffPayrollTransaction.TransactionDateUtc",
        "PersonnelFilePayrollTransaction.TransactionDateUtc",
        "PersonnelFileContractHistory.ContractDate",
        "PersonnelFileContractHistory.ContractEndDate",
        "PersonnelFileCompensationConcept.StartDate",
        "PersonnelFileCompensationConcept.EndDate",
        "PersonnelFileSettlement.PlazaStartDate",
        "PersonnelFileSettlement.RequestDate",
        "PersonnelFileSettlement.RetirementDate",
        "PersonnelFileSettlement.SeniorityStartDate",
        "PersonnelFileRetirementRequest.RequestDate",
        "PersonnelFileRetirementRequest.RetirementDate",
        "RetirementRequestClosedRecord.PreviousEndDate",

        // ── Deuda pendiente. Cada línea que desaparece es una conversión hecha. ────────────────────────
        "CommercialPlanVersion.EffectiveFromUtc",
        "CommercialPlanVersion.EffectiveToUtc",
        "Company.BillableSinceUtc",
        "CompanyCommercialAddon.StatusEffectiveDateUtc",
        "CompanyCommercialAddonChange.EffectiveDateUtc",
        "CompanySubscription.EndDateUtc",
        "CompanySubscription.StartDateUtc",
        "CompanySubscriptionPlanChange.EffectiveDateUtc",
        "CompanySubscriptionStatusChangeRequest.EffectiveDateUtc",
        "ExitInterviewAnswer.ValueDate",
        "IncomeTaxWithholdingBracket.EffectiveFromUtc",
        "IncomeTaxWithholdingBracket.EffectiveToUtc",
        "JobProfile.EffectiveFromUtc",
        "JobProfile.EffectiveToUtc",
        "PersonnelFile.BirthDate",
        "PersonnelFileAdditionalBenefit.EndDate",
        "PersonnelFileAdditionalBenefit.StartDate",
        "PersonnelFileAssetAccess.DeliveryDateUtc",
        "PersonnelFileAssetAccess.EndDateUtc",
        "PersonnelFileAssetAccess.StartDateUtc",
        "PersonnelFileAssociation.JoinedDate",
        "PersonnelFileAssociation.LeftDate",
        "PersonnelFileAuthorizationSubstitution.EndDate",
        "PersonnelFileAuthorizationSubstitution.StartDate",
        "PersonnelFileCertificateRequest.DeliveredDateUtc",
        "PersonnelFileCertificateRequest.IssuedDateUtc",
        "PersonnelFileCertificateRequest.NeededByDateUtc",
        "PersonnelFileCertificateRequest.RequestDateUtc",
        "PersonnelFileEconomicAidRequest.DisbursementDateUtc",
        "PersonnelFileEconomicAidRequest.RequestDateUtc",
        "PersonnelFileEconomicAidRequest.ResolutionDateUtc",
        "PersonnelFileEducation.EndDate",
        "PersonnelFileEducation.StartDate",
        "PersonnelFileEmployeeProfile.HireDate",
        "PersonnelFileEmployeeProfile.RetirementDate",
        "PersonnelFileFamilyMember.BirthDate",
        "PersonnelFileFamilyMember.DeceasedDate",
        "PersonnelFileIdentification.ExpiryDate",
        "PersonnelFileIdentification.IssuedDate",
        "PersonnelFileInsurance.EndDateUtc",
        "PersonnelFileInsurance.StartDateUtc",
        "PersonnelFileInsuranceBeneficiary.BirthDate",
        "PersonnelFileMedicalClaim.ClaimDateUtc",
        "PersonnelFileMedicalClaim.ResolutionDateUtc",
        "PersonnelFilePerformanceEvaluation.EvaluationDateUtc",
        "PersonnelFilePersonnelAction.ActionDateUtc",
        "PersonnelFilePersonnelAction.EffectiveFromUtc",
        "PersonnelFilePersonnelAction.EffectiveToUtc",
        "PersonnelFilePositionCompetencyResult.EvaluationDateUtc",
        "PersonnelFilePreviousEmployment.EntryDate",
        "PersonnelFilePreviousEmployment.RetirementDate",
        "PersonnelFileRetirementRequest.CancellationDateUtc",
        "PersonnelFileRetirementRequest.ExecutionDateUtc",
        "PersonnelFileRetirementRequest.ResolutionDateUtc",
        "PersonnelFileRetirementRequest.ReversalDateUtc",
        "PersonnelFileSelectionContest.ContestDateUtc",
        "PersonnelFileTraining.EndDate",
        "PersonnelFileTraining.StartDate",
        "PersonnelFileVacationRequest.DecisionDateUtc",
        "SalaryTabulatorChangeRequest.EffectiveFromUtc",
        "SalaryTabulatorChangeRequest.EffectiveToUtc",
        "SalaryTabulatorLine.EffectiveFromUtc",
        "SalaryTabulatorLine.EffectiveToUtc",
        "VacationReturn.ReturnDateUtc",
    };

    [Fact]
    public void DomainDayFields_ShouldBeDateOnly_NotDateTime()
    {
        var violations = new List<string>();
        var observed = new List<string>();

        foreach (var (key, property) in EnumerateDomainDateTimeProperties())
        {
            if (!DayNamePattern.IsMatch(property.Name))
            {
                continue;
            }

            observed.Add(key);

            if (!KnownDayFieldsStillTypedAsInstant.Contains(key))
            {
                violations.Add(
                    $"{key} ({property.PropertyType.Name}): el nombre denota un DÍA, así que el tipo debe ser " +
                    "DateOnly sobre columna `date` (§1.2). Guardar un día en `timestamptz` obliga a cada " +
                    "consumidor a recordar la convención «medianoche UTC» y corre el día cuando el cuerpo trae " +
                    "offset. Si de verdad es un instante, renómbralo para que lo diga y añádelo a la " +
                    "allow-list con su motivo.");
            }
        }

        Assert.Empty(violations);

        // Centinela zero-match: si el regex de familia deja de matchear, el bucle pasa vacuamente.
        Assert.True(
            observed.Count >= 77,
            $"El filtro de familia solo matcheó {observed.Count} propiedades; el inventario del 2026-08-15 tenía 82; B-02 convirtió 3. " +
            "Un regex roto convierte este guardrail en decoración.");
    }

    /// <summary>
    /// La allow-list no puede pudrirse: una entrada que ya no existe en el dominio es progreso sin registrar,
    /// y deja la lista más permisiva de lo que nadie decidió.
    /// </summary>
    [Fact]
    public void CalendarDateAllowList_ShouldNotContainStaleEntries()
    {
        var live = EnumerateDomainDateTimeProperties()
            .Where(entry => DayNamePattern.IsMatch(entry.Property.Name))
            .Select(entry => entry.Key)
            .ToHashSet(StringComparer.Ordinal);

        var stale = KnownDayFieldsStillTypedAsInstant.Where(entry => !live.Contains(entry)).ToList();

        Assert.True(
            stale.Count == 0,
            "Estas entradas de la allow-list ya no existen como `DateTime` en el dominio — bórralas: " +
            string.Join(", ", stale));
    }

    /// <summary>
    /// B-01 — el cuerpo de un JSON Patch llega como <c>JsonElement</c>, así que NINGÚN converter de
    /// `Program.cs` se le aplica. <c>TryGetDateTime()</c> devuelve <c>Kind=Unspecified</c> cuando el texto no
    /// trae zona y <c>Kind=Local</c> cuando trae offset; el primero lo rechaza `timestamptz` (500) y el
    /// segundo corre el día. Los lectores tienen que ir por <c>CalendarDateReader</c>.
    /// </summary>
    [Fact]
    public void PatchAppliers_ShouldNotReadDatesWithRawTryGetDateTime()
    {
        var applicationRoot = Path.Combine(FindRepositoryRoot(), "src", "CLARIHR.Application");
        var violations = new List<string>();
        var scanned = 0;

        foreach (var file in Directory.EnumerateFiles(applicationRoot, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            scanned++;
            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (lines[index].Contains("TryGetDateTime(", StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{Path.GetRelativePath(applicationRoot, file)}:{index + 1} — usa TryGetDateTime en crudo. " +
                        "Lee con CalendarDateReader: el JsonElement no pasa por ningún converter (§4.5).");
                }
            }
        }

        Assert.Empty(violations);

        // Centinela zero-match: si la ruta se rompe, el bucle no recorre nada y el test pasa vacuamente.
        Assert.True(scanned > 100, $"Solo se escanearon {scanned} archivos bajo {applicationRoot}.");
    }

    private static IEnumerable<(string Key, PropertyInfo Property)> EnumerateDomainDateTimeProperties()
    {
        foreach (var type in typeof(LegalRepresentative).Assembly.GetTypes().Where(type => type.IsClass))
        {
            var properties = type.GetProperties(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

            foreach (var property in properties)
            {
                if (property.PropertyType == typeof(DateTime) || property.PropertyType == typeof(DateTime?))
                {
                    yield return ($"{type.Name}.{property.Name}", property);
                }
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src", "CLARIHR.Application")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root could not be resolved from test output path.");
    }
}
