# Analisis actual de arquitectura

## 1. Proposito

Este documento describe el estado actual de la arquitectura del backend CLARIHR a partir del codigo vigente del repositorio y queda alineado con la reevaluacion profunda actualizada al **30 de marzo de 2026**.

## 2. Resumen ejecutivo

La base arquitectonica sigue siendo coherente con el foundation document:

- `CLARIHR.Domain`
- `CLARIHR.Application`
- `CLARIHR.Infrastructure`
- `CLARIHR.Api`
- `CLARIHR.Backoffice.Api`

Tambien se mantiene una disciplina CQRS visible, validacion centralizada, RBAC por modulo, auditoria transversal y una intencion clara de `tenant-scoped by default`.

Sin embargo, la reevaluacion confirma que la arquitectura actual ya no puede describirse de forma tan optimista como antes. El sistema conserva buen esqueleto, pero tiene tensiones reales en cuatro frentes:

- filtro tenant global `fail-open`
- controllers que mezclan HTTP con auditoria, `SaveChangesAsync` y generacion de archivos
- alta dependencia de disciplina manual por la cantidad de endpoints administrativos
- gobernanza documental y contractual claramente por detras de la superficie real de la API

## 3. Estado actual verificado

Snapshot de esta reevaluacion:

- `37` controllers entre Core API y Backoffice API
- `332` acciones HTTP en `src/CLARIHR.Api/Controllers` y `src/CLARIHR.Backoffice.Api/Controllers`
- build limpio
- validaciones dirigidas de auth/plataforma/suscripciones/add-ons aprobadas

La arquitectura observable de una request sigue siendo:

1. ASP.NET Core recibe la request.
2. Los middleware aplican correlacion, request logging, headers y manejo de excepciones.
3. El controller traduce a command o query.
4. `ICommandDispatcher` o `IQueryDispatcher` resuelven el handler.
5. FluentValidation valida.
6. Application aplica autorizacion, tenant, reglas y persistencia via contratos.
7. Infrastructure ejecuta EF Core, auth, auditoria y servicios auxiliares.

## 4. Decisiones arquitectonicas vigentes

### 4.1 CQRS explicito

La separacion entre lectura y escritura sigue siendo visible y util. Hay handlers claros por caso de uso y DTOs de lectura en la mayoria de modulos.

### 4.2 Multi-tenant por defecto, pero no fail-closed

El sistema sigue orientado a tenant isolation, pero [ApplicationDbContext.cs#L287](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/Persistence/ApplicationDbContext.cs#L287) deja el filtro global en modo `fail-open`. Arquitectonicamente eso significa que la garantia tenant-scoped no depende solo del modelo, sino de que todos los flujos mantengan contexto y filtros adicionales de forma impecable.

### 4.3 Capacidades globales fuera de tenant

`CommercialAddon`, `CommercialPlan`, `CompanySubscription` y `PlatformOperator` introducen un plano global fuera del tenant. La decision ahora esta mejor aislada: la administracion global sale del core tenant-scoped y vive en `CLARIHR.Backoffice.Api`, con tokens `platform`, autorizacion por `PlatformOperator` persistido y `PlatformAuditLog` separado del `AuditLog` tenant-scoped.
Desde el 30 de marzo de 2026, `CommercialAddon` tambien deja de estar acoplado solo al cobro masivo por empleado activo y pasa a modelar pricing global reutilizable con `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity` y `minimumMonthlyFee` segun corresponda.
Desde el 3 de abril de 2026, la Core API tambien expone un plano global acotado para `InternalCatalogValue`: `AccountInternalCatalogsController` publica definiciones y valores reutilizables de requisitos de `JobProfiles` sin `tenantId` activo, mientras `JobProfileAdministration` puede alimentar ese catalogo automaticamente al crear o editar perfiles. Arquitectonicamente sigue separado del `JobCatalog` tenant-scoped existente y mantiene auditoria durable de plataforma para las altas globales aceptadas.

### 4.4 Controllers con superficie mixta

La arquitectura ya no puede afirmar que todos los controllers sean delgados. Ejemplos concretos:

- [PersonnelFileReportingController.cs#L151](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFileReportingController.cs#L151) audita exports, ejecuta `SaveChangesAsync()` y construye CSV/XLSX.
- [PersonnelFileCompensationController.cs#L226](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFileCompensationController.cs#L226) mezcla auditoria, persistencia y generacion de archivo en la capa API.

No es una ruptura total de Clean Architecture, pero si una erosion clara de responsabilidades.

### 4.5 Contratos publicos con guardrails centralizados

Desde marzo de 2026 el backend endurecio la gobernanza contractual con una convencion transversal:

- toda entidad persistida hereda `PublicId` y lo persiste como `public_id`
- la API publica expone `publicId` o `<Entidad>PublicId`, nunca `id` ni `internalId`
- cuando existe codigo de negocio, la superficie publica y persistida usa `code` y `normalizedCode` en `UPPERCASE`
- `IamUser` separa el `PublicId` de la fila tenant-scoped del `LinkedUserPublicId` que referencia al usuario autenticado global, evitando colisiones entre tenants

La garantia ya no depende solo de disciplina manual: hay convenciones de modelo, transformacion central de contratos y pruebas de guardrail sobre Swagger y `ApplicationDbContext`.

### 4.6 Modelo comercial y acceso efectivo: coherente, pero no canonico

La auditoria puntual sobre suscripciones, add-ons, permisos y accesos confirma que el backend ya tiene una cadena tecnica real de enforcement:

1. `CommercialPlan` define `PlanEntitlements`.
2. `CommercialAddon` define `CommercialAddonEntitlements`.
3. `PlanEntitlementService` calcula `effectiveModules` como la union de modulos habilitados por plan y add-ons activos.
4. varios authorization services y `RbacAuthorizationService` niegan acceso si el modulo comercial correspondiente no esta habilitado.

Eso significa que, en backend, los add-ons no estan realmente "fuera" de `effectiveModules`. Al contrario: hoy son productos comerciales separados que pueden aportar modulos al conjunto efectivo final.

La tension arquitectonica no esta en la ausencia total de union, sino en la falta de una fuente canonica simple para gobernar el modelo comercial y su traduccion a acceso:

- `FREE` conserva una base muy amplia de modulos habilitados, lo que debilita la diferenciacion comercial real entre planes.
- el marketplace y la consulta de add-ons elegibles no usan compatibilidad ni redundancia por modulo como criterio de elegibilidad; hoy filtran principalmente por estado y no propiedad previa.
- la vista de suscripcion expone por separado `CurrentPlan.ModuleKeys`, `ActiveAddons.ModuleKeys` y `EffectiveModules`, lo cual es util, pero deja la interpretacion comercial repartida entre varias superficies.
- el modulo `USERS` termina gobernado de forma indirecta a traves de `RBAC_USERS` y `PermissionMatrixCatalog`, lo que funciona tecnicamente, pero aumenta el costo mental para entender por que una capacidad esta o no habilitada.

## 5. Tensiones confirmadas por la reevaluacion

### 5.1 Gobernanza dificil por amplitud de superficie

La cantidad de endpoints administrativos vuelve costoso mantener consistencia de:

- permisos
- tenant scope
- auditoria
- documentacion de contratos
- pruebas de alto riesgo

### 5.2 Uso extensivo de `IgnoreQueryFilters()`

La reevaluacion encontro uso amplio de `IgnoreQueryFilters()` en repositorios de companies, org units, personnel files, salary tabulator, IAM, audit, competency framework, locations y otros. No todos los usos son incorrectos, pero el patron ya es suficientemente extendido como para requerir una matriz de gobernanza y pruebas dedicadas.

### 5.3 Documentacion arquitectonica desactualizada

El documento previo ya no representaba fielmente el sistema:

- reportaba `308` endpoints cuando la superficie actual medida es `332`
- afirmaba controllers universalmente delgados cuando ya hay excepciones visibles

### 5.4 Matriz resumida de `IgnoreQueryFilters()`

La reevaluacion encontro el siguiente patron resumido:

| Repositorio o servicio | Motivo visible | Filtro alterno o control compensatorio | Cobertura visible | Lectura de riesgo |
|---|---|---|---|---|
| `LocationHierarchyRepository`, `WorkCenterRepository`, `WorkCenterTypeRepository`, `LegalRepresentativeRepository`, `JobProfileRepository`, `JobCatalogRepository`, `OrgStructureCatalogRepository`, `PositionDescriptionCatalogRepository`, `CostCenterRepository`, `PositionSlotRepository`, `OrgUnitRepository`, `SalaryTabulatorRepository`, `CompetencyFrameworkRepository`, `AuditLogRepository` | `ExistsOutsideTenantAsync` o equivalentes para distinguir `not found` vs `tenant mismatch` | el acceso normal sigue tenant-scoped; el bypass se usa como verificacion de existencia | parcial, por modulo | uso plausible, pero requiere disciplina constante |
| `PersonnelFileRepository` | `ExistsOutsideTenantAsync`, `DocumentExistsOutsideTenantAsync` y unicidad de identificacion | el check de identificacion compensa con `TenantId == tenantId` explicito; los otros se usan para mismatch | parcial | uso justificado, pero sensible por volumen de PII |
| `FieldPermissionService.Read` y `FieldPermissionService.Write` | diferenciar `RoleNotFound` de `TenantMismatch` | el query principal sigue tenant-scoped y el bypass solo decide el error | parcial | uso razonable, pero de alta sensibilidad IAM |
| `UserCompanyRepository` | resolver rol tenant-scoped uniendo contra `IamRoles` sin filtro | la membresia y compania si van filtradas antes del join | parcial | revisar cuidadosamente para no normalizar bypasses innecesarios |
| `LocationGroupRepository.GetByIdIgnoreFiltersAsync` | obtener entidad completa ignorando filtros | no se observa compensacion estructural en el nombre del metodo; requiere auditoria puntual de usos | no visible en esta reevaluacion | mayor prioridad de revision entre los bypasses encontrados |

### 5.5 Gobernanza comercial aun demasiado distribuida

La reevaluacion de suscripciones y add-ons deja una tension adicional:

- el sistema ya calcula correctamente `effectiveModules`, pero la semantica comercial sigue repartida entre catalogo de planes, catalogo de add-ons, snapshots de suscripcion, `PermissionMatrixCatalog` y servicios de autorizacion por modulo.
- no existe todavia una matriz canonica que responda, en un solo lugar, que modulo habilita cada producto comercial, que recursos protege, que permisos dependen de ese modulo y que add-ons son redundantes o incompatibles para un plan dado.
- mientras esa matriz no exista, la plataforma depende de disciplina manual para evitar drift entre pricing, UX de marketplace, permisos y enforcement real.

## 6. Conclusiones

La prioridad arquitectonica del proyecto no es redisenar desde cero. La base sigue siendo rescatable y, en muchos modulos, sana. La prioridad correcta es endurecer las garantias que hoy dependen demasiado de disciplina manual:

1. tenant isolation con `fail-closed`
2. controllers realmente delgados en exports y reportes
3. gobernanza de bypasses como `IgnoreQueryFilters()`
4. separacion clara entre auditoria tenant-scoped y auditoria global de plataforma
5. contratos y analisis vivos alineados con la superficie real del backend
6. gobernanza canonica entre plan, add-ons, `effectiveModules` y permisos
