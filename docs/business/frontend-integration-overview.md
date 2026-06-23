# Guía de integración Frontend — Resumen del release (rama `feature/seguros-empleado-fase1`)

| | |
|---|---|
| **Para** | Equipo Frontend |
| **Tipo** | Punto de entrada / índice + **cambios de contrato (BREAKING)** consolidados |
| **Alcance** | Cambios FE-facing acumulados en esta rama: **Seguros del Empleado** (feature nueva) + **Información Laboral** (catálogo `employment-statuses` + `institutionalEmail` editable) |
| **Idioma de errores** | Bilingüe (ES/EN); el `code` es estable |

> Este documento es el **mapa de entrada**. Cada área tiene su guía detallada (§0). Aquí va lo transversal: breaking changes, catálogos nuevos, errores, permisos y un checklist consolidado.

---

## 0. Mapa de documentos detallados

| Área | Guía detallada |
|---|---|
| **Seguros del Empleado + Beneficiarios** | [`docs/business/seguros-empleado-frontend-integration.md`](./seguros-empleado-frontend-integration.md) |
| **Información Laboral** (estado de empleo + correo institucional) | [`docs/business/employment-information-frontend-integration.md`](./employment-information-frontend-integration.md) |

> Para el contrato campo-por-campo, endpoints y ejemplos JSON: **leé la guía detallada del área**. Lo de abajo es el resumen ejecutivo.

---

## 1. TL;DR — qué tenés que hacer

1. **Seguros (feature nueva):** nueva sección en el expediente para registrar **seguros** y **beneficiarios**. Nombre de seguro y rango salen de **catálogo** (rango dependiente del seguro); el beneficiario gana **tipo de documento**, **% de asignación** y **principal/contingente**. Leer requiere el permiso **`ViewInsurance`**. → guía de Seguros.
2. **Información Laboral:** el **estado del empleado** ahora viene de catálogo (`employment-statuses`, antes daba 404 → ya funciona) y el **correo institucional es editable** desde el `PUT` (es el login del empleado, re-sincroniza la cuenta). → guía de Información Laboral.
3. **Sin migración de datos** en estas áreas (greenfield): no se preservan registros previos de seguros/beneficiarios.

---

## 2. Breaking changes consolidados

| # | Cambio | Impacto FE |
|---|---|---|
| B-1 | **Beneficiario de seguro: forma del response cambió** (3 campos nuevos: `documentTypeCode`, `allocationPercentage`, `beneficiaryType`). | Actualizar el modelo/parseo del beneficiario. |
| B-2 | **Nombre de seguro y rango ahora son catálogo** (antes texto libre). | Reemplazar inputs libres por `<select>` (rango dependiente). |
| B-3 | **Leer seguros exige `PersonnelFiles.ViewInsurance`** (sin autoservicio). | Ocultar la sección si falta el permiso; manejar `403`. |
| B-4 | **`employmentStatusCode` ya no es texto libre** → catálogo `employment-statuses`. | Dropdown alimentado del catálogo; manejar `422`. |
| B-5 | **`institutionalEmail` ahora editable** en el `PUT` de Información Laboral (es el login). | Permitir editarlo; avisar que cambia el correo de inicio de sesión; manejar `409`. |

---

## 3. Seguros del Empleado (resumen)

- **Endpoints:** CRUD de seguros y beneficiarios bajo `/api/v1/personnel-files/{publicId}/insurances[/{id}][/beneficiaries[/{id}]]` (12 endpoints). `If-Match`/`ETag` en escrituras; `isActive` solo por **PATCH**.
- **Selectores (catálogos):**
  - Nombre de seguro: `GET /api/v1/reference-catalogs/insurance-types?countryCode=SV`
  - Rango (**dependiente**): `GET /api/v1/reference-catalogs/insurance-ranges?countryCode=SV&parentCode={insuranceCode}`
  - Tipo de documento del beneficiario: `…/reference-catalogs/identification-types?countryCode=SV`
  - Parentesco: `…/reference-catalogs/kinships?countryCode=SV`
  - Moneda: `…/general-catalogs/currencies?countryCode=SV`
- **Reglas de UI clave:** suma de `%` de beneficiarios **principales activos ≤ 100 %** (mostrar total; `=100` es completitud, no bloqueo); **anti-duplicado** de póliza por empleado y de beneficiario por seguro; montos ≥ 0; fechas `start ≤ end`; **el seguro no afecta la nómina** (cuotas informativas); **varios seguros** permitidos.
- **Permiso:** lectura `PersonnelFiles.ViewInsurance` (sin autoservicio); escritura `PersonnelFiles.Manage`.

→ **Detalle completo** (contratos, JSON, flujos): [`seguros-empleado-frontend-integration.md`](./seguros-empleado-frontend-integration.md).

---

## 4. Información Laboral (resumen)

- **Estado del empleado (catálogo):** `GET /api/v1/general-catalogs/employment-statuses?countryCode=SV`. Reemplazar el input libre por dropdown. Código inválido → `422 EMPLOYMENT_STATUS_CODE_INVALID`. *(Antes este catálogo daba 404; ya está sembrado y responde.)*
- **Correo institucional editable:** en el `PUT` de Información Laboral, `institutionalEmail` ahora se puede **cambiar**. Es el **login** del empleado → al cambiarlo se re-sincroniza su cuenta (mismo password, nuevo correo). **Omitir/`null` = sin cambios** (no se puede vaciar mientras haya cuenta vinculada). Correo en uso → `409 PERSONNEL_FILE_LINKED_USER_CONFLICT`; formato inválido → `422`.

→ **Detalle completo:** [`employment-information-frontend-integration.md`](./employment-information-frontend-integration.md).

---

## 5. Catálogos nuevos (consolidado)

| Selector | Endpoint | Notas |
|---|---|---|
| Nombre de seguro | `/api/v1/reference-catalogs/insurance-types?countryCode=SV` | País-scoped. |
| Rango de seguro | `/api/v1/reference-catalogs/insurance-ranges?countryCode=SV&parentCode={insuranceCode}` | **Dependiente** del seguro; opcional. |
| Estado del empleado | `/api/v1/general-catalogs/employment-statuses?countryCode=SV` | Ya sembrado (antes 404). |

Respuesta de cada ítem de catálogo: `{ "id": guid, "code": string, "name": string, "sortOrder": int }`. **Enviar el `code`** en los campos correspondientes.

> Catálogos ya existentes reutilizados: `identification-types`, `kinships`, `currencies`.

---

## 6. Errores nuevos (consolidado)

| Code | HTTP | Área | Disparador |
|---|---|---|---|
| `INSURANCE_POLICY_DUPLICATE` | 409 | Seguros | Póliza repetida en el empleado |
| `INSURANCE_BENEFICIARY_DUPLICATE` | 409 | Seguros | Beneficiario (documento) repetido en el seguro |
| `INSURANCE_BENEFICIARY_ALLOCATION_INVALID` | 422 | Seguros | Principales activos exceden 100 % |
| `common.validation` (campo) | 400 | Seguros | Catálogo/monto/fecha/% inválido (mapear por clave de campo) |
| `EMPLOYMENT_STATUS_CODE_INVALID` | 422 | Info. Laboral | Estado de empleo fuera de catálogo |
| `PERSONNEL_FILE_LINKED_USER_CONFLICT` | 409 | Info. Laboral | Correo institucional ya en uso por otra cuenta |
| `CONCURRENCY_CONFLICT` | 409 | Ambas | `If-Match` desactualizado → recargar |
| (política) | 403 | Seguros | Falta `ViewInsurance` (lectura) / `Manage` (escritura) |

> Los `common.validation` (400) traen un diccionario `validationErrors` con la **clave = campo**; mapealo al input. Los `code` dedicados traen mensaje localizado (ES/EN).

---

## 7. Permisos nuevos

| Permiso | Uso |
|---|---|
| `PersonnelFiles.ViewInsurance` | **Leer** seguros/beneficiarios (o Admin). **Sin autoservicio.** |
| `PersonnelFiles.Manage` | Escribir seguros/beneficiarios (ya existente). |

> Si el usuario no tiene `ViewInsurance`, **ocultá** la sección de Seguros; si igual llama, esperá `403`.

---

## 8. Checklist consolidado para el FE

**Seguros**
- [ ] Sección Seguros visible solo con `ViewInsurance`.
- [ ] `<select>` de nombre de seguro (`insurance-types`).
- [ ] `<select>` de rango **dependiente** (`insurance-ranges?parentCode=…`); limpiar al cambiar el seguro; opcional.
- [ ] Form de seguro: cuotas (≥0), póliza, valor asegurado (≥0), **moneda** (catálogo), fechas (`start ≤ end`), activo.
- [ ] Beneficiarios: nombre, documento + **tipo** (`identification-types`), nacimiento, **parentesco**, **% asignación**, **principal/contingente**.
- [ ] Validar en vivo: suma de **principales activos ≤ 100 %** (mostrar total).
- [ ] `If-Match`/`ETag` en PUT/PATCH/DELETE; `isActive` solo por PATCH.
- [ ] Mapear errores `INSURANCE_*` (409/422) y `common.validation` (400).

**Información Laboral**
- [ ] Estado del empleado: dropdown desde `employment-statuses`; manejar `422 EMPLOYMENT_STATUS_CODE_INVALID`.
- [ ] Correo institucional **editable** en el `PUT`; avisar que cambia el login; manejar `409 PERSONNEL_FILE_LINKED_USER_CONFLICT` y `422` (formato); omitir/`null` para no cambiarlo.

---

> **Notas.** (1) El `insuranceCode`/`rangeCode`/`employmentStatusCode` y demás se envían como **`code`** del catálogo. (2) El seed de catálogos de seguro es un **punto de partida** (la lista final de tipos/rangos la confirma el negocio). (3) La **vista de historial/diff** de seguros (auditoría) queda como siguiente paso del módulo de Auditoría.
