# Guía de integración Frontend — Seguros del Empleado y Beneficiarios

| | |
|---|---|
| **Para** | Equipo Frontend |
| **Tipo** | Guía de integración + **cambios de contrato (BREAKING)** |
| **Módulos** | Expedientes de Personal · Seguros (`insurances`) · Beneficiarios (`beneficiaries`) · Catálogos de referencia (`reference-catalogs`) · Catálogo de monedas (`general-catalogs`) |
| **Documentos base** | `docs/business/analisis-seguros-empleado.md` (D-01…D-15) · `docs/technical/plan-tecnico-seguros.md` |
| **Idioma de errores** | Bilingüe (ES/EN) según `Accept-Language` / claim de idioma; el `code` es estable |

---

## 0. Estado de implementación (backend)

| Pieza | Estado |
|---|---|
| Entidades + CRUD de seguro y beneficiario (12 endpoints REST) | ✅ Ya existía |
| **Catálogo país `insurance-types`** (nombre de seguro) | ✅ Implementado |
| **Catálogo país `insurance-ranges`** (rango, **jerárquico** bajo el seguro) | ✅ Implementado |
| **Validación de catálogo** de nombre de seguro, rango (con pertenencia), moneda ISO-4217, parentesco, tipo de documento | ✅ Implementado |
| **Beneficiario enriquecido**: `documentTypeCode`, `allocationPercentage`, `beneficiaryType` | ✅ Implementado |
| **Regla de suma 100 %** (principales activos ≤ 100) + **anti-duplicado** (póliza / beneficiario) | ✅ Implementado |
| **Validación** de montos ≥ 0 y orden de fechas | ✅ Implementado |
| **Permiso dedicado de lectura `PersonnelFiles.ViewInsurance`** | ✅ Implementado |
| **Auditoría con diff (before/after)** en editar/eliminar | ✅ Implementado |
| Independencia de nómina (las cuotas **no** afectan ingresos/egresos) | ✅ Garantizado |
| Seed SV de catálogos (7 tipos + rangos de ejemplo) | ✅ Implementado (dev) |

> Build verde · **1874 pruebas unitarias en verde**. Esta guía es el contrato vigente. **Sin migración de datos** (D-14): no se preservan registros previos de seguros/beneficiarios.

---

## 1. TL;DR (qué cambió y qué tenés que hacer)

1. **El nombre de seguro y el rango ahora vienen de catálogo** (antes texto libre). Hay que poblar dos `<select>`:
   - **Nombre de seguro** → `insurance-types`.
   - **Rango** → `insurance-ranges`, **dependiente del nombre de seguro** (se carga con `parentCode = insuranceCode`). El rango es **opcional**.
2. **El beneficiario gana 3 campos**: `documentTypeCode` (catálogo `identification-types`), `allocationPercentage` (0–100) y `beneficiaryType` (`PRINCIPAL` / `CONTINGENTE`, default `PRINCIPAL`). **El response del beneficiario cambió de forma** (ver §4) — **BREAKING**.
3. **La moneda se valida contra ISO-4217** (catálogo `currencies`). Si se informa, debe ser un código válido.
4. **Leer seguros requiere el permiso `PersonnelFiles.ViewInsurance`** (o Admin). **No hay autoservicio**: el empleado **no** ve sus propios seguros. Escribir requiere `PersonnelFiles.Manage` (o Admin).
5. **Reglas nuevas de negocio**: la suma de `%` de los beneficiarios **principales activos** no puede exceder **100 %**; no se puede repetir la **misma póliza** en un empleado ni el **mismo beneficiario** (tipo+documento) en un seguro.
6. **El seguro NO afecta la nómina.** Las cuotas (empleado/patronal) son **informativas**; no generan ingresos/egresos ni planilla.
7. **El empleado puede tener varios seguros** (sin "único activo"). El `isActive` del seguro/beneficiario **solo se cambia por PATCH** (el PUT lo preserva).

---

## 2. Modelo conceptual

```
Empleado (PersonnelFile, COMPLETED)
 └─ Seguros (insurances)                      ← 0..N (beneficio; no afecta nómina)
       ├─ insuranceCode      → catálogo insurance-types   (país)
       ├─ rangeCode?         → catálogo insurance-ranges  (país, HIJO de insuranceCode)
       ├─ employeeContribution? / employerContribution?   (informativas)
       ├─ policyNumber? · insuredAmount? · currencyCode? (ISO-4217)
       ├─ isActive · startDateUtc? · endDateUtc?
       └─ Beneficiarios (beneficiaries)        ← 0..N
             ├─ fullName · documentNumber? · documentTypeCode? (identification-types)
             ├─ birthDate? · kinshipCode (kinships)
             ├─ allocationPercentage? (0–100) · beneficiaryType (PRINCIPAL|CONTINGENTE)
             └─ isActive
```

---

## 3. Endpoints

Base: `/api/v1/personnel-files/{publicId}`. El expediente debe estar **COMPLETED**.

### 3.1 Seguros

| Verbo | Ruta | Permiso | Notas |
|---|---|---|---|
| GET | `/insurances` | `ViewInsurance` | Lista (incluye beneficiarios). |
| GET | `/insurances/{insurancePublicId}` | `ViewInsurance` | Detalle. |
| POST | `/insurances` | `Manage` | Crea. `201` + `Location` + `ETag`. |
| PUT | `/insurances/{insurancePublicId}` | `Manage` | Reemplaza campos de negocio (**sin** `isActive`). Header `If-Match`. |
| PATCH | `/insurances/{insurancePublicId}` | `Manage` | JSON Patch (`application/json-patch+json`). `If-Match`. Único camino para `isActive`. |
| DELETE | `/insurances/{insurancePublicId}` | `Manage` | `If-Match`. Borra en cascada los beneficiarios. |

### 3.2 Beneficiarios

Base: `/insurances/{insurancePublicId}/beneficiaries`.

| Verbo | Ruta | Permiso |
|---|---|---|
| GET | `/beneficiaries` | `ViewInsurance` |
| GET | `/beneficiaries/{beneficiaryPublicId}` | `ViewInsurance` |
| POST | `/beneficiaries` | `Manage` |
| PUT | `/beneficiaries/{beneficiaryPublicId}` | `Manage` |
| PATCH | `/beneficiaries/{beneficiaryPublicId}` | `Manage` |
| DELETE | `/beneficiaries/{beneficiaryPublicId}` | `Manage` |

> Todas las escrituras usan **`If-Match`** con el `concurrencyToken` actual; la respuesta devuelve el nuevo token en el header **`ETag`**. DELETE devuelve el `concurrencyToken` del **expediente padre**.

### 3.3 Catálogos (para poblar selectores)

| Selector | Endpoint |
|---|---|
| **Nombre de seguro** | `GET /api/v1/reference-catalogs/insurance-types?countryCode=SV` |
| **Rango** (dependiente) | `GET /api/v1/reference-catalogs/insurance-ranges?countryCode=SV&parentCode={insuranceCode}` |
| **Tipo de documento** (beneficiario) | `GET /api/v1/reference-catalogs/identification-types?countryCode=SV` |
| **Parentesco** (beneficiario) | `GET /api/v1/reference-catalogs/kinships?countryCode=SV` |
| **Moneda** | `GET /api/v1/general-catalogs/currencies?countryCode=SV` |

Respuesta de catálogo (cada ítem): `{ "id": guid, "code": string, "name": string, "sortOrder": int }`. **Enviar el `code`** en los campos `insuranceCode` / `rangeCode` / `documentTypeCode` / `kinshipCode` / `currencyCode`.

---

## 4. Contratos (request / response)

### 4.1 Seguro

**POST (Add) body** — `application/json`:
```jsonc
{
  "insuranceCode": "VIDA",            // requerido, catálogo insurance-types
  "employeeContribution": 12.50,       // opcional, ≥ 0 (informativa)
  "employerContribution": 25.00,       // opcional, ≥ 0 (informativa)
  "rangeCode": "PREMIUM",              // opcional, catálogo insurance-ranges (hijo de insuranceCode)
  "policyNumber": "POL-001",           // opcional (anti-duplicado por empleado)
  "insuredAmount": 50000.00,           // opcional, ≥ 0
  "currencyCode": "USD",               // opcional, ISO-4217
  "isActive": true,                    // requerido (solo en Add)
  "startDateUtc": "2026-01-01T00:00:00Z", // opcional
  "endDateUtc": "2026-12-31T00:00:00Z"    // opcional, ≥ startDateUtc
}
```
**PUT (Update) body**: igual **pero sin** `isActive` (se preserva).

**Response** `PersonnelFileInsuranceResponse`:
```jsonc
{
  "insurancePublicId": "guid",
  "insuranceCode": "VIDA",
  "employeeContribution": 12.50,
  "employerContribution": 25.00,
  "rangeCode": "PREMIUM",
  "policyNumber": "POL-001",
  "insuredAmount": 50000.00,
  "currencyCode": "USD",
  "isActive": true,
  "startDateUtc": "2026-01-01T00:00:00Z",
  "endDateUtc": "2026-12-31T00:00:00Z",
  "beneficiaries": [ /* PersonnelFileInsuranceBeneficiaryResponse[] */ ],
  "concurrencyToken": "guid"
}
```

### 4.2 Beneficiario  ⚠️ **forma del response CAMBIÓ (BREAKING)**

**POST/PUT body**:
```jsonc
{
  "fullName": "Jane Doe",        // requerido
  "documentNumber": "0614...",   // opcional
  "documentTypeCode": "DUI",     // NUEVO — opcional, catálogo identification-types
  "birthDate": "1990-06-15T00:00:00Z", // opcional
  "kinshipCode": "CONYUGE",      // requerido, catálogo kinships
  "allocationPercentage": 50,    // NUEVO — opcional, 0–100
  "beneficiaryType": "PRINCIPAL" // NUEVO — opcional, "PRINCIPAL" | "CONTINGENTE" (default PRINCIPAL)
}
```
**Response** `PersonnelFileInsuranceBeneficiaryResponse` (orden de campos nuevo):
```jsonc
{
  "beneficiaryPublicId": "guid",
  "fullName": "Jane Doe",
  "documentNumber": "0614...",
  "documentTypeCode": "DUI",          // NUEVO
  "birthDate": "1990-06-15T00:00:00Z",
  "kinshipCode": "CONYUGE",
  "allocationPercentage": 50,         // NUEVO
  "beneficiaryType": "PRINCIPAL",     // NUEVO
  "isActive": true,
  "concurrencyToken": "guid"
}
```

### 4.3 PATCH (JSON Patch RFC 6902)

`Content-Type: application/json-patch+json`. Paths soportados:
- **Seguro**: `/insuranceCode`, `/employeeContribution`, `/employerContribution`, `/rangeCode`, `/policyNumber`, `/insuredAmount`, `/currencyCode`, `/startDateUtc`, `/endDateUtc`, `/isActive`.
- **Beneficiario**: `/fullName`, `/documentNumber`, `/documentTypeCode`, `/birthDate`, `/kinshipCode`, `/allocationPercentage`, `/beneficiaryType`, `/isActive`.

`isActive` **no** es removible (solo `replace`). Ejemplo (activar/desactivar):
```json
[ { "op": "replace", "path": "/isActive", "value": false } ]
```

---

## 5. Catálogos: detalle

- **`insurance-types`** (seed SV propuesto, editable por el negocio): `VIDA`, `MEDICO_HOSPITALARIO`, `GASTOS_MEDICOS`, `DENTAL`, `VISION`, `ACCIDENTES`, `OTRO`.
- **`insurance-ranges`** (jerárquico, hijo del seguro): se cargan con `parentCode={insuranceCode}`. Seed de ejemplo: para `VIDA` y `MEDICO_HOSPITALARIO` → `BASICO`, `INTERMEDIO`, `PREMIUM`. Un seguro sin rangos definidos deja el selector **vacío** (válido, el campo es opcional).
- **`identification-types`** (ya existente): `DUI`, `NIT`, `PASSPORT`, `RESIDENT_CARD`.
- **`kinships`** (ya existente): `CONYUGE`, `PAREJA`, `PADRE`, `MADRE`, `HIJO_A`, `HERMANO_A`, `ABUELO_A`, `NIETO_A`, `TIO_A`, `OTRO`.
- **`currencies`**: ISO-4217 (SV opera en `USD`).

> **Patrón dependiente seguro→rango**: al cambiar el `<select>` de nombre de seguro, **recargá** el de rango con `?parentCode={nuevoInsuranceCode}` y **limpiá** el rango seleccionado.

---

## 6. Permisos y visibilidad

| Acción | Permiso requerido |
|---|---|
| Ver seguros y beneficiarios + historial | `PersonnelFiles.ViewInsurance` (o `Admin` / super-admin IAM) |
| Crear/editar/activar/eliminar | `PersonnelFiles.Manage` (o `Admin`) |

- **Sin autoservicio**: el empleado **no** puede ver sus propios seguros (a diferencia de compensación). Si el usuario no tiene `ViewInsurance`, **ocultá** la sección y esperá `403` si igual llama.
- Un usuario con permiso de lectura general del expediente pero **sin** `ViewInsurance` recibirá **403** al consultar seguros: tratalo como "sección no autorizada".

---

## 7. Reglas de UI / validación (replicar en cliente)

1. **Rango dependiente del seguro** (§5). Opcional.
2. **Asignación de beneficiarios (suma 100 %)**: la suma de `allocationPercentage` de los beneficiarios **PRINCIPALES activos** no puede **exceder 100 %** (bloqueo duro del backend → `422`). Mostrá el **total acumulado** en vivo. *El "= 100 %" exacto es completitud*: mostralo como **advertencia suave** (no bloquees por no llegar a 100, para permitir carga incremental). Los **contingentes** y los **inactivos** no cuentan en el 100 %.
3. **`beneficiaryType`**: default `PRINCIPAL`; valores `PRINCIPAL` | `CONTINGENTE`.
4. **Anti-duplicado**: no permitas dos seguros con la **misma póliza** en el empleado, ni dos beneficiarios **activos** con el mismo (tipo+documento) en un seguro (backend → `409`).
5. **Montos ≥ 0** (cuotas, valor asegurado) y **fechas** `start ≤ end`.
6. **Moneda** = código ISO-4217 del catálogo.
7. **El seguro no afecta la nómina**: no muestres las cuotas como deducciones de planilla; son informativas del beneficio.
8. **`isActive`** se cambia **solo por PATCH** (el PUT lo preserva). Para activar/desactivar usá PATCH `/isActive`.
9. **Varios seguros permitidos** por empleado.

---

## 8. Manejo de errores

| Code | HTTP | Disparador | Sugerencia UI |
|---|---|---|---|
| `common.validation` (`insuranceCode`) | 400 | Nombre de seguro fuera de catálogo | Error en el campo |
| `common.validation` (`rangeCode`) | 400 | Rango fuera de catálogo **o** no pertenece al seguro | Error en el campo + recargar opciones |
| `common.validation` (`currencyCode`) | 400 | Moneda no ISO-4217 | Error en el campo |
| `common.validation` (`documentTypeCode`) | 400 | Tipo de documento fuera de catálogo | Error en el campo |
| `common.validation` (`kinshipCode`) | 400 | Parentesco fuera de catálogo | Error en el campo |
| `common.validation` (`startDateUtc`) | 400 | `endDate < startDate` | Error en fechas |
| `common.validation` (montos) | 400 | Monto negativo | Error en el campo |
| `common.validation` (`allocationPercentage`) | 400 | `%` fuera de [0,100] | Error en el campo |
| `INSURANCE_POLICY_DUPLICATE` | 409 | Póliza repetida en el empleado | Toast/mensaje |
| `INSURANCE_BENEFICIARY_DUPLICATE` | 409 | Beneficiario (documento) repetido en el seguro | Toast/mensaje |
| `INSURANCE_BENEFICIARY_ALLOCATION_INVALID` | 422 | Principales activos exceden 100 % | Bloquear guardado + mostrar total |
| `CONCURRENCY_CONFLICT` | 409 | `If-Match` desactualizado | Recargar y reintentar |
| `STATE_RULE_VIOLATION` | 422 | Expediente no completado | La sección solo aplica a expedientes COMPLETED |
| (política) | 403 | Falta `ViewInsurance` (lectura) o `Manage` (escritura) | Ocultar/deshabilitar |

> Los errores de catálogo/campo llegan como `common.validation` (400) con un **diccionario de campos** (`validationErrors`); mapeá la **clave** al campo del formulario. Los errores con `code` dedicado (`INSURANCE_*`) traen **mensaje localizado** (ES/EN).

---

## 9. Concurrencia (optimistic locking)

- Toda escritura (PUT/PATCH/DELETE) requiere el header **`If-Match: "{concurrencyToken}"`** con el token devuelto en el `ETag`/payload de la última lectura o escritura.
- Respuesta `409 CONCURRENCY_CONFLICT` → recargá el recurso, mostrá el estado actual y pedí confirmación antes de reintentar.

---

## 10. Flujos de referencia

**Crear un seguro con beneficiarios:**
1. Abrí el expediente (COMPLETED) → sección Seguros → "Agregar".
2. Seleccioná **nombre de seguro** (`insurance-types`); cargá **rangos** dependientes (`insurance-ranges?parentCode=...`) y elegí uno (opcional).
3. Completá cuotas (≥0), póliza, valor asegurado, **moneda** (ISO-4217), estado activo y, opcionalmente, fechas (`start ≤ end`).
4. `POST /insurances` → guardá el `concurrencyToken` (ETag).
5. Por cada beneficiario: `POST .../beneficiaries` con nombre, documento + **tipo**, parentesco, **% y principal/contingente**. Validá en vivo que los **principales activos** no superen 100 %.

**Activar/desactivar** (seguro o beneficiario): `PATCH` con `replace /isActive`.

---

## 11. Notas y pendientes (informativo)

- **Historial/diff visible (D-15):** el backend ya **audita con before/after** cada alta/edición/baja de seguro y beneficiario. La **vista de historial** (endpoint de lectura de auditoría por entidad) depende del módulo de Auditoría y **queda como siguiente paso**; cuando exista, esta sección lo consumirá.
- **Permiso `ViewInsurance`:** el gate fino vive en el handler (la sección de seguros comparte controlador con otras sub-recursos de compensación). En la práctica el rol que ve seguros necesita `ViewInsurance` (o Admin); confirmá la asignación del permiso en Provisioning.
- **Seed de catálogos:** los `insurance-types`/`insurance-ranges` están sembrados para **dev**. Para producción, el **seed por país** se define en el aprovisionamiento/onboarding; la **lista exacta de tipos y rangos la confirma el negocio** (el seed actual es un punto de partida editable).
- **Sin impacto en nómina (D-01):** garantizado; no hay enlace a Ingresos/Egresos ni planilla.
