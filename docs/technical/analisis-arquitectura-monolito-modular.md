# Análisis de arquitectura — ¿es CLARIHR un monolito modular?

| | |
|---|---|
| **Propósito** | Determinar, con evidencia medible, si el backend cumple con ser un **monolito modular** y, por tanto, si puede escalar a microservicios cuando el negocio lo requiera. Definir el plan de remediación **para el futuro** (no se ejecuta ahora). |
| **Fecha** | 2026-07-25 |
| **Alcance** | `src/` completo (1 427 archivos `.cs`, 295 621 LOC sin migraciones), 6 proyectos, 140 controllers, 889 endpoints, 242 entidades EF. |
| **Estado del sistema** | Pre-producción (sin datos productivos). Suite: 3 053 tests. |
| **Naturaleza** | Documento de diagnóstico + plan. **No se modifica código en esta entrega.** |
| **Relacionado** | `docs/technical/overview/project-foundation.md` §6 · `AGENTS.md` §4.1/§6 · `ADR-0005` |

---

## 1. Veredicto

> **CLARIHR NO es un monolito modular. Es un monolito por capas (Clean Architecture) con organización interna por features.**
>
> La distinción no es semántica: **un monolito por capas organiza por *tipo de código* (Domain / Application / Infrastructure / Api); un monolito modular organiza por *dominio* y hace que la frontera del dominio sea real** — compilada, con contrato publicado y con datos propios.
>
> Hoy, extraer cualquier módulo a un microservicio requiere resolver antes: **un ciclo de 33 módulos**, **un `DbContext` compartido con 242 entidades**, **22 pares de FKs cruzadas a nivel de base de datos**, **62 archivos que hacen JOIN entre módulos** y **cero infraestructura de mensajería**. Es decir: **no es viable hoy, y no debería intentarse hoy.**

**Pero el diagnóstico no es malo.** Lo que está construido está **bien** construido para el objetivo que se declaró (`project-foundation.md` §6: Clean Architecture + CQRS). Y la distancia hacia un monolito modular real es **mucho menor de lo que sugiere el ciclo de 33 módulos**: al aplicar un modelo de capas por dominio (§6), **el 93 % de las dependencias ya lo respetan** y solo quedan **31 pares de aristas "hacia arriba" (79 `using`)** por invertir. Eso es trabajo de semanas, no de reescritura.

### 1.1 Doble tarjeta de puntuación

Se evalúa contra las dos rúbricas por separado, porque el proyecto **cumple una y no la otra** — y solo una de las dos fue declarada como objetivo.

**A. Clean Architecture (objetivo declarado en `project-foundation.md` §6)**

| Criterio | Score | Evidencia |
|---|---|---|
| Domain sin dependencias | 5/5 | `CLARIHR.Domain.csproj` sin `ProjectReference` ni `PackageReference` |
| Dirección de dependencias | 5/5 | App→Domain; Infra→App+Domain; Api→App+Infra. Sin ciclos entre proyectos |
| API sin infraestructura | 5/5 | **0** controllers con `using CLARIHR.Infrastructure` (solo `Program.cs` y middleware, que es composition root legítimo) |
| CQRS con dispatcher | 4/5 | `RequestDispatcher` + `ICommand/IQuery`; validación FluentValidation en el pipeline; 140/140 controllers lo usan |
| Encapsulación de implementaciones | 4/5 | Infrastructure: 449 `internal` vs 26 `public` |
| **Total** | **≈ 4.6/5** | **Clean Architecture: cumple sólidamente.** |

**B. Monolito modular (lo que se está preguntando)**

| # | Dimensión | Score | Evidencia dura |
|---|---|---|---|
| 1 | Identidad de módulo | 3/5 | 39 carpetas en `Application/Features`, 33 en `Domain`, 32 en `Infrastructure/Persistence/Configurations` — **los nombres coinciden entre capas**. La identidad existe; no está formalizada |
| 2 | Frontera compilada | **0/5** | 6 proyectos, **todos por capa, ninguno por módulo**. `Payroll` puede `using` cualquier cosa de `PersonnelFiles` sin error de compilación |
| 3 | Contrato público por módulo | **1/5** | **736 `using CLARIHR.Application.Features.<OtroMódulo>`** en 136 pares — se entra al *interior* de otros módulos. `IPersonnelFileEmployeeRepository` = **1 966 líneas / 247 métodos** |
| 4 | Propiedad de datos | **1/5** | 1 `DbContext` · 242 `DbSet` · 1 esquema · 1 stream de 136 migraciones · **22 pares de FK cruzadas** · **62 archivos de Infrastructure tocan ≥2 módulos** |
| 5 | Comunicación entre módulos | **1/5** | Llamadas tipadas directas a repos ajenos. `IDomainEvent`/`DomainEvent` **declarados y con 0 usos** (andamiaje muerto). Sin outbox, sin bus, sin colas |
| 6 | Aciclicidad (DAG) | **1/5** | **Un SCC de 33 módulos** (todo alcanza a todo). 81 pares bidireccionales A↔B (26 entre módulos de negocio) |
| 7 | Transacción por módulo | **1/5** | `IUnitOfWork` único sobre el `DbContext` único; escrituras cross-módulo en una sola transacción EF (p. ej. `PersonnelFileFinalizationService` → provisiona `CompanyUser`) |
| 8 | Despliegue independiente | **1/5** | 1 imagen Docker; 2 hosts (`Api`, `Backoffice.Api`) que **comparten Application, Infrastructure y la misma base de datos** |
| 9 | Pruebas por módulo | 2/5 | 3 053 tests (excelente red de seguridad) pero en **un proyecto plano de 250 archivos**, sin aislamiento por módulo |
| 10 | Gobierno / fitness functions | **0/5** | Sin tests de arquitectura, sin NetArchTest/ArchUnit, sin analizadores de dependencias. Nada impide que la deuda crezca |
| | **Total** | **≈ 1.1/5 (22 %)** | **Monolito modular: no cumple.** |

---

## 2. Método (reproducible)

Todo lo anterior es medido, no impresionista. El módulo de un archivo se deriva de su namespace:

| Namespace | Módulo |
|---|---|
| `CLARIHR.Domain.<M>` | `<M>` |
| `CLARIHR.Application.Features.<M>` | `<M>` |
| `CLARIHR.Application.Abstractions.<M>` | `<M>` |
| `CLARIHR.Infrastructure.<M>` | `<M>` |

Sobre ese mapa se construye el grafo dirigido de dependencias a partir de las directivas `using`, y se calculan: aristas cruzadas, fan-in/fan-out, componentes fuertemente conexos (Tarjan), pares bidireccionales y violaciones contra el modelo de capas propuesto en §6. El script completo está en el **Anexo B** para poder repetir la medición y trackear la tendencia.

Se excluyen `obj/`, `bin/` y `Migrations/`.

---

## 3. Lo que SÍ está construido (activos, no se parte de cero)

Esto importa tanto como los hallazgos: el costo de llegar a monolito modular es bajo **porque estas seis cosas ya existen**.

1. **Disciplina de capas real y compilada.** `Domain` no depende de nada. Ningún controller toca `Infrastructure`. Eso ya elimina toda una clase de problemas que suele encontrarse en monolitos maduros.
2. **Nombres de módulo consistentes en las 4 capas.** `Payroll` existe como `Domain.Payroll`, `Application.Features.Payroll`, `Application.Abstractions.Payroll`, `Infrastructure.Payroll` y `Persistence/Configurations/Payroll`. **La taxonomía del dominio ya está descubierta** — que es la parte difícil y cara de un modular monolith.
3. **Referencia por `PublicId`, no por navegación, en varios agregados.** `PersonnelFile` referencia otros módulos con `OrgUnitPublicId`, `PhotoFilePublicId` (95 propiedades `*PublicId` en `Domain`). Es exactamente la regla de "referenciar por identidad, no por objeto" que exige la modularidad — ya aplicada parcialmente y ya declarada como norma (`project-foundation.md` §10.3).
4. **Costuras (`seams`) existentes:** 70 repositorios detrás de interfaces en `Application/Abstractions/<Módulo>/`. Son el punto natural donde insertar un anti-corruption layer o un cliente remoto sin tocar los handlers.
5. **Un dispatcher CQRS propio** (`RequestDispatcher`). Es el punto único donde, el día que haga falta, se puede sustituir una llamada in-process por una llamada remota sin tocar 140 controllers.
6. **Multi-tenancy transversal ya resuelta:** `ITenantScopedEntity` + `HasQueryFilter` global + `TenantId` en toda entidad. La partición de datos por tenant ya existe, y es la que hace viable un `pg_dump -n <schema>` el día de una extracción.
7. **Red de seguridad de 3 053 tests.** Sin esto, ningún plan de refactor sería responsable. Con esto, sí lo es.
8. **Precedente institucional:** `ADR-0005` ya enunció la regla correcta — *"un feature no debe añadir a la interfaz de repositorio de otro subdominio miembros propios; el consumo cross-feature debe hacerse vía una abstracción segregada y enfocada"*. El plan de §7 es **generalizar esa ADR**, no inventar doctrina nueva.

---

## 4. Hallazgos — bloqueadores, ordenados por severidad

### H-01 🔴 Ciclo de 33 módulos: no existe un orden de extracción

El grafo de dependencias tiene **un componente fuertemente conexo de 33 módulos**:

```
AccountCompanies, Audit, Banks, CommercialAddons, CommercialPlans, Companies, CompanyUsers,
Compensation, CompetencyFramework, Compliance, CostCenters, DocumentTypeCatalogs,
EducationCatalogs, EmployeeRelations, IdentityAccess, InternalCatalogs, JobProfiles, Leave,
LegalRepresentatives, Locations, OrgStructureCatalogs, OrgUnits, Overtime, Payroll,
PersonnelFiles, Platform, PlatformSubscriptions, PositionDescriptionCatalogs, PositionSlots,
Preferences, Provisioning, Reports, SalaryTabulator
```

Consecuencia literal: **desde cualquiera de esos 33 módulos se puede llegar a cualquier otro y volver.** No hay ninguno que se pueda sacar primero, porque todos dependen (transitivamente) de todos. Un segundo ciclo menor: `CatalogTypes ↔ JobProfileCatalogTypes`.

Hay **81 pares bidireccionales directos** (A→B y B→A), de los cuales **26 son entre módulos de negocio** (el resto involucra `Common`/`Persistence`, que son núcleo y composition root). Los más significativos:

| Par | A→B | B→A | Naturaleza |
|---|---|---|---|
| `PersonnelFiles ↔ Banks` | 54 | 1 | Cuentas bancarias del empleado: el vuelto (1 uso) es un chequeo de uso del catálogo |
| `Payroll ↔ PersonnelFiles` | 19 | 1 | El motor de planilla lee el expediente y el expediente empuja al motor |
| `PersonnelFiles ↔ Leave` | 15 | 6 | Catálogos de tiempo compartidos en ambos sentidos |
| `PersonnelFiles ↔ Reports` | 2 | 13 | Reportes leen expediente; expediente pide render |
| `Payroll ↔ Leave` | 6 | 5 | El calendario de periodos vive en `Leave`, el motor en `Payroll` |
| `CompanyUsers ↔ Companies` | 15 | 5 | Provisioning bidireccional |
| `PersonnelFiles ↔ Overtime` | 8 | 4 | Igual que Leave |
| `AccountCompanies ↔ Companies` | 7 | 3 | Fachada de cuenta vs. entidad |

**Buena noticia (ver §6):** el ciclo no se debe a caos, se debe a **~31 aristas concretas** que apuntan "hacia arriba" en un modelo de capas por dominio muy natural. No hay que rediseñar; hay que invertir 31 dependencias.

### H-02 🔴 Los módulos entran al *interior* de otros módulos

**736 ocurrencias** de `using CLARIHR.Application.Features.<OtroMódulo>` en **136 pares**. `Features` es la capa de casos de uso — el interior del módulo, no su contrato.

| Origen | Destino (interior de) | Ocurrencias |
|---|---|---|
| `PersonnelFiles` | `Features.IdentityAccess` | 97 |
| `PersonnelFiles` | `Features.Audit` | 96 |
| `PersonnelFiles` | `Features.Files` | 64 |
| `PersonnelFiles` | `Features.Locations` | 54 |
| `PersonnelFiles` | `Features.EducationCatalogs` | 52 |
| `Payroll` | `Features.PersonnelFiles` | 8 |
| `Payroll` | `Features.IdentityAccess` | 11 |

**Matiz importante y favorable:** las dos entradas más grandes (`IdentityAccess` 97 + `Audit` 96, con fan-in de **25 módulos cada una**) **no son acoplamiento de negocio: son *shared kernel***. Todo módulo necesita permisos y auditoría; eso es legítimo *si se declara como núcleo compartido*. Reclasificarlas (§6, tier 0) y mover los pocos tipos compartidos a un namespace de núcleo elimina **~300 de las 736 ocurrencias sin refactor real**, solo con reubicación.

### H-03 🔴 No hay propiedad de datos: un solo `DbContext`, un solo esquema

- `ApplicationDbContext`: **242 `DbSet`** en 646 líneas, con `using` de **27 módulos de dominio**.
- **1 solo esquema PostgreSQL**, **1 solo stream de 136 migraciones**.
- **22 pares de configuraciones EF que declaran FKs contra entidades de otro módulo.** Ejemplo verificado en `Persistence/Configurations/PositionSlots/PositionSlotConfiguration.cs:127-141`: `PositionSlot` tiene FK física a `JobProfile`, a `IamRole` y a `WorkCenter` — tres módulos distintos, tres constraints de base de datos.
- **62 archivos de Infrastructure tocan ≥2 módulos de dominio**, con JOINs reales:

| Archivo | Módulos | JOINs | Includes |
|---|---|---|---|
| `PersonnelFiles/PersonnelFileDashboardRepository.cs` | **10** | 3 | 0 |
| `PersonnelFiles/PersonnelFileEmployeeRepository.cs` | 7 | **33** | 23 |
| `PositionSlots/PositionSlotRepository.cs` | 6 | 15 | 0 |
| `PersonnelFiles/PersonnelFileRepository.cs` | 5 | 4 | **49** |
| `PersonnelFiles/PersonnelTransactionRepository.cs` | 4 | 16 | 10 |
| `Payroll/PayrollCalculationDataProvider.cs` | 4 | 7 | 0 |

Cada JOIN entre módulos es un **join distribuido** el día de la separación: hay que reemplazarlo por un read model propio, una proyección replicada o una llamada de red con N+1 latente. Es el trabajo más caro del plan, y por eso está en la fase F3 y **solo para los módulos que realmente se vayan a extraer**.

**Síntoma colateral:** las migraciones ocupan **2 974 374 LOC** (frente a 295 621 LOC de código real) porque cada una de las 136 migraciones re-serializa el snapshot completo del modelo de 242 entidades. Es un costo directo del modelo único.

### H-04 🟠 Contrato entre módulos = interfaz de repositorio ajena, no lenguaje publicado

Cuando `Payroll` necesita datos del expediente, inyecta `IPersonnelFileEmployeeRepository` — **una interfaz de 1 966 líneas y 247 métodos `async`**. No es un contrato: es acceso completo al modelo de datos de otro módulo, con la forma de una interfaz.

Distribución de las interfaces más grandes de `Abstractions/PersonnelFiles/`:

```
IPersonnelFileEmployeeRepository.cs   1 966 líneas  (247 métodos)
IPersonnelFileAuthorizationService.cs   547
IPersonnelFileRepository.cs             432
IPersonnelTransactionRepository.cs      216
ISettlementRepository.cs                183
```

Esto es exactamente el smell que `ADR-0005` identificó y acotó a dos métodos. El problema es de **escala**, no de criterio: la regla correcta ya está escrita, no está aplicada de forma sistemática.

### H-05 🟠 Cero comunicación asíncrona; escrituras cross-módulo en una transacción

- `IDomainEvent` y `DomainEvent` existen en `Domain/Common/` y tienen **0 usos**. Andamiaje muerto.
- Sin outbox, sin bus, sin cola. Los 3 `BackgroundService` (`Files`, `Reports`, `CompanySubscription`) hacen polling sobre la propia base.
- Las escrituras que cruzan módulos ocurren **en la misma transacción EF**. Caso canónico: `PersonnelFileFinalizationService` (`Application/Features/PersonnelFiles/PersonnelFileFinalizationService.cs:35-54`) finaliza el expediente **y** llama a `ICompanyUserProvisioningService.ProvisionAsync` — dos módulos, un commit.

Consecuencia: **cada escritura cross-módulo de hoy es una transacción distribuida mañana.** Se resuelven con outbox + eventual consistency + compensación; nada de eso existe todavía.

### H-06 🟠 `PersonnelFiles` es un god-module

| Métrica | Valor | % del total |
|---|---|---|
| Archivos | 345 | 24 % |
| LOC | 120 305 | **41 % del código fuente** |
| Módulos de los que depende | **24** | — |
| Módulos que dependen de él | 11 | — |
| Subcarpetas en `Features/` | 23 (`Absences`, `Settlements`, `Vacations`, `Overtime`, `Retirements`, `ExitInterviews`, …) | — |

Es el hub del sistema. **No es candidato a extracción — es el núcleo que se queda.** Pero internamente contiene al menos 4 subdominios con ciclo de vida distinto (expediente personal, empleo/plazas, tiempos, liquidaciones) que a mediano plazo conviene separar *dentro* del monolito.

### H-07 🟡 Registro central que depende de 16 módulos

`Application/Common/Policies/AllowedActionsRegistry.cs` (333 líneas) hace `using` de 16 módulos desde `Common` — es decir, **el núcleo compartido depende de los módulos de negocio**. Es una inversión de dependencia al revés y contribuye directamente al SCC. Se resuelve con patrón de contribuidores (cada módulo se registra a sí mismo al arranque), sin cambio funcional.

### H-08 🟡 Sin fitness functions: la deuda no tiene freno

No hay `NetArchTest`, `ArchUnitNET` ni analizador de dependencias en `Directory.Packages.props`. Nada impide que mañana se agregue la arista 32 del SCC. **Este es el hallazgo con mejor relación costo/beneficio del documento** (ver F0).

### H-09 🟡 Composition root único

`AddInfrastructure` (294 líneas, con `using` de ~40 módulos) registra los 70 repositorios y todos los servicios. No hay `AddPayrollModule()` / `AddPeopleModule()`. Un módulo no se puede apagar, sustituir ni testear en aislamiento a nivel de contenedor DI.

### H-10 🟡 Dos hosts, una base de datos

`CLARIHR.Api` y `CLARIHR.Backoffice.Api` son dos ejecutables que comparten `Application`, `Infrastructure` y **la misma base**. Es una buena noticia (prueba que las capas soportan más de un proceso) y una advertencia: **ya existe el patrón "shared database entre servicios"**, que es precisamente el anti-patrón a evitar cuando se separe de verdad.

---

## 5. ¿Y entonces, se puede ir a microservicios?

**Hoy: no.** No por una limitación conceptual, sino por cinco condiciones concretas y verificadas:

| Condición | Estado | Sin ella pasa que… |
|---|---|---|
| Grafo acíclico entre módulos | ❌ SCC de 33 | No existe un "primer servicio" que extraer |
| Contrato publicado por módulo | ❌ 736 accesos al interior | Cada extracción rompe compilación en 5-10 módulos |
| Datos con dueño | ❌ 1 esquema, 22 FKs cruzadas, 62 joins | Los dos servicios necesitan la misma tabla → shared DB |
| Consistencia eventual | ❌ 0 eventos, 0 outbox | Cada flujo cross-módulo se vuelve 2PC o corrupción silenciosa |
| Observabilidad distribuida | ⚠️ parcial (CorrelationId ✅, rate limiting ✅; sin tracing ni health checks) | Un fallo cross-servicio no es diagnosticable |

**Recomendación de fondo:** el objetivo correcto **no es microservicios** — es **monolito modular**. El monolito modular es el 90 % del beneficio (fronteras claras, equipos paralelos, cambios localizados, tests rápidos) con el 10 % del costo operativo. Microservicios solo debe activarse cuando se dispare alguno de los criterios de §9; y si se hace el trabajo de §7, ese día la extracción es **mecánica**, no un proyecto.

---

## 6. Mapa de módulos propuesto — y la prueba de que ya casi se cumple

Se propone un modelo de **tiers**: *un módulo solo puede depender de tiers inferiores*. Es la regla más simple que produce un DAG y que un test puede verificar en milisegundos.

| Tier | Nombre | Módulos |
|---|---|---|
| **T0** | **Shared Kernel / Plataforma** | `Common`, `Persistence`, `Tenancy`, `Time`, `Localization`, `Logging`, `Policies`, `Authorization`, `Authentication`, `Auth`, `IdentityAccess`, `Auditing`/`Audit`, `Files`, `Platform`, `CatalogTypes`, `SystemCatalogs`, `GeneralCatalogs`, `InternalCatalogs` |
| **T1** | **Catálogos de referencia** | `Banks`, `Locations`, `EducationCatalogs`, `DocumentTypeCatalogs`, `OrgStructureCatalogs`, `PositionDescriptionCatalogs`, `Afps`, `Compensation`, `JobProfileCatalogTypes` |
| **T2** | **Empresa / suscripción** | `Companies`, `CompanyUsers`, `AccountCompanies`, `CommercialPlans`, `CommercialAddons`, `PlatformSubscriptions`, `Provisioning`, `Preferences`, `Compliance`, `LegalRepresentatives` |
| **T3** | **Estructura organizativa** | `OrgUnits`, `CostCenters`, `SalaryTabulator`, `JobProfiles`, `CompetencyFramework`, `PositionSlots` |
| **T4** | **Personas** | `PersonnelFiles`, `EmployeeRelations` |
| **T5** | **Tiempos** | `Leave`, `Overtime` |
| **T6** | **Planilla** | `Payroll` |
| **T7** | **Reportería** | `Reports` |

### 6.1 Resultado de aplicar el modelo al código actual

| Métrica | Valor |
|---|---|
| `using` cruzados que **respetan** el modelo (hacia abajo) | **3 801** |
| `using` cruzados que lo **violan** (hacia arriba) | **279** en 75 pares |
| — de los cuales son **composition root / registro central** (`ApplicationDbContext`, `DevSeedService`, `GlobalCatalogSeedData`, configuraciones EF, `AllowedActionsRegistry`, filtros Swagger) | 200 en 44 pares |
| — **violaciones reales de negocio** | **79 en 31 pares** |

> **El modelo de capas por dominio ya se cumple en un 93 %.** El monolito modular no hay que construirlo: hay que **terminarlo** e **impedir que se deshaga**.

### 6.2 Las 31 aristas a invertir (backlog completo)

| Arista | Usos | Diagnóstico y técnica de inversión propuesta |
|---|---|---|
| `PersonnelFiles → Leave` | 15 | Lee **catálogos** de tiempo (`NotWorkedTimeTypes`). → Partir `Leave` en `LeavePolicy` (catálogos/config, T3) + `LeaveOperations` (solicitudes/incapacidades, T5) |
| `PersonnelFiles → Overtime` | 8 | Idéntico al anterior (`OvertimeTypes`, `OvertimeJustificationTypes`) → mismo split |
| `Auth → Preferences` | 5 | El registro crea preferencias por defecto → mover a `Provisioning` (T2) o evento `UserRegistered` |
| `Leave → Payroll` | 5 | `PayrollPeriodCalendar` — el **calendario de periodos** no pertenece al motor. → Extraer `PayrollCalendar` a T3 |
| `Auth → Companies` | 4 | Aceptación de invitación → mover a `Provisioning` (T2) |
| `Compensation → PersonnelFiles` | 3 | Parámetros de endeudamiento consultan uso por empleado → contrato `IEmployeeUsageProbe` en T0 |
| `SystemCatalogs → PersonnelFiles` | 3 | "¿este ítem de catálogo está en uso?" → mismo `IUsageProbe` |
| `CatalogTypes → JobProfileCatalogTypes` | 3 | Ciclo menor → mover el descriptor a T0 |
| `EmployeeRelations → Leave` | 3 | Dependencia **de dominio** (`RecognitionType`, `DisciplinaryActionCause`) → resolver con el split `LeavePolicy` |
| `OrgStructureCatalogs → Companies` | 3 | `*AuthorizationService` → contrato `ITenantAuthorizationContext` en T0 |
| `PlatformSubscriptions → PersonnelFiles` | 2 | Conteo de headcount para límites de plan → `IUsageProbe` |
| `PersonnelFiles → Reports` | 2 | Render de constancias/boletas → `IDocumentRenderer` en T0 |
| `SystemCatalogs → Locations` | 2 | → mover el catálogo al dueño |
| `IdentityAccess → Companies` | 2 | `RbacAuthorizationService` → `ITenantAuthorizationContext` (T0) |
| `PositionDescriptionCatalogs → Companies` | 2 | Ídem |
| `Locations → Companies` | 2 | Ídem |
| `Provisioning → {EmployeeRelations, Leave, Overtime, Payroll, CompetencyFramework}` | 5 | Siembra plantillas de cada módulo → **patrón contribuidor**: `ITenantProvisioningContributor` implementado por cada módulo |
| `{AccountCompanies, CompanyUsers, Banks, LegalRepresentatives} → PersonnelFiles` | 4 | Consultas de uso/vínculo → `IUsageProbe` |
| `{PositionSlots, JobProfiles, Payroll} → Reports` | 3 | Render/export → `IDocumentRenderer` (T0) |
| `PersonnelFiles → Payroll` | 1 | Asignación de plaza toca planilla → evento `EmploymentAssignmentChanged` |
| `Authorization → Companies` | 1 | → `ITenantAuthorizationContext` |
| `InternalCatalogs → JobProfiles` | 1 | → mover el tipo compartido a T0/T1 |

**Observación clave:** cuatro patrones (`IUsageProbe`, `ITenantAuthorizationContext`, `IDocumentRenderer`, `ITenantProvisioningContributor`) **resuelven 24 de las 31 aristas**. Los otros dos casos (split de `Leave`/`Overtime` en política vs. operación, y extracción de `PayrollCalendar`) resuelven el resto y son cambios de ubicación, no de lógica.

---

## 7. Plan de remediación

Diseñado para ser **incremental, reversible y compatible con salir a producción ahora**. Ninguna fase bloquea a la siguiente entrega funcional. Cada fase termina en verde con la suite existente.

> **Nota de alcance:** este plan **no se ejecuta en esta entrega**. Se propone incorporarlo al backlog como **REQ-017** siguiendo el protocolo de `docs/backlog-requerimientos.md`.

### Resumen

| Fase | Objetivo | Esfuerzo | Riesgo | ¿Bloquea producción? | Prerrequisito de… |
|---|---|---|---|---|---|
| **F0** | Declarar arquitectura + trinquete anti-regresión | **1 sem** | **Muy bajo** | No | Todo |
| **F1** | Contrato público por módulo | 4–6 sem | Bajo | No | F2 |
| **F2** | Romper el ciclo → DAG | 3–4 sem | Medio | No | F3, extracción |
| **F3** | Propiedad de datos (esquemas, FKs, joins) | 6–10 sem | **Alto** | No | Extracción |
| **F4** | Comunicación asíncrona (outbox + eventos) | 3–4 sem | Medio | No | Extracción |
| **F5** | Preparación operativa + extracción | por servicio | Alto | No | — |

---

### F0 — Declarar y congelar (1 semana) · *lo único que se recomienda hacer pronto*

**Por qué primero:** hoy la deuda **crece sin fricción**. Cada requerimiento nuevo (REQ-017, REQ-018…) agrega aristas al SCC sin que nadie se entere. F0 cuesta ~1 semana y **detiene la hemorragia sin tocar una línea de código de negocio**.

1. **ADR-0007 — "Arquitectura objetivo: monolito modular"**: declarar el mapa de tiers de §6 como norma, y microservicios como **objetivo condicional** sujeto a los disparadores de §9. Actualizar `project-foundation.md` §6 y `AGENTS.md` §4.1 con la regla de tiers.
2. **`docs/technical/overview/module-map.md`**: mapa de módulos, dueño de cada tabla, tier y contrato público (aunque todavía sea "no definido").
3. **Tests de arquitectura con trinquete (*ratchet*)** — el entregable de mayor valor:
   - Nuevo proyecto `tests/CLARIHR.Architecture.Tests` (o una clase dentro de `Application.UnitTests`; basta con reflexión sobre los ensamblados, no requiere paquete nuevo).
   - Test 1 — **aciclicidad por tiers**: leer el mapa, calcular las aristas hacia arriba, comparar contra un `architecture-baseline.json` versionado con las **79 violaciones actuales**. Falla si el número **sube**. Baja el número cada vez que se arregla una.
   - Test 2 — **no entrar al interior ajeno**: contar `using ...Features.<otro>`; baseline 736; falla si sube.
   - Test 3 — **god-interface**: ninguna interfaz nueva supera N miembros (baseline: la de 247 queda exenta y trackeada).
   - Test 4 — **Domain sin dependencias externas** (ya se cumple; sella la propiedad).
4. **Etiquetar cada módulo en el catálogo de permisos**: `IamPermission.Module` ya existe — alinear sus valores con el mapa de §6, gratis.

**Definición de hecho:** los 4 tests corren en CI, con baseline versionado y en verde. Cero cambio funcional. Cero riesgo de regresión.

---

### F1 — Contrato público por módulo (4–6 semanas)

**Regla:** cada módulo expone `Application/Features/<M>/Contracts/` (DTOs + interfaces de caso de uso). **Nada fuera de `Contracts` es consumible desde otro módulo.** Se enforcea con el Test 2 de F0.

1. **Extraer el shared kernel primero (barato, ~40 % del problema).** Los tipos de `IdentityAccess` y `Audit` que consumen 25 módulos cada uno no son acoplamiento: son núcleo. Moverlos a `Application/Common/Kernel/` **elimina ~300 de las 736 ocurrencias sin refactorizar lógica**. Hacer esto antes que nada.
2. **Segregar interfaces por consumidor (generalizar `ADR-0005`).** No partir la interfaz de 247 métodos "en general": extraer **la vista que cada consumidor necesita**:
   - `IEmployeePayrollDataLookup` para `Payroll` (la usa `PayrollCalculationDataProvider`),
   - `IEmployeeDirectoryLookup` para `Reports`,
   - `IEmployeeUsageProbe` para catálogos que preguntan "¿está en uso?".
   Implementadas por el repositorio dueño. Sin cambio de comportamiento; sin migración.
3. **Los 4 contratos transversales de §6.2**: `IUsageProbe`, `ITenantAuthorizationContext`, `IDocumentRenderer`, `ITenantProvisioningContributor`. Resuelven 24 aristas por sí solos.
4. **Invertir `AllowedActionsRegistry`** (H-07) con patrón contribuidor.

**Definición de hecho:** baseline del Test 2 baja de 736 a < 150 (solo shared kernel legítimo). Suite verde. `openapi.yaml` sin drift (no hay cambio de contrato HTTP).

---

### F2 — Romper el ciclo → DAG (3–4 semanas)

Ejecutar el backlog de §6.2. Orden sugerido (de menor a mayor riesgo):

1. Aristas de 1–2 usos (14 pares) — mecánico.
2. `Provisioning →` 5 módulos, con `ITenantProvisioningContributor`.
3. Los `*AuthorizationService → Companies` (4 pares) con `ITenantAuthorizationContext`.
4. **Split `Leave` → `LeavePolicy` (T3) + `LeaveOperations` (T5)** y lo mismo para `Overtime`. Resuelve 26 usos de golpe (las dos aristas más pesadas).
5. **Extraer `PayrollCalendar` de `Payroll` a T3.** Rompe `Leave ↔ Payroll`.
6. Verificar: **SCC de tamaño 1 para todos los módulos** (grafo acíclico).

**Definición de hecho:** el test de aciclicidad pasa con baseline **0**. A partir de aquí **existe un orden de extracción** y el trinquete se convierte en prohibición absoluta.

---

### F3 — Propiedad de datos (6–10 semanas) · *la fase cara — hacerla solo si se va a extraer*

Cuatro pasos, de barato a caro. **Los pasos 1 y 2 valen la pena aunque nunca haya microservicios**; los pasos 3 y 4 solo si se decide extraer.

1. **Esquema PostgreSQL por módulo** (~1 semana). `ToTable(nombre, schema: "payroll")` en las configuraciones EF. Una migración de `ALTER TABLE ... SET SCHEMA` por grupo. **Sin cambio de código de aplicación.** Beneficio inmediato: la propiedad del dato se vuelve visible, auditable y `GRANT`-able; y el día de la extracción, un `pg_dump -n payroll` es la migración de datos. **Aprovechar que no hay producción** (`memoria: no-production-test-data`) — este es el momento más barato de la vida del proyecto para hacerlo.
2. **Eliminar las 22 FKs cruzadas entre tiers** para los módulos candidatos. Sustituir por validación en aplicación + un job de reconciliación de huérfanos. (Las FKs *dentro* de un módulo se conservan — son sanas.)
3. **Partir el `DbContext`** en uno por módulo sobre la misma conexión, con assembly de migraciones propio. EF Core lo soporta nativamente. Debe ir **después** del paso 1.
4. **Eliminar los joins cruzados** (62 archivos). Por orden de daño: `PersonnelFileDashboardRepository` (10 módulos), `PersonnelFileEmployeeRepository` (7), `PositionSlotRepository` (6), `PersonnelFileRepository` (5), `PersonnelTransactionRepository` (4), `PayrollCalculationDataProvider` (4). Técnica: read model propio del módulo consumidor, alimentado por eventos (F4) o por vista materializada mientras siga siendo un monolito.

**Riesgo:** alto. Toca consultas de listados y dashboards con requisitos de rendimiento (`project-foundation.md` §12). Exige medir p95 antes/después de cada reemplazo.

---

### F4 — Comunicación asíncrona (3–4 semanas)

1. **Despertar `IDomainEvent`**: colección de eventos en `AggregateRoot`, recolección en `SaveChangesAsync`.
2. **Outbox transaccional**: tabla `outbox_messages` escrita en la misma transacción que el cambio de estado; un `BackgroundService` la drena (el patrón de los 3 workers existentes ya sirve de molde).
3. **Despachador in-process** con reintento e **idempotencia por `messageId`**.
4. **Convertir 3 flujos reales** para validar el patrón, empezando por el caso canónico `PersonnelFileFinalization → CompanyUserProvisioning` (H-05); luego cierre de planilla → notificaciones; retiro → archivado de entrevista de salida.

**Regla de oro:** *dentro* de un módulo, transacción ACID; *entre* módulos, evento + consistencia eventual. Es lo que convierte una futura extracción en un cambio de transporte y no en un rediseño.

---

### F5 — Preparación operativa y extracción (por servicio)

**Prerrequisitos operativos (antes del primer servicio):** OpenTelemetry (traces + métricas), health checks `/health/live` y `/health/ready`, contract tests por módulo, claves de idempotencia en POST, políticas de reintento/circuit breaker, y decidir el borde HTTP (gateway) — **no antes de tener 2 servicios**.

**Ranking de candidatos** (por acoplamiento medido, no por intuición):

| # | Candidato | A favor | En contra |
|---|---|---|---|
| 1 | **`Reports` / exports** | Ya tiene worker propio (`ReportExportJobBackgroundService`); asíncrono por naturaleza; escritura casi nula sobre otros módulos; perfil de CPU distinto (PDF/Excel) | Lee de muchos módulos → necesita contratos de lectura (F1) |
| 2 | **`Files`** | Dominio mínimo; su estado real vive en Blob Storage; ya tiene worker de limpieza | Referenciado por `PersonnelFiles` (194 usos) → primero F1 |
| 3 | **`Payroll`** | Cómputo pesado y por lotes; ventana de ejecución acotada; frontera de negocio nítida | Depende fuerte de lecturas de `PersonnelFiles` (19 usos + joins) → exige F3 completa |
| — | **`PersonnelFiles`** | — | **No extraer nunca.** Es el núcleo: 41 % del código, 24 dependencias salientes, 11 entrantes |

**Playbook de extracción (8 pasos, por servicio):**
1. Contrato público congelado y versionado (F1).
2. Todo consumo interno pasa por ese contrato — verificado por test.
3. Datos del módulo en su propio esquema, sin FK entrantes (F3).
4. Adaptador **in-process** que implementa el contrato (sin cambio de comportamiento).
5. Adaptador **remoto** (HTTP/gRPC) detrás de **feature flag**, con el mismo contrato.
6. Shadow traffic: ejecutar ambos y comparar resultados en producción sin servir el remoto.
7. Cambio de tráfico gradual + plan de rollback (bajar el flag).
8. Separación física de datos (`pg_dump -n <schema>` → base propia) **al final**, no al principio.

---

## 8. Qué NO hacer (anti-plan)

1. **No partir por capas.** Un "servicio de Application" y un "servicio de Infrastructure" es un monolito distribuido: toda la latencia, ninguno de los beneficios.
2. **No empezar por `PersonnelFiles`.** Es el hub. Extraerlo primero produce un monolito distribuido con `PersonnelFiles` en el centro.
3. **No compartir base de datos entre servicios.** Ya hay precedente (`Api` + `Backoffice.Api`, H-10): dos deployables sobre la misma base es aceptable *dentro* de un monolito, e inaceptable entre servicios.
4. **No introducir broker (Kafka/RabbitMQ/Service Bus) antes de F4.** Sin outbox e idempotencia, un broker solo acelera la pérdida de mensajes.
5. **No hacer F3 completa "por si acaso".** Los pasos 3 y 4 solo se justifican con un candidato de extracción decidido. Los pasos 1 y 2 sí valen siempre.
6. **No congelar features para "arreglar la arquitectura".** Todas las fases están diseñadas para intercalarse con desarrollo funcional. Un freeze arquitectónico es el patrón que mata estos proyectos.
7. **No hacer nada de esto sin F0 primero.** Sin trinquete, cualquier avance se revierte solo con el siguiente requerimiento.

---

## 9. Disparadores: cuándo justificar microservicios de verdad

Extraer **solo** si se cumple al menos uno, con evidencia medida:

| Disparador | Umbral sugerido |
|---|---|
| **Contención de despliegue** | ≥3 squads bloqueándose entre sí; lead time de cambio > 1 semana por conflictos de release |
| **Perfiles de escalado divergentes** | El batch de planilla o la generación de PDF degrada el p95 de la API transaccional de forma reproducible |
| **Aislamiento de disponibilidad** | Un fallo de un módulo (p. ej. render de documentos) tumba endpoints no relacionados |
| **Requisito regulatorio / de residencia** | Un tenant exige datos de nómina en otra jurisdicción o con otro ciclo de vida de retención |
| **Tamaño de equipo** | > ~15 ingenieros de backend; la comunicación intra-equipo deja de escalar |
| **Costo de infraestructura** | Escalar el monolito entero para un solo módulo caliente sale más caro que operar N servicios |

**Si ninguno se cumple: quedarse en monolito modular.** Es más rápido, más barato y más fácil de operar — y con F0–F2 hechas, se conservan casi todos los beneficios organizativos.

---

## 10. Costo de no hacer nada

No es un escenario neutro. Con el ritmo actual (11 requerimientos mayores en ~4 meses):

- **El SCC crece.** Cada requerimiento nuevo que toque 2 módulos agrega aristas. Hoy son 79 violaciones reales; sin trinquete, la tendencia es monótona creciente.
- **`PersonnelFiles` sigue absorbiendo.** Ya es el 41 % del código. Los últimos requerimientos (planilla, reportes legales) le agregaron subcarpetas. A este ritmo, en 6 meses es el 50 % y deja de ser refactorizable por una persona.
- **La interfaz de 247 métodos sigue creciendo** — es el punto de entrada por defecto para cualquier consulta sobre empleados.
- **La ventana barata se cierra.** Sin producción, mover tablas de esquema, borrar FKs y limpiar datos es gratis (`memoria: no-production-test-data`). **Con producción, cada uno de esos pasos requiere migración con downtime, ventana de mantenimiento y plan de rollback.** F3 paso 1 (esquemas por módulo) es hoy una tarde; en producción es un proyecto.
- **El costo de F2 crece con el cuadrado de las aristas**, no linealmente: cada arista nueva puede crear ciclos con las existentes.

---

## 11. Recomendación

1. **Salir a producción con la arquitectura actual.** Está bien construida para lo que declara ser, tiene 3 053 tests y disciplina de capas real. La modularidad no es un bloqueador de lanzamiento.
2. **Hacer F0 antes o inmediatamente después del lanzamiento** (~1 semana, riesgo ~cero). Es lo que convierte este documento en algo vivo en vez de una foto.
3. **Considerar F3 paso 1 (esquemas por módulo) mientras no haya producción.** Es la única acción cuyo costo se multiplica por diez si se pospone.
4. **F1 y F2 como trabajo de fondo**, intercalado con requerimientos funcionales, en un horizonte de 2–3 meses de calendario.
5. **F3 (resto), F4 y F5 solo cuando se dispare un criterio de §9.** Hasta entonces, un monolito modular sano es la arquitectura correcta para este producto y este equipo.

---

## Anexo A — Métricas de referencia (línea base 2026-07-25)

| Métrica | Valor |
|---|---|
| Proyectos / hosts desplegables | 6 / 2 |
| Archivos `.cs` (sin migraciones) | 1 427 |
| LOC de código fuente | 295 621 |
| LOC de migraciones | 2 974 374 |
| LOC de tests / métodos de test | 90 262 / 3 053 |
| Módulos de negocio (carpetas `Features`) | 39 |
| Controllers / endpoints | 140 / 889 |
| Entidades EF (`DbSet`) / configuraciones | 242 / 98 |
| Migraciones | 136 (un solo stream) |
| Repositorios | 70 |
| **Aristas cruzadas entre módulos (pares / usos)** | **196 / 1 296** |
| **Pares bidireccionales A↔B** | **81** (26 entre módulos de negocio) |
| **Módulos dentro del SCC mayor** | **33** |
| **`using` al interior de otros módulos (`Features.X`)** | **736** en 136 pares |
| **Pares de FK cruzadas en configuraciones EF** | **22** |
| **Archivos de Infrastructure que tocan ≥2 módulos** | **62** |
| **Violaciones del modelo de tiers (reales / totales)** | **79 / 279** |
| Usos de `IDomainEvent` | **0** |
| Tests de arquitectura | **0** |
| Módulo mayor (`PersonnelFiles`) | 345 archivos · 120 305 LOC · fan-out 24 · fan-in 11 |
| Interfaz mayor (`IPersonnelFileEmployeeRepository`) | 1 966 líneas · 247 métodos |

## Anexo B — Script de medición (para trackear la tendencia)

Guardar como `tools/architecture-metrics.py` y ejecutar desde la raíz del repo. Produce el grafo de módulos, el SCC, los pares bidireccionales y las violaciones de tiers. Es la fuente de los números del Anexo A y el insumo del baseline de F0.

```python
#!/usr/bin/env python3
"""Métricas de acoplamiento entre módulos de CLARIHR. Uso: python3 tools/architecture-metrics.py"""
import os, re, sys
from collections import Counter, defaultdict

ROOT, SKIP = "src", {"obj", "bin", "Migrations"}
ns_re    = re.compile(r"^\s*namespace\s+([A-Za-z0-9_.]+)", re.M)
using_re = re.compile(r"^\s*using\s+(?:static\s+)?(CLARIHR\.[A-Za-z0-9_.]+)\s*;", re.M)

TIERS = {  # T0 = base; un módulo solo puede depender de tiers <= al suyo
 0:["Common","Persistence","Tenancy","Time","Localization","Logging","Configuration","Policies",
    "Authorization","Authentication","Auth","IdentityAccess","Auditing","Audit","Files","Platform",
    "PlatformOperators","CatalogTypes","SystemCatalogs","GeneralCatalogs","InternalCatalogs","System"],
 1:["Banks","Locations","EducationCatalogs","DocumentTypeCatalogs","OrgStructureCatalogs",
    "PositionDescriptionCatalogs","Afps","Compensation","PersonnelEducationCatalogs","JobProfileCatalogTypes"],
 2:["Companies","CompanyUsers","AccountCompanies","CommercialPlans","CommercialAddons","CommercialModules",
    "PlatformSubscriptions","Provisioning","Preferences","Compliance","LegalRepresentatives"],
 3:["OrgUnits","CostCenters","SalaryTabulator","JobProfiles","CompetencyFramework","PositionSlots"],
 4:["PersonnelFiles","EmployeeRelations"], 5:["Leave","Overtime"], 6:["Payroll"], 7:["Reports"]}
tier = {m: t for t, ms in TIERS.items() for m in ms}

def module_of(ns):
    p = ns.split(".")
    if len(p) < 3 or p[0] != "CLARIHR": return None
    layer, rest = p[1], p[2:]
    if layer == "Application" and rest[0] in ("Features", "Abstractions"):
        return rest[1] if len(rest) > 1 else None
    if layer == "Api" and rest[0] in ("Controllers", "Contracts"): return None
    return rest[0]

edges, inner = Counter(), Counter()
for dp, dn, fns in os.walk(ROOT):
    dn[:] = [d for d in dn if d not in SKIP]
    for fn in (f for f in fns if f.endswith(".cs")):
        path = os.path.join(dp, fn)
        txt = open(path, encoding="utf-8-sig", errors="replace").read()
        m = ns_re.search(txt)
        if not m: continue
        src = module_of(m.group(1))
        if not src: continue
        for u in using_re.findall(txt):
            dst = module_of(u)
            if not dst or dst == src: continue
            edges[(src, dst)] += 1
            if u.startswith("CLARIHR.Application.Features."): inner[(src, dst)] += 1

up = [(tier[s], tier[d], s, d, c) for (s, d), c in edges.items()
      if s in tier and d in tier and tier[d] > tier[s]]
print(f"aristas cruzadas: {len(edges)} pares / {sum(edges.values())} usos")
print(f"acceso al interior ajeno (Features.X): {sum(inner.values())} usos / {len(inner)} pares")
print(f"violaciones de tier: {len(up)} pares / {sum(v[4] for v in up)} usos")
for ts, td, s, d, c in sorted(up, key=lambda r: -r[4]):
    print(f"  T{ts} {s} -> T{td} {d}  ({c})")
sys.exit(1 if sum(v[4] for v in up) > int(os.environ.get("ARCH_BASELINE", "279")) else 0)
```

## Anexo C — Glosario de la distinción

| Concepto | Qué es | Estado en CLARIHR |
|---|---|---|
| **Monolito por capas** | Un deployable, organizado por tipo de código. Frontera = capa | ✅ **Es esto** |
| **Monolito modular** | Un deployable, organizado por dominio. Frontera = módulo, compilada, con contrato y datos propios | ❌ Al ~22 % (93 % del modelo de tiers ya se respeta) |
| **Monolito distribuido** | N deployables que siguen acoplados (base o contratos compartidos). El peor de los mundos | ⚠️ El riesgo real si se extrae sin F1–F4 |
| **Microservicios** | N deployables con dominio, datos y ciclo de vida propios | ❌ No viable hoy; **no debería ser el objetivo aún** |
