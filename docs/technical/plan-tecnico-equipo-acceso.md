# Plan Técnico — Endurecimiento de Equipo o Acceso del Empleado (Nivel A)

| | |
|---|---|
| **Origen** | `docs/business/analisis-equipo-acceso-empleado.md` (Nivel A aprobado por el negocio) |
| **Entidad base** | `PersonnelFileAssetAccess` (`src/CLARIHR.Domain/PersonnelFiles/PersonnelFileEmployee.cs:669`) — ya existente |
| **Estado** | **Implementado** (2026-06-22). Build verde; 1852 pruebas unitarias en verde. |
| **Patrón de referencia** | Endurecimiento de sustituciones (`20260622155324_SubstitutionTypesAndHardenAuthSubstitution`) — mismo patrón texto-libre→catálogo + reglas + *check constraint*. |
| **País de referencia** | El Salvador (SV) |

---

## 1. Objetivo

Convertir dos códigos de **texto libre** del registro de equipo/acceso en **catálogos** país-scoped y validar la **coherencia de fechas**, sin ampliar el alcance funcional (sigue siendo registro documental). Tres RF:

- **RF-102** — `AssetTypeCode` → catálogo `asset-access-types`.
- **RF-103** — `DeliveryStatusCode` → catálogo `delivery-statuses` (opcional; `NO_APLICA` para accesos/licencias).
- **RF-101** — `StartDateUtc ≤ EndDateUtc` y `DeliveryDateUtc ≥ StartDateUtc` (ambas opcionales; se validan solo si están presentes).

## 2. Decisiones de diseño

1. **Catálogos como `GeneralCatalogItem`** (país-scoped, patrón `SubstitutionTypeCatalogItem`/`PaymentMethodCatalogItem`). Reutilizan `GeneralCatalogItemConfigurationBase<T>` (columnas, índices, FK a `country_catalog`).
2. **Validación centralizada** en `AssetAccessCommandSupport.ValidateAsync` (espejo de `AuthorizationSubstitutionCommandSupport`), invocada por los handlers **Add / PUT / PATCH** — un único hogar para catálogos + fechas, handlers delgados.
3. **Reglas de fecha puras y testeables** en `AssetAccessRules.ValidateDates` (sin BD). **No** hay reglas cross-row (a diferencia de plazas/sustituciones): los activos/accesos **coexisten** legítimamente (laptop + teléfono + uniforme a la vez), así que **no** hay solape/único-activo.
4. **Errores 422 con código estable** (no `common.validation` genérico) para que el frontend reaccione por código; paridad ES/EN obligatoria (guarda `BackendMessageLocalizationTests`).
5. **Defensa en profundidad**: además de la validación de aplicación, *check constraints* en BD (`end_date_utc`/`delivery_date_utc` coherentes con `start_date_utc`).
6. **`AccessLevelCode` permanece texto libre** (Nivel B / P-08, no aprobado).

## 3. Cambios por capa

### Catálogos (8 puntos de enganche, por catálogo)
- **Dominio:** `AssetAccessTypeCatalogItem`, `DeliveryStatusCatalogItem` en `Domain/GeneralCatalogs/GeneralCatalogItems.cs`.
- **EF config:** en `Infrastructure/Persistence/Configurations/GeneralCatalogs/GeneralCatalogItemConfiguration.cs` (tablas `asset_access_type_catalog_items`, `delivery_status_catalog_items`).
- **DbContext:** `DbSet`s en `ApplicationDbContext.cs`.
- **Categorías:** `AssetAccessType`, `DeliveryStatus` en `PersonnelCurriculumCatalogCategories` (`Catalogs/PersonnelReferenceCatalogs.cs`).
- **Key-map (lectura UI):** `asset-access-types`, `delivery-statuses` en `Catalogs/GeneralCatalogKeyMap.cs` (la guarda de bijección exige esta entrada por cada categoría).
- **Repositorio:** casos en `GetCountryScopedCatalogItemsAsync` (lectura) y `CatalogCodeIsActiveAsync` (validación) en `Infrastructure/PersonnelFiles/PersonnelFileRepository.cs`.
- **Seed:** `assetAccessTypes` y `deliveryStatuses` en `Persistence/DevSeedService.cs`.

### Feature equipo/acceso
- `Employment/AssetAccess.Rules.cs` *(nuevo)* — `AssetAccessErrors` (4 códigos) + `AssetAccessRules.ValidateDates`.
- `Employment/AssetAccess.Handlers.cs` — `AssetAccessCommandSupport.ValidateAsync` + invocación en los 3 handlers de escritura.
- `Configurations/PersonnelFiles/PersonnelFileEmployeeConfiguration.cs` — 2 `HasCheckConstraint` en `personnel_file_assets_accesses`.
- `Localization/BackendMessages.resx` + `BackendMessages.es.resx` — 4 entradas (EN/ES).

### Migración
- `Migrations/20260622202650_AssetAccessCatalogsAndHardenAssetAccess.cs` — crea las 2 tablas de catálogo + 2 *check constraints*; `Down` reversible. Snapshot actualizado.

## 4. Catálogos sembrados (SV)

- **`asset-access-types`:** `EQUIPO_COMPUTO`, `TELEFONO_MOVIL`, `UNIFORME`, `LICENCIA_SOFTWARE`, `ACCESO_SISTEMA`, `MOBILIARIO`, `HERRAMIENTA`, `OTRO`.
- **`delivery-statuses`:** `PENDIENTE`, `ENTREGADO`, `EN_USO`, `DEVUELTO`, `EXTRAVIADO`, `DANADO`, `NO_APLICA`.

## 5. API

Sin cambios en los 6 endpoints de `personnel-files/{id}/assets-accesses`. Lectura de catálogos para la UI:
- `GET /api/v1/general-catalogs/asset-access-types?countryCode=SV`
- `GET /api/v1/general-catalogs/delivery-statuses?countryCode=SV`

Errores nuevos (422): `ASSET_ACCESS_TYPE_CODE_INVALID`, `ASSET_ACCESS_DELIVERY_STATUS_CODE_INVALID`, `ASSET_ACCESS_DATE_RANGE_INVALID`, `ASSET_ACCESS_DELIVERY_DATE_INVALID`.

## 6. Migración / despliegue

```bash
# Generación (ya ejecutada). Nota: dotnet ef 9.0.9 con runtime net10 → usar roll-forward.
DOTNET_ROLL_FORWARD=Major dotnet ef migrations add AssetAccessCatalogsAndHardenAssetAccess \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj

# Aplicación: automática al iniciar la API (Database.MigrateAsync) o manual:
DOTNET_ROLL_FORWARD=Major dotnet ef database update \
  --project src/CLARIHR.Infrastructure/CLARIHR.Infrastructure.csproj \
  --startup-project src/CLARIHR.Api/CLARIHR.Api.csproj
```

**Datos existentes.** La validación de catálogo aplica en **escritura**: filas previas con tipo/estado de texto libre permanecen, pero un nuevo guardado exige códigos de catálogo. Si hubiera datos productivos, mapear los valores actuales a los códigos sembrados (o agregarlos al catálogo) antes de exigir el guardado. `DevSeedService` no crea filas de equipo/acceso, por lo que las *check constraints* de fecha no chocan con datos sembrados.

## 7. Verificación

- `dotnet build CLARIHR.slnx -c Debug` → verde.
- `dotnet test tests/CLARIHR.Application.UnitTests -c Debug` → **1852/1852** (incluye `AssetAccessRulesTests`, paridad de localización y bijección de key-map).
- Pendiente opcional: pruebas de integración de los endpoints (ninguna existente toca equipo/acceso; CI no las exige).

## 8. Fuera de alcance (Nivel B — diferido)

Devolución/condición, identificación del activo (serie/etiqueta/cantidad/valor), acta/responsiva + adjunto, integración con egreso, provisión real IAM y permiso dedicado. Ver preguntas abiertas P-03…P-09 del análisis de negocio.
