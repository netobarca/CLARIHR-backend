# HU-0016 — Administración de Representantes Legales (Registro y Actualización de Información Institucional)

## 0) Validación del estado actual
Resultado del análisis del backend/documentación actual:

- **No existe** módulo tenant-scoped para `LegalRepresentatives` (entidad + CRUD + seguridad + auditoría + tests).
- La entidad `Company` hoy solo maneja:
  - `name`
  - `slug`
  - `status`
  - `createdByUserPublicId`
- El módulo `AccountCompanies` solo permite:
  - crear empresa
  - listar empresa(s) del owner
  - actualizar nombre de empresa
  - archivar/reactivar
  - cambiar empresa activa
- No hay tablas, endpoints, DTOs ni permisos funcionales para:
  - registro de representante legal
  - actualización de datos de representante legal
  - estado/vigencia de representación

Conclusión:
- El requerimiento está **no cubierto** en su alcance funcional.
- Se requiere HU nueva para introducir catálogo y ciclo de vida de representantes legales por empresa.

---

## 1) Descripción del requerimiento (visión de negocio)
Como **Dueño de la cuenta / Administrador de Empresa / área administrativa-jurídica**,
quiero **registrar y actualizar la información de los representantes legales de la institución**,
para asegurar que la empresa mantenga datos vigentes, trazables y consistentes de las personas facultadas para representación formal.

Resultado esperado:
- La institución puede mantener **más de un representante legal** por empresa.
- El sistema conserva historial de cambios y vigencias.
- La información puede consultarse y exportarse para procesos internos, auditoría y cumplimiento.

---

## 2) Objetivo funcional (qué habilita)
- Catálogo tenant-scoped de representantes legales por empresa.
- Gobierno de datos sobre identidad, cargo, documento y vigencia.
- Distinción entre representante principal y alternos.
- Base para consumo por módulos futuros (contratos, firma de documentos, cumplimiento regulatorio).
- Trazabilidad de cambios administrativos críticos.

---

## 3) Alcance API (backend)
### Incluye
- CRUD de `LegalRepresentative` por empresa.
- Activación/inactivación con validaciones de consistencia.
- Marcado de representante principal (`isPrimary`) con regla de unicidad por empresa.
- Consulta:
  - listado paginado con filtros
  - detalle por id
  - export de catálogo (`csv|xlsx`)
- Soporte de vigencia (`effectiveFromUtc`, `effectiveToUtc`).

### Fuera de alcance (por ahora, pero preparado)
- Firma electrónica avanzada y flujo de firma documental.
- Validación automática contra padrones gubernamentales externos.
- Motor de poderes notariales multinivel con versionamiento documental completo.
- Integración obligatoria con ERP/gestor documental externo.

---

## 4) Actores y permisos
- **AccountOwner / Dueño de la cuenta**: administración total.
- **CompanyAdmin**: administración funcional completa.
- **LegalAdmin / ComplianceAdmin**: crea/edita/actualiza representantes.
- **HRRead / ComplianceRead**: consulta/exporta.

Permisos sugeridos:
- `LegalRepresentatives.Read`
- `LegalRepresentatives.Admin`
- `iam.administration.manage` (override)
- `platform_admin` (override)

Regla base:
- La API valida siempre coincidencia entre `companyId` de la ruta y claim `tid`.

---

## 5) Datos que debe manejar el backend (modelo mínimo)

### 5.1 Entidad principal: `LegalRepresentative`
- `Id` (GUID público)
- `CompanyId` (GUID, obligatorio)
- `FirstName` (string, obligatorio)
- `LastName` (string, obligatorio)
- `FullName` (derivado/normalizado para búsqueda)
- `DocumentType` (enum): `NationalId|Passport|TaxId|Other`
- `DocumentNumber` / `NormalizedDocumentNumber` (string, obligatorio, único por empresa)
- `PositionTitle` (string, obligatorio)  
  Ejemplo: representante legal, apoderado, gerente general
- `RepresentationType` (enum): `PrimaryLegalRepresentative|AlternateLegalRepresentative|AttorneyInFact`
- `AuthorityDescription` (string, opcional)  
  Alcance de representación (texto controlado/libre v1)
- `AppointmentInstrument` (string, opcional)  
  Referencia de escritura/acuerdo/documento
- `AppointmentDateUtc` (date, opcional)
- `EffectiveFromUtc` (date, obligatorio)
- `EffectiveToUtc` (date, opcional)
- `Email` (string, opcional)
- `Phone` (string, opcional)
- `IsPrimary` (bool)
- `IsActive` (bool)
- `ConcurrencyToken` (GUID)
- `CreatedAtUtc`, `UpdatedAtUtc`

### 5.2 Proyección de uso (preparado para v2)
- `LegalRepresentativeUsage`:
  - `legalRepresentativeId`
  - `activeDocumentReferencesCount`
  - `canInactivate`

> En v1 puede devolverse en cero/default si aún no existen módulos consumidores.

---

## 6) Reglas de negocio (backend)

### RN-01 Unicidad de documento por empresa
- `NormalizedDocumentNumber` es único por tenant.
- No permitir duplicados activos/inactivos del mismo documento dentro de la empresa.

### RN-02 Representante principal
- Solo puede existir **un** representante activo con `IsPrimary = true` por empresa.
- Si un registro se marca como principal, el backend debe resolver conflicto (rechazo o swap explícito según endpoint).

### RN-03 Vigencia
- `EffectiveFromUtc` obligatorio.
- Si `EffectiveToUtc` existe, debe ser `>= EffectiveFromUtc`.
- Un representante inactivo no debe reportarse como vigente en consultas “solo activos”.

### RN-04 Integridad de estado
- `activate` requiere que la vigencia no esté inválida.
- `inactivate` conserva historial (no hard delete).
- Operaciones por id distinguen `NotFound` vs `TenantMismatch`.

### RN-05 Concurrencia y auditoría
- Toda escritura por id valida `ConcurrencyToken`.
- Toda escritura registra auditoría con `before/after`.

### RN-06 Seguridad tenant-scoped
- Ninguna operación puede leer o modificar representantes de otro tenant.

---

## 7) Requerimientos técnicos del Backend (arquitectura + persistencia)

### RT-01 Arquitectura
- Mantener patrón vigente:
  - Clean Architecture
  - CQRS (`Commands/Queries + Handlers`)
  - FluentValidation
  - `ProblemDetails`
  - tenant-scoped por `tid`

### RT-02 Persistencia (PostgreSQL)
Tabla mínima sugerida:
- `legal_representatives`

Índices recomendados:
- `uq_legal_representatives__public_id`
- `uq_legal_representatives__tenant_document`
- `ux_legal_representatives__tenant_primary_active` (índice único parcial donde `is_primary = true and is_active = true`)
- `ix_legal_representatives__tenant_active`
- `ix_legal_representatives__tenant_normalized_name`
- `ix_legal_representatives__tenant_effective_dates`

### RT-03 Rendimiento
- Listados paginados obligatorios (`page`, `pageSize`).
- Filtros por `isActive`, `isPrimary`, `representationType`, `q`.
- Lecturas con `AsNoTracking`.
- Export por proyección plana única.

### RT-04 Seguridad y autorización
- Implementar `ILegalRepresentativeAuthorizationService` siguiendo patrón funcional existente.
- Lectura:
  - `LegalRepresentatives.Read`
  - `LegalRepresentatives.Admin`
  - overrides
- Escritura:
  - `LegalRepresentatives.Admin`
  - overrides

### RT-05 Auditoría
Eventos sugeridos:
- `LEGAL_REPRESENTATIVE_CREATED`
- `LEGAL_REPRESENTATIVE_UPDATED`
- `LEGAL_REPRESENTATIVE_ACTIVATED`
- `LEGAL_REPRESENTATIVE_INACTIVATED`
- `LEGAL_REPRESENTATIVE_SET_PRIMARY`

---

## 8) Endpoints propuestos (API v1)

### 8.1 Catálogo de representantes legales
- `POST   /api/v1/companies/{companyId}/legal-representatives`
- `GET    /api/v1/companies/{companyId}/legal-representatives?isActive=&isPrimary=&representationType=&q=&page=&pageSize=`
- `GET    /api/v1/legal-representatives/{id}`
- `PUT    /api/v1/legal-representatives/{id}`
- `PATCH  /api/v1/legal-representatives/{id}/activate`
- `PATCH  /api/v1/legal-representatives/{id}/inactivate`
- `PATCH  /api/v1/legal-representatives/{id}/set-primary`

### 8.2 Export y consumo interno
- `GET    /api/v1/companies/{companyId}/legal-representatives/export?format=csv|xlsx&isActive=&isPrimary=&representationType=&q=`
- `GET    /api/v1/legal-representatives/{id}/usage`

---

## 9) Contratos (DTOs) mínimos

### 9.1 `CreateLegalRepresentativeRequest`
- `firstName`*
- `lastName`*
- `documentType`*
- `documentNumber`*
- `positionTitle`*
- `representationType`*
- `authorityDescription`
- `appointmentInstrument`
- `appointmentDateUtc`
- `effectiveFromUtc`*
- `effectiveToUtc`
- `email`
- `phone`
- `isPrimary`

### 9.2 `UpdateLegalRepresentativeRequest`
- mismo contrato de create + `concurrencyToken`*

### 9.3 `ConcurrencyRequest`
- `concurrencyToken`*

### 9.4 `SetPrimaryLegalRepresentativeRequest`
- `concurrencyToken`*

### 9.5 `LegalRepresentativeResponse`
- `id`, `companyId`, `firstName`, `lastName`, `fullName`
- `documentType`, `documentNumber`
- `positionTitle`, `representationType`, `authorityDescription`
- `appointmentInstrument`, `appointmentDateUtc`
- `effectiveFromUtc`, `effectiveToUtc`
- `email`, `phone`
- `isPrimary`, `isActive`
- `concurrencyToken`, `createdAtUtc`, `updatedAtUtc`

### 9.6 `LegalRepresentativeUsageResponse`
- `legalRepresentativeId`
- `activeDocumentReferencesCount`
- `canInactivate`

---

## 10) Errores y códigos recomendados
- `400` `VALIDATION_ERROR`
- `401` `UNAUTHENTICATED`
- `403` `FORBIDDEN` / `TENANT_MISMATCH`
- `404` `LEGAL_REPRESENTATIVE_NOT_FOUND`
- `409` `LEGAL_REPRESENTATIVE_DOCUMENT_CONFLICT`, `LEGAL_REPRESENTATIVE_PRIMARY_CONFLICT`, `CONCURRENCY_CONFLICT`
- `422` `LEGAL_REPRESENTATIVE_EFFECTIVE_DATES_INVALID`, `LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION`

---

## 11) Criterios de aceptación (backend)
1. Puedo crear, consultar, actualizar y activar/inactivar representantes legales por empresa.
2. La API garantiza unicidad de documento por tenant.
3. La API garantiza como máximo un representante principal activo por empresa.
4. La API valida reglas de vigencia (`effectiveFromUtc`/`effectiveToUtc`).
5. Toda operación relevante queda auditada.
6. La API rechaza operaciones cross-tenant.
7. La API valida concurrencia en operaciones de escritura.
8. Puedo exportar el catálogo en `csv|xlsx`.
9. Puedo consultar detalle por id y listado paginado con filtros.
10. La API expone `usage/canInactivate` para preparar integraciones futuras.

---

## 12) Plan de pruebas

### Unit tests
- normalización de nombres y documento
- unicidad de documento por tenant
- regla de único `isPrimary` activo
- reglas de vigencia
- refresh de `ConcurrencyToken`

### Integration tests
- flujo feliz CRUD + activate/inactivate + set-primary + export
- `409` por documento duplicado
- `409` por conflicto de representante principal
- `403` por tenant mismatch
- `403` por falta de permisos
- auditoría de escrituras

### Validación final
- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`

---

## 13) Recomendaciones de implementación (mejores prácticas HRIS)
- Implementar por fases:
  - **Fase 1:** catálogo + CRUD + principal + vigencia + auditoría + export.
  - **Fase 2:** uso por contratos/firma + validaciones externas + gobierno documental avanzado.
- Mantener reglas críticas en backend, no en frontend.
- Preparar extensión de permisos de lectura parcial para datos sensibles (enmascaramiento de documento) si compliance lo exige.

---

## 14) Supuestos y decisiones cerradas
- “Institución” se modela como `Company` (tenant) en CLARIHR.
- Esta HU cubre administración de datos de representantes, no firma digital ni validación notarial externa.
- Se mantiene política de no hard delete para preservar trazabilidad histórica.
- El modelo se diseña para convivencia con módulos futuros de documentos/contratos.
