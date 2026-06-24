# Análisis de Negocio — Transacciones Fuera de Nómina del Empleado

> **Tipo de documento:** Análisis de requerimiento (validación + GAP) y especificación funcional.
> **Módulo:** Expediente de Personal → Compensación (`PersonnelFiles/Compensation`).
> **Fecha:** 2026-06-23 · **Versión:** v2.1 (decisiones P-01…P-10 + residuales PR-01…PR-03 → D-11…D-13 **ratificadas**).
> **Autor:** Analista de Negocio (CLARIHR).
> **Estado:** Requerimiento de negocio **cerrado**; listo para plan técnico.

---

## 0. Veredicto ejecutivo (resultado de la validación)

El requerimiento pedía **validar si ya está implementado y alineado, o si no existe**, y solo en caso necesario agregar la información requerida.

**Resultado: el requerimiento NO está implementado. Es desarrollo nuevo (net-new).**

No existe en el código (`src/`) ni en la documentación (`docs/`) ninguna entidad, catálogo, endpoint o migración para "transacción fuera de nómina" (búsqueda nula de `OffPayroll`, `FueraNomina`, `out-of-payroll`, etc.).

### ⚠️ Hallazgo crítico de alineación (la trampa de nombres)

Sí existe un **"primo cercano" con un nombre casi idéntico** que **NO debe confundirse ni reutilizarse**: la entidad **`PersonnelFilePayrollTransaction`** ("transacción **de** nómina"). Es un concepto distinto:

| Aspecto | `PersonnelFilePayrollTransaction` (YA EXISTE) | Transacción **fuera** de nómina (SOLICITADO) |
|---|---|---|
| Propósito | Bitácora de **movimientos dentro de la nómina** (débitos/créditos de una corrida de planilla) | **Gasto que la empresa asume** por un empleado, **fuera** de la planilla (herramientas, EPP, uniformes, regalos…) |
| Período | `PayrollPeriodCode` **obligatorio** (período de planilla) | **Año + Mes** (período de **imputación**, D-05) |
| Naturaleza contable | Bandera `IsDebit` (débito/crédito) | No aplica (es un costo/gasto; admite valores negativos por **ajuste que referencia el original**, D-04/D-12) |
| Origen externo | `SourceSystem` / `SourceReference` / `SourceSyncedUtc` (importación desde nómina externa) | No aplica (captura manual de RR. HH.) |
| Tipo de registro | **Texto libre** (`TransactionTypeCode`, validado solo `NotEmpty().MaximumLength(80)`) | **Catálogo administrado** de tipos (con `Code` ingresado por el usuario, D-03) |
| Mutabilidad | **Registro de auditoría inmutable**: el `PATCH` solo cambia `isActive`; sin `PUT` ni borrado | Registro de RR. HH. **editable/corregible** (CRUD) |
| Adjuntos | No | **Sí** (comprobante de cualquier índole, D-07) |
| Vínculo a activos | No | **Opcional** a `AssetAccess` (D-01) |

**Evidencia en código:** `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs:587-677`; `src/CLARIHR.Application/Features/PersonnelFiles/Compensation/PayrollTransactions.cs` (validador línea 118; sin validación de catálogo); `src/CLARIHR.Api/Controllers/PersonnelFileCompensationController.cs:368-566` (el `PATCH` "supports only the `isActive` flag (the business fields are an immutable audit record)", línea 547).

**Conclusión de alineación:** reutilizar `PersonnelFilePayrollTransaction` sería un error de diseño: (1) contaminaría semánticamente la nómina y sus reportes/exportaciones; (2) forzaría un período de planilla en lugar de Año+Mes; (3) perdería el catálogo administrado de tipos; (4) es un ledger inmutable y sin adjuntos pensado para importación. **Se construye una entidad hermana nueva** siguiendo el "slice vertical" del módulo de Compensación (plantilla más cercana: `MedicalClaims`).

> Las 10 preguntas abiertas fueron **respondidas y ratificadas** por el negocio (ver §17). El resto del documento ya incorpora esas decisiones.

---

## 1. Resumen del producto o requerimiento

Se requiere incorporar al **Expediente de Personal** la capacidad de **registrar transacciones (gastos) fuera de nómina** asociadas a un empleado, junto con un **catálogo administrable** de los **tipos** de dichas transacciones.

Una **transacción fuera de nómina** es un **gasto que la empresa asume** y que está **relacionado con un empleado**, pero que **no forma parte del cálculo ni del pago de la planilla**. Ejemplos: herramientas de trabajo, equipo de protección personal (EPP), uniformes, artículos promocionales, reconocimientos, regalos, etc.

El módulo tiene **dos piezas**:

1. **Registro de transacción fuera de nómina** (operativo): captura, por empleado, los datos de cada gasto (tipo, fecha, moneda, valor, año, mes, comentario), con **comprobante adjunto** y **vínculo opcional** al activo entregado.
2. **Catálogo de "tipos de transacción fuera de nómina"** (configuración): lista administrable de las categorías que alimentan el campo "tipo de registro".

**Problema que resuelve:** hoy estos gastos no se registran de forma estructurada en el expediente del empleado. Centralizarlos permite **trazabilidad del costo total que la empresa invierte por colaborador** (más allá del salario), respaldo documental (comprobantes), soporte para auditoría, y explotación analítica (costo por empleado, por tipo, por período y **por moneda**), sin mezclarlos con la nómina.

---

## 2. Objetivos del negocio

- **OB-01.** Tener un **registro estructurado, documentado y auditable** de los gastos que la empresa asume por cada empleado fuera de la planilla.
- **OB-02.** **Estandarizar** las categorías mediante un **catálogo administrable**, evitando texto libre inconsistente.
- **OB-03.** Permitir el **análisis del costo total por empleado** y por **tipo**, **período (año/mes de imputación)** y **moneda**.
- **OB-04.** **No contaminar la nómina**: mantener estos gastos explícitamente **fuera** del cálculo de planilla y de sus reportes.
- **OB-05.** Soportar **auditoría y cumplimiento** (quién registró qué, cuándo, por qué valor y con qué comprobante).
- **OB-06.** **Relacionar** opcionalmente el gasto con el **activo/entrega** correspondiente (uniformes, herramientas, EPP) para trazabilidad costo↔custodia.
- **OB-07.** Reutilizar las **convenciones existentes** del módulo (catálogos country-scoped, concurrencia, auditoría, adjuntos, exportación) para minimizar costo y riesgo.

---

## 3. Alcance funcional

Incluye:

- **AF-01.** CRUD del **catálogo de tipos** de transacción fuera de nómina (crear, listar, editar, activar/desactivar). Campos: **Code (ingresado por el usuario)** + **Descripción**.
- **AF-02.** **Registrar** una transacción para un empleado: tipo (catálogo), fecha de transacción, moneda, valor, año, mes y comentario.
- **AF-03.** **Adjuntar comprobante(s)** de cualquier índole (factura, recibo, foto, etc.) a la transacción.
- **AF-04.** **Vincular opcionalmente** la transacción a un registro de **Equipo o Acceso (AssetAccess)** del mismo empleado.
- **AF-05.** **Listar / buscar / paginar / filtrar** (por tipo, rango de fechas, año/mes, estado, texto) y **consultar** por id.
- **AF-06.** **Editar** una transacción y **activar/desactivar** (baja lógica).
- **AF-07.** **Validaciones**: tipo activo del catálogo; moneda ISO‑4217; valor ≠ 0 (admite negativos); mes 1–12; año en rango; fecha no futura; AssetAccess (si se vincula) debe pertenecer al mismo empleado.
- **AF-08.** **Auditoría** de alta/edición/baja (before/after, actor, fecha).
- **AF-09.** **Permisos** dedicados de gestión y consulta; **uso interno de RR. HH.** (sin autoservicio del empleado).
- **AF-10.** **Exportación** del listado del empleado, con **totalización por moneda** (sin conversión).
- **AF-11.** **Localización** bilingüe (es / en).

---

## 4. Fuera de alcance (esta fase)

- **FA-01.** **Cálculo o integración con la nómina/planilla** (no existe motor de nómina; ver §16). Registros **informativos**, no afectan pagos ni deducciones.
- **FA-02.** **Flujo de aprobación / autorización** del gasto. **Reconocido como necesario a futuro (D-09)**, pero **diferido**: en esta fase solo se captura el gasto **ya incurrido**. (Se recomienda diseñar el modelo sin cerrar la puerta a un futuro estado/flujo — ver §18.)
- **FA-03.** **Reembolso al empleado** o conciliación con cuentas por pagar / tesorería / contabilidad.
- **FA-04.** **Conversión de moneda (FX)** y reportería multimoneda consolidada. Se hace **totalización por moneda**, sin conversión (D-08).
- **FA-05.** **Importación masiva** desde sistemas externos (a diferencia de `PayrollTransaction`).
- **FA-06.** **Topes / presupuestos** por empleado, tipo o período y alertas asociadas (D-10: no hay topes).
- **FA-07.** **Compartir el catálogo de tipos** con el de AssetAccess u otros (D-02: análisis concluye **no compartir**; el vínculo es a nivel de registro, no de catálogo).
- **FA-08.** **Reportes analíticos agregados** avanzados (dashboards) más allá del listado, su exportación y la totalización por moneda.

---

## 5. Actores o usuarios involucrados

| Actor | Rol en este módulo |
|---|---|
| **Administrador / Configurador de catálogos** | Crea y mantiene el **catálogo de tipos**. |
| **Gestor de RR. HH. / Analista de Compensación** | **Registra, edita, da de baja, adjunta comprobantes y vincula a activos**; consulta y exporta. |
| **Supervisor / Gerente con permiso de consulta** | **Consulta** (solo lectura) y exporta. |
| **Empleado (autoservicio)** | **Sin acceso (D-06):** estos registros son **internos de RR. HH.**; el empleado **no** los crea ni visualiza (montos de regalos/reconocimientos = dato sensible). |
| **Sistema CLARIHR** | Validaciones, normalización, auditoría, concurrencia, almacenamiento de adjuntos. |
| **Almacenamiento de archivos (Azure Blob)** | Persistencia de los comprobantes. |
| **Sistema externo (futuro)** | Eventual consumidor (BI/contabilidad). No integrado. |

---

## 6. Requerimientos funcionales

### RF-001 — Crear tipo de transacción fuera de nómina (catálogo)

**Descripción:** El administrador crea una categoría indicando **Code** (ingresado por el usuario) y **Descripción** (p. ej. `UNIFORMES` / "Uniformes", `EPP` / "Equipo de protección", `RECONOCIMIENTOS` / "Reconocimientos", `REGALOS` / "Regalos", `PROMOCIONALES` / "Promocionales", `HERRAMIENTAS` / "Herramientas de trabajo").

**Reglas de negocio:**
- **Code** obligatorio, ≤80, **único por país/tenant**, normalizado en mayúsculas (D-03).
- **Descripción** obligatoria, ≤200.
- Catálogo **country-scoped**; ítem nace activo (`IsActive = true`); `SortOrder` configurable.

**Criterios de aceptación:**
- Dado permiso de gestión de catálogos y Code+Descripción válidos, cuando se crea, entonces queda disponible para selección.
- Dado un Code duplicado (mismo país), cuando se guarda, entonces se rechaza por unicidad.

**Prioridad:** Alta. **Dependencias:** Convenciones de catálogo (`GeneralCatalogItem`, `GeneralCatalogKeyMap`, seed).

---

### RF-002 — Administrar el catálogo de tipos (listar, editar, activar/desactivar)

**Descripción:** Listar, editar (Descripción y `SortOrder`) y activar/desactivar tipos. Los inactivos no se ofrecen al crear, pero se conservan en transacciones históricas (snapshot, D-07).

**Reglas de negocio:** sin borrado físico si está referenciado; baja lógica.

**Criterios de aceptación:** dado un tipo en uso, cuando se desactiva, entonces deja de ofrecerse pero las transacciones previas siguen mostrando su descripción.

**Prioridad:** Media. **Dependencias:** RF-001.

---

### RF-003 — Registrar una transacción fuera de nómina

**Descripción:** Registrar, para un empleado, una transacción con: **tipo** (catálogo), **fecha de transacción**, **moneda**, **valor**, **año**, **mes** y **comentario**.

**Reglas de negocio:**
- Empleado por id de expediente (no texto libre); el nombre se muestra desde el expediente.
- **Tipo:** obligatorio; existe y **activo** en catálogo (RN-03).
- **Fecha:** obligatoria; **no futura** (RN-05).
- **Moneda:** obligatoria; **ISO‑4217** válida; default = moneda de la empresa (RN-06).
- **Valor:** obligatorio; **≠ 0**, **admite negativos** (ajustes/notas de crédito). Un valor **negativo** exige **referenciar la transacción original** que corrige (RN-04 / D-04 / D-12).
- **Año:** obligatorio; rango razonable. **Mes:** obligatorio; **1–12**. Representan el **período de imputación** (puede diferir de la fecha), con autocompletado sugerido desde la fecha (RN-07 / D-05).
- **Comentario:** opcional; ≤2000.
- Expediente en estado **empleado completado** (`IsCompletedEmployee`).

**Criterios de aceptación:**
- Dado expediente completado y datos válidos, cuando se registra, entonces se persiste y retorna `201 Created` con `ETag`.
- Dado tipo inactivo/inexistente → `422`; valor = 0 / mes fuera de 1–12 / año fuera de rango / fecha futura → `400/422`.

**Prioridad:** Alta. **Dependencias:** RF-001; catálogo de monedas; expediente.

---

### RF-004 — Adjuntar comprobante(s) a una transacción

**Descripción:** Subir, listar, descargar y eliminar **comprobantes** (de cualquier índole) asociados a una transacción, siguiendo el patrón de documentos del expediente / adjuntos de `MedicalClaim`.

**Reglas de negocio:**
- Se admite **uno o varios** adjuntos por transacción (D-07).
- Tipos de archivo y tamaño según la política de archivos existente; almacenamiento en Azure Blob.
- Nuevo `FilePurpose.OffPayrollTransactionDocument` (hoy el enum no lo tiene; ver §13).
- Clasificación del documento (DocumentTypeCatalogItem) **opcional** ("de cualquier índole").

**Criterios de aceptación:**
- Dado una transacción existente, cuando se sube un comprobante válido, entonces queda asociado, listable y descargable.
- Dado un adjunto, cuando se elimina, entonces se remueve (con auditoría) sin afectar la transacción.

**Prioridad:** Alta. **Dependencias:** Infraestructura de archivos (Blob), `FilePurpose`, `DocumentTypeCatalogItem`.

---

### RF-005 — Vincular opcionalmente la transacción a un Equipo/Acceso (AssetAccess)

**Descripción:** Permitir asociar **opcionalmente** la transacción a un registro de `PersonnelFileAssetAccess` del **mismo empleado** (p. ej. el costo de un uniforme entregado).

**Reglas de negocio:**
- Vínculo **opcional**; campo `AssetAccessPublicId?`.
- Si se provee, el AssetAccess debe **existir y pertenecer al mismo empleado** (RN-08); si no, `422` `OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND`.
- Relación **muchos-a-uno**: varias transacciones pueden referenciar un mismo activo; una transacción referencia a lo sumo un activo.
- Se guarda un **snapshot** del nombre del activo (`AssetOrAccessName`) para preservar la referencia (D-07).
- **No** se comparten catálogos de tipos entre módulos (D-02): la conexión es a nivel de registro.

**Criterios de aceptación:**
- Dado un AssetAccess del mismo empleado, cuando se vincula, entonces la transacción muestra la referencia.
- Dado un AssetAccess de otro empleado o inexistente, cuando se intenta vincular, entonces se rechaza con `422`.

**Prioridad:** Media. **Dependencias:** Módulo Equipo o Acceso (`PersonnelFileAssetAccess`).

---

### RF-006 — Listar, buscar y filtrar transacciones de un empleado

**Descripción:** Consulta paginada con filtros por **tipo**, **rango de fechas**, **año/mes**, **estado** y **texto** (comentario), con ordenamiento; consistente con `payroll-transactions`.

**Criterios de aceptación:** dado N transacciones, cuando se pagina/filtra, entonces se retorna la colección con metadatos.

**Prioridad:** Alta. **Dependencias:** RF-003.

---

### RF-007 — Consultar / Editar / Dar de baja una transacción

**Descripción:** Consultar por id; editar campos de negocio (con concurrencia `If-Match`); activar/desactivar (baja lógica). A diferencia de `PayrollTransaction` (inmutable), aquí **sí** se editan los campos de negocio (D-08-mut).

**Reglas de negocio:** mismas validaciones que RF-003; requiere `concurrencyToken` vigente (conflicto → `409/412`); sin borrado físico (RN-10). Un **ajuste negativo** debe **referenciar** la transacción original que corrige (mismo empleado; recomendado: misma moneda y que la referenciada sea un gasto original, no otro ajuste) (D-12).

**Criterios de aceptación:** token vigente + datos válidos → actualiza y retorna nuevo `ETag`; token desactualizado → conflicto.

**Prioridad:** Media. **Dependencias:** RF-003.

---

### RF-008 — Exportar el listado con totalización por moneda

**Descripción:** Exportar (Excel/CSV) las transacciones filtradas del empleado, incluyendo **totales por moneda a nivel de empleado** (sin conversión), reutilizando el servicio de exportación del módulo.

**Reglas de negocio:** la totalización es **por empleado**, con subtotales **por cada `CurrencyCode`**, considerando los signos (negativos restan) (RN-12 / D-08 / D-13).

**Criterios de aceptación:** dado un conjunto multimoneda, cuando se exporta, entonces se muestran subtotales por moneda sin convertir entre ellas.

**Prioridad:** Media. **Dependencias:** RF-006; `ReportExportResources`.

---

### RF-009 — Auditoría de operaciones

**Descripción:** Toda alta/edición/baja y operación de adjuntos registra auditoría (actor, fecha, before/after).

**Prioridad:** Alta. **Dependencias:** `IAuditService`, `PersonnelFileEmployeeAudits`.

---

## 7. Requerimientos no funcionales

- **Seguridad:** permisos dedicados; multi-tenant (`TenantId`); **datos sensibles** (montos de regalos/reconocimientos) **solo RR. HH.**, sin autoservicio; control de acceso a la descarga de comprobantes.
- **Rendimiento:** listados paginados; índices por `PersonnelFileId`, `TransactionDateUtc`, `OffPayrollTransactionTypeCode`, (`Year`,`Month`), `CurrencyCode`, `AssetAccessPublicId`; exportación con límite síncrono.
- **Disponibilidad / Escalabilidad:** según el API existente; volumen moderado por empleado; adjuntos en Blob.
- **Usabilidad:** selector de tipo; autocompletar año/mes desde fecha (editable); validaciones claras.
- **Auditoría:** trazabilidad completa before/after; baja lógica; auditoría de adjuntos.
- **Compatibilidad:** ISO‑4217; fechas UTC normalizadas; totalización por moneda sin FX.
- **Mantenibilidad:** "slice vertical" del módulo + **módulo de reglas puro** testeable.
- **Accesibilidad / i18n:** etiquetas y errores bilingües (`.resx`).
- **Concurrencia:** `concurrencyToken` por ítem + `If-Match`.

---

## 8. Historias de usuario

### HU-001 — Configurar tipos de gasto
Como **administrador de catálogos**, quiero **crear y mantener los tipos** (Code + descripción), para **estandarizar** la clasificación de gastos.
- Dado permiso de gestión, cuando creo "REGALOS / Regalos", entonces queda activo y disponible.

### HU-002 — Registrar un gasto con comprobante
Como **analista de compensación**, quiero **registrar un gasto con su comprobante**, para **dejar trazabilidad y respaldo** del costo asumido.
- Dado expediente completado, cuando ingreso datos válidos y adjunto la factura, entonces se guarda y se muestra en el listado.

### HU-003 — Relacionar el gasto con el activo entregado
Como **analista de compensación**, quiero **vincular** el gasto del uniforme con su **entrega** (AssetAccess), para **conectar costo y custodia**.
- Dado un uniforme entregado al mismo empleado, cuando lo vinculo, entonces la transacción muestra la referencia al activo.

### HU-004 — Consultar y totalizar por moneda
Como **gerente con permiso de consulta**, quiero **ver, filtrar y exportar** las transacciones con **totales por moneda**, para **entender el costo** por período sin mezclar divisas.
- Dado un empleado con gastos en USD y otra moneda, cuando exporto, entonces veo subtotales separados por moneda.

### HU-005 — Corregir con ajuste
Como **analista de compensación**, quiero **registrar un ajuste (valor negativo)** o **editar/dar de baja**, para **corregir** sin perder el rastro de auditoría.
- Dado un gasto mal capturado, cuando registro un ajuste negativo o lo edito con token vigente, entonces queda corregido y auditado.

---

## 9. Reglas de negocio

- **RN-01.** La transacción está **asociada a un empleado** existente (por id), no a texto libre.
- **RN-02.** Es **independiente de la nómina**: no afecta cálculos ni pagos; no se mezcla con `PayrollTransaction`.
- **RN-03.** El **tipo** debe existir y estar **activo** en el catálogo al crear/editar.
- **RN-04.** El **valor** es obligatorio y **≠ 0**; **admite negativos** (ajustes/notas de crédito). Si el valor es **negativo**, es **obligatorio** referenciar la transacción original que corrige (`CorrectsTransactionPublicId`), que debe **existir, estar activa y pertenecer al mismo empleado** (recomendado: **misma moneda** y que sea un gasto original, no otro ajuste).
- **RN-05.** La **fecha de transacción** es obligatoria y **no futura**.
- **RN-06.** La **moneda** es obligatoria, **ISO‑4217**, validada contra catálogo; default = empresa.
- **RN-07.** **Año** y **Mes** (1–12) obligatorios; representan el **período de imputación** (puede diferir de la fecha); se sugieren desde la fecha pero son editables.
- **RN-08.** El **vínculo a AssetAccess** es opcional; si se establece, debe pertenecer al **mismo empleado**.
- **RN-09.** Desactivar un **tipo** no afecta transacciones históricas (snapshot de la descripción).
- **RN-10.** No hay **borrado físico**; bajas lógicas.
- **RN-11.** Toda escritura (incluidos adjuntos) queda **auditada** y protegida por **concurrencia optimista**.
- **RN-12.** La **totalización por moneda** se calcula **a nivel de empleado**: para cada empleado, subtotales por `CurrencyCode` (sin conversión), considerando los signos (los ajustes negativos restan).
- **RN-13.** Operaciones de escritura requieren expediente en estado **empleado completado**.
- **RN-14.** Aislamiento **multi-tenant** en toda operación.
- **RN-15.** Los registros son **internos de RR. HH.**; el empleado no tiene acceso (sin autoservicio).

---

## 10. Flujos principales

### Flujo A — Crear un tipo (catálogo)
1. Administrador → catálogos → "Tipos de transacción fuera de nómina" → Nuevo.
2. Ingresa **Code** + **Descripción**. 3. Sistema valida unicidad/longitud. 4. Guarda (activo).

### Flujo B — Registrar una transacción (con comprobante y vínculo opcional)
1. Gestor abre **expediente** → Compensación → "Transacciones fuera de nómina" → Agregar.
2. Elige **tipo**, ingresa **fecha**, **moneda**, **valor** (±), **año**, **mes**, **comentario**.
3. (Opcional) **adjunta comprobante(s)** y/o **vincula** a un AssetAccess del empleado.
4. Sistema valida (tipo activo, ISO moneda, valor ≠ 0, mes 1–12, año/rango, fecha no futura, expediente completado, AssetAccess del mismo empleado).
5. **Persiste**, registra **auditoría**, retorna `201 Created` con `ETag`.

### Flujo C — Consultar / filtrar / exportar
1. Abre la sección → aplica filtros (tipo, fechas, año/mes, texto) → pagina.
2. Exporta → archivo con **subtotales por moneda**.

### Flujo D — Editar / ajustar / dar de baja
1. Abre transacción (obtiene `concurrencyToken`).
2. Edita, registra **ajuste negativo**, o cambia estado a inactivo.
3. Sistema valida concurrencia/reglas, persiste y audita.

---

## 11. Flujos alternativos y excepciones

- **E-01 (tipo inválido/inactivo):** `422` `OFF_PAYROLL_TX_TYPE_CODE_INVALID`.
- **E-02 (moneda inválida):** `422` `OFF_PAYROLL_TX_CURRENCY_INVALID`.
- **E-03 (valor = 0):** `400/422` (se permiten negativos, no cero).
- **E-03b (valor negativo sin referencia al original):** `422` `OFF_PAYROLL_TX_CORRECTION_REQUIRED`.
- **E-04 (mes fuera de 1–12 / año fuera de rango):** `400/422`.
- **E-05 (fecha futura):** `400/422`.
- **E-06 (AssetAccess de otro empleado/inexistente):** `422` `OFF_PAYROLL_TX_ASSET_ACCESS_NOT_FOUND`.
- **E-06b (transacción original corregida inexistente/otro empleado):** `422` `OFF_PAYROLL_TX_CORRECTED_NOT_FOUND`.
- **E-07 (expediente no completado):** `409/422` `StateRuleViolation`.
- **E-08 (conflicto de concurrencia):** `409/412` `ConcurrencyConflict`.
- **E-09 (transacción/adjunto inexistente):** `404` `ItemNotFound`.
- **E-10 (sin permiso / no autenticado):** `403` / `401`.
- **E-11 (tipo duplicado en catálogo):** `409/422` de unicidad.
- **E-12 (adjunto inválido — tipo/tamaño):** `400/413/422` según política de archivos.
- **E-13 (intento de borrado físico):** no soportado; baja lógica.

---

## 12. Datos requeridos

### Entidad: TipoTransacciónFueraDeNómina (catálogo) — `OffPayrollTransactionTypeCatalogItem`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| CountryCatalogItemId / CountryCode | FK / texto | Sí | País válido | Alcance por país |
| Code | Texto (≤80) | **Sí (lo ingresa el usuario)** | Único por país; mayúsculas | Código identificador (D-03) |
| Name (**Descripción**) | Texto (≤200) | Sí | No vacío | **Descripción del tipo** |
| IsActive | Booleano | Sí | — | Baja lógica |
| SortOrder | Entero ≥0 | No | — | Orden de despliegue |
| ConcurrencyToken | GUID | Sí (sistema) | — | Concurrencia |
| CreatedUtc / ModifiedUtc | Fecha | Sí/No | — | Auditoría |

**Seed inicial sugerido:** `HERRAMIENTAS`/"Herramientas de trabajo", `EPP`/"Equipo de protección", `UNIFORMES`/"Uniformes", `PROMOCIONALES`/"Promocionales", `RECONOCIMIENTOS`/"Reconocimientos", `REGALOS`/"Regalos".

### Entidad: TransacciónFueraDeNómina — `PersonnelFileOffPayrollTransaction`

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| PersonnelFileId | FK (long) | Sí | Expediente existente y **completado** | Empleado asociado |
| OffPayrollTransactionTypeCode | Texto (≤80) | Sí | Existe y **activo** en catálogo | Tipo de registro |
| TransactionTypeNameSnapshot | Texto (≤200) | No (recomendado) | — | Snapshot de la descripción del tipo |
| TransactionDateUtc | Fecha (UTC) | Sí | No futura; normalizada | Fecha de la transacción |
| CurrencyCode | Texto (3) | Sí | ISO‑4217 (catálogo) | Moneda |
| Amount | Decimal | Sí | **≠ 0** (admite negativos) | Valor del gasto / ajuste |
| Year | Entero | Sí | Rango (p. ej. 2000–2100) | Año de **imputación** |
| Month | Entero | Sí | 1–12 | Mes de **imputación** |
| Comment | Texto (≤2000) | No | Longitud | Comentario |
| AssetAccessPublicId | GUID? | No | Si existe, del **mismo empleado** | Vínculo opcional a AssetAccess |
| AssetNameSnapshot | Texto (≤200) | No | — | Snapshot del activo vinculado |
| CorrectsTransactionPublicId | GUID? | **Sí, si `Amount` < 0** | Si existe, transacción **activa del mismo empleado** (recomendado: misma moneda, no un ajuste) | Transacción original que corrige el ajuste (D-12) |
| IsActive | Booleano | Sí | — | Baja lógica |
| ConcurrencyToken | GUID | Sí (sistema) | — | Concurrencia |
| CreatedAtUtc / ModifiedAtUtc | Fecha | Sí/No | — | Auditoría |

### Entidad: ComprobanteDeTransacción — `OffPayrollTransactionDocument` (adjunto)

| Campo | Tipo | Obligatorio | Validaciones | Descripción |
|---|---|---|---|---|
| PublicId | GUID | Sí (sistema) | Único | Identificador externo |
| OffPayrollTransactionId | FK | Sí | Transacción existente | Transacción a la que pertenece |
| StoredFileId / ruta Blob | ref | Sí | Política de archivos | Archivo en Azure Blob (`FilePurpose.OffPayrollTransactionDocument`) |
| DocumentTypeCode | Texto | No | Catálogo (opcional) | Clasificación opcional ("de cualquier índole") |
| FileName / ContentType / SizeBytes | varios | Sí | Tipo/tamaño permitidos | Metadatos del archivo |
| CreatedAtUtc | Fecha | Sí | — | Auditoría |

> **Nota (D-05):** la fecha ya contiene año/mes, pero **Año + Mes** se modelan **explícitos** porque representan el **período de imputación** (puede diferir de la fecha del gasto). Se autocompletan desde la fecha y quedan editables.

---

## 13. Integraciones necesarias

- **Catálogo de monedas** (`CurrencyCatalogItem`): validación ISO‑4217 + default por empresa.
- **Catálogos del módulo** (`GeneralCatalogItem`, `GeneralCatalogKeyMap`, seed): nuevo catálogo de tipos.
- **Almacenamiento de archivos (Azure Blob)** + **`FilePurpose.OffPayrollTransactionDocument`** (nuevo valor; hoy el enum solo tiene `ProfileImage, PersonnelDocument, ReportExport, CompanyLogo, Attachment, MedicalClaimDocument`) + `DocumentTypeCatalogItem` (clasificación opcional).
- **Módulo Equipo o Acceso** (`PersonnelFileAssetAccess`): vínculo opcional a nivel de registro (validación de pertenencia).
- **Servicio de auditoría** (`IAuditService` / `PersonnelFileEmployeeAudits`).
- **Servicio de exportación** (`ReportExportResources`): listado + subtotales por moneda.
- **Autenticación/Autorización** (IAM): permisos dedicados.
- **(Fuera de alcance)** Nómina/planilla, contabilidad/tesorería, FX, BI externo, motor de aprobaciones (futuro).

---

## 14. Roles y permisos

| Rol | Permisos | Restricciones |
|---|---|---|
| Administrador de catálogos | Crear/editar/activar/desactivar **tipos** | Solo su tenant; sin borrado físico |
| Gestor RR. HH. / Compensación | **CRUD** de transacciones, **adjuntos**, **vínculo a AssetAccess**, consulta y exportación (`PersonnelFiles.ManageOffPayrollTransactions`) | Solo su tenant; expediente completado |
| Supervisor / Gerente (consulta) | **Consulta / exportación** (lectura) | Sin escritura |
| Empleado (autoservicio) | **Sin acceso (D-06)** | Datos internos de RR. HH. |
| Sistema | Validación, auditoría, concurrencia, almacenamiento | — |

> **Nota técnica:** seguir el patrón `MedicalClaims`: `AuthorizationPolicySet` a nivel de clase (superset) y el chequeo fino del permiso dedicado en los handlers. **Sin autoservicio** (a diferencia de `MedicalClaims`).

---

## 15. Criterios de aceptación generales

- **CA-01.** Catálogo de tipos administrable (Code+Descripción, baja lógica), country-scoped y semillado.
- **CA-02.** Alta de transacción con todas sus validaciones (incl. negativos y año/mes de imputación).
- **CA-03.** Adjuntar/listar/descargar/eliminar **comprobantes**.
- **CA-04.** **Vínculo opcional** a AssetAccess con validación de pertenencia.
- **CA-05.** Listar/filtrar/paginar/consultar/editar/dar de baja.
- **CA-06.** Exportación con **subtotales por moneda** (sin conversión).
- **CA-07.** Auditoría + concurrencia en todas las escrituras.
- **CA-08.** Las transacciones **no** aparecen ni afectan reportes de **nómina**.
- **CA-09.** Permisos dedicados; **sin acceso del empleado**; aislamiento multi-tenant.
- **CA-10.** Mensajes/etiquetas **bilingües**; pruebas (unitarias de reglas + integración) en verde.

---

## 16. Riesgos, supuestos y dependencias

### Riesgos
- **R-01 (confusión de conceptos):** alto riesgo de confundir con `PersonnelFilePayrollTransaction`. Mitigación: nomenclatura `OffPayroll…`, documentación, revisión de diseño.
- **R-02 (doble captura con AssetAccess):** uniformes/herramientas/EPP existen como custodia en AssetAccess (sin monto). Con el vínculo opcional, riesgo de inconsistencia costo↔custodia. Mitigación: el vínculo es informativo y validado por pertenencia; capacitar a RR. HH.
- **R-03 (privacidad):** montos de regalos/reconocimientos son sensibles. Mitigación: interno de RR. HH., permiso dedicado, sin autoservicio.
- **R-04 (adjuntos):** almacenamiento de comprobantes implica costos, control de acceso y posibles datos personales. Mitigación: política de archivos existente, control de descarga, auditoría.
- **R-05 (expectativa de aprobación):** el negocio reconoce que faltará el flujo de aprobación (diferido). Mitigación: diseñar sin cerrar la puerta a un estado/flujo futuro; comunicar el alcance de esta fase.
- **R-06 (multimoneda sin FX):** totales por moneda pueden malinterpretarse como comparables. Mitigación: mostrar claramente subtotales separados, sin conversión.

### Supuestos
- **S-01.** No existe motor de nómina; registros informativos.
- **S-02.** Se reutilizan convenciones del módulo (catálogos, CQRS, auditoría, adjuntos, exportación, concurrencia).
- **S-03.** Catálogo de monedas y almacenamiento Blob disponibles.
- **S-04.** Un empleado por transacción (no prorrateo colectivo).

### Dependencias
- **DP-01.** Catálogo de monedas; convenciones de catálogos.
- **DP-02.** Servicios de auditoría, exportación, autorización y **archivos (Blob)**.
- **DP-03.** Permisos IAM (`ManageOffPayrollTransactions`, lectura).
- **DP-04.** Módulo Equipo o Acceso (para el vínculo).

---

## 17. Decisiones ratificadas (cierre de preguntas abiertas)

| ID | Pregunta | **Decisión del negocio** | Implicación |
|---|---|---|---|
| **D-01** | Vínculo con AssetAccess | **Vincular opcionalmente** | Campo `AssetAccessPublicId?` + snapshot; validar pertenencia al mismo empleado (RF-005). |
| **D-02** | ¿Compartir catálogo de tipos? | **No compartir** (análisis abajo) | Catálogo dedicado `OffPayrollTransactionType`; la conexión es a nivel de registro (D-01). |
| **D-03** | Code del tipo | **Lo ingresa el usuario** | Form de catálogo con Code + Descripción; Code único por país. |
| **D-04** | Valores negativos | **Se permiten** (deben referenciar el original, D-12) | `Amount ≠ 0`; negativo ⇒ `CorrectsTransactionPublicId` obligatorio. |
| **D-05** | Semántica Año/Mes | **Período de imputación** | Campos explícitos, autocompletados desde la fecha pero editables. |
| **D-06** | Visibilidad al empleado | **Interno de RR. HH.** | Sin autoservicio; permiso dedicado; dato sensible. |
| **D-07** | Adjuntos | **Sí, comprobante de cualquier índole** | Nuevo `FilePurpose.OffPayrollTransactionDocument`; uno o varios por transacción; clasificación opcional. |
| **D-08** | Moneda / reportería | **Sin conversión; sí totalización por moneda** | Subtotales por `CurrencyCode` **por empleado** (D-13), con signos; sin FX. |
| **D-09** | Flujo de aprobación | **Necesario a futuro, DIFERIDO** | Esta fase: solo captura del gasto ya incurrido; diseñar sin cerrar la puerta. |
| **D-10** | Topes / presupuesto | **No hay topes** | Sin límites ni alertas. |

### Análisis D-02 — ¿Compartir el catálogo de tipos con AssetAccess? → **NO**

**Tipos solicitados (fuera de nómina):** Herramientas de trabajo, Equipo de protección (EPP), Uniformes, Promocionales, Reconocimientos, Regalos.
**Tipos de AssetAccess (seed real):** `EQUIPO_COMPUTO, TELEFONO_MOVIL, UNIFORME, LICENCIA_SOFTWARE, ACCESO_SISTEMA, MOBILIARIO, HERRAMIENTA, OTRO` (`DevSeedService.cs:396-406`).

- **Solapamiento parcial:** solo **2 de 6** coinciden (Uniforme, Herramienta). EPP, Promocionales, Reconocimientos y Regalos **no existen** en AssetAccess.
- **Semánticas distintas:** AssetAccessType clasifica **activos en custodia** (con fecha de entrega/devolución, estado de entrega, nivel de acceso); los tipos fuera de nómina clasifican **categorías de gasto**, incluyendo conceptos **no-activos** (promocionales, reconocimientos, regalos) que se consumen/entregan y **no se devuelven**.
- **Conflictos al compartir:** forzaría meter Regalos/Reconocimientos/EPP en el catálogo de activos (donde "estado de entrega / devolución / nivel de acceso" no aplica) y expondría tipos solo-activo (LICENCIA_SOFTWARE, ACCESO_SISTEMA) en el selector de gasto.
- **Beneficio nulo:** la conexión útil ya se logra **a nivel de registro** (D-01: una transacción apunta a una entrega concreta), no a nivel de catálogo. Compartir solo agrega acoplamiento de ciclos de vida/activación.

**Conclusión:** catálogo **dedicado** (cumple "si no nos da ningún beneficio no lo hagas").

### Decisiones residuales ratificadas (técnicas)

| ID | Pregunta | **Decisión del negocio** | Implicación |
|---|---|---|---|
| **D-11** | Adjuntos: ¿límite/tipos? | **Hereda la política general de archivos** | Sin límites/whitelist específicos; reutiliza la validación de archivos existente. |
| **D-12** | Ajustes negativos | **Deben referenciar** la transacción original | Campo `CorrectsTransactionPublicId` **obligatorio si `Amount` < 0**; validar que la referenciada exista, esté activa y sea del **mismo empleado** (RN-04, errores `OFF_PAYROLL_TX_CORRECTION_REQUIRED` / `OFF_PAYROLL_TX_CORRECTED_NOT_FOUND`). |
| **D-13** | Totalización por moneda | **Por empleado** | Subtotales por `CurrencyCode` a nivel de empleado, con signos (RN-12 / RF-008). |

---

## 18. Recomendaciones del Analista de Negocio

1. **Construir como módulo hermano nuevo**, NO reutilizar `PersonnelFilePayrollTransaction`. Plantilla: `MedicalClaims` (monto+moneda+fecha+tipo-catálogo+comentario, **+ adjuntos**, CRUD, reglas puras, errores bilingües, auditoría before/after, concurrencia, exportación).
2. **Nomenclatura explícita:** entidad `PersonnelFileOffPayrollTransaction`, catálogo `OffPayrollTransactionTypeCatalogItem`, rutas `/personnel-files/{id}/off-payroll-transactions`, wire-key `off-payroll-transaction-types`, `FilePurpose.OffPayrollTransactionDocument`.
3. **Validar en escritura** tipo y moneda contra catálogo (a diferencia de `PayrollTransaction`, que los dejó como texto libre).
4. **Snapshots** de descripción del tipo y nombre del activo vinculado (preservar historial).
5. **Diseñar para el futuro flujo de aprobación (D-09):** reservar la posibilidad de un `StatusCode`/estado sin implementar el workflow ahora, para no requerir migración disruptiva después.
6. **Adjuntos y vínculo a AssetAccess entran en Fase 1** (son decisiones ratificadas), reutilizando la infraestructura de archivos y el módulo de Equipo/Acceso.
7. **Tratar los montos como dato sensible:** permiso dedicado, sin autoservicio, control de descarga de comprobantes.
8. **Secuencia de PRs propuesta:**
   - **PR-1.** Catálogo de tipos (`OffPayrollTransactionTypeCatalogItem`) + wire-key + seed (6 valores) + permiso de catálogo.
   - **PR-2.** Entidad `PersonnelFileOffPayrollTransaction` + migración + configuración EF + índices.
   - **PR-3.** Comandos/consultas CQRS + módulo de reglas puro + errores bilingües (incl. negativos con **referencia obligatoria al original**, año/mes imputación, validación de tipo/moneda).
   - **PR-4.** Controlador + rutas + concurrencia (`If-Match`) + permiso `ManageOffPayrollTransactions`.
   - **PR-5.** **Adjuntos** (`FilePurpose.OffPayrollTransactionDocument`, subir/listar/descargar/eliminar) reutilizando la infraestructura de archivos.
   - **PR-6.** **Vínculo opcional a AssetAccess** (validación de pertenencia + snapshot).
   - **PR-7.** Búsqueda/paginación/**exportación con subtotales por moneda a nivel de empleado**.
   - **PR-8.** Pruebas de integración + paridad de localización (es/en).
9. **Fase 2 (futuro):** flujo de aprobación (D-09), reembolso/conciliación contable, FX/reportería consolidada, dashboards de costo por empleado.

---

## Anexo — Evidencia de la validación (trazabilidad a código)

| Hallazgo | Evidencia |
|---|---|
| No existe feature "fuera de nómina" | Búsqueda nula de `OffPayroll`/`FueraNomina`/`out-of-payroll` en `src/` y `docs/` |
| Sí existe `PersonnelFilePayrollTransaction` (concepto distinto) | `src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs:587-677` |
| Tipo de `PayrollTransaction` es texto libre (sin catálogo) | `…/Compensation/PayrollTransactions.cs:114-122` |
| `PayrollTransaction` es inmutable (PATCH solo `isActive`) | `…/Controllers/PersonnelFileCompensationController.cs:536-566` |
| `PayrollTransaction` usa `PayrollPeriodCode` + `IsDebit` + `SourceSystem/Reference/SyncedUtc` | `PersonnelFileEmployee.cs:593-641` |
| Plantilla recomendada (`MedicalClaims`) | `…/Compensation/MedicalClaims.cs`, `MedicalClaims.Rules.cs`, `MedicalClaims.Handlers.cs` |
| Moneda como `string CurrencyCode` ISO‑4217 validada | `…/Compensation/Insurances.cs` (validación de `CurrencyCode`) |
| AssetAccess: entidad y campos para el vínculo (PublicId, `AssetOrAccessName`) | `PersonnelFileEmployee.cs:679-760` |
| AssetAccess type catalog (seed real, base del análisis D-02) | `DevSeedService.cs:396-406`; categoría `AssetAccessType` (`PersonnelReferenceCatalogs.cs:105`, wire `asset-access-types`) |
| `FilePurpose` actual (sin valor para esta feature → agregar uno) | `src/CLARIHR.Domain/Files/FileEnums.cs:12-20` |
| No hay motor de nómina (solo configuración + bitácora) | `…/Compensation/PersonnelFileCompensation.cs` |

---

> **Cierre:** El requerimiento **no estaba implementado**; el "primo" `PersonnelFilePayrollTransaction` es un concepto **distinto** y no se reutiliza. Con las **13 decisiones ratificadas (D-01…D-13)**, el requerimiento de negocio queda **cerrado y completo**: módulo nuevo con catálogo dedicado (Code+Descripción), año/mes de imputación, **ajustes negativos que referencian el original**, **adjuntos** (política general de archivos), **vínculo opcional a AssetAccess**, totalización por moneda **por empleado**, uso **interno de RR. HH.**, y aprobación **diferida** a una fase futura. Listo para el **plan técnico** y la secuencia de 8 PRs.
