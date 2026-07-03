# Plan Técnico — Revalidación de Catálogos del Expediente de Empleados

| | |
|---|---|
| **Tipo de documento** | Plan técnico de implementación (Fases 0–4) |
| **Audiencia** | Equipo de desarrollo backend, QA, tech lead |
| **Documento de negocio** | [`analisis-revalidacion-catalogos.md`](../business/analisis-revalidacion-catalogos.md) (v2.0, D-01…D-16 **ratificadas**, seed §20) |
| **Módulos** | Expediente de Personal (`PersonnelFiles`), Catálogos (`GeneralCatalogs`/`PersonnelReferenceCatalog`/`EducationCatalogs`/`Compensation`), Backoffice de catálogos |
| **Estado** | Propuesto — listo para implementar |
| **Fecha** | 2026-06-30 |
| **País de referencia (seed)** | El Salvador (SV) |

---

## 1. Objetivo y enfoque

Cerrar el entregable de catálogos del expediente: **crear los faltantes, ampliar los incompletos y estandarizar los de texto libre**, entregando **cada catálogo con su seed inicial vía `HasData`** (llega a todos los ambientes, incluido producción). Alcance por catálogo, estado y decisiones en el documento de negocio (§0 matriz, §19 decisiones, §20 seed).

**Principios de ejecución (ratificados):**

1. **Seed obligatorio (requisito duro):** ningún catálogo se entrega vacío. Todo valor va por `GlobalCatalogSeedData` (`HasData` → `InsertData`/`UpdateData` en migración). **Nunca** solo `DevSeedService` (no backfillea tenants provisionados).
2. **Reutilizar los patrones existentes** (Caso A/B/C de §3) — no se inventa infraestructura de catálogo.
3. **País obligatorio SV** (D-07); sin bandera "aplica a todos".
4. **Mantenimiento = backoffice** (D-14): los catálogos se administran vía los subsistemas de backoffice ya existentes; **sin** autoservicio de tenant y **sin** permisos nuevos.
5. **Validación server-side** por código de catálogo activo en cada punto de consumo (422).
6. **Faseo** (D-15): F0 quick wins → F1 simples/ampliaciones → F2 educación → F3 formato de documento → F4 AFP. **MVP = F0 + F1** (cierra 9/14).

---

## 2. Línea base verificada en el código

| # | Tema | Hallazgo (archivo:línea) | Implicación |
|---|---|---|---|
| 1 | Auto-discovery EF | `ApplicationDbContext.cs:394` `ApplyConfigurationsFromAssembly` | Basta crear el `IEntityTypeConfiguration<T>`; no se registra a mano |
| 2 | Base country-scoped | `CountryScopedCatalogItem.cs` (Code/Name/IsActive/SortOrder/CountryCode/CountryCatalogItemId) | Base de General + Reference |
| 3 | Base global | `SystemScopedCatalogItem.cs` (sin país) | Base de Education/DocumentType |
| 4 | Familia General (thin) | `ContractTypeCatalogItem` `GeneralCatalogItems.cs:764`; config base `GeneralCatalogItemConfigurationBase` (idx `(country,normalizedCode)`) | Receta Caso A (§3.1) |
| 5 | Familia General (enriquecida) | `CompensationConceptTypeCatalogItem.cs:13` + config dedicada + read endpoint dedicado | Receta Caso B (§3.2) — DTO genérico no lleva columnas extra |
| 6 | Familia Reference (jerárquica) | `InsuranceRangeCatalogItem`→`InsuranceTypeCatalogItem` (`PersonnelReferenceCatalogItem.cs:250`), `ConfigureUniqueCodeIndex` override | Receta Caso C (§3.3) |
| 7 | Wire key + guardrail | `GeneralCatalogKeyMap.cs` (`CatalogKeys`/`ReferenceCatalogKeys`) + bijection `GeneralCatalogKeyMapGuardrailsTests.cs:26/47` | Cada categoría nueva ↔ 1 wire key |
| 8 | Repo dispatch | `PersonnelFileRepository.cs`: `GetCatalogItemsAsync:1308`, `CatalogCodeIsActiveAsync:1443`, `GetReferenceCatalogItemsAsync:1398`, `ReferenceCatalogCodeIsActiveAsync:1516` | Un `case` por catálogo consumido |
| 9 | Seed | `GlobalCatalogSeedData.cs` (`CreateGeneralCatalogSeed:926`, `CreateSeedPublicId:23`, `ResolveCountryId:950`, ids negativos `-9xxx`) | Factory por catálogo, ids negativos únicos |
| 10 | Backoffice CRUD (country) | `SystemCatalogsController.cs` + `SystemCatalogAdministration.cs` (`SystemCatalogType` enum, `SystemCatalogKeyMap.TryParse:520`, `SystemCatalogFactory.Create:439`) + `SystemCatalogRepository.cs` (dispatch ×5) | DTO **plano** (code/name/sortOrder) |
| 11 | Backoffice CRUD (educación) | `EducationCatalogsController.cs` (`[Authorize(PlatformOperator)]`) + `EducationCatalogCommands.cs` (`EducationCatalogFactory.Create:255`) + repo dispatch ×7 (incl. `IsInUseAsync:111`) | DTO **plano**; enum `EducationCatalogType` |
| 12 | Persona (atributos código) | `PersonnelFile.MaritalStatus:83`/`Profession:85`, `UpdatePersonalInfo:204`, validación `ValidatePersonalInfoCodesAsync`→`ReferenceCatalogCodeIsActiveAsync` (`PersonnelReferenceCatalogs.cs:145/460`) | Precedente para `PersonalTitleCode`/`AfpCode` |
| 13 | Dirección | `PersonnelFileAddress:1023` (sin tipo; Country/Dept/Muni **texto libre**), sin validación de catálogo en handlers | Agregar `AddressTypeCode` + guard nuevo |
| 14 | Sub-entidad validada (patrón) | `KinshipCode` en `FamilyMembers.Handlers.cs:117/246/489` vía `ValidateKinshipCodeAsync` | Patrón a replicar en dirección/hobbies/… |
| 15 | Documento — número | Validación genérica `IsValidCode`+`MaxLength(80)` (`Identifications.cs:167`); tipo validado `ValidateIdentificationTypeCodeAsync` (`Identifications.Handlers.cs:117/234/469`) | **No hay** máscara por tipo; insertar validación async |
| 16 | Educación (FK por id interno) | `PersonnelFileEducation` FKs `long` `EducationStudyTypeCatalogItemId:1723`/`EducationCareerCatalogItemId:1727`; guards **`== 0`** (relajados de `<=0`, comentario `:1642`) | Ids negativos de HasData son válidos; carrera↔tipo-estudio **no** existe hoy |
| 17 | Educación global | `EducationCareerCatalogItem : SystemScopedCatalogItem` (`EducationCatalogItems.cs:55`), idx único **1 columna** `NormalizedCode` | Convertir a country-scoped = swap de base + índice compuesto + reseed |
| 18 | Sin máscara/regex | Solo regex genéricas (`PersonnelFileCommon.cs:58-65`); ninguna por tipo/valor | Motor de formato = net-new |
| 19 | Errores bilingües | `PersonnelFileErrors` (`PersonnelFileCommon.cs:211`) + 3 resx (`BackendMessages.resx`/`.es.resx`/`.es-SV.resx`); paridad `BackendMessageLocalizationTests` | 1 error nuevo (formato de documento) |
| 20 | Migraciones | EF 9.0.9 requiere `DOTNET_ROLL_FORWARD=Major`; límite 63 chars en índices; cleanup `id>0` al mover DevSeed→HasData | Ver §6 |

---

## 3. Arquitectura y convenciones (recetas)

### 3.1. Caso A — catálogo country-scoped **thin** (Code + Name), familia General

Ejemplar: `ContractTypeCatalogItem`/`PaymentMethodCatalogItem`. **7 puntos de toque:**

1. **Entidad** `GeneralCatalogItems.cs`: `sealed class XxxCatalogItem : GeneralCatalogItem` (ctor privado + `static Create(countryCatalogItemId, countryCode, code, name, isActive, sortOrder)`).
2. **EF config**: `sealed XxxCatalogItemConfiguration : GeneralCatalogItemConfigurationBase<XxxCatalogItem>` con nombres de tabla/índices + `GlobalCatalogSeedData.GetXxxCatalogItems()`. **Ojo 63 chars** en `ix_..._active_sort`.
3. **DbSet** en `ApplicationDbContext.cs` (bloque 219–301).
4. **Categoría** `PersonnelCurriculumCatalogCategories.Xxx` + **wire key** `GeneralCatalogKeyMap.CatalogKeys["xxx"]` (guardrail de bijección reflexivo).
5. **Repo dispatch**: `GetCatalogItemsAsync` (case `"XXX"`) + `CatalogCodeIsActiveAsync` (case `"XXX"`).
6. **Seed factory** `GlobalCatalogSeedData.GetXxxCatalogItems()` con `CreateGeneralCatalogSeed("XXX_CATALOG", -96xxL, "SV", CODE, Nombre, sort)`.
7. **(Consumo)** validar el código con `PersonnelCurriculumCatalogValidation.ValidateCodeAsync`/`CatalogCodeIsActiveAsync`.

La familia **Reference** (`PersonnelReferenceCatalogItemBase`) es un stack paralelo idéntico con su propio dict `ReferenceCatalogKeys`, `PersonnelReferenceCatalogCategories`, y switches `GetReferenceCatalogItemsAsync`/`ReferenceCatalogCodeIsActiveAsync`. **Regla de elección:** atributo de persona (validado como `MaritalStatus`/`Kinship`) → **Reference**; lista de apoyo para un `xxxCode` de formulario → **General**.

### 3.2. Caso B — catálogo **enriquecido** (columnas extra)

Ejemplar: `CompensationConceptTypeCatalogItem` (8 extra), `BankCatalogItem` (alias/swift/routing). Deriva de `CountryScopedCatalogItem` **directo** (no de `GeneralCatalogItem`) y usa **config dedicada** (no la base) que mapea columnas extra + los 3 índices estándar. El seed usa un factory dedicado (`CreateCompensationConceptTypeSeed`). **Requiere endpoint de lectura dedicado** porque el DTO genérico `PersonnelCatalogItemResponse` solo lleva Code/Name/IsActive/SortOrder — pero **también** se cablea a la vía genérica (wire key + 2 switches) para combobox/validación por código.

> **⚠️ Limitación de CRUD de backoffice para catálogos enriquecidos (decisión de plan DP-03):** los DTOs de `SystemCatalogsController`/`EducationCatalogsController` son **planos** (code/name/sortOrder). Un catálogo con columnas extra **no** puede editar esas columnas por el CRUD genérico. Estrategia: las columnas extra se **entregan por seed** (`HasData`) y se mantienen por migración; el CRUD de backoffice administra code/name/activo/orden. Un endpoint de administración dedicado para las columnas extra queda **diferido** (fuera de MVP).

### 3.3. Caso C — jerárquico (hijo → padre por id interno `long`)

Ejemplar: `InsuranceRangeCatalogItem`→`InsuranceTypeCatalogItem`. El hijo guarda `long ParentId` + nav + guard `== 0`; la config override `Configure` (FK `Restrict` + índice `(parentId,active,sort)`) y **`ConfigureUniqueCodeIndex`** → `(country, parentId, code)` **solo si** el code se repite entre padres. Validación de emparejamiento en repo (`ReferenceInsuranceRangeBelongsToTypeAsync`). **Seed: padre antes que hijo, en la misma migración**; la PublicId determinista del hijo incluye el id del padre. Este es el patrón de **Carreras→Tipos de estudio** y **Tipos de estudio→Nivel educativo**.

### 3.4. Backoffice CRUD (D-14) — cómo se administra

Dos subsistemas; **agregar un catálogo NO es genérico** (enum + keymap + factory + dispatch de repo):

- **`SystemCatalogs`** (country-scoped): `SystemCatalogType` (enum) + **dos** copias del keymap (`SystemCatalogsController.TryMapCatalogKey:197` **y** `SystemCatalogKeyMap.TryParse:520`) + `SystemCatalogFactory.Create:439` + `SystemCatalogRepository` dispatch ×5.
- **`EducationCatalogs`** (global): `EducationCatalogType` + `KeyMap:25` (única) + `EducationCatalogFactory.Create:255` + repo dispatch ×7 (incl. `IsInUseAsync` predicado FK).

Los catálogos **thin nuevos** (títulos, direcciones, hobbies, asociaciones, beneficios, nivel educativo) se agregan a estos subsistemas → CRUD completo. Los **enriquecidos** ⇒ ver DP-03.

### 3.5. Asignación de rangos de id de seed (bloques libres)

Bloques `-9xxx` libres: **`-9596…-9699`** (limpio) y **`-9795…-9999`** (cola). Asignación propuesta (10–20 por catálogo, `sortOrder` +10):

| Catálogo | Rango | | Catálogo | Rango |
|---|---|---|---|---|
| Títulos personales | `-9600…-9619` | | Beneficios adicionales | `-9670…-9689` |
| Tipos de direcciones | `-9620…-9629` | | AFP (maestro) | `-9690…-9699` |
| Hobbies | `-9630…-9649` | | Nivel educativo (global) | `-9800…-9809` |
| Asociaciones | `-9650…-9669` | | Carreras (reseed country) | `-9780…-9789` |

Ampliaciones **in-place** (sin id nuevo, `UpdateData`): contratos `-9460…-9467`, rubros `SALARIO_BASE`, formas de pago (+`BOLETA` = 1 id nuevo en su bloque `-93xx`), tipos de estudio `-9765…-9769`, AFP/ISSS params `-9727/-9728`, identification-types (formato).

---

## 4. Decisiones de plan (DP)

- **DP-01 · Familia por catálogo.** Reference: **Títulos personales, Tipos de direcciones** (atributos de persona, se validan como `MaritalStatus`/`Kinship`). General: **Hobbies, Asociaciones, Beneficios adicionales** (listas de apoyo de sub-entidades). Enriquecido dedicado: **AFP** (mirror `BankCatalogItem`). Education: **Nivel educativo, Tipos de estudio, Carreras**. Ampliación in-place: **Contratos, Rubros, Formas de pago, Tipos de documentos**.
- **DP-02 · Sin permisos nuevos.** Consumo dentro de endpoints de expediente existentes (Read/Manage). Lectura de AFP = `[Authorize]` authn-only (mirror `CompensationConceptTypesController`). Administración = backoffice `PlatformOperator`. **No** se tocan `AuthorizationPolicyConventionGovernanceTests`.
- **DP-03 · Enriquecidos = seed + backoffice thin** (ver §3.2): columnas extra por `HasData`/migración; CRUD genérico edita solo code/name/activo/orden; admin dedicado de columnas extra **diferido**.
- **DP-04 · AfpCode a nivel persona (RATIFICADO RT-05).** La afiliación AFP del empleado se guarda como `PersonnelFile.AfpCode` (nullable, como `Profession`), validada contra el catálogo AFP. Afiliación única por persona (cuenta vitalicia).
- **DP-05 · Parámetros país de AFP en `CompensationConceptTypeCatalogItem`.** Se agregan 2 columnas (`DefaultPensionedEmployerRate`, `MinContributionBase`) a la fila `AFP`/`ISSS`; el `ContributionCap` existente = valor máximo. **Valores de ley SV (RT-04, LISP 2022, editables):** empleado **7.25%** · patrono **8.75%** · pensionado **8.75%** (igual) · IBC máx **$7,045.06/mes** (2026) · IBC mín **= salario mínimo vigente**. Una sola fuente de verdad (D-06); sin tablas nuevas.
- **DP-06 · Carreras → country-scoped por DROP & RECREATE (RATIFICADO RT-02).** Se recrea `EducationCareerCatalogItem` como country-scoped (D-07): swap de base, índice compuesto `(country,code)`, columnas de país + FK a tipo-estudio + 3 atributos. **Autorizado eliminar datos existentes** ("no importa que haya datos, se deben eliminar"): la migración dropea/limpia carreras (y los `personnel_file_educations` que las referencien por FK `RESTRICT`) y siembra limpio. **Sin** cleanup `id>0` ni preservación.
- **DP-07 · Texto libre → catálogo por DROP & RECREATE (RATIFICADO RT-06).** Columna de código **requerida** nueva; **sin backfill** — las filas de texto libre existentes se limpian/eliminan (autorizado). `OTRO`/`OTRA` se siembra como valor de catálogo (no como destino de mapeo). Aplica a hobbies, asociaciones, beneficios y parentesco de contacto de emergencia. "Es salario base" → ver DP-08.
- **DP-08 · "Es salario base" como booleano** (D-12): `IsBaseSalary` en `CompensationConceptTypeCatalogItem`; la regla `CompensationConcepts.Rules` deja de depender del string mágico `SALARIO_BASE` (se mantiene como fallback/const).

---

## 5. Desglose por PRs

> Formato por PR: **Objetivo · Patrón · Touch-points · Migración · Seed · Validación · Pruebas**. "Receta §3.x" = seguir esa plantilla.

### FASE 0 — Quick wins (cierra/valida Parentesco, Formas de pago, Rubros)

#### PR-0.1 — Formas de pago: sembrar `BOLETA`
- **Objetivo:** agregar "boleta de pago" (D-13).
- **Touch-points:** `GlobalCatalogSeedData.GetPaymentMethodCatalogItems()` (+1 fila `BOLETA`, id nuevo en bloque `-93xx`).
- **Migración:** `SeedPaymentMethodBoleta` (`InsertData` 1 fila).
- **Pruebas:** integración de lectura `general-catalogs/payment-methods` incluye `BOLETA`.

#### PR-0.2 — Rubros salariales: booleano `IsBaseSalary` (DP-08)
- **Patrón:** ampliar catálogo enriquecido (Caso B).
- **Touch-points:** `CompensationConceptTypeCatalogItem` (+prop `IsBaseSalary` + `Create`/`UpdateDetails`); config dedicada (+columna `is_base_salary`); `GlobalCatalogSeedData` (marcar `SALARIO_BASE`=true); `CompensationConcepts.Rules.cs:15` (leer el flag; const `SALARIO_BASE` como fallback); response enriquecido (`CompensationConceptTypes.cs`) +campo.
- **Migración:** `AddIsBaseSalaryToCompensationConceptType` (AddColumn + `UpdateData` SALARIO_BASE=true).
- **Pruebas:** unit de la regla (base salary detectado por flag); integración read.

#### PR-0.3 — Contacto de emergencia → catálogo Parentesco (RF-004)
- **Patrón:** sub-entidad validada (§3, patrón `KinshipCode`).
- **Touch-points:** `EmergencyContacts.Handlers.cs` (Add `:114→118`, Update, Patch) — insertar `ValidateKinshipCodeAsync(repository, personnelFile.TenantId, "relationship", input.Relationship, ct)`; **reutiliza** el catálogo `Kinship` (no se crea catálogo). Sin cambio de esquema.
- **Migración:** opcional `CleanEmergencyContactRelationship` (RT-06: limpiar valores no conformes; **sin backfill de preservación**; nuevas escrituras validadas contra Kinship). Sin cambio de esquema.
- **Pruebas:** unit handlers (422 si código inválido); integración Add/Update/Patch; test de backfill idempotente.

---

### FASE 1 — Catálogos simples nuevos + ampliaciones (MVP con F0)

#### PR-1.1 — Títulos personales (RF-001)
- **Patrón:** Reference thin (§3.1) + atributo de persona (§2 #12).
- **Touch-points:**
  - Catálogo: `PersonalTitleCatalogItem : PersonnelReferenceCatalogItemBase` + config + DbSet + categoría `PersonnelReferenceCatalogCategories.PersonalTitle` + `ReferenceCatalogKeys["personal-titles"]` + repo `GetReferenceCatalogItemsAsync`/`ReferenceCatalogCodeIsActiveAsync` (case `"PERSONALTITLE"`).
  - Persona: `PersonnelFile.PersonalTitleCode` (prop nullable + `UpdatePersonalInfo` + `NormalizeOptionalCode`); EF `personal_title_code` (80); DTOs (`PersonnelFilePersonalInfoResponse`, `PersonnelFileResponse`, list); validador create/update (`.MaximumLength(80).Must(IsValidCode)`); `PersonnelFilePatchState` + applier.
  - Validación: extender `ValidatePersonalInfoCodesAsync` (`PersonnelReferenceCatalogs.cs:145`) con el nuevo código.
  - Backoffice: `SystemCatalogType.PersonalTitle` (+2 keymaps + `SystemCatalogFactory` + repo dispatch ×5).
- **Seed:** `GetPersonalTitleCatalogItems()` §20.1 (bloque `-9600`, incluye `OTRO`).
- **Migración:** `AddPersonalTitleCatalogAndPersonField` (CreateTable + AddColumn persona + InsertData).
- **Pruebas:** guardrail bijección Reference; unit validación título; integración read `reference-catalogs/personal-titles` + PUT persona (422 inválido).

#### PR-1.2 — Tipos de direcciones (RF-002)
- **Patrón:** Reference thin + campo en dirección (§2 #13, patrón §3 `KinshipCode`).
- **Touch-points:**
  - Catálogo: `AddressTypeCatalogItem` (igual que PR-1.1, categoría `AddressType`, key `address-types`, case `"ADDRESSTYPE"`).
  - Dirección: `PersonnelFileAddress.AddressTypeCode` (ctor/`Create:1065`/`Update:1074`/`UpdateAddress:338`); EF `address_type_code` (80); `AddressInput`/`PersonnelFileAddressResponse`/`AddressInputValidator`; `PersonnelFileAddressPatchState` + branch `IsSegment("addressTypeCode")`.
  - Validación: **guard nuevo** en `Address.Handlers.cs` (Add/Update/Patch) — `ValidateAddressTypeCodeAsync` (nuevo helper mirror `ValidateKinshipCodeAsync`). Tipo **opcional** (D-03).
  - Backoffice: `SystemCatalogType.AddressType` (+dispatch).
- **Seed:** §20.2 (bloque `-9620`).
- **Migración:** `AddAddressTypeCatalogAndAddressField`.
- **Pruebas:** unit guard dirección; integración address CRUD con tipo.

#### PR-1.3 — Hobbies (RF-005)
- **Patrón:** General thin (§3.1) + migración de texto libre (DP-07).
- **Touch-points:**
  - Catálogo: `HobbyCatalogItem : GeneralCatalogItem` (categoría `Hobby`, key `hobbies`, case `"HOBBY"`, 2 switches).
  - Sub-entidad: `PersonnelFileHobby.HobbyCode` (+columna; `HobbyName` se conserva como etiqueta opcional o se reemplaza) — entidad `PersonnelFile.cs:1395` + `Create`/`Update`; EF; `HobbyInput`/response/validator/patch applier.
  - Validación: `Hobbies.Handlers.cs` (Add/Update/Patch) — `CatalogCodeIsActiveAsync(Hobby)`.
  - Backoffice: `SystemCatalogType.Hobby`.
- **Seed:** §20.5 (bloque `-9630`, incl. `OTRO`).
- **Migración:** `AddHobbyCatalogAndCode` (CreateTable + AddColumn `hobby_code` + `Sql` limpieza de `personnel_file_hobbies` — **RT-06 sin backfill**).
- **Pruebas:** unit validación; integración interests/hobbies; backfill.

#### PR-1.4 — Asociaciones (RF-006)
- **Igual que PR-1.3** con `AssociationCatalogItem` (key `associations`, case `"ASSOCIATION"`); sub-entidad `PersonnelFileAssociation.AssociationCode` (`PersonnelFile.cs:1548`; conserva `Role/JoinedDate/LeftDate/Payment`). Seed §20.6 (`-9650`). Migración `AddAssociationCatalogAndCode`.

#### PR-1.5 — Beneficios adicionales: catálogo de tipos (RF-010)
- **Patrón:** General thin; el código **ya existe** (`BenefitTypeCode`), solo se valida.
- **Touch-points:**
  - Catálogo: `AdditionalBenefitTypeCatalogItem` (categoría `AdditionalBenefitType`, key `additional-benefit-types`, case `"ADDITIONALBENEFITTYPE"`, 2 switches).
  - Validación: `AdditionalBenefits.Handlers.cs` (Add `:51`, Update `:123`, Patch `:217`) — insertar `CatalogCodeIsActiveAsync`; validador `:81` pasa de `NotEmpty+MaxLength` a código de catálogo.
  - Backoffice: `SystemCatalogType.AdditionalBenefitType`.
- **Seed:** §20.10 (bloque `-9670`, incl. `OTRO`). **Sin cambio de esquema** en la sub-entidad (la columna existe).
- **Migración:** `AddAdditionalBenefitTypeCatalog` (CreateTable + InsertData + `Sql` limpieza de `benefit_type_code` no conforme — **RT-06 sin backfill**).
- **Pruebas:** unit 422 inválido; integración additional-benefits.

#### PR-1.6 — Tipos de contratos: `Abbreviation` + `IsTemporary` (RF-011)
- **Patrón:** ampliación a enriquecido (Caso B) — `ContractTypeCatalogItem` deja la config base y pasa a config dedicada.
- **Touch-points:** entidad (+`Abbreviation`, +`IsTemporary` + `Create`/`Update`); **config dedicada** `ContractTypeCatalogItemConfiguration` (columnas `abbreviation`/`is_temporary` + 3 índices estándar); seed factory (reseed 8 filas §20.11 con abrev+temporal); response de lectura (agregar campos — endpoint enriquecido opcional o exponer `IsTemporary` donde `ContractHistories` lo consuma). **DP-03** aplica (CRUD backoffice edita code/name; abrev/temporal por seed).
- **Migración:** `EnrichContractTypeCatalog` (AddColumn ×2 + `UpdateData` 8 filas).
- **Pruebas:** unit; integración read contract-types con nuevos campos.

#### PR-1.7 — Tipos de estudios: `Abbreviation` (RF-008 parte 1)
- **Patrón:** ampliación education (columna extra en subclase).
- **Touch-points:** `EducationStudyTypeCatalogItem` (+`Abbreviation` + `Create`); config subclase (override `Configure` columna `abbreviation`); `CreateEducationCatalogSeed`/reseed; backoffice `EducationCatalogFactory`/response — **DP-03** (rompe contrato plano education → abrev por seed, CRUD thin).
- **Migración:** `AddAbbreviationToStudyType` (AddColumn + `UpdateData`).
- *(La FK a Nivel educativo va en F2.)*

---

### FASE 2 — Educación estructurada (RF-014, RF-008 parte 2, RF-009)

#### PR-2.1 — Nivel educativo (RF-014)
- **Patrón:** Education thin global (§3.4 EducationCatalog).
- **Touch-points:** `EducationLevelCatalogItem : EducationCatalogItem` + config (+`HasData`) + DbSet; `EducationCatalogType.EducationLevel` (enum) + `KeyMap` + `EducationCatalogFactory.Create` + repo dispatch ×7. **Nota `IsInUseAsync`:** el predicado FK no es contra `PersonnelFileEducations` sino contra `EducationStudyTypeCatalogItems` (nivel referenciado por tipo-estudio) — ajustar ese arm.
- **Seed:** §20.14 (global, bloque `-9800`).
- **Migración:** `AddEducationLevelCatalog`.

#### PR-2.2 — Tipos de estudios ↔ Nivel educativo (RF-008 parte 2)
- **Patrón:** jerárquico (Caso C, hijo→padre por id `long`).
- **Touch-points:** `EducationStudyTypeCatalogItem.EducationLevelCatalogItemId` (+nav + guard `== 0`); config override (FK `Restrict` + índice); reseed §20.8 con mapeo a nivel; backoffice command/factory +ref (DP-03).
- **Migración:** `AddEducationLevelFkToStudyType` (padre ya sembrado en PR-2.1; AddColumn FK + `UpdateData`).

#### PR-2.3 — Carreras: country-scoped + atributos + FK (RF-009) — **PR más pesado**
- **Patrón:** conversión SystemScoped→country-scoped (DP-06) + enriquecido + jerárquico (Caso C).
- **Touch-points:**
  - Entidad: `EducationCareerCatalogItem` base → country-scoped; +`Abbreviation`/`Increment`(**decimal % 0–100, RT-03**)/`IsRecognized`(bool) + FK `EducationStudyTypeCatalogItemId`; `Create`/`Update` con `countryCatalogItemId+countryCode`.
  - Config: dedicada — columnas país + FK carrera→tipo-estudio + índice único **compuesto** `(CountryCatalogItemId, NormalizedCode)` (reemplaza el de 1 columna) + columnas extra.
  - Seed: `CreateGeneralCatalogSeed`/factory carrera (§20.9, país SV, bloque `-9780`); PublicId cambia clave `code`→`country:code`.
  - Call sites: `EducationCatalogFactory.Create` (carrera ahora requiere país + tipo-estudio) + comandos/validadores backoffice — **decisión:** o se agrega país al contrato Education CRUD, o carrera se administra por seed (DP-03) con CRUD diferido. **Recomendado:** CRUD de carrera **diferido** en esta fase (solo seed), por el costo de tocar el contrato genérico.
  - `PersonnelFileEducation.EducationCareerCatalogItemId` (FK `long`) **no cambia** (scope-agnóstico).
- **Migración:** `RecreateCareerCatalogCountryScoped` — **drop & recreate autorizado (RT-02):** limpiar `personnel_file_educations` (FK `RESTRICT`) + carreras, dropear tabla/índice viejo, recrear con columnas de país + FK tipo-estudio + `Abbreviation`/`Increment`/`IsRecognized` + índice compuesto `(country,code)`, sembrar limpio §20.9. **Sin** preservación de datos ni cleanup `id>0`.
- **Pruebas:** integración education create con carrera+tipo; verificar FK carrera→tipo-estudio; smoke de reseed en PG.

---

### FASE 3 — Documentos con formato

#### PR-3.1 — Validación de número por formato (RF-003)
- **Patrón:** ampliación Reference (columna extra) + validación async nueva + motor regex (net-new).
- **Touch-points:**
  - Catálogo: `IdentificationTypeCatalogItem.NumberFormat` (nullable) + `Create`; config override columna `number_format`.
  - Repo: `IPersonnelFileRepository.GetIdentificationTypeNumberFormatAsync(companyId, code, ct)` (**default no-op** como `GetCatalogItemNameAsync:227`) + impl mirror `GetCountryScopedCatalogNameAsync<IdentificationTypeCatalogItem>` proyectando `NumberFormat`.
  - Validación: `PersonnelReferenceCatalogs.ValidateIdentificationNumberFormatAsync(...)` (regex anclada; `Regex.IsMatch` sobre `NormalizedIdentificationNumber`); insertar en `Identifications.Handlers.cs` en los **3 sitios** justo tras `ValidateIdentificationTypeCodeAsync` (`:117/234/469`) y antes del dup-check.
  - Error: `PersonnelFileErrors.IdentificationNumberFormatInvalid` (`ErrorType.UnprocessableEntity`) + 3 resx (EN/es/es-SV).
- **Seed:** `UpdateData` DUI/NIT/PASSPORT/RESIDENT_CARD con patrones §20.3.
- **Migración:** `AddNumberFormatToIdentificationType` (AddColumn + `UpdateData` patrones).
- **Pruebas:** unit (DUI válido/ inválido → 422; tipo sin patrón → genérico); paridad localización (3 resx); integración identifications.

---

### FASE 4 — AFP maestro (mini-proyecto)

#### PR-4.1 — Catálogo AFP (identidad) (RF-007 parte 1)
- **Patrón:** enriquecido dedicado (Caso B, mirror `BankCatalogItem`).
- **Touch-points:** `AfpCatalogItem : CountryScopedCatalogItem` (+`Abbreviation`/`Address`/`Phone`/`Fax`/`ContactName` nullable + helpers de normalización) + config dedicada + DbSet; categoría `Afp` + wire key `afps` (thin code/name) + 2 switches (combobox/validación); **endpoint de lectura dedicado** `AfpCatalogController` (`GET /api/v1/afps`, `[Authorize]`, DTO completo) + query/handler/repo projection (mirror `CompensationConceptTypes`).
- **Seed:** §20.7(a) Confía/Crecer/Otra (bloque `-9690`; contacto nullable a completar).
- **Migración:** `AddAfpCatalog`.
- **Nota DP-03:** admin de columnas extra diferido (seed + read).

#### PR-4.2 — Afiliación del empleado (RF-007 parte 2, DP-04)
- **Touch-points:** `PersonnelFile.AfpCode` (nullable, patrón `Profession`) + EF `afp_code` (80) + DTOs + validador + patch; validación `CatalogCodeIsActiveAsync(Afp)` en `PersonnelFileCore.Handlers`.
- **Migración:** `AddAfpCodeToPersonnelFile`.

#### PR-4.3 — Parámetros país de AFP/ISSS (RF-007 parte 3, DP-05)
- **Touch-points:** `CompensationConceptTypeCatalogItem` (+`DefaultPensionedEmployerRate` `numeric(11,8)`, +`MinContributionBase` `numeric(18,2)`) + `Create`/`UpdateDetails` + config; response enriquecido +campos; `UpdateData` filas `AFP`/`ISSS` con valores de ley (RT-04): empleado 7.25% · patrono 8.75% · pensionado 8.75% · IBC máx $7,045.06 · IBC mín = salario mínimo (editables).
- **Migración:** `AddPensionParamsToCompensationConceptType` (AddColumn ×2 + `UpdateData`).
- **Nota:** los valores legales quedan como **defaults editables** (D-06); el **cálculo** es del módulo de Nómina (fuera de alcance).

---

### Transversal
- **CRUD de backoffice:** cada catálogo thin (PR-1.1–1.5, 2.1) se registra en su subsistema (§3.4). Enriquecidos: DP-03 (seed + thin). **Sin permisos nuevos** (DP-02).

---

## 6. Migraciones (orden y gotchas)

Orden = orden de PRs. Comando: `DOTNET_ROLL_FORWARD=Major dotnet ef migrations add <Nombre> --project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api`; verificar `... migrations has-pending-model-changes` (sin drift).

| Gotcha | Acción |
|---|---|
| **63 chars** en nombres de índice | Acortar `ix_<tabla>__country_active_sort`→`ix_<tabla>__active_sort` si excede (precedente off-payroll) |
| **DevSeed→HasData / DBs de desarrollo** | Prepend `Sql` de limpieza `WHERE id > 0` (positivo=DevSeed, negativo=HasData) para no colisionar en la uq de código; no-op en fresh/server |
| **Educación por id `long` + FK `RESTRICT`** | En reseed de carreras/estudios, limpiar `personnel_file_educations` de ejemplo primero; guards ya son `== 0` (aceptan ids negativos) |
| **PublicId determinista** | `seedPrefix` único por catálogo; al convertir carreras a país, la clave cambia `code`→`country:code` (nuevos PublicId) |
| **Backfill de texto libre** | `migrationBuilder.Sql("UPDATE ... SET code = ... ELSE 'OTRO'")` **después** del InsertData del catálogo |

---

## 7. Seed inicial

Definido en el documento de negocio **§20** (14 catálogos, valores SV). **Cada PR entrega su seed** vía `GlobalCatalogSeedData` + `HasData`/`UpdateData` en su migración (requisito duro §1.1). `OTRO`/`OTRA` obligatorio en catálogos con migración de texto libre. Valores legales de AFP = defaults editables (a confirmar con Nómina).

---

## 8. Pruebas

- **Unit (obligatorio por PR):** validación de código (422 inválido/inactivo), reglas puras (rubros base-salary por flag), validación de formato de documento (match/mismatch/sin patrón).
- **Guardrails:** `GeneralCatalogKeyMapGuardrailsTests` (bijección General y Reference — falla si categoría sin wire key); `BackendMessageLocalizationTests` (3 resx para el error nuevo).
- **Integración:** lectura de cada catálogo (`general-catalogs`/`reference-catalogs`/dedicado); consumo end-to-end (persona con título/AfpCode; dirección con tipo; hobbies/asociaciones/beneficios con código; identificación con formato; educación con carrera↔tipo). DBs por-GUID en :5432/:5433; aplicar todas las migraciones a DB efímera.
- **Smoke de migración** en PG real para los reseed de educación (PR-2.3) por el cleanup `id>0` y las FK `RESTRICT`.
- **No aplica** `AuthorizationPolicyConventionGovernanceTests` (sin permisos nuevos, DP-02) ni `AllowedActionsCoverageIntegrationTests` para los reads AFP (GET-only).

---

## 9. Riesgos — RESUELTOS (2026-06-30)

Los 6 riesgos de plan quedaron resueltos **antes** de construir:

| # | Riesgo | Resolución ratificada |
|---|---|---|
| **RT-01** | Admin de columnas enriquecidas | **Solo por seed en Fase 1**; el CRUD de backoffice edita code/name/activo/orden; admin dedicado de columnas extra **diferido** (confirma DP-03). |
| **RT-02** | Conversión de carreras / datos productivos | **DROP & RECREATE autorizado**: "no importa que haya datos, se deben eliminar; dropear y crear la nueva estructura". PR-2.3 deja de ser riesgo alto: se recrea limpio, **sin** migración de preservación ni cleanup `id>0`. |
| **RT-03** | Semántica de "Incremento" | **% de incremento salarial por grado**: `decimal` (0–100), lo consumirá Nómina. Se siembra 0. |
| **RT-04** | Parámetros legales AFP | **Valores de ley (LISP 2022) sembrados como defaults editables:** empleado 7.25% · patrono 8.75% · pensionado 8.75% (igual) · IBC máx $7,045.06/mes (2026) · IBC mín = salario mínimo vigente. |
| **RT-05** | Ubicación de `AfpCode` | **A nivel persona/expediente** (confirma DP-04). |
| **RT-06** | Migración de texto libre | **DROP & RECREATE**: sin backfill; columna de código requerida, filas de texto libre existentes se limpian/eliminan. |

**Sin riesgos abiertos que bloqueen el MVP. Sin dependencias externas nuevas.**

---

> **Listo para implementar.** Secuencia recomendada: **MVP = PR-0.1 → PR-1.7** (10 PRs, cierra 9/14 catálogos), luego F2 (educación), F3 (formato), F4 (AFP). Cada PR: build verde + unit + guardrails + integración del área + migración sin drift, antes de continuar. Guía de integración frontend a generar tras el MVP.
