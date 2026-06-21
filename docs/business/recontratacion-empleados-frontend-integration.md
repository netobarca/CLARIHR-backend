# Guía de integración Frontend — Recontratación de Empleados

| | |
|---|---|
| **Para** | Equipo Frontend |
| **Tipo** | Guía de integración + **contrato de API nuevo** (aditivo, sin breaking changes) |
| **Módulos** | Personnel Files · Employee Profile · Finalización · Employment Assignments · Contract History · Personnel Actions · Identity (provisión) |
| **Análisis funcional** | [`docs/business/analisis-recontratacion-empleados.md`](./analisis-recontratacion-empleados.md) |
| **Plan técnico** | [`docs/technical/plan-tecnico-recontratacion-empleados.md`](../technical/plan-tecnico-recontratacion-empleados.md) |
| **Idioma de mensajes** | Bilingüe (ES / EN según `Accept-Language`) |

---

## 1. TL;DR (qué hay de nuevo y qué tenés que hacer)

1. **Un nuevo endpoint atómico** recontrata a un ex‑empleado **reutilizando su mismo expediente**:
   `POST /api/v1/personnel-files/{publicId}/rehire`. En **una sola llamada** cierra el período anterior, reabre el expediente, abre el **nuevo período** (nueva fecha de ingreso, contrato, plaza), re‑aprovisiona el usuario y registra la acción `RECONTRATACION`.
2. **No se crea un expediente nuevo.** La recontratación **siempre** opera sobre el expediente existente del ex‑empleado (una sola ficha por persona y por empresa). Para localizarlo usá la **búsqueda de expedientes por documento que ya existe** (no cambia).
3. **Nuevo período = nueva antigüedad.** La antigüedad/vacaciones se reinician desde `newHireDate`. Mostralo explícito en la UI para evitar errores legales.
4. **Elegibilidad "no recontratable".** Un expediente puede marcarse como no recontratable **al retirarlo** (vía el `PATCH` del expediente, campos nuevos `isRehireBlocked` / `rehireBlockedReason`). Recontratar a un marcado **exige** un permiso nuevo (`PersonnelFiles.AuthorizeRehire`) **y** una justificación.
5. **Confirmación manual de cierre/liquidación.** Mientras no exista módulo de nómina, el flujo **exige** que el usuario confirme manualmente que el período anterior está cerrado/liquidado (`priorPeriodClosureConfirmed: true`).
6. **Email institucional (D‑09).** Por defecto se conserva el email anterior; si ese email **ya está en uso** por otra ficha, el backend responde `409 REHIRE_INSTITUTIONAL_EMAIL_IN_USE` y debés **pedir un email nuevo** (`newInstitutionalEmail`) y reintentar.
7. **Nuevo endpoint de solo lectura** para la **línea de tiempo de períodos**: `GET /api/v1/personnel-files/{publicId}/employment-periods` (RF‑011).
8. **Concurrencia con `If-Match`** (igual que el resto del módulo): el `concurrencyToken` del expediente viaja en el header, **no** en el body.

> ✅ **No hay breaking changes.** Todo lo anterior es **aditivo**. Los contratos existentes (employee-profile, employment-assignments, contract-history, finalize, personnel-actions) no cambian.

---

## 2. Modelo conceptual

Un empleado tiene **períodos laborales**. Cada vez que se retira y luego se recontrata, se abre un **nuevo período** sobre el **mismo expediente**:

```
PersonnelFile (mismo expediente, misma cédula)
 ├─ Período 1:  ingreso 2020‑01‑01 → retiro 2024‑01‑31   (histórico, inmutable)
 └─ Período 2:  ingreso 2026‑06‑01 → (vigente)            ← lo abre /rehire
```

El backend **deriva** la línea de tiempo de los registros existentes (contratos + asignaciones + acciones de personal); **no** hay una entidad/tabla nueva de "período". Cada **contrato** representa un período.

Estado de partida de una recontratación: el expediente está **`Completed` + `isActive=false`** y su perfil laboral tiene **`isEmploymentActive=false`** (retirado). Tras `/rehire` queda **`Completed` + `isActive=true`** con **`isEmploymentActive=true`**.

---

## 3. Endpoint principal — Recontratar

### `POST /api/v1/personnel-files/{publicId}/rehire`

Recontrata al ex‑empleado identificado por `publicId` (su expediente). **Atómico**: o se aplica todo, o nada.

**Headers**
| Header | Obligatorio | Valor |
|---|---|---|
| `If-Match` | Sí | `"{concurrencyToken}"` del expediente (con comillas). Igual que finalize. |
| `Content-Type` | Sí | `application/json` |
| `Accept-Language` | Opcional | `es` / `en` para localizar los errores |

**Body (`RehireEmployeeRequest`)**

| Campo | Tipo | Obligatorio | Notas |
|---|---|---|---|
| `newHireDate` | `date-time` (UTC) | Sí | Nueva fecha de ingreso (inicio del nuevo período). |
| `contractTypeCode` | `string` | Sí | Tipo de contrato del nuevo período (máx. 80). |
| `contractStartDate` | `date-time` (UTC) | Sí | Inicio del nuevo contrato. |
| `contractEndDate` | `date-time?` (UTC) | No | Fin del contrato; si va, debe ser **> `contractStartDate`**. `null` = indefinido. |
| `positionSlotPublicId` | `guid` | Sí | Plaza del nuevo período. Se **elige explícitamente** (puede ser la misma de antes; el backend **no** la propone — D‑16). Debe estar vigente y con cupo. |
| `assignmentTypeCode` | `string` | Sí | Tipo de asignación, **del catálogo** `assignment-types` (`CurriculumAssignmentType`). |
| `createUserAccount` | `bool?` | No (default `true`) | Si `true`, re‑aprovisiona/reactiva la cuenta de usuario (requiere email institucional y que la plaza tenga rol). Si `false`, solo reactiva el expediente sin cuenta. |
| `newInstitutionalEmail` | `string?` (email) | **Condicional** | **Requerido solo** si el email anterior ya está en uso (ver §6). Si no, se conserva el actual. |
| `priorPeriodClosureConfirmed` | `bool` | Sí | Debe ser **`true`**: confirmación manual de que el período anterior está cerrado/liquidado (D‑13/D‑17). |
| `authorizationReason` | `string?` | **Condicional** | **Requerido** si el expediente está marcado "no recontratable" (máx. 500). Justificación del override. |

> El `concurrencyToken` **no** va en el body; va en `If-Match`.

**Respuesta `200 OK` (`RehireEmployeeResponse`)**

```jsonc
{
  "personnelFile": {            // expediente actualizado (PersonnelFileResponse)
    "publicId": "…",           // ⚠️ el id del expediente se serializa como `publicId`
    "lifecycleStatus": "Completed",
    "isActive": true,
    "institutionalEmail": "…",
    "linkedUserPublicId": "…",  // null si createUserAccount=false
    "concurrencyToken": "…",    // NUEVO token → usalo para la próxima operación
    "…": "…"
  },
  "user": {                      // CompanyUserResponse | null (null si no se aprovisionó cuenta)
    "…": "…"
  },
  "invitationExpiresUtc": null   // fecha de expiración de invitación, si se emitió
}
```

> **Convención de nombres (PublicContract resolver):** en las **responses** los identificadores se exponen como `publicId`, `companyPublicId`, `orgUnitPublicId`, `linkedUserPublicId`, `positionSlotPublicId`, etc. (no `id`). Tenelo en cuenta al parsear.

**Curl de ejemplo**

```bash
curl -X POST "$API/api/v1/personnel-files/$PF/rehire" \
  -H "Authorization: Bearer $TOKEN" \
  -H "If-Match: \"$CONCURRENCY_TOKEN\"" \
  -H "Content-Type: application/json" \
  -H "Accept-Language: es" \
  -d '{
    "newHireDate": "2026-06-01T00:00:00Z",
    "contractTypeCode": "INDEFINIDO",
    "contractStartDate": "2026-06-01T00:00:00Z",
    "contractEndDate": null,
    "positionSlotPublicId": "…",
    "assignmentTypeCode": "INDEFINIDO",
    "createUserAccount": true,
    "newInstitutionalEmail": null,
    "priorPeriodClosureConfirmed": true,
    "authorizationReason": null
  }'
```

**Efectos (todo en una transacción):**
- Cierra el contrato y las asignaciones activas del período anterior (quedan como histórico inmutable).
- Reactiva el expediente y abre el nuevo período: nueva `hireDate`, empleo activo, campos de retiro **limpios**, vacaciones reiniciadas.
- Crea el **nuevo contrato** activo y la **nueva asignación de plaza** (principal), aplicando las reglas multi‑plaza (cupo/estado/vigencia).
- Re‑aprovisiona/reactiva la cuenta de usuario (si `createUserAccount=true`).
- Registra la acción de personal `RECONTRATACION` (auditoría, con la justificación si hubo override).

---

## 4. Localización del ex‑empleado (sin cambios de API)

La búsqueda de expedientes por documento **ya existe** y no cambia. El flujo de FE:

1. El usuario busca por **tipo + número de documento** (dentro de su empresa).
2. Si **no existe** → ofrecé **"crear nueva contratación"** (alta normal). No es recontratación.
3. Si existe y está **activo** (`isEmploymentActive=true`) → **no** habilites recontratar (ya está activo; ofrecé editar).
4. Si existe y está **retirado** (`Completed` + `isActive=false`, perfil `isEmploymentActive=false`) → mostrá el **resumen del período anterior** (usá `GET …/employment-periods`, §7) y la acción **"Recontratar"**.

---

## 5. Elegibilidad: marca "no recontratable" + autorización

### 5.1 Fijar la marca al retirar — `PATCH /api/v1/personnel-files/{publicId}`

El `PATCH` del expediente (JSON Patch RFC 6902) acepta **dos paths nuevos**, pensados para fijarse **al retirar**, normalmente en la misma operación que pone `isActive=false`:

| Path | Tipo | Descripción |
|---|---|---|
| `/isRehireBlocked` | `bool` | Marca "no recontratable". |
| `/rehireBlockedReason` | `string?` | Justificación del bloqueo (máx. 500). |

```jsonc
PATCH /api/v1/personnel-files/{publicId}
If-Match: "{concurrencyToken}"
Content-Type: application/json-patch+json

[
  { "op": "replace", "path": "/isActive", "value": false },
  { "op": "replace", "path": "/isRehireBlocked", "value": true },
  { "op": "replace", "path": "/rehireBlockedReason", "value": "Incumplimiento grave" }
]
```

- Requiere `PersonnelFiles.Admin` (gestión de expedientes; el **owner** lo tiene). 
- La marca vive a nivel de **expediente** y **sobrevive** a la recontratación (se conserva entre períodos). Para quitarla: `"/isRehireBlocked": false`.

### 5.2 Recontratar a un marcado (override controlado)

- Si el expediente tiene `isRehireBlocked=true`, `/rehire` **exige**:
  1. que el usuario tenga el permiso **`PersonnelFiles.AuthorizeRehire`** (permiso **nuevo**, distinto de `Admin`/gestión), **y**
  2. que el body traiga `authorizationReason` (no vacío).
- Si falta cualquiera de las dos → `422 REHIRE_REQUIRES_AUTHORIZATION`.
- UX sugerida: cuando el FE detecta `isRehireBlocked=true`, mostrá una **advertencia bloqueante** con el motivo (`rehireBlockedReason`) y pedí la justificación. Si el usuario **no** tiene `AuthorizeRehire`, deshabilitá la confirmación y sugerí escalar a un rol facultado (Jefe de RRHH).

> **Importante:** `PersonnelFiles.AuthorizeRehire` **no** está implícito en `PersonnelFiles.Admin`. Un analista que gestiona expedientes **no** puede auto‑autorizar el override; necesita el permiso dedicado (que el owner u otro rol facultado puede otorgar por la matriz RBAC).

---

## 6. Email institucional (D‑09)

- Por defecto se **conserva** el email institucional anterior si está **libre** → la cuenta del recontratado se **reactiva** con ese email.
- Si el email anterior **ya está en uso** por otra ficha/usuario del tenant, `/rehire` responde **`409 REHIRE_INSTITUTIONAL_EMAIL_IN_USE`**.
  - El FE debe **pedir un email nuevo** y reintentar con `newInstitutionalEmail`.
- Si `createUserAccount=true` y el expediente **no** tiene email institucional, el backend responde `422 PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL`. Capturá uno (en `newInstitutionalEmail`) antes de reintentar.

---

## 7. Línea de tiempo de períodos (RF‑011)

### `GET /api/v1/personnel-files/{publicId}/employment-periods`

Solo lectura. Requiere `PersonnelFiles.Read`. Deriva los períodos del historial de contratos (cada contrato = un período), en orden cronológico ascendente.

**Respuesta `200 OK`**

```jsonc
{
  "personnelFileId": "…",
  "periodCount": 2,
  "periods": [
    {
      "sequence": 1,
      "startDate": "2020-01-01T00:00:00Z",
      "endDate":   "2026-06-01T00:00:00Z",   // cerrado al recontratar
      "contractTypeCode": "INDEFINIDO",
      "positionSlotPublicId": null,
      "isCurrent": false,
      "notes": "Periodo anterior"
    },
    {
      "sequence": 2,
      "startDate": "2026-06-01T00:00:00Z",
      "endDate":   null,
      "contractTypeCode": "INDEFINIDO",
      "positionSlotPublicId": "…",
      "isCurrent": true,                       // período vigente destacado
      "notes": null
    }
  ]
}
```

> El detalle fino de cada período (motivo de retiro, plazas múltiples, acciones) se consulta con los endpoints existentes: `…/contract-history`, `…/employment-assignments`, `…/personnel-actions`, `…/employee-profile`.

---

## 8. Catálogo de errores (qué mostrar)

Todos siguen el formato `ProblemDetails` con un campo `code` y mensaje localizado (ES/EN según `Accept-Language`).

| `code` | HTTP | Cuándo | Acción sugerida en FE |
|---|---|---|---|
| `REHIRE_NOT_AN_EMPLOYEE` | 422 | El expediente no es de tipo empleado. | No habilitar recontratación. |
| `REHIRE_NOT_RETIRED` | 422 | El empleado está activo (o no tiene período previo). | Indicar que ya está activo; ofrecer editar. |
| `REHIRE_PRIOR_PERIOD_OPEN` | 422 | Falta confirmar el cierre/liquidación del período anterior. | Pedir el check de confirmación (`priorPeriodClosureConfirmed`). |
| `REHIRE_REQUIRES_AUTHORIZATION` | 422 | Expediente "no recontratable" sin permiso/justificación. | Mostrar advertencia + pedir autorización (permiso `AuthorizeRehire` + motivo). |
| `REHIRE_INSTITUTIONAL_EMAIL_IN_USE` | 409 | El email anterior ya está en uso. | Pedir `newInstitutionalEmail` y reintentar. |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` desactualizado. | Recargar el expediente y reintentar con el token nuevo. |
| `PERSONNEL_FILE_NOT_FOUND` | 404 | Expediente inexistente en el tenant. | Tratar como "no existe" → alta nueva. |
| `PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL` | 422 | `createUserAccount=true` sin email institucional. | Capturar email (`newInstitutionalEmail`). |
| `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE` | 422 | La plaza no tiene rol IAM y se pidió crear cuenta. | Asignar rol a la plaza o `createUserAccount=false`. |
| `PERSONNEL_FILE_LINKED_USER_CONFLICT` | 409 | El email mapea a un usuario ligado a otra ficha. | Equivalente a email en uso → pedir email nuevo. |
| `EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_FOUND` | 404 | La plaza elegida no existe. | Elegir otra plaza. |
| `EMPLOYMENT_ASSIGNMENT_POSITION_SLOT_NOT_ASSIGNABLE` | 422 | Plaza suspendida o fuera de vigencia para la fecha. | Revisar vigencia/estado; ajustar `newHireDate` o plaza. |
| `EMPLOYMENT_ASSIGNMENT_CAPACITY_EXCEEDED` | 422 | La plaza no tiene cupo para el período. | Elegir otra plaza o liberar cupo. |
| `EMPLOYMENT_ASSIGNMENT_TYPE_CODE_INVALID` | 422 | `assignmentTypeCode` no está en el catálogo. | Usar un código válido de `assignment-types`. |

---

## 9. Permisos (RBAC)

| Capacidad | Permiso |
|---|---|
| Localizar ex‑empleado, ejecutar la recontratación, capturar el nuevo período, ver la línea de tiempo | `PersonnelFiles.Admin` (gestión) / `PersonnelFiles.Read` (solo lectura) |
| Fijar/quitar la marca "no recontratable" al retirar | `PersonnelFiles.Admin` |
| **Autorizar** la recontratación de un "no recontratable" (override) | **`PersonnelFiles.AuthorizeRehire`** (permiso **nuevo**; el **owner** lo tiene por defecto; se otorga por la matriz RBAC) |

---

## 10. Flujo de UI recomendado (wizard)

1. **Localizar** al ex‑empleado por documento → validar que está **retirado**.
2. **Resumen del período anterior** (`GET …/employment-periods`) + leyenda **"Nuevo vínculo: la antigüedad se reinicia desde la nueva fecha de ingreso"**.
3. **Confirmar cierre/liquidación** del período anterior (checkbox → `priorPeriodClosureConfirmed: true`). Sin esto, no habilites continuar.
4. **Si `isRehireBlocked=true`**: advertencia con el motivo + captura de **justificación**; si el usuario no tiene `AuthorizeRehire`, bloqueá y sugerí escalar.
5. **Datos del nuevo período**: `newHireDate`, contrato (`contractTypeCode`, fechas), **plaza** (`positionSlotPublicId`) elegida explícitamente y `assignmentTypeCode` (del catálogo).
6. **Datos no laborales**: vienen pre‑cargados del expediente (se editan con los endpoints existentes si hace falta; la identificación no cambia).
7. **Email**: dejá vacío para conservar el anterior; si el backend responde `409 …EMAIL_IN_USE`, pedí `newInstitutionalEmail` y reintentá.
8. **Enviar** `POST …/rehire` con `If-Match`.
9. **Éxito**: mostrá el expediente activo, el **nuevo período** y la **línea de tiempo** (2+ períodos). Guardá el **nuevo `concurrencyToken`** de la respuesta.

---

## 11. Notas de implementación / fuera de alcance

- **Atomicidad:** si algo falla (p. ej. cupo de plaza, email en conflicto), **no** queda nada a medias; el expediente sigue como estaba. Reintentá tras corregir.
- **Concurrencia:** siempre `If-Match` con el `concurrencyToken` vigente; ante `409 CONCURRENCY_CONFLICT`, recargá.
- **Multi‑empresa:** la recontratación es **intra‑empresa**. La misma persona en otra empresa es un vínculo independiente (no es recontratación).
- **Fuera de alcance (esta fase):** cálculo de liquidación/prestaciones del período anterior, recontratación masiva/por lote, transferencia cross‑empresa, recontratación de candidatos (solo empleados), notificaciones/onboarding automático (RF‑010, opcional).
- **Evaluaciones/competencias previas** quedan **archivadas** (consultables como histórico del período anterior; no continúan activas en el nuevo período).
