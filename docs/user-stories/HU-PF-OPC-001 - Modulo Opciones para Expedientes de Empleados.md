# HU-PF-OPC-001 - Modulo Opciones para Expedientes de Empleados (Impresion, Listado, Filtros, Agrupacion y Consulta Dinamica)

## 0) Validacion del estado actual
Resultado del analisis funcional y tecnico:

- `PersonnelFiles` ya dispone de listados, export y componentes base de seguridad/auditoria.
- Se identifico necesidad de ampliar el modulo con opciones operativas avanzadas:
  - impresion del expediente (total o por secciones)
  - listado con filtros por columna y ordenamiento
  - agrupacion por criterios de negocio
  - consulta dinamica para exploracion avanzada
- La expectativa de negocio exige mantener compatibilidad con endpoints ya consumidos.

Conclusion:
- Se requiere una HU aditiva sobre `PersonnelFiles` sin ruptura contractual para consumidores existentes.

---

## 1) Historia de usuario
Como **Administrador de RRHH / Analista de talento**,
quiero **imprimir expedientes y consultar listados de empleados con filtros, agrupaciones y busqueda dinamica**,
para **analizar informacion de personal rapidamente y generar reportes operativos confiables**.

---

## 2) Objetivo funcional
- Habilitar impresion de expediente completo o por secciones.
- Mejorar el listado general con filtros de columna, orden y export consistente.
- Exponer una consulta dinamica configurable por filtros, agrupacion y ordenamiento.
- Reflejar capacidades reales de reportes para `PERSONNEL_FILES`.

---

## 3) Alcance
### Incluye
- Endpoint de impresion de expediente.
- Extension de listado existente (`GET personnel-files`) con filtros aditivos y sorting.
- Extension de export (`csv|xlsx`) para respetar mismos filtros/orden.
- Endpoint de consulta dinamica (`dynamic-query`).
- Lista blanca de campos filtrables/agrupables/ordenables.
- Tope de cardinalidad para agrupaciones (proteccion de performance).
- Hardening de indices SQL para consultas frecuentes.

### Fuera de alcance
- Generacion de PDF nativo en backend (v1 retorna payload estructurado para cliente).
- Integraciones BI externas.
- Diseñador visual de consultas.

---

## 4) Actores y permisos
- `CompanyAdmin` / `HRAdmin`: acceso operativo completo.
- `HRRead` / `Analyst`: consulta, impresion y export segun permisos de lectura.
- `platform_admin`: override.

Reglas de seguridad:
- Lectura y consulta via `EnsureCanReadAsync`.
- Gestion via `EnsureCanManageAsync` cuando aplique.
- Tenant isolation obligatorio en toda consulta y reporte.

---

## 5) Capacidades funcionales requeridas
### 5.1 Impresion de expediente
- Imprimir expediente completo o secciones seleccionadas.
- Endpoint:
  - `GET /api/v1/personnel-files/{id}/print?sections=...`
- Contrato:
  - `PersonnelFilePrintResponse`
    - `generatedAtUtc`
    - `includedSections`
    - `personnelFile`
- Auditoria:
  - evento `REPORT_PRINTED`.

### 5.2 Listado de empleados con filtros y orden
- Extender endpoint existente:
  - `GET /api/v1/companies/{companyId}/personnel-files`
- Mantener:
  - `page`, `pageSize`, `q`
- Agregar filtros aditivos sugeridos:
  - `maritalStatus`
  - `nationality`
  - `profession`
  - `createdFromUtc`
  - `createdToUtc`
- Orden:
  - `sortBy`
  - `sortDirection`.

### 5.3 Export con mismos filtros del listado
- Extender:
  - `GET /api/v1/companies/{companyId}/personnel-files/export?format=xlsx|csv`
- Debe aplicar exactamente filtros y orden del listado.
- Auditoria:
  - evento `REPORT_EXPORTED` con filtros completos aplicados.

### 5.4 Agrupacion y consulta dinamica
- Nuevo endpoint:
  - `POST /api/v1/companies/{companyId}/personnel-files/dynamic-query`
- Request v1:
  - `filters[]`
  - `groupBy[]`
  - `sort[]`
  - `q`
  - `page`
  - `pageSize`
- Response:
  - `items[]`
  - `groups[]`
  - `totalCount`
  - metadatos de paginacion.

### 5.5 Capabilities de reportes
- Corregir:
  - `GET /api/v1/companies/{companyId}/reports/capabilities?resource=PERSONNEL_FILES`
- Debe retornar `supportsPrint=true` y `supportsExport=true`.

---

## 6) Reglas de negocio
### RN-01 Compatibilidad
- Cambios en endpoints existentes son aditivos.
- Contratos actuales no deben romper consumidores previos.

### RN-02 Validacion de filtros/agrupaciones/sort
- Solo se permiten campos definidos en lista blanca.
- Operadores invalidos devuelven `400/422`.
- Se limita cantidad de agrupaciones por consulta para evitar sobrecarga.

### RN-03 Seguridad
- `401` para llamadas sin autenticacion.
- `403` para falta de permisos o acceso cross-tenant.
- `404` para expediente inexistente en endpoints por id.

### RN-04 Consistencia de export
- Export `csv|xlsx` debe reflejar exactamente filtros y orden del listado.

### RN-05 Auditoria
- Impresion: `REPORT_PRINTED`.
- Exportacion: `REPORT_EXPORTED`.
- Registrar actor, tenant, filtros y volumen de registros.

### RN-06 Performance
- Paginacion obligatoria en listados y consulta dinamica.
- Topes de agrupacion y bucketting en dinamica.

---

## 7) Requerimientos tecnicos
### RT-01 Arquitectura
- Implementacion dentro de `PersonnelFiles` siguiendo patrones actuales:
  - CQRS
  - FluentValidation
  - tenant-scoped
  - ProblemDetails
  - auditoria unificada.

### RT-02 Persistencia/Repositorio
- Extender repositorio para:
  - filtros de columna
  - sorting
  - dynamic query con agrupaciones.
- Evitar expresiones LINQ no traducibles por EF para queries de agregacion.

### RT-03 SQL hardening
- Crear migracion con indices orientados a filtros/agrupaciones mas frecuentes.
- Incluir migracion en `001_apply_clarihr_schema.sql`.

### RT-04 Capabilities
- Actualizar registro de capacidades de reportes para `PERSONNEL_FILES`.

---

## 8) APIs y contratos
### Nuevos endpoints
- `GET /api/v1/personnel-files/{id}/print?sections=...`
- `POST /api/v1/companies/{companyId}/personnel-files/dynamic-query`

### Endpoints extendidos
- `GET /api/v1/companies/{companyId}/personnel-files`
- `GET /api/v1/companies/{companyId}/personnel-files/export`
- `GET /api/v1/companies/{companyId}/reports/capabilities?resource=PERSONNEL_FILES`

### Contratos principales
- `PersonnelFilePrintResponse`
- `DynamicQueryPersonnelFilesRequest`
- `PersonnelFileDynamicQueryResponse`
- `PersonnelFileDynamicFilterInput`
- `PersonnelFileDynamicSortInput`

---

## 9) Criterios de aceptacion (Gherkin)
### CA-01 Impresion completa
Given un expediente valido con permisos de lectura  
When invoco `GET /personnel-files/{id}/print` sin `sections`  
Then recibo `200` con `includedSections` completas y `generatedAtUtc`.

### CA-02 Impresion por secciones
Given un expediente valido  
When invoco `GET /personnel-files/{id}/print?sections=personalInfo,educations`  
Then recibo `200` con solo esas secciones incluidas.

### CA-03 Listado con filtros y orden
Given multiples expedientes en tenant  
When consulto `GET /companies/{companyId}/personnel-files` con filtros de columna y `sortBy/sortDirection`  
Then recibo resultados filtrados, ordenados y paginados.

### CA-04 Export coherente con listado
Given filtros aplicados en listado  
When exporto `csv` o `xlsx` con los mismos parametros  
Then el archivo contiene exactamente el mismo set filtrado/ordenado.

### CA-05 Consulta dinamica agrupada
Given filtros y `groupBy` validos  
When invoco `POST /dynamic-query`  
Then recibo `items[]`, `groups[]`, `totalCount` y metadatos de pagina.

### CA-06 Validaciones semanticas en dinamica
Given `groupBy` o `filters` con campos/operadores no permitidos  
When invoco `POST /dynamic-query`  
Then recibo `400/422` con detalle de validacion.

### CA-07 Capabilities de reportes
Given usuario autorizado  
When consulta `reports/capabilities` para `PERSONNEL_FILES`  
Then `supportsPrint=true` y `supportsExport=true`.

---

## 10) Plan de pruebas
### Unit tests
- Validadores de `dynamic-query`:
  - campos permitidos
  - operadores invalidos
  - reglas de agrupacion/sort.

### Application tests
- Autorizacion.
- Tenant mismatch.
- Errores semanticos `400/422`.

### Integration tests
- `print` completo y por secciones.
- listado con filtros + orden.
- `dynamic-query` con agrupaciones (estado civil, nacionalidad, unidad).
- export `xlsx/csv` con filtros aplicados.
- capabilities `PERSONNEL_FILES` con impresion habilitada.

### Error tests
- `401` sin autenticacion.
- `403` sin permisos o cross-tenant.
- `400/422` por query invalida.
- `404` por expediente inexistente.

### Regresion
- Smoke sobre create/get/update/curriculum/documentos/export/analytics.

---

## 11) Definicion de terminado (DoD)
- Endpoints nuevos y extendidos operativos.
- Validadores y whitelist implementados.
- Auditoria de impresion/export confirmada.
- Capabilities de reportes alineadas con funcionalidad real.
- Migracion de indices creada y referenciada.
- Documentacion tecnica actualizada.
- Unit + integration tests en verde.

---

## 12) Supuestos cerrados
- Se implementa como extension de `PersonnelFiles`.
- Impresion v1 retorna payload estructurado (sin PDF).
- Exportacion mantiene `csv|xlsx`.
- Consulta dinamica v1 usa campos existentes del expediente.
- Se conserva el modelo actual de seguridad, auditoria y tenant isolation.
