# Plan Técnico de Implementación — Recontratación de Empleados

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación |
| **Audiencia** | Equipo de Desarrollo, Tech Lead, QA |
| **Documento de negocio** | [`docs/business/analisis-recontratacion-empleados.md`](../business/analisis-recontratacion-empleados.md) (decisiones D-01…D-19) |
| **Módulos** | `PersonnelFiles` · `PositionSlots` · `CompanyUsers`/Identity · `IdentityAccess` (RBAC) · Localization |
| **Estado** | Propuesto — listo para revisión técnica |
| **Fecha** | 2026-06-20 |

---

## 1. Objetivo y enfoque

Implementar la **recontratación** reutilizando el expediente existente (mismo `PersonnelFile`, mismo tenant), abriendo un **nuevo período laboral** con nueva fecha de ingreso, preservando el período anterior como **historial derivado**, y re-provisionando los accesos del empleado.

**Insight central del análisis de código:** el `Finalize` actual se bloquea con la condición `LifecycleStatus != Draft || LinkedUserPublicId.HasValue`. Si introducimos un método de dominio **`ReopenForRehire()`** que pone `LifecycleStatus = Draft`, **limpia `LinkedUserPublicId`** y reactiva (`IsActive = true`), entonces:

- El **`Finalize` existente vuelve a ser válido sin modificarlo** (queda `Draft` + `LinkedUserPublicId == null`).
- La provisión de usuario (`CompanyUserProvisioningService.ProvisionAsync(... AllowExistingMembershipReuse: true)`) **reutiliza el mismo usuario/email** si sigue libre (D-09); si el email fue reasignado, el chequeo `LinkedUserConflict` ya obliga a capturar uno nuevo.

Esto reduce el trabajo a: **un método de dominio (reopen), un módulo de reglas puro, un comando orquestador atómico, una marca de elegibilidad, un permiso nuevo y un puñado de errores localizados.** No se requiere entidad nueva de historial (D-14) ni relajar invariantes del `Finalize`.

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo) | Implicación |
|---|---|---|---|
| 1 | Ciclo de vida | `PersonnelFileLifecycleStatus { Draft=1, Completed=2 }`. **No** existe "Terminated". El retiro deja `Completed` + `IsEmploymentActive=false` + `RetirementDate`. | El "retirado" es un `Completed` inactivo, no un estado propio. |
| 2 | Métodos del agregado | `PersonnelFile.cs`: `Complete(Guid)`, `CompleteWithoutLinkedUser()`, `Activate()`, `Inactivate()`. **No hay reopen/revert.** | **Agregar `ReopenForRehire()`.** |
| 3 | Bloqueo de Finalize | `FinalizePersonnelFileValidationResolver` (`FinalizePersonnelFile.cs`): `if (LifecycleStatus != Draft \|\| LinkedUserPublicId.HasValue) → StateRuleViolation`. | Limpiar `LinkedUserPublicId` en el reopen ⇒ Finalize válido **sin cambios**. |
| 4 | Unicidad de email | El resolver solo falla si el email está ligado a **otro** expediente (`LinkedUserConflict`). | Reusar mismo email funciona; email reasignado ⇒ pedir uno nuevo (D-09). |
| 5 | Provisión de usuario | `CompanyUserProvisioningService.ProvisionAsync(... AllowExistingMembershipReuse: true)`. Al retirar, `DeactivateCompanyUser` **desactiva** (no borra) user+membership+IAM. | Re-provisión debe **reactivar** al usuario desactivado (tarea explícita, §9). |
| 6 | Historial derivable | `PersonnelFileContractHistory.IsActive` + `SetActive(bool)`; asignaciones con `StartDate/EndDate`. | El período anterior se **deriva** si queda bien **cerrado** (contrato `IsActive=false`, asignaciones con `EndDate`, acción de retiro). |
| 7 | Flujo de retiro | **No hay comando dedicado**: es `UpdatePersonnelFileEmployeeProfileCommand` (pone `IsEmploymentActive=false` + `Retirement*`) + `PATCH /personnel-files/{id}` `isActive=false` + opcional `DeactivateCompanyUser`. | El reopen debe **cerrar defensivamente** el contrato/asignaciones del período previo (puede no estar garantizado hoy). |
| 8 | Catálogos | `EmploymentStatusCode`, `ContractTypeCode`, `Retirement*` son **texto libre** (no validados). `AssignmentTypeCode` **sí** es catálogo (`CurriculumAssignmentType`). | El nuevo período usa estado libre (p. ej. `ACTIVO`); la asignación usa un `assignmentTypeCode` válido. |
| 9 | Patrón CQRS | `ICommand<T>`/`ICommandHandler<T,>` auto-registrados por reflexión (`DependencyInjection.cs`). Base `PersonnelFileEmployeeCommandHandlerBase.LoadForManageAsync<T>(...)`. Transacción `IUnitOfWork.BeginTransactionAsync` + `IAuditService`. | Seguir el patrón para el nuevo comando. |
| 10 | RBAC | Permisos en `PersonnelFilePermissionCodes` (`PersonnelFileCommon.cs`); políticas en `PersonnelFilePolicies` registradas en `Program.cs`; semilla owner en `ProvisioningConstants.CompanyAdminPermissions` → `OwnerPermissionCatalog`. El **owner** recibe todos por defecto. | Agregar `AuthorizeRehire` como permiso nuevo + semilla owner (D-10/D-18). |
| 11 | Reglas multi-plaza | Módulo puro `EmploymentAssignmentRules.Evaluate()` (`EmploymentAssignments.Rules.cs`), testeado aislado. | Reusar para la nueva asignación; replicar el patrón puro para reglas de re-hire. |
| 12 | Localización | Errores `Error(code, msg, ErrorType)` en `*Errors.cs`; recursos `BackendMessages.resx` / `.es.resx` / `.es-SV.resx`; test de paridad `BackendMessageLocalizationTests`. | Todo error nuevo necesita EN + ES (+ es-SV) o falla el test de paridad. |

## 3. Arquitectura de la solución

### 3.1 Cambios de dominio (`src/CLARIHR.Domain/PersonnelFiles/PersonnelFile.cs`)

**a) Método de reapertura (D-08):**
```csharp
public void ReopenForRehire()
{
    if (RecordType != PersonnelFileRecordType.Employee)
        throw new InvalidOperationException("Only employee files can be rehired.");
    if (LifecycleStatus != PersonnelFileLifecycleStatus.Completed)
        throw new InvalidOperationException("Only completed files can be reopened for rehire.");

    LifecycleStatus = PersonnelFileLifecycleStatus.Draft;
    LinkedUserPublicId = null;   // <- desbloquea el Finalize existente
    IsActive = true;             // reactiva el expediente para el nuevo período
    RefreshConcurrencyToken();
}
```

**b) Marca "no recontratable" (D-11/D-18)** — a nivel de persona (sobrevive a la sobrescritura del perfil 1:1):
```csharp
public bool IsRehireBlocked { get; private set; }
public string? RehireBlockedReason { get; private set; }

public void BlockRehire(string? reason) { IsRehireBlocked = true; RehireBlockedReason = reason; RefreshConcurrencyToken(); }
public void ClearRehireBlock() { IsRehireBlocked = false; RehireBlockedReason = null; RefreshConcurrencyToken(); }
```
La marca se fija **al retirar**, vía la misma operación `PATCH /personnel-files/{id}` que hoy pone `isActive=false` (se agrega `isRehireBlocked`/`rehireBlockedReason` a `PatchPersonnelFileRequest` y su applier). Requiere `PersonnelFiles.Manage` (ya gobernado por la matriz RBAC; el owner lo tiene — D-18).

### 3.2 Módulo de reglas puro `RehireEligibilityRules`
Nuevo archivo `src/CLARIHR.Application/Features/PersonnelFiles/Rehire/RehireEligibilityRules.cs`, siguiendo el patrón de `EmploymentAssignmentRules` (testeable sin BD):
```csharp
internal static class RehireEligibilityRules
{
    internal sealed record Input(
        PersonnelFileRecordType RecordType,
        PersonnelFileLifecycleStatus LifecycleStatus,
        bool IsEmploymentActive,
        bool IsRehireBlocked,
        bool CallerHasAuthorizeRehirePermission,
        bool AuthorizationReasonProvided,
        bool PriorPeriodClosureConfirmed);

    internal static Result Evaluate(Input i)
    {
        if (i.RecordType != PersonnelFileRecordType.Employee) return Result.Failure(RehireErrors.NotAnEmployee);
        if (i.LifecycleStatus != PersonnelFileLifecycleStatus.Completed || i.IsEmploymentActive)
            return Result.Failure(RehireErrors.NotRetired);                 // RN-02
        if (!i.PriorPeriodClosureConfirmed) return Result.Failure(RehireErrors.PriorPeriodOpen);  // D-13/D-17
        if (i.IsRehireBlocked && !(i.CallerHasAuthorizeRehirePermission && i.AuthorizationReasonProvided))
            return Result.Failure(RehireErrors.RequiresAuthorization);      // RN-06/D-04
        return Result.Success();
    }
}
```

### 3.3 Comando orquestador atómico (recomendado)
Nuevo archivo `src/CLARIHR.Application/Features/PersonnelFiles/Rehire/RehireEmployee.cs`. Endpoint único `POST /api/v1/personnel-files/{id}/rehire`. Hace todo en **una transacción** (RN-11, E9):

```
RehireEmployeeCommand(
    PersonnelFileId, NewHireDate, ContractTypeCode, ContractStartDate, ContractEndDate?,
    PositionSlotPublicId, AssignmentTypeCode,            // plaza elegida explícitamente (D-16)
    NewInstitutionalEmail?,                              // requerido solo si el anterior está en uso (D-09)
    PriorPeriodClosureConfirmed: bool,                   // confirmación manual (D-13/D-17)
    AuthorizationReason?,                                // requerido si IsRehireBlocked (D-04)
    ConcurrencyToken)
```

**Pasos del handler** (`RehireEmployeeCommandHandler : PersonnelFileEmployeeCommandHandlerBase`):
1. `LoadForManageAsync<RehireEmployeeResponse>(...)` → tenant + `PersonnelFiles.Manage` + carga.
2. Resolver permiso `AuthorizeRehire` del caller; correr `RehireEligibilityRules.Evaluate(...)`. Si falla → devolver el error.
3. **Cerrar el período anterior** (para el historial derivado, D-14): marcar `PersonnelFileContractHistory` activo como `SetActive(false)` con fecha de fin; finalizar asignaciones activas (`EndDate` + `IsActive=false`); asegurar que exista la acción de retiro (si no, registrarla).
4. `personnelFile.ReopenForRehire()` (Draft + limpia link + reactiva).
5. **Upsert del perfil** (`UpsertEmployeeProfileAsync`): `HireDate = NewHireDate`, `EmploymentStatusCode = "ACTIVO"`, `IsEmploymentActive = true`, **limpiar** `Retirement*`, nuevas fechas de contrato, **reset** `VacationConfigurationJson`, preservar `EmployeeCode` (D-03 reinicia acumulados).
6. **Nuevo contrato** (`AddContractHistoryAsync`, `IsActive=true`) + **nueva asignación** (`AddEmploymentAssignmentAsync`) validando cupo/estado vía `EmploymentAssignmentRules.Evaluate()` (RN-08/D-16).
7. **Email**: si `NewInstitutionalEmail` viene, fijarlo; si no, conservar el actual (D-09).
8. **Re-finalizar/provisión**: invocar el núcleo de finalización (ver §3.4) → `ProvisionAsync(AllowExistingMembershipReuse: true)`, `Complete(linkedUserId)`.
9. **Acción de personal** `RECONTRATACION` (`AddPersonnelActionAsync`): `effectiveFrom = NewHireDate`, autor, y autorizador/motivo si hubo override (RF-009/RN-10).
10. `SaveChanges` + `IAuditService` + commit. Devolver `RehireEmployeeResponse` con el nuevo `concurrencyToken`.

> **Alternativa (no recomendada):** UX por pasos — `POST /{id}/reopen-for-rehire` (pasos 2–5) + endpoints existentes (`employee-profile`, `employment-assignments`, `contract-history`) + `PATCH /finalize`. Reusa más superficie pero **expone un Draft de re-hire a medias** (riesgo de abandono e inconsistencia). Se documenta como respaldo si el frontend exige un wizard.

### 3.4 Núcleo de finalización reutilizable
El `FinalizePersonnelFileCommandHandler` hoy hace: validar → `ProvisionAsync` → `Complete()`/`CompleteWithoutLinkedUser()`. Extraer ese núcleo a un servicio interno (p. ej. `IPersonnelFileFinalizationService.FinalizeAsync(personnelFile, createUserAccount, positionSlotId, ct)`) y hacer que **`FinalizeHandler` y `RehireHandler`** lo invoquen. Evita duplicar la lógica de provisión y mantiene una sola fuente de verdad de las invariantes de finalización.

### 3.5 Re-provisión de usuario (D-09)
`CompanyUserProvisioningService` ya reutiliza membresía existente (`AllowExistingMembershipReuse: true`). **Tarea concreta:** garantizar que, cuando reutiliza un user/membership/IAM **desactivado** (por el retiro), los **reactive** (`user.Reactivate()` / `membership.Reactivate()` / `iamUser.SetActive(true)` + `SyncRoles`). Verificar el camino de reuso en `CompanyUserProvisioningService.cs` y `DeactivateCompanyUser.cs` para añadir la reactivación si falta.

### 3.6 Permisos (D-10/D-18)
- **Constante:** `PersonnelFilePermissionCodes.AuthorizeRehire = "PersonnelFiles.AuthorizeRehire"` en `Features/PersonnelFiles/Common/PersonnelFileCommon.cs`.
- **Semilla owner:** entrada en `ProvisioningConstants.CompanyAdminPermissions[]` (módulo PersonnelFiles) para que el **owner** lo tenga por defecto y sea **otorgable** por la matriz RBAC.
- **Política (opcional):** `PersonnelFilePolicies.AuthorizeRehire` registrada en `Program.cs` con `PermissionClaimEvaluator.HasAnyPermission(...)` (espejo de `Manage`). La **comprobación efectiva del override** se hace en el handler (paso 2) para poder degradar a "requiere autorización" en vez de 403 duro.

### 3.7 Errores + localización
Nuevo `Features/PersonnelFiles/Common/RehireErrors.cs` con, al menos: `NotAnEmployee`, `NotRetired`, `PriorPeriodOpen`, `RequiresAuthorization`, `RehireBlocked` (si se distingue de RequiresAuthorization), `InstitutionalEmailInUse`. Cada código necesita entrada en `BackendMessages.resx` (EN), `BackendMessages.es.resx` (ES) y `BackendMessages.es-SV.resx` o **falla `BackendMessageLocalizationTests`**.

### 3.8 Persistencia / migración
- EF config en `PersonnelFileConfiguration.cs`: mapear `is_rehire_blocked` (bool, default false) y `rehire_blocked_reason` (string?, maxlen 500).
- Migración: `dotnet ef migrations add AddRehireBlockToPersonnelFile --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj`.
- **Sin** entidad/tabla nueva de historial (D-14). El timeline (RF-011) es una **proyección de lectura** sobre `ContractHistory` + `PersonnelActions` + `EmploymentAssignments`.

## 4. Diagrama de la transacción de recontratación

```
POST /personnel-files/{id}/rehire   (If-Match: token)
        │
        ▼
[1] LoadForManage (tenant + PersonnelFiles.Manage)
[2] Eligibilidad  ── RehireEligibilityRules.Evaluate ──► (falla) 422 + error localizado
        │ (ok)
        ▼  ──────────────── BEGIN TX ────────────────
[3] Cerrar período anterior  (ContractHistory.SetActive(false), asignaciones EndDate, acción de retiro)
[4] personnelFile.ReopenForRehire()      → Draft + LinkedUserPublicId=null + IsActive=true
[5] Upsert perfil  (nueva HireDate, ACTIVO, limpia Retirement*, reset vacaciones)
[6] Nuevo contrato + nueva asignación  (EmploymentAssignmentRules: cupo/estado)
[7] Email  (conservar / nuevo si en uso)
[8] Finalización (núcleo)  → ProvisionAsync(reuse) → reactivar user → Complete(userId)  → Completed
[9] PersonnelAction RECONTRATACION  (efectiva = Nueva fecha; autor; autorizador si override)
        │  SaveChanges + Audit
        ▼  ──────────────── COMMIT ─────────────────
   200 OK + nuevo ETag/concurrencyToken
```

## 5. Archivos a crear / modificar

| # | Archivo | Acción |
|---|---|---|
| 1 | `src/CLARIHR.Domain/PersonnelFiles/PersonnelFile.cs` | **Mod**: `ReopenForRehire()`, `IsRehireBlocked`, `RehireBlockedReason`, `BlockRehire/ClearRehireBlock`. |
| 2 | `src/CLARIHR.Infrastructure/Persistence/Configurations/PersonnelFiles/PersonnelFileConfiguration.cs` | **Mod**: mapear `is_rehire_blocked`, `rehire_blocked_reason`. |
| 3 | `src/CLARIHR.Infrastructure/Persistence/Migrations/` | **New**: `AddRehireBlockToPersonnelFile` (vía `dotnet ef migrations add`). |
| 4 | `src/CLARIHR.Application/Features/PersonnelFiles/Rehire/RehireEligibilityRules.cs` | **New**: módulo de reglas puro. |
| 5 | `src/CLARIHR.Application/Features/PersonnelFiles/Rehire/RehireEmployee.cs` | **New**: comando + validador + handler orquestador + response. |
| 6 | `src/CLARIHR.Application/Features/PersonnelFiles/Common/RehireErrors.cs` | **New**: catálogo de errores de re-hire. |
| 7 | `src/CLARIHR.Application/Features/PersonnelFiles/FinalizePersonnelFile.cs` | **Mod**: extraer núcleo de finalización a `IPersonnelFileFinalizationService` reutilizable. |
| 8 | `src/CLARIHR.Application/Features/CompanyUsers/CompanyUserProvisioningService.cs` | **Mod**: reactivar user/membership/IAM desactivado al reusar (D-09). |
| 9 | `src/CLARIHR.Application/Features/PersonnelFiles/Common/PersonnelFileCommon.cs` | **Mod**: `PersonnelFilePermissionCodes.AuthorizeRehire`. |
| 10 | `src/CLARIHR.Application/Features/Provisioning/Common/ProvisioningConstants.cs` | **Mod**: semilla owner del permiso `AuthorizeRehire`. |
| 11 | `src/CLARIHR.Api/Program.cs` | **Mod (opc.)**: política `PersonnelFilePolicies.AuthorizeRehire`. |
| 12 | `src/CLARIHR.Api/Controllers/PersonnelFileEmploymentController.cs` | **Mod**: endpoint `POST .../rehire`. |
| 13 | `src/CLARIHR.Api/Contracts/PersonnelFiles/PersonnelFileRequests.cs` | **Mod**: `RehireEmployeeRequest`; agregar `isRehireBlocked`/`rehireBlockedReason` a `PatchPersonnelFileRequest`. |
| 14 | `src/CLARIHR.Application/.../Shell/PersonnelFileCore.PatchAppliers.cs` | **Mod**: applier para la marca al retirar (D-18). |
| 15 | `src/CLARIHR.Infrastructure/Localization/BackendMessages{,.es,.es-SV}.resx` | **Mod**: mensajes EN/ES/es-SV de los errores nuevos. |
| 16 | `src/CLARIHR.Application/.../Rehire/` (read model) | **New (Fase 4)**: query `GetEmploymentPeriodsTimeline` (RF-011, derivado). |
| 17 | `tests/CLARIHR.Application.UnitTests/RehireEligibilityRulesTests.cs` · `RehireEmployeeCommandTests.cs` | **New**: pruebas. |
| 18 | `tests/CLARIHR.Api.IntegrationTests/…` | **New**: round-trip de recontratación. |

## 6. Plan por fases (incremental, cada fase desplegable y verificable)

- **Fase 0 — Marca de elegibilidad.** Items #1 (solo campos+marca), #2, #3, #13/#14 (PATCH), #15 parcial. Permite marcar "no recontratable" al retirar. *Salida:* migración + PATCH + test.
- **Fase 1 — Dominio + reglas.** Items #1 (`ReopenForRehire`), #4, #6, #15. Pruebas puras de `RehireEligibilityRules`. *Sin endpoint todavía.*
- **Fase 2 — Núcleo de finalización reutilizable.** Item #7. Refactor con cobertura de los tests de Finalize existentes intactos (no romper comportamiento).
- **Fase 3 — Comando + endpoint + permiso.** Items #5, #9, #10, #11, #12, #13. Handler tests. *Salida:* recontratación funcional end-to-end (sin reactivación fina de usuario).
- **Fase 4 — Re-provisión de usuario.** Item #8 (reactivar desactivados; lógica de email D-09). Handler/integration tests del camino de usuario.
- **Fase 5 — Historial + cierre + E2E.** Item #16 (timeline RF-011), endurecer el cierre del período anterior (paso [3]), integración round-trip, paridad de localización, docs.

## 7. Estrategia de pruebas

- **Reglas puras** (`RehireEligibilityRulesTests`, patrón `EmploymentAssignmentRulesTests`): bloqueado sin permiso → `RequiresAuthorization`; activo/no-empleado → `NotRetired`/`NotAnEmployee`; período no confirmado → `PriorPeriodOpen`; caso feliz → `Success`.
- **Handler** (`RehireEmployeeCommandTests`, fakes `TestPersonnelFileRepository`/`TestPositionSlotRepository`/`TestUserRepository`/`TestCompanyUserProvisioningService`): reabre y re-finaliza; conserva email libre vs exige nuevo si en uso; bloquea sin `AuthorizeRehire`; cierra el período anterior; registra la acción `RECONTRATACION`; atomicidad (rollback en fallo de provisión).
- **Integración** (`CLARIHR.Api.IntegrationTests`, `IntegrationTestWebApplicationFactory.ResetDatabaseAsync()`): crear → finalizar → retirar → **recontratar** → asertar `IsActive`/empleo activo, período anterior presente como histórico, nueva asignación con cupo, acción de auditoría y usuario reactivado.
- **Paridad de localización** (`BackendMessageLocalizationTests`): todos los `REHIRE_*` con EN+ES(+es-SV).

## 8. Verificación (comandos)

```bash
# Compilar
dotnet build CLARIHR.sln

# Migración
dotnet ef migrations add AddRehireBlockToPersonnelFile \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
dotnet ef database update \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj

# Unit + paridad de localización
dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj
dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj --filter "BackendMessageLocalizationTests"

# Integración (Postgres :5432; usar CLARIHR_INTEGRATION_TEST_CONNECTION_STRING si Docker está abajo)
dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj
```

## 9. Riesgos y decisiones técnicas a resolver en diseño/PR

1. **Reactivación del usuario desactivado (alto):** confirmar que `CompanyUserProvisioningService` reactiva user+membership+IAM al reusar; si no, agregarlo. Sin esto, el recontratado queda sin acceso.
2. **Cierre del período anterior (alto):** el retiro hoy **no** garantiza cerrar `ContractHistory`/asignaciones ni registrar acción. El paso [3] del handler debe hacerlo **defensivamente** para que el historial derivado (D-14) y RF-011 sean correctos. Considerar además un comando de retiro dedicado a futuro.
3. **Atómico vs wizard (medio):** se recomienda el comando atómico (§3.3). Confirmar con frontend; si exigen wizard, usar la alternativa con un preview.
4. **Extracción del núcleo de finalización (medio):** refactor de `FinalizePersonnelFile.cs`; mantener verde la batería de tests de Finalize.
5. **`ReopenForRehire` limpia `LinkedUserPublicId` (medio):** un Draft de re-hire abandonado queda sin usuario vinculado (recuperable re-finalizando). Documentar el estado transitorio.
6. **Método para finalizar asignaciones (bajo):** verificar/añadir capacidad de cerrar una asignación activa (`EndDate` + `IsActive=false`) si no existe en el dominio/repositorio.
7. **ADR sugerido:** registrar la decisión "reopen-for-rehire / `Completed → Draft`" como ADR (formato `docs/technical/adr/`), por ser arquitectónicamente significativa.

## 10. Trazabilidad: decisión de negocio → componente técnico

| Decisión | Implementación |
|---|---|
| D-01 reactivar expediente | `ReopenForRehire()` sobre el mismo `PersonnelFile` (§3.1) |
| D-02 por-tenant | Búsqueda/carga ya tenant-scoped; unicidad por (tenant, tipo, número) intacta |
| D-03 nueva antigüedad | Nueva `HireDate` + reset `VacationConfigurationJson` (paso [5]) |
| D-04/D-11/D-18 elegibilidad | `IsRehireBlocked` + `RehireEligibilityRules` + permiso `AuthorizeRehire` (§3.1–3.6) |
| D-08 Completed→Draft | `ReopenForRehire()` transitorio dentro de la TX atómica (§3.1, §4) |
| D-09 email | Reuso si libre; `NewInstitutionalEmail` si en uso (paso [7], §3.5) |
| D-10 permiso nuevo | `PersonnelFilePermissionCodes.AuthorizeRehire` + semilla owner (§3.6) |
| D-12 sin espera mínima | Sin validación temporal en `RehireEligibilityRules` |
| D-13/D-17 cierre/liquidación | Flag `PriorPeriodClosureConfirmed` (confirmación manual) + paso [3] |
| D-14 historial derivado | Sin entidad nueva; timeline proyectado (item #16) |
| D-15/D-19 archivado | Evaluaciones/competencias previas quedan ligadas al histórico, consultables (no migran al nuevo período) |
| D-16 plaza explícita | `PositionSlotPublicId` obligatorio en el comando + `EmploymentAssignmentRules` (paso [6]) |
| RF-009/RN-10 auditoría | `PersonnelAction RECONTRATACION` (paso [9]) |
| RN-11/E9 atomicidad | Una sola transacción con rollback (§4) |

---

> **Nota.** Plan basado en el código a 2026-06-20. Símbolos clave verificados: `FinalizePersonnelFileValidationResolver` (bloqueo `LinkedUserPublicId.HasValue`), `PersonnelFile.Complete/CompleteWithoutLinkedUser/Activate/Inactivate` (sin reopen), `CompanyUserProvisioningService.ProvisionAsync(AllowExistingMembershipReuse)`, `EmploymentAssignmentRules.Evaluate`, `PersonnelFilePermissionCodes`/`ProvisioningConstants`, `BackendMessageLocalizationTests`. Antes de implementar, confirmar los 7 puntos de §9.
