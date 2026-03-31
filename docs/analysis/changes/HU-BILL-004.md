# HU-BILL-004 — Cierre de implementación

## 1. Información general

- **Código HU:** HU-BILL-004
- **Título:** Add-ons especializados en el catálogo comercial global
- **Módulo:** Platform / Billing / Commercial Catalog
- **Fecha de cierre:** 2026-03-30
- **Estado:** Completada
- **Responsable:** Codex
- **Referencia funcional:** HU-BILL-004 — Crear add-ons de uso especializado
- **Referencia técnica:** `CommercialAddon`, `CommercialAddonsController`, migración `20260331034318_AddSpecializedCommercialAddonPricing`

---

## 2. Objetivo de la HU

### Resumen
Generalizar el catálogo global de `CommercialAddon` para soportar add-ons `Specialized` cobrados por seats o volumen, sin crear un recurso paralelo y sin mezclar esta HU con activación por empresa, consumo, cargos o facturación.

---

## 3. Alcance implementado

### Incluye
- Generalización del agregado `CommercialAddon` para soportar pricing `Massive` y `Specialized`.
- Nuevo `CommercialAddonBillingModel` con `PerActiveEmployee`, `PerSeat` y `PerVolume`.
- Contratos API refactorizados a `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity` y `minimumMonthlyFee`.
- Filtros de búsqueda por `type` y `billingModel` en `GET /api/platform/commercial-addons`.
- Migración EF con rename de `price_per_active_employee` a `unit_price`, nuevas columnas y backfill del catálogo masivo existente.
- Cobertura unitaria e integración para add-ons especializados, compatibilidad de lectura para masivos preexistentes y auditoría durable en writes globales.

### No incluye
- Activación del add-on en empresas, asignación de seats, registro de consumo, cálculo de cargos o facturación.
- Versionamiento de precios, descuentos, bundles, dependencias entre add-ons o eliminación física.

---

## 4. Impacto funcional

### Cambios funcionales
- El backoffice global ahora administra un solo catálogo de add-ons comerciales con dos variantes: `Massive` y `Specialized`.
- Los add-ons especializados pueden configurarse por `PerSeat` o `PerVolume`, con unidad comercial propia y cantidad mínima opcional.
- La baja operativa del catálogo sigue resolviéndose por `Inactivate`; no se expone delete físico.

### Flujo afectado
- Catálogo comercial de add-ons globales
- `api/platform/commercial-addons`

### ¿Requiere actualización de flujo de negocio?
- Sí

### Documento vivo afectado
- `docs/business/current-system-business-flows.md`

---

## 5. Impacto técnico

### Capas afectadas
- [x] Domain
- [x] Application
- [x] Infrastructure
- [x] API
- [x] Tests
- [x] Documentation
- [x] SQL / Data

### Componentes modificados
- Aggregate `CommercialAddon` y enums `CommercialAddonType`, `CommercialAddonBillingModel`, `CommercialAddonPeriodicity`, `CommercialAddonStatus`
- Commands, queries, handlers, validators y mapper de commercial add-ons
- `ICommercialAddonRepository`, `CommercialAddonRepository`, `CommercialAddonConfiguration`
- `CommercialAddonsController`
- Migración `20260331034318_AddSpecializedCommercialAddonPricing`

### Resumen técnico
`CommercialAddon` deja de estar acoplado únicamente al cobro por empleado activo. El agregado ahora modela pricing global con reglas de coherencia por tipo: `Massive` obliga `PerActiveEmployee` y la unidad reservada `active employee`; `Specialized` obliga `PerSeat` o `PerVolume`, bloquea `minimumMonthlyFee` y admite `minimumQuantity` opcional.

---

## 6. Cambios en API

### Endpoints nuevos
- Ninguno

### Endpoints modificados
- `GET /api/platform/commercial-addons` — agrega filtros `type` y `billingModel`; el listado ya no se limita a add-ons masivos.
- `POST /api/platform/commercial-addons` — request/response migran a pricing genérico.
- `PUT /api/platform/commercial-addons/{publicId}` — request/response migran a pricing genérico.
- `GET /api/platform/commercial-addons/{publicId}` — response expone pricing genérico.

### Contratos afectados
- Request: Sí
- Response: Sí
- Códigos de error: No
- Paginación / filtros / sorting: Sí
- Autenticación / autorización: No

### Documentación actualizada
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

---

## 7. Cambios en datos y persistencia

### Cambios realizados
- Rename de columna `price_per_active_employee` a `unit_price` en `commercial_addons`
- Columna nueva `billing_model`
- Columna nueva `measurement_unit`
- Columna nueva `minimum_quantity`
- Índice nuevo `ix_commercial_addons__billing_model`
- Backfill de filas existentes a `billing_model='PerActiveEmployee'` y `measurement_unit='active employee'`

### Scripts o migraciones relacionados
- `src/CLARIHR.Infrastructure/Persistence/Migrations/20260331034318_AddSpecializedCommercialAddonPricing.cs`

### Consideraciones
El catálogo sigue siendo global y no tenant-scoped. La migración preserva los add-ons masivos ya registrados y los adapta al modelo genérico sin crear tablas paralelas.

---

## 8. Seguridad

### Validaciones de seguridad aplicadas
- [ ] Tenant isolation
- [x] Autenticación
- [x] Autorización / RBAC
- [ ] Permisos por acción
- [ ] Permisos por campo
- [ ] Protección de datos sensibles
- [x] Auditoría
- [ ] No aplica

### Resumen
Las rutas continúan restringidas al backoffice global con token `platform`. `ReadOnly` conserva acceso de lectura; `Admin` conserva mutaciones. Los writes de add-ons especializados siguen dejando `PlatformAuditLog` persistente con actor y payload serializado.

### Documento vivo afectado
- `docs/analysis/current-state/security-analysis.md`

---

## 9. Rendimiento

### Consideraciones de rendimiento
- [x] Paginación
- [x] Proyección a DTO
- [x] `AsNoTracking()`
- [x] Índices revisados
- [x] Evitar N+1
- [ ] Proceso pesado fuera del request path
- [ ] No aplica

### Resumen
El listado conserva paginación, proyección y `AsNoTracking()`. Se agrega filtro por `billingModel` soportado por índice dedicado y se mantiene búsqueda libre por `code/name` sin joins pesados.

### Documento vivo afectado
- `docs/analysis/current-state/performance-analysis.md`

---

## 10. Pruebas realizadas

### Unit tests agregados o modificados
- `CommercialAddonDomainTests` valida combinaciones válidas e inválidas de `Massive` y `Specialized`, cantidades mínimas, unidad reservada, pricing coherente y transiciones de estado.
- `CommercialAddonAdministrationTests` valida autorización, conflicto por código, create especializado válido, rechazo de unidad incoherente, concurrencia y filtros por `type`/`billingModel`.

### Cobertura mínima validada
- [x] Happy path
- [x] Validaciones
- [x] Errores esperados
- [x] Permisos
- [x] Tenant scope
- [x] Reglas críticas
- [ ] No aplica

### Ejecución
- `dotnet build CLARIHR.slnx -v minimal`
- `dotnet test tests/CLARIHR.Application.UnitTests/CLARIHR.Application.UnitTests.csproj -v minimal --filter "FullyQualifiedName~CommercialAddonDomainTests|FullyQualifiedName~CommercialAddonAdministrationTests"`
- `dotnet test tests/CLARIHR.Api.IntegrationTests/CLARIHR.Api.IntegrationTests.csproj -v minimal --filter "FullyQualifiedName~BackofficeCommercialAddonsIntegrationTests"`

### Documento vivo afectado
- `docs/analysis/current-state/testing-analysis.md`

---

## 11. Documentación actualizada

### Documentos actualizados
- `docs/business/current-system-business-flows.md`
- `docs/analysis/current-state/architecture-analysis.md`
- `docs/analysis/current-state/security-analysis.md`
- `docs/analysis/current-state/performance-analysis.md`
- `docs/analysis/current-state/testing-analysis.md`
- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`
- `docs/analysis/changes/hu-index.md`

### Documentos no requeridos
- `docs/technical/overview/project-foundation.md`
- `docs/decisions/ADR-XXXX.md`

---

## 12. Riesgos, limitaciones y pendientes

### Riesgos identificados
- La migración asume que todas las filas previas del catálogo corresponden a add-ons masivos, lo cual es correcto para el estado actual del sistema.

### Limitaciones actuales
- `measurementUnit` es texto validado; no existe todavía un catálogo administrable de unidades comerciales.
- No existe endpoint de auditoría visible específico para add-ons; la trazabilidad completa sigue en `PlatformAuditLog`.

### Pendientes
- Activar add-ons por empresa y relacionarlos con consumo, seats o cobros.
- Introducir versionamiento de precios, descuentos y modelos híbridos.

---

## 13. Verificación funcional y técnica

### Pasos de validación
1. Autenticarse en `/api/platform/auth/login` con un usuario ligado a `PlatformOperator`.
2. Crear un add-on `Specialized` en `POST /api/platform/commercial-addons` con `billingModel=PerSeat`.
3. Actualizarlo a `PerVolume`, luego ejecutar `activate` e `inactivate`.
4. Consultar el listado con filtros `type=Specialized` y `billingModel=PerVolume`.
5. Verificar que un add-on masivo preexistente siga resolviéndose correctamente con `measurementUnit=active employee`.

### Resultado esperado
El backoffice global administra add-ons `Massive` y `Specialized` desde un único recurso `CommercialAddon`, con contrato genérico de pricing, control de concurrencia, migración compatible con datos previos y auditoría durable de plataforma.
