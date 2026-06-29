# Plan técnico — Requisitos mínimos para publicar un expediente (Borrador → Activo)

| | |
| --- | --- |
| **Objetivo** | Que el backend devuelva la lista COMPLETA de requisitos mínimos que faltan para que un expediente pase de borrador a publicado/activo, para guiar al usuario con un checklist. |
| **Estado** | Diseño (Bug 3 ya implementado; checklist completo pendiente de ratificación de negocio) |
| **Fecha** | 2026-06-28 |
| **Endpoints** | `GET …/personnel-files/{publicId}/finalize/preview` · `PATCH …/personnel-files/{publicId}/finalize` |

## 1. Resumen ejecutivo

El mecanismo de "requisitos mínimos para publicar" **ya existe** como cimiento: `finalize/preview` devuelve `{ isEligible, issues[] }` donde cada issue es `{ code, message, section, fieldKey, navigationKey, isBlocking }`. **No hay que inventar un contrato nuevo** — hay que (a) dejar de hacer *short-circuit* para que liste TODO lo que falta de una vez, (b) ampliar las reglas más allá de email+plaza a los verdaderos prerrequisitos de publicación, y (c) usar el flag `isBlocking` (hoy siempre `true`) para separar **bloqueos** de **advertencias**.

El estado del expediente es binario: `PersonnelFileLifecycleStatus.Draft = 1 → Completed = 2` (`PersonnelFileEnums.cs`). "Publicar/activar" == esa transición, que hace `finalize`. No hay otro motor de completitud.

## 2. Estado actual (qué valida hoy `finalize/preview`)

Motor: `FinalizePersonnelFileValidationResolver.ValidateAsync` (`src/CLARIHR.Application/Features/PersonnelFiles/FinalizePersonnelFile.cs`). Reglas que emite hoy:

| # | Código | section / fieldKey | Bloqueo | Flujo |
|---|---|---|---|---|
| 1 | `PERSONNEL_FILE_FINALIZE_ONLY_EMPLOYEE` | personnel-file / recordType | sí | **early return** |
| 2 | `PERSONNEL_FILE_STATE_RULE_VIOLATION` | personnel-file / lifecycleStatus | sí | **early return** |
| 3 | `PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL` | personnel-file / institutionalEmail | sí | acumula |
| 4 | `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT` | employment / assignedPositionSlotPublicId | sí | **early return** |
| 5 | `…TENANT_MISMATCH` / `POSITION_SLOT_NOT_FOUND` | employment / assignedPositionSlotPublicId | sí | early return |
| 6 | `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE` | employment / assignedPositionSlotPublicId | sí | acumula (solo si `createUserAccount`) |
| 7 | `PERSONNEL_FILE_LINKED_USER_CONFLICT` | personnel-file / institutionalEmail | sí | acumula (solo si `createUserAccount`) |

**Dos límites para ser un "checklist guiado":**
1. **Hace short-circuit** (casos 1, 2, 4, 5 hacen `return`): el preview nunca devuelve la foto completa de todo lo que falta — solo el primer bloqueo.
2. **Es un gate de aprovisionamiento de cuenta**, no un checklist de expediente: solo valida tipo=Employee, estado=Draft, email institucional, plaza + rol, conflicto de usuario. **No** valida identidad, contrato, compensación, etc.
3. `isBlocking` existe en el contrato pero **nunca se pone en `false`** (no hay concepto de advertencia todavía).

**Ya resuelto (Bug 3, esta entrega):** cuando el cliente no envía `positionSlotPublicId`, el resolver ahora **deduce la plaza activa+primaria** del agregado (`GetActivePrimaryPositionSlotPublicIdAsync`) en vez de reportarla como ausente. Mantiene `positionSlotPublicId` como override y sigue exigiendo selección si hay varias plazas activas sin primaria.

## 3. Diseño propuesto

### 3.1 Quitar el short-circuit (acumular todo)
Refactor de `ValidateAsync`: en vez de `return` temprano en cada bloqueo, **acumular todos los issues** y devolverlos juntos. Excepción razonable: los casos 1 y 2 (no es Employee, o ya no es Draft) sí cortan, porque invalidan toda la evaluación (no tiene sentido listar requisitos de un no-empleado o de un expediente ya publicado).

### 3.2 Ampliar el catálogo de requisitos
Propuesta de requisitos mínimos a evaluar (➡️ **negocio debe ratificar cuáles son BLOQUEO y cuáles ADVERTENCIA**):

| Requisito | Sección | fieldKey | Propuesta | Cómo evaluar |
|---|---|---|---|---|
| Nombre completo | personnel-file | fullName | Bloqueo | ya obligatorio al crear; revalidar no-vacío |
| Fecha de nacimiento | personal-info | birthDate | Bloqueo | no-nula |
| Al menos una identificación (DUI/NIT) | personal-info | identifications | Bloqueo | ≥1 `PersonnelFileIdentification` activa |
| Nacionalidad | personal-info | nationality | Advertencia | no-vacía |
| Plaza activa+primaria | employment | assignedPositionSlotPublicId | Bloqueo | **ya** (deducida, Bug 3) |
| Contrato vigente | employment | contractHistory | Bloqueo | ≥1 `PersonnelFileContractHistory` activa con tipo + fecha inicio |
| Fecha de ingreso | employment | hireDate | Bloqueo | no-nula |
| Salario base | compensation | compensationConcepts | **A definir** | ≥1 `CompensationConcept` SALARIO_BASE de la plaza activa+primaria |
| Email institucional | personnel-file | institutionalEmail | Bloqueo (si crea cuenta) | **ya** |
| Rol de la plaza | employment | assignedPositionSlotPublicId | Bloqueo (si crea cuenta) | **ya** |
| Sin conflicto de usuario | personnel-file | institutionalEmail | Bloqueo (si crea cuenta) | **ya** |

> El salario base es la decisión más delicada: las constancias de salario/embajada (módulo de constancias) lo necesitan, pero si la nómina aún no está viva quizá deba ser **advertencia** y no bloqueo. Negocio decide.

### 3.3 Usar `isBlocking`
- `isEligible` pasa a ser `Issues.Where(i => i.IsBlocking).Count == 0` (las advertencias no bloquean la publicación pero se muestran).
- El FE pinta bloqueos en rojo (impiden publicar) y advertencias en ámbar (informativas).

### 3.4 Sin cambio de contrato
El DTO `FinalizePersonnelFilePreviewResponse` / `FinalizePersonnelFilePreviewIssueResponse` ya sirve. `navigationKey` ya enruta a `personnel-files` / `personal-info` / `employment-information` para los deep-links del checklist.

## 4. Plan de implementación (PRs)

- **PR-1 (hecho):** Bug 3 — deducir plaza activa+primaria en el resolver (`GetActivePrimaryPositionSlotPublicIdAsync`) + 2 tests de regresión.
- **PR-2:** Refactor anti-short-circuit del resolver (acumular issues) + tests de que el preview lista múltiples faltantes a la vez.
- **PR-3:** Reglas de identidad (nombre, fecha nacimiento, identificación, nacionalidad) + códigos de error `PERSONNEL_FILE_FINALIZE_REQUIRES_*` bilingües (resx) + tests.
- **PR-4:** Reglas de empleo (contrato vigente, fecha de ingreso) — requiere consultar `PersonnelFileContractHistory` activo desde el resolver (inyectar el repo, como ya se hizo para la plaza).
- **PR-5:** Regla de compensación (salario base) — según ratificación; consulta `CompensationConcept` de la plaza activa+primaria (la consulta de plaza ya existe).
- **PR-6:** Habilitar `isBlocking` (bloqueos vs advertencias) + ajustar `isEligible` + guía de integración frontend.

## 5. Decisiones de negocio a ratificar (bloquean PR-3..6)

1. **¿Qué requisitos son BLOQUEO y cuáles ADVERTENCIA?** (ver tabla 3.2). En particular: **salario base** ¿bloquea publicar o solo advierte?
2. **Identificación obligatoria:** ¿basta una identificación de cualquier tipo, o se exige DUI específicamente para SV?
3. **Contrato:** ¿se exige contrato vigente para publicar, o el expediente puede publicarse con la plaza sin contrato formal cargado?
4. **¿La publicación SIN crear cuenta de usuario** (`createUserAccount=false`) relaja algún requisito? Hoy email/rol/conflicto solo aplican cuando se crea cuenta.

## 6. Notas

- El resolver es compartido por `preview` (GET) y `finalize` (PATCH): una sola implementación cubre ambos.
- Toda regla nueva que consulte datos del expediente debe inyectar el repositorio correspondiente en los dos handlers (`FinalizePersonnelFileCommandHandler`, `PreviewFinalizePersonnelFileQueryHandler`) y pasarlo a `ValidateAsync` — patrón ya establecido por el Bug 3.
- Relación: el módulo de constancias (`analisis-consulta-solicitudes-constancia`) consume salario server-side; si el salario base es bloqueo de publicación, las constancias de salario quedan garantizadas para todo expediente activo.
