using System.Reflection;
using CLARIHR.Api.Authorization;
using CLARIHR.Api.Common.Conventions;
using Microsoft.AspNetCore.Mvc;

namespace CLARIHR.Application.UnitTests;

/// <summary>
/// Guardrail de §9 — <b>un recurso gobernado por RBAC le dice al cliente si puede escribir.</b>
/// <para>
/// Sin <c>[ResourceActions]</c> el recurso no emite <c>allowedActions</c>, y el frontend no tiene forma de
/// saber si el usuario puede guardar sin replicar los códigos de permiso a mano — que es exactamente donde ya
/// se equivocó (hallazgo <c>00001 / F-01</c>: el guard de la ruta pedía el permiso de otro módulo, y la
/// pantalla mostraba «Guardar» a quien iba a recibir un <c>403</c>).
/// </para>
/// <para>
/// <b>El alcance real, medido al cerrar <c>00001 / B-01</c>:</b> de los <b>90</b> controllers gobernados con
/// escrituras, <b>51</b> no declaran el recurso — más de la mitad del producto.
/// </para>
/// <para>
/// ⚠️ <b>Este número costó tres intentos y conviene no repetirlos.</b> Un <c>grep</c> del texto
/// <c>AuthorizationPolicySet</c> dio 57: contaba también los controllers donde la cadena aparece en un
/// comentario que explica por qué NO lo llevan. Leer la lista de violaciones desde la salida de xUnit dio 5:
/// la colección venía truncada con <c>···</c>. El número bueno sale de reflexión + un <c>Assert</c> que
/// imprime la lista entera, que es justo por lo que este test la imprime en vez de usar <c>Assert.Empty</c>.
/// </para>
/// <para>
/// <b>Por qué se congelan y no se arreglan aquí.</b> Saldar uno son TRES pasos, y el tercero es el que se
/// olvida: sin registrar la clave en <c>AllowedActionsRegistry</c> el filtro es fail-closed y
/// <c>allowedActions</c> sale <c>null</c> aunque el atributo y la interfaz estén puestos. Cada registro
/// necesita su terna de permisos, y equivocarla le diría al frontend «puedes editar» a quien no puede — el
/// defecto que este guardrail existe para evitar, cometido al arreglarlo. Sus pantallas aún no se han
/// ejercitado en la corrida de QA, así que no habría con qué comprobarlo: se drenan cuando toque cada una,
/// con un test que demuestre que <c>canEdit</c> distingue lector de administrador.
/// </para>
/// <para>
/// ⚠️ La allow-list separa <b>DISEÑO</b> de <b>DEUDA</b> a propósito. Sin esa distinción una lista de
/// excepciones convierte la deuda en «comportamiento esperado» y nadie la vuelve a mirar.
/// </para>
/// </summary>
public sealed class ResourceActionsCoverageGuardrailsTests
{
    private static readonly Assembly ApiAssembly = typeof(AuthorizationPolicySetAttribute).Assembly;

    /// <summary>
    /// <b>DISEÑO</b> — no exponen un recurso que el cliente edite, así que <c>allowedActions</c> no aplica.
    /// Devuelven archivos, trabajos asíncronos o son superficie de sesión.
    /// </summary>
    private static readonly HashSet<string> ByDesign = new(StringComparer.Ordinal)
    {
    };

    /// <summary>
    /// <b>DEUDA</b> — sí deberían declarar el recurso. Cada línea que desaparece es una pantalla que dejó de
    /// obligar al cliente a adivinar permisos. <b>Vaciar esta lista es el avance.</b>
    /// </summary>
    private static readonly HashSet<string> Pending = new(StringComparer.Ordinal)
    {
        // Los 51 medidos el 2026-08-16 por reflexión (de 90 controllers gobernados con escrituras).
        // Agrupados por familia para drenarlos por módulo cuando su pantalla se ejercite.

        // ── Expediente de personal (23) ────────────────────────────────────
        "PersonnelFileAuthorizationSubstitutionController",
        "PersonnelFileBackgroundController",
        "PersonnelFileCompensationConceptsController",
        "PersonnelFileCompensationController",
        "PersonnelFileCompensatoryTimeAbsencesController",
        "PersonnelFileCompensatoryTimeCreditDocumentsController",
        "PersonnelFileCompensatoryTimeCreditsController",
        "PersonnelFileCompetencyController",
        "PersonnelFileDisciplinaryActionDocumentsController",
        "PersonnelFileDisciplinaryActionsController",
        "PersonnelFileDocumentsController",
        "PersonnelFileEmploymentController",
        "PersonnelFileIncapacitiesController",
        "PersonnelFileIncapacityDocumentsController",
        "PersonnelFileInterestsController",
        "PersonnelFileLactationController",
        "PersonnelFilePersonalInfoController",
        "PersonnelFileRecognitionDocumentsController",
        "PersonnelFileRecognitionsController",
        "PersonnelFileTalentController",
        "PersonnelFileVacationPeriodsController",
        "PersonnelFileVacationRequestsController",
        "PersonnelFilesController",

        // ── Planilla y compensación (17) ───────────────────────────────────
        "AguinaldoExemptionsController",
        "IncomeTaxBracketsController",
        "OffPayrollTransactionsController",
        "OneTimeDeductionResolutionController",
        "OneTimeDeductionsController",
        "OneTimeIncomeResolutionController",
        "OneTimeIncomesController",
        "OvertimeRecordResolutionController",
        "OvertimeRecordsController",
        "PayrollConfigurationController",
        "PayrollRunResolutionController",
        "PayrollRunsController",
        "RecurringDeductionResolutionController",
        "RecurringDeductionsController",
        "RecurringIncomeResolutionController",
        "RecurringIncomesController",
        "SettlementsController",

        // ── Retiro y salida (5) ───────────────────────────────────────────
        "ExitInterviewFormsController",
        "ExitInterviewsController",
        "RetirementRequestResolutionController",
        "RetirementRequestReversalController",
        "RetirementRequestsController",

        // ── Ausencias y salud (3) ─────────────────────────────────────────
        "LeaveConfigurationController",
        "MedicalClaimsController",
        "VacationPlansController",

        // ── Solicitudes del empleado (3) ──────────────────────────────────
        "CertificateRequestsController",
        "CompanyCertificateSettingsController",
        "EconomicAidRequestsController",
    };

    [Fact]
    public void GovernedWriteControllers_ShouldDeclareTheirResourceForAllowedActions()
    {
        var violations = new List<string>();
        var observed = new List<string>();

        foreach (var controller in GovernedWriteControllers())
        {
            observed.Add(controller.Name);

            if (controller.GetCustomAttribute<ResourceActionsAttribute>() is not null)
            {
                continue;
            }

            if (ByDesign.Contains(controller.Name) || Pending.Contains(controller.Name))
            {
                continue;
            }

            // TRES pasos para saldar cada uno: [ResourceActions] en el controller, el DTO implementando
            // ISupportsAllowedActions, y la clave registrada en AllowedActionsRegistry. Sin el tercero el
            // filtro es fail-closed y `allowedActions` sale `null` aunque los otros dos estén.
            violations.Add(controller.Name);
        }

        Assert.True(
            violations.Count == 0,
            $"{violations.Count} controllers gobernados no declaran su recurso:\n  " +
            string.Join("\n  ", violations.Order(StringComparer.Ordinal)));

        // Centinela zero-match: si el filtro de familia deja de encontrar controllers, el bucle pasa vacuamente.
        Assert.True(
            observed.Count >= 85,
            $"El filtro de familia sólo encontró {observed.Count} controllers gobernados con escrituras; " +
            "el inventario del 2026-08-16 tenía 90. Un filtro roto convierte este guardrail en decoración.");
    }

    /// <summary>
    /// La allow-list no puede pudrirse: una entrada que ya declara el recurso —o que dejó de existir— es
    /// progreso sin registrar, y deja la lista más permisiva de lo que nadie decidió.
    /// </summary>
    [Fact]
    public void ResourceActionsAllowList_ShouldNotContainStaleEntries()
    {
        var live = GovernedWriteControllers()
            .Where(controller => controller.GetCustomAttribute<ResourceActionsAttribute>() is null)
            .Select(controller => controller.Name)
            .ToHashSet(StringComparer.Ordinal);

        var stale = ByDesign.Concat(Pending).Where(entry => !live.Contains(entry)).OrderBy(x => x).ToList();

        Assert.True(
            stale.Count == 0,
            "Estas entradas de la allow-list ya declaran [ResourceActions] o dejaron de existir — bórralas: " +
            string.Join(", ", stale));
    }

    /// <summary>
    /// Familia por regex sobre la forma, no lista a mano (§9): un controller que declara
    /// <c>[AuthorizationPolicySet]</c> —es decir, cuya autorización gobierna el producto— y expone al menos un
    /// verbo de escritura.
    /// </summary>
    private static IEnumerable<Type> GovernedWriteControllers() =>
        ApiAssembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false } && type.Name.EndsWith("Controller", StringComparison.Ordinal))
            .Where(type => type.GetCustomAttribute<AuthorizationPolicySetAttribute>() is not null)
            .Where(HasGovernedWrite)
            .OrderBy(type => type.Name, StringComparer.Ordinal);

    private static bool HasGovernedWrite(Type controller) =>
        controller
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Any(method =>
                method.GetCustomAttribute<HttpPostAttribute>() is not null ||
                method.GetCustomAttribute<HttpPutAttribute>() is not null ||
                method.GetCustomAttribute<HttpPatchAttribute>() is not null ||
                method.GetCustomAttribute<HttpDeleteAttribute>() is not null);
}
