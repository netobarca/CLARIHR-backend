# Guía de Integración Frontend — Transacciones Fuera de Nómina

| | |
|---|---|
| **Tipo de documento** | Guía de integración Frontend ↔ API |
| **Audiencia** | Equipo de desarrollo Frontend |
| **Módulo** | Expediente de Personal → Compensación (`personnel-files/{id}/off-payroll-transactions`) |
| **Documento de negocio** | [`analisis-transacciones-fuera-nomina-empleado.md`](../business/analisis-transacciones-fuera-nomina-empleado.md) (v2.1, D-01…D-13) |
| **Plan técnico** | [`plan-tecnico-transacciones-fuera-nomina.md`](./plan-tecnico-transacciones-fuera-nomina.md) |
| **Estado backend** | **Implementado** (Fase 1) — migración `20260624191922_AddOffPayrollTransactionsAndCatalog`, 1946 pruebas unitarias verdes |
| **Versión API** | `v1` |

---

## 1. ¿Qué es este requerimiento?

Una **transacción fuera de nómina** es un **gasto que la empresa asume por un empleado**, pero que **no forma parte de la planilla** (no afecta cálculos ni pagos de nómina). Ejemplos: herramientas de trabajo, equipo de protección personal (EPP), uniformes, artículos promocionales, reconocimientos y regalos.

El módulo permite a RR. HH. **registrar, consultar, editar, ajustar y dar de baja** estos gastos por empleado, **adjuntar comprobantes**, **vincularlos opcionalmente** a un equipo/acceso entregado, y obtener **totales por moneda**.

> ⚠️ **No confundir con "transacciones de nómina"** (`payroll-transactions`): ese es otro módulo (movimientos *dentro* de la planilla, inmutable). Este es un módulo **hermano e independiente**, con su propia ruta `off-payroll-transactions`.

### Características clave para el Frontend

| Característica | Detalle |
|---|---|
| **Tipo de transacción** | Viene de un **catálogo administrable** (`off-payroll-transaction-types`), no es texto libre. |
| **Período de imputación** | Campos **`year` + `month`** explícitos (1–12), independientes de la fecha de la transacción. Sugiéralos desde la fecha, pero permita editarlos. |
| **Valor (`amount`)** | Obligatorio, **≠ 0**, **admite negativos** (ajustes/notas de crédito). |
| **Ajuste negativo** | Si `amount < 0`, es **obligatorio** referenciar la transacción original mediante `correctsTransactionPublicId`. |
| **Moneda** | ISO-4217 (3 letras). Si se omite, el backend usa la moneda por defecto de la empresa. |
| **Vínculo opcional** | `assetAccessPublicId` → un registro de "Equipo o Acceso" **del mismo empleado**. |
| **Adjuntos** | Uno o varios comprobantes por transacción (factura, recibo, foto…). Clasificación opcional. |
| **Totales** | Subtotales **por moneda** a nivel de empleado (sin conversión). |
| **Sin autoservicio** | **Uso interno de RR. HH.** El empleado **no** ve ni crea estos registros (dato sensible). |
| **Baja lógica** | El `DELETE` desactiva (no borra físicamente). |

---

## 2. Permisos y autenticación

Todas las llamadas requieren `Authorization: Bearer <token>`.

| Permiso | Habilita |
|---|---|
| `PersonnelFiles.ViewOffPayrollTransactions` | Lectura (listar, consultar, totales, ver adjuntos). |
| `PersonnelFiles.ManageOffPayrollTransactions` | Escritura (crear, editar, patch, baja, adjuntar/eliminar comprobantes). |
| `PersonnelFiles.Admin` | Superset (incluye ambos). |

**No hay autoservicio (D-06):** un usuario sin estos permisos (p. ej. el propio empleado) recibe **`403 Forbidden`**. El Frontend debería **ocultar** la sección de "Transacciones fuera de nómina" si el usuario no tiene al menos `ViewOffPayrollTransactions` o `Admin`.

---

## 3. Catálogo de tipos (para poblar el selector)

El campo "tipo" se alimenta de un catálogo *country-scoped*. Para poblar el `<select>`:

```
GET /api/v1/general-catalogs/off-payroll-transaction-types?countryCode=SV
```

**Respuesta `200`:**

```json
[
  { "id": "…", "category": "OffPayrollTransactionType", "code": "HERRAMIENTAS",    "name": "Herramientas de trabajo",      "isSystem": false, "isActive": true, "sortOrder": 10 },
  { "id": "…", "category": "OffPayrollTransactionType", "code": "EPP",             "name": "Equipo de protección personal", "isSystem": false, "isActive": true, "sortOrder": 20 },
  { "id": "…", "category": "OffPayrollTransactionType", "code": "UNIFORMES",       "name": "Uniformes",                     "isSystem": false, "isActive": true, "sortOrder": 30 },
  { "id": "…", "category": "OffPayrollTransactionType", "code": "PROMOCIONALES",   "name": "Artículos promocionales",       "isSystem": false, "isActive": true, "sortOrder": 40 },
  { "id": "…", "category": "OffPayrollTransactionType", "code": "RECONOCIMIENTOS", "name": "Reconocimientos",               "isSystem": false, "isActive": true, "sortOrder": 50 },
  { "id": "…", "category": "OffPayrollTransactionType", "code": "REGALOS",         "name": "Regalos",                       "isSystem": false, "isActive": true, "sortOrder": 60 }
]
```

> El Frontend envía el **`code`** (p. ej. `"HERRAMIENTAS"`) en `transactionTypeCode`, no el `id`. El backend guarda además un *snapshot* del nombre (`transactionTypeName` en la respuesta) para que las transacciones históricas conserven la descripción aunque el tipo se desactive.

El catálogo se administra con los endpoints genéricos de catálogos del sistema (crear/editar/activar tipos), fuera del alcance de esta guía.

---

## 4. Referencia de endpoints

Base: `/api/v1/personnel-files/{publicId}/off-payroll-transactions`

| Verbo | Ruta | Operación | Permiso |
|---|---|---|---|
| `GET` | `/` | Listar transacciones del empleado | View |
| `GET` | `/totals` | Totales por moneda | View |
| `GET` | `/{txId}` | Consultar una transacción | View |
| `POST` | `/` | Crear transacción → `201` + `ETag` | Manage |
| `PUT` | `/{txId}` | Reemplazar campos de negocio (`If-Match`) | Manage |
| `PATCH` | `/{txId}` | Modificación parcial / activar-desactivar (`If-Match`, JSON Patch) | Manage |
| `DELETE` | `/{txId}` | Baja lógica (`If-Match`) | Manage |
| `GET` | `/{txId}/documents` | Listar comprobantes | View |
| `GET` | `/{txId}/documents/{docId}` | Consultar un comprobante | View |
| `GET` | `/{txId}/documents/{docId}/read-url` | URL temporal de descarga (SAS) | View |
| `POST` | `/{txId}/documents` | Adjuntar comprobante → `201` + `ETag` | Manage |
| `DELETE` | `/{txId}/documents/{docId}` | Eliminar comprobante (`If-Match`) | Manage |

---

## 5. Contratos (request / response)

### 5.1 Transacción — objeto de respuesta

```jsonc
{
  "offPayrollTransactionPublicId": "0f9c…",   // id de la transacción
  "offPayrollTransactionTypeCode": "HERRAMIENTAS",
  "transactionTypeName": "Herramientas de trabajo", // snapshot (puede ser null)
  "transactionDateUtc": "2026-06-15T00:00:00Z",
  "currencyCode": "USD",
  "amount": 125.50,                           // ≠ 0; negativo = ajuste
  "year": 2026,                               // período de imputación
  "month": 6,
  "comment": "Compra de taladro",
  "assetAccessPublicId": "ab12…",             // null si no se vinculó
  "assetName": "Taladro Bosch GSB 13",        // snapshot del activo (null si no aplica)
  "correctsTransactionPublicId": null,        // != null sólo en ajustes
  "isActive": true,
  "concurrencyToken": "7b3e…"                 // úsalo como If-Match
}
```

### 5.2 Crear — `POST /off-payroll-transactions`

**Body (`AddOffPayrollTransactionRequest`):**

```jsonc
{
  "transactionTypeCode": "HERRAMIENTAS",      // requerido (code del catálogo)
  "transactionDateUtc": "2026-06-15T00:00:00Z", // requerido, no futura
  "currencyCode": "USD",                      // opcional (default = empresa)
  "amount": 125.50,                           // requerido, ≠ 0
  "year": 2026,                               // requerido (2000–2100)
  "month": 6,                                 // requerido (1–12)
  "comment": "Compra de taladro",             // opcional (≤2000)
  "assetAccessPublicId": "ab12…",             // opcional (mismo empleado)
  "correctsTransactionPublicId": null         // requerido SÓLO si amount < 0
}
```

**Respuesta `201 Created`:** cuerpo = objeto de transacción (§5.1). Cabeceras: `Location` (URL del recurso) y `ETag` (= `concurrencyToken` inicial).

### 5.3 Editar — `PUT /off-payroll-transactions/{txId}`

- Cabecera **obligatoria** `If-Match: "<concurrencyToken>"`.
- Body = `UpdateOffPayrollTransactionRequest` (mismos campos que el create).
- Reemplaza los **campos de negocio**; **no** cambia `isActive` (eso se hace por `PATCH`/`DELETE`).
- Respuesta `200` con el objeto actualizado y nuevo `ETag`.

### 5.4 Activar / desactivar (o modificación parcial) — `PATCH /off-payroll-transactions/{txId}`

- `Content-Type: application/json-patch+json` (RFC 6902).
- Cabecera **obligatoria** `If-Match`.
- Soporta los campos de negocio y `isActive`.

**Ejemplo: reactivar una transacción dada de baja:**

```json
[ { "op": "replace", "path": "/isActive", "value": true } ]
```

**Ejemplo: corregir el mes de imputación:**

```json
[ { "op": "replace", "path": "/month", "value": 7 } ]
```

### 5.5 Ajuste negativo (nota de crédito)

Para corregir/revertir un gasto ya registrado, cree **una nueva transacción** con `amount` negativo que **referencie** el original:

```jsonc
{
  "transactionTypeCode": "HERRAMIENTAS",
  "transactionDateUtc": "2026-06-20T00:00:00Z",
  "currencyCode": "USD",
  "amount": -125.50,                          // negativo
  "year": 2026,
  "month": 6,
  "comment": "Reverso por devolución del taladro",
  "correctsTransactionPublicId": "0f9c…"      // OBLIGATORIO (id del original)
}
```

Reglas que valida el backend (devuelve `422` si fallan):
- La transacción original **existe**, está **activa** y pertenece **al mismo empleado**.
- Es un **gasto original** (no otro ajuste) y está en la **misma moneda**.

### 5.6 Baja lógica — `DELETE /off-payroll-transactions/{txId}`

- Cabecera **obligatoria** `If-Match`.
- **No borra físicamente**: marca `isActive = false` (RN-10). La transacción sigue apareciendo en el listado (con `isActive: false`) pero **deja de sumar** en los totales.
- Respuesta `200` con `{ "parentConcurrencyToken": "…" }` (token del expediente padre).

---

## 6. Totalización por moneda — `GET /off-payroll-transactions/totals`

```json
[
  { "currencyCode": "USD", "total": 1250.00, "count": 8 },
  { "currencyCode": "EUR", "total":  300.00, "count": 2 }
]
```

- Subtotales **por cada moneda** a nivel de empleado, considerando los signos (los ajustes negativos restan).
- **Sin conversión (FX)**: muéstrelos como bloques separados; **no** los sume entre sí.
- Sólo cuenta transacciones **activas**.

---

## 7. Concurrencia optimista (`If-Match` / `ETag`)

Todas las escrituras sobre un recurso existente (`PUT`, `PATCH`, `DELETE` de transacciones y de adjuntos) exigen la cabecera:

```
If-Match: "<concurrencyToken>"
```

El token se obtiene del `ETag` de la respuesta anterior o del campo `concurrencyToken`. Flujo recomendado en el Frontend:

1. `GET` la transacción → guarde `concurrencyToken`.
2. Al guardar, envíe `If-Match` con ese token.
3. La respuesta trae un **nuevo** `ETag` → actualícelo en memoria.
4. Si recibe **`409 Conflict`** (`CONCURRENCY_CONFLICT`): el registro fue modificado por otro usuario → recargue y reintente.

---

## 8. Adjuntos (comprobantes) — flujo completo

Los comprobantes reutilizan la infraestructura de archivos (Azure Blob). Subir un comprobante es un **proceso de 3 pasos**:

**Paso 1 — Crear la sesión de carga** (obtiene una URL de subida directa):

```
POST /api/v1/files/upload-session
{
  "fileName": "factura-001.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 348120,
  "purpose": "OffPayrollTransactionDocument"   // ⬅️ usar exactamente este purpose
}
```
Respuesta: `{ "filePublicId": "…", "uploadUrl": "https://…SAS…", … }`.

> Tipos permitidos: `application/pdf`, `image/jpeg`, `image/png` (extensiones `.pdf`, `.jpg`, `.jpeg`, `.png`). Tamaño máx. 10 MB.

**Paso 2 — Subir el binario** directamente al blob con un `PUT` a `uploadUrl`, y **completar**:

```
PATCH /api/v1/files/{filePublicId}/complete
```

**Paso 3 — Asociar el archivo a la transacción:**

```
POST /api/v1/personnel-files/{publicId}/off-payroll-transactions/{txId}/documents
{
  "filePublicId": "…",                         // del paso 1
  "documentTypeCatalogItemPublicId": null,     // OPCIONAL (clasificación)
  "observations": "Factura del proveedor"      // opcional
}
```
Respuesta `201` con metadatos del documento (`OffPayrollTransactionDocumentResponse`) y su `ETag`.

> `documentTypeCatalogItemPublicId` es **opcional** (D-07: "comprobante de cualquier índole"). Envíe `null` u omítalo si no clasifica el documento.

**Descargar un comprobante** (la URL del blob es temporal, ~15 min):

```
GET /api/v1/personnel-files/{publicId}/off-payroll-transactions/{txId}/documents/{docId}/read-url
→ { "readUrl": "https://…SAS…", "expiresUtc": "2026-06-24T19:45:00Z" }
```
Use `readUrl` para mostrar/descargar el archivo. **No** la cachee más allá de `expiresUtc`.

**Eliminar un comprobante:** `DELETE …/documents/{docId}` con `If-Match` (baja lógica + limpieza del blob).

### Objeto de respuesta de un comprobante

```jsonc
{
  "id": "…",
  "documentTypeCatalogItemPublicId": null,
  "documentTypeCode": null,
  "documentTypeName": null,
  "observations": "Factura del proveedor",
  "filePublicId": "…",
  "fileName": "factura-001.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 348120,
  "isActive": true,
  "concurrencyToken": "…",
  "createdAtUtc": "2026-06-24T19:30:00Z",
  "modifiedAtUtc": null
}
```

---

## 9. Vínculo opcional a "Equipo o Acceso"

Para conectar el gasto con el activo entregado (p. ej. el costo del uniforme con su entrega):

1. Liste los equipos/accesos del empleado: `GET /api/v1/personnel-files/{publicId}/asset-accesses`.
2. Use el `publicId` de un registro como `assetAccessPublicId` en el create/update.
3. El backend valida que pertenezca **al mismo empleado** (si no → `422 OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND`) y guarda un *snapshot* del nombre (`assetName`).

---

## 10. Mapa de errores

| HTTP | `code` | Causa | Manejo sugerido en UI |
|---|---|---|---|
| `400` | `common.validation` | Campo inválido (tipo vacío, `amount`=0, `month`∉1–12, `year` fuera de rango, fecha futura, moneda ≠ 3 letras, negativo sin referencia). | Mostrar errores por campo (ver §11). |
| `422` | `OFF_PAYROLL_TX_TYPE_CODE_INVALID` | El tipo no existe / inactivo en el catálogo. | "El tipo seleccionado no es válido." Recargar catálogo. |
| `422` | `OFF_PAYROLL_TX_CURRENCY_REQUIRED` | Sin moneda y la empresa no tiene moneda por defecto. | Pedir moneda explícita. |
| `422` | `OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND` | El equipo/acceso vinculado no existe o es de otro empleado. | Revalidar el selector de activos. |
| `422` | `OFF_PAYROLL_TX_CORRECTION_REQUIRED` | `amount < 0` sin `correctsTransactionPublicId`. | Forzar selección de la transacción original. |
| `422` | `OFF_PAYROLL_TX_CORRECTED_NOT_FOUND` | La transacción original referenciada no existe / otro empleado. | Revalidar la referencia. |
| `422` | `OFF_PAYROLL_TX_CORRECTED_INVALID` | El original está inactivo, es un ajuste, o está en otra moneda. | Explicar la regla; elegir otro original. |
| `409` | `CONCURRENCY_CONFLICT` | `If-Match` desactualizado. | Recargar y reintentar. |
| `404` | `PERSONNEL_FILE_ITEM_NOT_FOUND` / `PERSONNEL_FILE_DOCUMENT_NOT_FOUND` | Transacción/documento inexistente. | Refrescar el listado. |
| `403` | `PERSONNEL_FILES_FORBIDDEN` | Sin permiso (o intento de autoservicio). | Ocultar la sección / mensaje de acceso. |
| `422` | `PERSONNEL_FILE_STATE_RULE_VIOLATION` | El expediente no está en estado "empleado completado". | No permitir registrar gastos aún. |
| `400/413` | `FILE_*` | Adjunto con tipo/tamaño/propósito incorrecto. | Validar antes de subir (ver §8). |

> Los mensajes están localizados (es / en) según la cultura de la petición. Use el `code` para la lógica del Frontend, no el texto.

---

## 11. Validaciones de formulario (espejo en el Frontend)

| Campo | Regla |
|---|---|
| `transactionTypeCode` | Requerido. Del catálogo. ≤ 80. |
| `transactionDateUtc` | Requerido. **No futura** (≤ hoy). |
| `amount` | Requerido. **≠ 0**. Permite negativos. |
| `year` | Requerido. 2000–2100. Autocompletar desde la fecha (editable). |
| `month` | Requerido. 1–12. Autocompletar desde la fecha (editable). |
| `currencyCode` | Opcional. Si se envía, exactamente **3 letras** ISO-4217. |
| `comment` | Opcional. ≤ 2000. |
| `assetAccessPublicId` | Opcional. |
| `correctsTransactionPublicId` | **Requerido si `amount < 0`**; de lo contrario debe ir vacío/nulo. |

**Sugerencia UX:** al cambiar la fecha, autocompletar `year`/`month`; mostrar el campo "transacción que corrige" sólo cuando `amount < 0`; en la lista, marcar visualmente las filas `isActive: false` (dadas de baja) y los ajustes negativos.

---

## 12. Checklist de integración Frontend

- [ ] Ocultar la sección si el usuario no tiene `View`/`Manage`/`Admin`.
- [ ] Poblar el selector de tipo desde `general-catalogs/off-payroll-transaction-types`.
- [ ] Formulario de alta/edición con las validaciones de §11.
- [ ] Autocompletar `year`/`month` desde la fecha (editables).
- [ ] Manejar `If-Match`/`ETag` en `PUT`/`PATCH`/`DELETE` y el `409`.
- [ ] Flujo de ajuste negativo con selección obligatoria del original.
- [ ] Baja lógica vía `DELETE` (la fila permanece, marcada inactiva).
- [ ] Adjuntos: flujo de 3 pasos (upload-session → complete → attach) con `purpose = OffPayrollTransactionDocument`; descarga vía `read-url`.
- [ ] Panel de **totales por moneda** (bloques separados, sin sumar entre monedas).
- [ ] Vínculo opcional a Equipo/Acceso del mismo empleado.
- [ ] Mapear los `code` de error a mensajes/acciones de UI (§10).

---

> **Resumen:** módulo backend **completo y probado**. Rutas bajo `personnel-files/{id}/off-payroll-transactions`, permisos `View/ManageOffPayrollTransactions` (interno de RR. HH., sin autoservicio), catálogo `off-payroll-transaction-types`, valores con signo + ajustes referenciados, adjuntos reutilizando la infraestructura de archivos, y totales por moneda. **Diferido a fase futura (D-09):** flujo de aprobación del gasto, reembolso/conciliación contable, conversión FX y exportación a Excel.
