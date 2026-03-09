# HU-CUR-001 - Modulo Curriculum para Aspirante (Educacion, Idiomas, Capacitaciones, Empleos Anteriores y Referencias)

## 0) Validacion del estado actual
Resultado del analisis funcional y tecnico realizado:

- El expediente de personal (`PersonnelFiles`) ya existe y soporta secciones base, seguridad tenant-scoped, auditoria y concurrencia.
- El componente de Curriculum no existia como bloque formal completo en la primera iteracion.
- El requerimiento funcional solicita 5 secciones curriculares con opcion de actualizar datos y uso de catalogos.
- El modelo operativo acordado para esta HU es actualizacion por reemplazo completo por seccion (`PUT` por seccion + `concurrencyToken`).

Conclusion:
- Se requiere extension del modulo existente `PersonnelFiles` para incorporar Curriculum como parte nativa del expediente.

---

## 1) Historia de usuario
Como **Administrador de RRHH / Analista de Reclutamiento**,
quiero **registrar y actualizar el Curriculum del aspirante dentro de su expediente**,
para **evaluar su perfil academico y experiencia con informacion estructurada, validada y auditable**.

---

## 2) Objetivo funcional
- Incorporar 5 secciones de Curriculum en el expediente:
  - Educacion
  - Idiomas
  - Capacitaciones
  - Empleos anteriores
  - Referencias
- Garantizar consistencia de datos con reglas semanticas y catalogos activos.
- Permitir actualizacion recurrente de cada seccion sin romper compatibilidad de APIs existentes.

---

## 3) Alcance
### Incluye
- Persistencia de las 5 secciones dentro de `PersonnelFiles`.
- Endpoints de reemplazo por seccion (`PUT`) con `items[]` y `concurrencyToken`.
- Validaciones de negocio por seccion (fechas, montos, materias, habilidades, etc.).
- Validacion de catalogos activos por codigo.
- Auditoria de actualizacion de expediente.
- Respuesta expandida en `PersonnelFileResponse` con datos curriculares.

### Fuera de alcance
- Carga de adjuntos documentales por cada registro curricular (certificados, diplomas, etc.).
- OCR o parseo automatico de CV.
- Versionado historico por item curricular (v1 reemplaza seccion completa).

---

## 4) Secciones y campos requeridos
### 4.1 Educacion
- `statusCode` (graduado/pausado/en curso) [catalogo]
- `degreeTitle`
- `studyTypeCode` [catalogo]
- `career`
- `institution`
- `countryCode` [catalogo `Country`]
- `specialty`
- `isCurrentlyStudying` (bool)
- `startDate`
- `endDate`
- `shiftCode` [catalogo]
- `modalityCode` [catalogo]
- `totalSubjects`
- `approvedSubjects`

### 4.2 Idiomas
- `languageCode` [catalogo]
- `levelCode` [catalogo]
- `speaks` (bool)
- `writes` (bool)
- `reads` (bool)

### 4.3 Capacitaciones
- `trainingName`
- `trainingTypeCode` [catalogo]
- `description`
- `topic`
- `institution`
- `instructors`
- `score`
- `startDate`
- `endDate`
- `isInternal` (bool)
- `isLocal` (bool)
- `countryCode` [catalogo]
- `durationValue`
- `durationUnitCode` [catalogo]
- `costAmount`
- `costCurrencyCode` [catalogo `Currency`]

### 4.4 Empleos anteriores
- `institution`
- `place`
- `lastPosition`
- `managerName`
- `entryDate`
- `retirementDate`
- `companyPhone`
- `exitReason`
- `firstSalaryAmount`
- `lastSalaryAmount`
- `averageCommissionAmount`
- `currencyCode` [catalogo `Currency`]

### 4.5 Referencias
- `personName`
- `address`
- `phone`
- `referenceTypeCode` [catalogo]
- `occupation`
- `workplace`
- `workPhone`
- `knownTimeYears`

---

## 5) Reglas de negocio
### RN-01 Modelo de actualizacion
- Cada seccion se actualiza con reemplazo completo.
- Si una seccion se envia vacia, queda vacia en el expediente.

### RN-02 Concurrencia
- Todo `PUT` por seccion requiere `concurrencyToken`.
- Si el token no coincide: `409 CONCURRENCY_CONFLICT`.

### RN-03 Seguridad y tenant
- Solo usuarios autorizados pueden gestionar expediente (`EnsureCanManageAsync`).
- Debe validarse aislamiento por tenant en toda operacion.

### RN-04 Educacion
- Debe existir solo `isCurrentlyStudying` como bandera de estudio actual.
- Si `isCurrentlyStudying = false`, `endDate` es requerida.
- `approvedSubjects <= totalSubjects` cuando ambos existan.
- Fechas deben ser consistentes (`endDate >= startDate` cuando aplique).

### RN-05 Idiomas
- Debe existir al menos una habilidad en `true` entre `speaks`, `writes`, `reads`.

### RN-06 Capacitaciones
- Fechas consistentes (`endDate >= startDate` cuando ambas existan).
- `durationValue > 0`.
- `costAmount >= 0`.
- `costCurrencyCode` obligatorio.

### RN-07 Empleos anteriores
- Fechas consistentes (`retirementDate >= entryDate` cuando ambas existan).
- Montos no negativos.
- `currencyCode` obligatorio.

### RN-08 Referencias
- `knownTimeYears >= 0`.
- Telefonos con formato valido de acuerdo a reglas del modulo.

### RN-09 Catalogos
- Todo codigo de catalogo debe existir y estar activo.
- Validacion por `CatalogCodeIsActiveAsync(tenantId, category, code)`.

### RN-10 Auditoria
- Toda actualizacion curricular registra evento `PersonnelFileUpdated` con before/after.

---

## 6) Catalogos funcionales
Categorias requeridas para Curriculum:
- `CurriculumEducationStatus`
- `CurriculumStudyType`
- `CurriculumShift`
- `CurriculumModality`
- `CurriculumLanguage`
- `CurriculumLanguageLevel`
- `CurriculumTrainingType`
- `CurriculumDurationUnit`
- `CurriculumReferenceType`
- `Country`
- `Currency` (reuso)

Fuente:
- `personnel_catalog_items` por categoria.

---

## 7) APIs e interfaces
### Endpoints nuevos
- `PUT /api/v1/personnel-files/{id}/educations`
- `PUT /api/v1/personnel-files/{id}/languages`
- `PUT /api/v1/personnel-files/{id}/trainings`
- `PUT /api/v1/personnel-files/{id}/previous-employments`
- `PUT /api/v1/personnel-files/{id}/references`

### Contrato base por endpoint
- Request:
  - `items[]`
  - `concurrencyToken`
- Response:
  - `PersonnelFileResponse` actualizado (aditivo, backward compatible).

### Reuso de API existente
- `GET /api/v1/companies/{companyId}/personnel-catalogs/{category}`
  para poblar combos de catalogos.

---

## 8) Persistencia y migracion
- Migracion: `hu026_personnel_files_curriculum.sql`
- Tablas:
  - `personnel_file_educations`
  - `personnel_file_languages`
  - `personnel_file_trainings`
  - `personnel_file_previous_employments`
  - `personnel_file_references`

Estructura estandar por tabla:
- `public_id`
- `personnel_file_id`
- `tenant_id`
- `created_utc`
- `modified_utc`
- `uq_*__public_id`
- `ix_*__tenant_file`

Constraints clave:
- Fechas consistentes.
- Montos no negativos.
- `approved_subjects <= total_subjects` cuando ambos existen.

---

## 9) Criterios de aceptacion (Gherkin)
### CA-01 Actualizacion de Educacion exitosa
Given un expediente existente y token vigente  
When envio `PUT /educations` con items validos  
Then recibo `200` y la seccion Educacion queda persistida con los datos enviados.

### CA-02 Regla de materias en Educacion
Given un payload con `approvedSubjects > totalSubjects`  
When envio `PUT /educations`  
Then recibo `422` indicando violacion semantica.

### CA-03 Regla de habilidades en Idiomas
Given un idioma con `speaks=false`, `writes=false`, `reads=false`  
When envio `PUT /languages`  
Then recibo `422` por no tener ninguna habilidad activa.

### CA-04 Regla de montos/fechas en Capacitaciones y Empleos
Given un payload con montos negativos o fechas invalidas  
When envio `PUT` a la seccion correspondiente  
Then recibo `422` con detalle de validacion.

### CA-05 Validacion de catalogo
Given un codigo inexistente o inactivo  
When envio `PUT` en cualquier seccion que usa catalogos  
Then recibo `422` por codigo invalido/inactivo.

### CA-06 Concurrencia
Given un `concurrencyToken` desactualizado  
When envio cualquier `PUT` curricular  
Then recibo `409`.

### CA-07 Seguridad
Given usuario sin autenticacion  
When invoca endpoints de Curriculum  
Then recibe `401`.

Given usuario autenticado sin permisos o tenant cruzado  
When invoca endpoints de Curriculum  
Then recibe `403`.

---

## 10) Plan de pruebas
### Unit tests
- Entidades de dominio: reglas de fechas, montos y materias.
- Validadores FluentValidation por cada seccion.

### Application tests
- Catalogos no existentes/inactivos.
- Concurrencia stale token.
- Tenant mismatch y permisos.

### Integration tests API
- Flujo feliz de los 5 `PUT`.
- Verificacion posterior via `GET /api/v1/personnel-files/{id}`.

### Error tests
- `422` reglas semanticas.
- `409` concurrencia.
- `403` autorizacion/tenant.
- `401` sin autenticacion.

### Regresion
- Smoke de `PersonnelFiles`: create/get/document/export/analytics sin ruptura.

---

## 11) Definicion de terminado (DoD)
- Migracion aplicada y referenciada en bootstrap SQL.
- Endpoints y contratos documentados.
- Validaciones funcionales implementadas.
- Auditoria, seguridad y tenant isolation verificados.
- Unit + integration tests en verde.
- Compatibilidad hacia atras confirmada para consumidores actuales.

---

## 12) Supuestos cerrados
- Curriculum vive dentro de `PersonnelFiles`.
- Estrategia v1: reemplazo completo por seccion.
- `isCurrentlyStudying` es la bandera unica en Educacion.
- `knownTimeYears` modela el "tiempo" de Referencias en anios.
- `isInternal` en Capacitaciones indica impartida por la empresa.
- `isLocal` en Capacitaciones indica realizada en el pais local del proceso.
