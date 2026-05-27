# Referencia de endpoints API

## 1. Proposito

Este documento es la referencia humana inicial de la API actual. No intenta duplicar todos los schemas runtime en markdown. Su funcion es explicar:

- inventario de modulos
- familias de rutas
- autenticacion y tenant scope
- flujos criticos
- comportamientos observables relevantes

El repositorio expone actualmente una Core API tenant-scoped y una Platform Backoffice API. Swagger runtime en Development es la fuente canonica del inventario exacto de endpoints y controladores vigentes.

## 2. Modelo de autenticacion y tenant

### 2.1 Endpoints publicos

El acceso publico se limita intencionalmente a:

- `POST /api/auth/register`
- `POST /api/auth/external`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `GET /api/system/status`

### 2.2 Core API autenticada

Las rutas del core (`/api/account/*`, `/api/company/*`, `/api/audit/*` y `/api/v1/*`) aceptan solo tokens emitidos con `client_type=core`.

### 2.3 Platform backoffice autenticado

Las rutas `/api/platform/*` aceptan solo tokens emitidos por `/api/platform/auth/*` con `client_type=platform`.

Reglas observables:

- estos tokens no incluyen `tid`
- el acceso global no se resuelve por email allow-list ni por RBAC tenant-scoped
- la autorizacion depende de un `PlatformOperator` activo persistido en base de datos
- un token `core` no sirve en `/api/platform/*` y un token `platform` no sirve en la Core API

### 2.4 API operativa tenant-scoped

La mayoria de modulos funcionales usan:

- `/api/v1/companies/{companyPublicId:guid}/...` para operaciones tenant-scoped de coleccion o listado
- `/api/v1/{resource}/{publicId:guid}` para operar recursos directos una vez establecido el contexto tenant

### 2.5 Estandar de identificadores y codigos

Convencion obligatoria de la API publica:

- la superficie externa usa `publicId` o `<Entidad>PublicId`
- `id` e `internalId` quedan reservados para persistencia y logica interna
- cuando un recurso tiene codigo de negocio, la API expone `code` y `normalizedCode`
- `code` y `normalizedCode` se publican en `UPPERCASE`

Swagger runtime es la referencia canonica del contrato. Las secciones narrativas de este documento ya siguen este estandar aunque algunos nombres historicos puedan aparecer en explicaciones de negocio.

### 2.6 Flujo de activacion de compania

La secuencia actual de onboarding es:

1. `POST /api/auth/register`
2. `POST /api/account/companies`
3. `PATCH /api/account/companies/{companyPublicId}/switch`

Despues del `switch`, el `access token` devuelto incluye contexto tenant y habilita los modulos `api/v1`.

### 2.7 Estandar observable de errores

Las respuestas de error de la API usan `ProblemDetails` enriquecido con:

- `code`: codigo estable de negocio o de validacion
- `traceId`: identificador de correlacion para soporte y logs
- `errors`: diccionario campo -> mensajes cuando el error es `400 common.validation`
- `details`: metadata estructurada opcional para errores de autorizacion o politicas

Reglas observables:

- las validaciones de negocio, las validaciones explicitas y los errores de `model binding` comparten el mismo envelope base
- cuando el body JSON es invalido, `errors` usa nombres del contrato publico y no expone prefijos internos como `request` o `$.`
- los errores de conversion JSON se devuelven con mensajes saneados por tipo esperado, por ejemplo UUID invalido, en lugar de detalles crudos del parser

### 2.8 Politica de cache HTTP

Las Core API y Platform Backoffice API aplican headers anti-cache a toda ruta `/api`:

- `Cache-Control: no-store`
- `Pragma: no-cache`
- `Expires: 0`

Esta regla cubre respuestas JSON, descargas y exportes con datos potencialmente sensibles de RRHH o plataforma. Los clientes no deben depender de cache HTTP compartido para rutas `/api`.

### 2.9 Politica de exportes de reporte

Los exportes sincronos existentes conservan su request shape, pero ahora aplican limite de `5,000` filas. El backend lee hasta `5,001` filas; si detecta overflow responde `413 REPORT_EXPORT_TOO_LARGE` y no genera archivo.

Para exportes grandes, los clientes deben usar jobs asincronos persistidos. El limite asincrono por job es `100,000` filas, los artefactos se almacenan en Azure Blob Storage y expiran despues de `24` horas. Si Blob Storage no esta configurado, crear un job responde `503 REPORT_EXPORT_STORAGE_NOT_CONFIGURED`.

Recursos soportados por jobs asincronos: `PERSONNEL_FILES`, `PERSONNEL_FILE_PERSONNEL_ACTIONS`, `PERSONNEL_FILE_PAYROLL_TRANSACTIONS`, `ORG_UNITS`, `POSITION_SLOTS`, `SALARY_TABULATOR`, `COST_CENTERS`, `LEGAL_REPRESENTATIVES`, `JOB_PROFILE_COMPETENCY_MATRIX` y `JOB_PROFILE_PDF`. El `resourceKey` es case-insensitive en el request y se normaliza a mayusculas. Los primeros nueve son recursos tabulares y aceptan `csv`, `xlsx` o `json`; `JOB_PROFILE_PDF` es un recurso documental y acepta unicamente `pdf` (renderiza el perfil de cargo como documento PDF via worker). Un par recurso/formato incompatible se rechaza al crear el job.

Exenciones observables: el export sincrono de datos de un unico `JOB_PROFILE` (JSON/print) se conserva por ser single-resource y es distinto de `JOB_PROFILE_PDF`, que es el job asincrono que renderiza el documento PDF; los diagram exports de `ORG_UNITS` y `POSITION_SLOTS` siguen siendo sincronos, pero rechazan grafos con mas de `5,000` nodos con `413 REPORT_EXPORT_LIMIT_EXCEEDED`.

## 3. Inventario de modulos

| Modulo | Controladores | Familias principales de rutas | Proposito |
|---|---|---|---|
| System | `SystemController` | `/api/system/status` | salud y estado de runtime |
| Auth | `AuthController` | `/api/auth/*` | registro, auth externa, login, refresh, recuperacion de contrasena y logout |
| Account companies | `AccountCompaniesController` | `/api/account/companies*` | crear, listar, archivar, reactivar, cambiar compania activa y resolver catalogos previos al contexto tenant |
| Account company subscriptions | `AccountCompanySubscriptionsController` | `/api/account/companies/{publicId}/subscription*` | consultar y administrar como owner el plan activo, marketplace de add-ons y modulos efectivos de la compania |
| Account internal catalogs | `AccountInternalCatalogsController` | `/api/account/internal-catalogs*` | exponer catalogos internos globales reutilizables por frontend y permitir altas controladas por similitud |
| Platform auth | `PlatformAuthController` | `/api/platform/auth*` | login, refresh y logout de operadores del backoffice global |
| Platform commercial modules | `CommercialModulesController` | `/api/platform/commercial-modules` | exponer el catalogo canonico de modulos comerciales asignables a planes y add-ons |
| Platform commercial addons | `CommercialAddonsController` | `/api/platform/commercial-addons*` | administrar el catalogo comercial global de add-ons reutilizables con pricing masivo o especializado |
| Platform bank catalogs | `BankCatalogsController` | `/api/platform/bank-catalogs*` | administrar el catalogo global de bancos por pais para consumo del core RH |
| Platform commercial plans | `CommercialPlansController` | `/api/platform/commercial-plans*` | administrar el catalogo comercial global de planes reutilizables |
| Platform subscriptions | `PlatformCompanySubscriptionsController`, `PlatformSubscriptionsController` | `/api/platform/companies/{companyPublicId}/subscription*`, `/api/platform/company-subscriptions` | consultar, previsualizar, activar, cambiar plan, administrar add-ons y listar suscripciones empresariales globales |
| **Education catalogs** | `EducationCatalogsController` (Core), `EducationCatalogsController` (Backoffice) | `/api/v1/education-catalogs/{catalogKey}` (Core lectura), `/api/platform/education-catalogs/{catalogKey}` (Backoffice CRUD) | catalogos de educacion globales de sistema administrados exclusivamente por Backoffice y consultados en modo lectura desde el Core |
| Company users | `CompanyUsersController` | `/api/company/users*` | invitar y administrar usuarios del tenant |
| Preferences | `UserPreferencesController`, `CompanyPreferencesController` | `/api/account/me/preferences`, `/api/v1/companies/{companyId}/preferences` | administrar preferencias personales (language y `socialLinks`) y preferencias operativas de compania (moneda y zona horaria) |
| Account company authorization | `AccountCompaniesController`, `AccountCompanyAuthorizationController` | `/api/account/companies/{companyPublicId}/access-context`, `/api/account/companies/{companyPublicId}/authorization*` | contexto de acceso, catalogo filtrado, roles, grants y policies del tenant |
| Audit | `AuditController` | `/api/audit*` | consulta y detalle de logs de auditoria |
| Org structure catalogs | `OrgStructureCatalogsController` | `/api/account/org-structure-catalogs/*`, `/api/v1/companies/{companyPublicId}/org-structure-catalogs/*` | tipos de compania, tipos de unidad y areas funcionales |
| Locations | `LocationHierarchyController`, `LocationLevelsController`, `LocationGroupsController`, `WorkCenterTypesController`, `WorkCentersController` | `/api/v1/companies/{companyPublicId}/location-*`, `/api/v1/companies/{companyPublicId}/work-*` | modelo geografico del tenant, grupos y work centers |
| Org units and cost centers | `OrgUnitsController`, `CostCentersController` | `/api/v1/companies/{companyPublicId}/org-units*`, `/api/v1/companies/{companyPublicId}/cost-centers*` | arbol organizacional, grafos, exportes y centros de costo |
| Legal representatives | `LegalRepresentativesController` | `/api/v1/companies/{companyPublicId}/legal-representatives*` | ciclo de vida y uso de representantes legales |
| Job and competency design | `JobCatalogsController`, `JobProfilesController`, `CompetencyFrameworkController`, `PositionDescriptionCatalogItemsController`, `PositionCategoryClassificationsController`, `PositionCategoriesController`, `PositionSlotsController` | `/api/v1/companies/{companyPublicId}/job-*`, `/api/v1/companies/{companyPublicId}/occupational-*`, `/api/v1/companies/{companyPublicId}/position-*` | diseno de puestos, competencias, catalogos y posiciones |
| Personnel files | `PersonnelFilesController`, `PersonnelFilePersonalInfoController`, `PersonnelFileBackgroundController`, `PersonnelFileInterestsController`, `PersonnelFileProfileController`, `PersonnelFileEmploymentController`, `PersonnelFileCompensationController`, `PersonnelFileTalentController`, `PersonnelFileDocumentsController`, `PersonnelFileAdministrationController`, `PersonnelFileReportingController` | `/api/v1/companies/{companyPublicId}/personnel-files*`, `/api/v1/personnel-files/*`, `/api/v1/personnel-custom-field-definitions*` | ciclo de vida del expediente, datos personales, antecedentes, intereses, perfil financiero, empleo, compensacion, talento, documentos y reporting |
| Salary governance | `SalaryTabulatorController` | `/api/v1/companies/{companyPublicId}/salary-tabulator/*` | lineas salariales, exportes y change requests |
| Report capabilities | `ReportsController` | `/api/v1/companies/{companyPublicId}/reports/capabilities` | descubrimiento de capacidades de reporte para frontend |
| Report export jobs | `ReportExportJobsController` | `/api/v1/companies/{companyPublicId}/report-export-jobs*`, `/api/v1/report-export-jobs/*` | cola persistida de exportes grandes, estado, cancelacion y descarga segura |

## 4. Flujos criticos y endpoints representativos

### 4.1 Autenticacion

- `POST /api/auth/register`: crea una cuenta de usuario y devuelve tokens
- `POST /api/auth/external`: crea o autentica un usuario mediante proveedor externo
- `POST /api/auth/login`: inicia sesion local
- `POST /api/auth/refresh`: rota la sesion usando un refresh token
- `POST /api/auth/password-reset/request`: solicita recuperacion de contrasena con respuesta uniforme `202`
- `POST /api/auth/password-reset/validate`: valida un token de recuperacion vigente
- `POST /api/auth/password-reset/redeem`: redime el token con la nueva contrasena y revoca sesiones activas
- `POST /api/auth/company-user-invitations/accept`: activa un usuario invitado por compania y define su contrasena final
- `POST /api/auth/logout`: revoca la sesion autenticada
- `GET /api/account/me/preferences`: obtiene preferencias de perfil del usuario autenticado
- `PUT /api/account/me/preferences`: actualiza preferencias de perfil (por ejemplo `language`) del usuario autenticado
- `PUT /api/account/me/preferences/social-links`: reemplaza la coleccion completa de links sociales del usuario autenticado
- `GET /api/v1/companies/{companyId}/preferences`: obtiene preferencias administrativas de la compania en contexto tenant
- `PUT /api/v1/companies/{companyId}/preferences`: actualiza preferencias administrativas de la compania (`currencyCode`, `timeZone`)

Comportamiento observable:

- el claim `language` del JWT core se resuelve desde preferencias del usuario autenticado; ya no depende de la configuracion de compania
- cuando no existe preferencia de usuario, la API emite y resuelve fallback `en`
- los `socialLinks` pertenecen al usuario autenticado y no al `personnel file`; el contrato usa `{ providerCode, url }`, exige `https` y reemplazo completo de la coleccion
- `password-reset` usa token hash persistido, un solo uso, expiracion corta, respuesta uniforme para evitar enumeracion y revocacion de refresh tokens al redimir

### 4.2 Onboarding de compania

- `GET /api/v1/companies/{companyId}/reference-catalogs/identification-types`
- `GET /api/account/companies`
- `POST /api/account/companies`
- `PATCH /api/account/companies/{companyPublicId}/switch`

Comportamiento observable:

- el catalogo de tipos documentales del representante legal se resuelve desde `reference-catalogs/identification-types` y devuelve opciones activas ordenadas por pais de compania
- la creacion de compania siembra datos iniciales de locations segun el pais
- el cambio de compania modifica el contexto tenant activo del usuario

### 4.3 Platform backoffice de suscripciones

- `POST /api/platform/auth/login`
- `GET /api/platform/commercial-addons`
- `GET /api/platform/commercial-plans`
- `GET /api/platform/commercial-modules`
- `POST /api/platform/companies/{companyPublicId}/subscription/preview`
- `PUT /api/platform/companies/{companyPublicId}/subscription`
- `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes/preview`
- `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes`
- `GET /api/platform/companies/{companyPublicId}/subscription/plan-changes`
- `PATCH /api/platform/companies/{companyPublicId}/subscription/plan-changes/{planChangePublicId}/cancel`
- `GET /api/platform/companies/{companyPublicId}/subscription/addons`
- `GET /api/platform/companies/{companyPublicId}/subscription/addons/eligible`
- `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes/preview`
- `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes`
- `GET /api/platform/companies/{companyPublicId}/subscription/addon-changes`
- `PATCH /api/platform/companies/{companyPublicId}/subscription/addon-changes/{addonChangePublicId}/cancel`
- `PATCH /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status`
- `GET /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status-history`

Comportamiento observable:

- el backoffice usa autenticacion separada de la Core API y nunca depende de `tid`
- solo usuarios ligados a un `PlatformOperator` activo pueden entrar al backoffice global
- `ReadOnly` puede consultar add-ons, planes, overview, historial e historial de estados; `Admin` puede mutar catalogos, reemplazar suscripciones y cambiar estados manualmente
- el catalogo de add-ons globales se administra solo desde `/api/platform/commercial-addons`; la Core API no expone este recurso
- `/api/platform/commercial-modules` expone el catalogo canonico de modulos comerciales que la plataforma puede asignar a planes y add-ons; no acepta claves arbitrarias
- cada plan define `fee` mensual base, precio por empleado activo, estado, limites configurables y `moduleKeys`; `FREE` y `MASTER` son planes de sistema y ambos quedan visibles en backoffice
- cada add-on define `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `status` y `moduleKeys`, con reglas distintas para `Massive` y `Specialized`
- `FREE` existe sembrado como plan de sistema para el provisioning actual, no puede renombrarse ni inactivarse, y cualquier empresa nueva sigue provisionandose contra ese plan con el catalogo modular completo actualmente alineado a `MASTER`
- `MASTER` existe como plan de sistema interno reservado para CLARI, mantiene precio `0` y siempre se resincroniza con el catalogo completo de modulos conocidos
- la vista previa y la activacion aceptan `expiresAtUtc` opcional y resuelven un estado inicial `Active` o `Scheduled` segun la fecha de inicio
- los cambios de plan usan el patron `preview + create + history + cancel`; calculan `Immediate`, `SpecificDate` o `NextBillingCycle`, conservan snapshot del plan actual y objetivo, y el processor aplica automaticamente los cambios vencidos
- los add-ons por empresa usan el mismo patron `preview + create + history + cancel`, distinguen estado actual por empresa de estado del catalogo y admiten activacion o desactivacion con fecha inmediata, especifica o siguiente ciclo de billing
- la activacion de add-ons queda bloqueada cuando la compania esta en `FREE`; el preview devuelve inelegibilidad y la activacion responde conflicto
- el ciclo de vida visible de la suscripcion incluye `Draft`, `Scheduled`, `Trial`, `Active`, `Suspended`, `Expired` y `Cancelled`
- las respuestas de overview, historial y listado global ahora exponen `statusChangedAtUtc`, `currentStatusReasonCode`, `currentStatusOrigin`, `canOperate` y `canGenerateCharges`; la respuesta detallada tambien devuelve `currentStatusObservations`
- cada cambio de estado manual o automatico deja historial con estado anterior, nuevo estado, fecha, motivo, observaciones y actor u origen del sistema
- la administracion de add-ons es comercial-only en V1: no altera todavia seats operativos, volumen contratado, pagos ni facturacion, pero si alimenta el gating operativo mediante los modulos efectivos por empresa
- reemplazar una suscripcion empresarial cierra la fila viva previa cuando corresponde, crea una nueva fila historica con snapshot de precios y mantiene trazabilidad completa de transiciones

### 4.4 Locations y organizacion

Endpoints representativos de lectura:

- `GET /api/v1/companies/{companyPublicId}/location-hierarchy`
- `GET /api/v1/companies/{companyPublicId}/location-levels`
- `GET /api/v1/companies/{companyPublicId}/location-groups/tree`
- `GET /api/v1/companies/{companyPublicId}/org-units/tree`
- `GET /api/v1/companies/{companyPublicId}/org-units/graph`
- `GET /api/v1/companies/{companyPublicId}/cost-centers/export`

Endpoints representativos de escritura:

- `POST /api/v1/companies/{companyPublicId}/location-groups`
- `POST /api/v1/companies/{companyPublicId}/work-centers`
- `POST /api/v1/companies/{companyPublicId}/org-units`
- `PATCH /api/v1/org-units/{publicId}/move`

### 4.5 Authorization tenant-scoped

Endpoints representativos:

- `GET /api/account/companies/{companyPublicId}/access-context`
- `GET /api/account/companies/{companyPublicId}/authorization/role-builder-catalog`
- `GET /api/account/companies/{companyPublicId}/authorization/roles`
- `POST /api/account/companies/{companyPublicId}/authorization/roles`
- `GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`
- `PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`
- `GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`
- `PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`
- `PUT /api/account/companies/{companyPublicId}/authorization/users/{userPublicId}/roles`
- `GET /api/account/companies/{companyPublicId}/authorization/resource-policies/{resourceKey}`

Comportamiento observable:

- la autorizacion visible del tenant se resuelve desde un `access-context` por compania
- el owner opera con un catalogo filtrado por compania para construir roles y grants
- la API publica ya no expone superficies legacy separadas de autorizacion
- las respuestas de policies por recurso siguen siendo responsibility del backend y no del frontend

### 4.6 Personnel files

Core:

- `POST /api/v1/companies/{companyPublicId}/personnel-files`
- `GET /api/v1/companies/{companyPublicId}/personnel-files`
- `GET /api/v1/personnel-files/{publicId}`
- `PUT /api/v1/personnel-files/{publicId}`
- `PATCH /api/v1/personnel-files/{publicId}` (JSON Patch RFC 6902; reemplaza a `/activate` e `/inactivate` vía `op replace /isActive`)

Datos personales (`PersonnelFilePersonalInfoController`):

- `GET /api/v1/personnel-files/{publicId}/personal-info` (el `PUT` se reubicó al shell: `PUT /api/v1/personnel-files/{publicId}`)
- `GET /api/v1/personnel-files/{publicId}/identifications`
- `GET /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/identifications`
- `PUT /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/addresses`
- `GET /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `POST /api/v1/personnel-files/{publicId}/addresses`
- `PUT /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `GET /api/v1/personnel-files/{publicId}/emergency-contacts`
- `GET /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `POST /api/v1/personnel-files/{publicId}/emergency-contacts`
- `PUT /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `GET /api/v1/personnel-files/{publicId}/family-members`
- `GET /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`
- `POST /api/v1/personnel-files/{publicId}/family-members`
- `PUT /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`

Antecedentes (`PersonnelFileBackgroundController`):

- `GET /api/v1/personnel-files/{publicId}/educations`
- `GET /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/educations`
- `PUT /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/languages`
- `GET /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `POST /api/v1/personnel-files/{publicId}/languages`
- `PUT /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `GET /api/v1/personnel-files/{publicId}/trainings`
- `GET /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `POST /api/v1/personnel-files/{publicId}/trainings`
- `PUT /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `GET /api/v1/personnel-files/{publicId}/previous-employments`
- `GET /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `POST /api/v1/personnel-files/{publicId}/previous-employments`
- `PUT /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `GET /api/v1/personnel-files/{publicId}/references`
- `GET /api/v1/personnel-files/{publicId}/references/{referencePublicId}`
- `POST /api/v1/personnel-files/{publicId}/references`
- `PUT /api/v1/personnel-files/{publicId}/references/{referencePublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/references/{referencePublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/references/{referencePublicId}`

Intereses (`PersonnelFileInterestsController`):

- `GET /api/v1/personnel-files/{publicId}/hobbies`
- `GET /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `POST /api/v1/personnel-files/{publicId}/hobbies`
- `PUT /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `GET /api/v1/personnel-files/{publicId}/associations`
- `GET /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/associations`
- `PUT /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/employee-relations`
- `GET /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/employee-relations`
- `PUT /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`

Perfil financiero — Bank Accounts (`PersonnelFileCompensationController`):

- `GET /api/v1/personnel-files/{publicId}/bank-accounts`
- `GET /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}`
- `POST /api/v1/personnel-files/{publicId}/bank-accounts`
- `PUT /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/bank-accounts/{bankAccountPublicId}`

Employment:

- `GET /api/v1/personnel-files/{publicId}/finalize/preview`
- `PATCH /api/v1/personnel-files/{publicId}/finalize`
- `GET /api/v1/personnel-files/{publicId}/employee-profile`
- `PUT /api/v1/personnel-files/{publicId}/employee-profile`
- `GET /api/v1/personnel-files/{publicId}/employment-assignments`
- `GET /api/v1/personnel-files/{publicId}/contract-history`
- `GET /api/v1/personnel-files/{publicId}/authorization-substitutions`
- `GET /api/v1/personnel-files/{publicId}/assets-accesses`
- `GET /api/v1/personnel-files/{publicId}/personnel-actions`

Compensation (`PersonnelFileCompensationController`) — every sub-resource is canonical (GET list, GET by id, POST→201, PUT con `If-Match`, PATCH JSON Patch [incluye `isActive`], DELETE físico que devuelve el token del padre). `bank-accounts` se lista arriba en «Perfil financiero».

- `GET /api/v1/personnel-files/{publicId}/salary-items`
- `GET /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}`
- `POST /api/v1/personnel-files/{publicId}/salary-items`
- `PUT /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/salary-items/{salaryItemPublicId}`
- `GET /api/v1/personnel-files/{publicId}/additional-benefits`
- `GET /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}`
- `POST /api/v1/personnel-files/{publicId}/additional-benefits`
- `PUT /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/additional-benefits/{additionalBenefitPublicId}`
- `GET /api/v1/personnel-files/{publicId}/payment-methods`
- `GET /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}`
- `POST /api/v1/personnel-files/{publicId}/payment-methods`
- `PUT /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/payment-methods/{paymentMethodPublicId}`
- `GET /api/v1/personnel-files/{publicId}/payroll-transactions` (paginado + filtros/búsqueda)
- `GET /api/v1/personnel-files/{publicId}/payroll-transactions/export`
- `GET /api/v1/personnel-files/{publicId}/payroll-transactions/{payrollTransactionPublicId}`
- `POST /api/v1/personnel-files/{publicId}/payroll-transactions`
- `PATCH /api/v1/personnel-files/{publicId}/payroll-transactions/{payrollTransactionPublicId}` (solo `isActive`; **sin PUT ni DELETE** — registro de auditoría inmutable)
- `GET /api/v1/personnel-files/{publicId}/insurances`
- `GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}`
- `POST /api/v1/personnel-files/{publicId}/insurances`
- `PUT /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}`
- `GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries`
- `GET /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}`
- `POST /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries`
- `PUT /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/insurances/{insurancePublicId}/beneficiaries/{beneficiaryPublicId}`
- `GET /api/v1/personnel-files/{publicId}/medical-claims`
- `GET /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}`
- `POST /api/v1/personnel-files/{publicId}/medical-claims`
- `PUT /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/medical-claims/{medicalClaimPublicId}`

Talent (`PersonnelFileTalentController`):

- `GET /api/v1/personnel-files/{publicId}/evaluations`
- `GET /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/evaluations`
- `PUT /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/position-competency-results`
- `GET /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `POST /api/v1/personnel-files/{publicId}/position-competency-results`
- `PUT /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `GET /api/v1/personnel-files/{publicId}/selection-contests`
- `GET /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `POST /api/v1/personnel-files/{publicId}/selection-contests`
- `PUT /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `GET /api/v1/personnel-files/{publicId}/curricular-competencies`
- `GET /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `POST /api/v1/personnel-files/{publicId}/curricular-competencies`
- `PUT /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`

Documents and reporting:

- `PUT /api/v1/personnel-files/{publicId}/documents`
- `POST /api/v1/personnel-files/{publicId}/documents`
- `GET /api/v1/personnel-files/{publicId}/print`
- `POST /api/v1/companies/{companyPublicId}/personnel-files/dynamic-query`
- `GET /api/v1/companies/{companyPublicId}/personnel-files/export`

Comportamiento observable:

- la mayoria de actualizaciones por seccion reemplazan el payload completo de la subseccion
- los expedientes ahora nacen en `Draft` y la transicion funcional dedicada del modulo es `finalize`
- `finalize/preview` permite al frontend validar readiness antes de cerrar el expediente y devuelve `isEligible` con issues bloqueantes sin ejecutar cambios; cada issue incluye `section`, `fieldKey` y `navigationKey` para mapear redireccion en UI; `createUserAccount` viaja como query param opcional (default `true`)
- `finalize` permite decidir si se aprovisiona usuario de compania en ese momento; con la opcion activa crea/reutiliza el usuario y emite invitacion, con la opcion desactivada completa el expediente sin crear cuenta
- los endpoints de reporting y export existen tanto a nivel tenant como a nivel recurso
- la profundizacion completa del modulo esta en `5.10 Personnel files`

### 4.7 Salary tabulator

Endpoints representativos:

- `GET /api/v1/companies/{companyPublicId}/salary-tabulator/lines`
- `GET /api/v1/companies/{companyPublicId}/salary-tabulator/export`
- `GET /api/v1/companies/{companyPublicId}/salary-tabulator/change-requests`
- `POST /api/v1/companies/{companyPublicId}/salary-tabulator/change-requests`
- `PATCH /api/v1/salary-tabulator/change-requests/{publicId}/approve`

Comportamiento observable:

- los cambios salariales se modelan como `change requests` con transiciones explicitas de estado
- la estrategia de eliminar `companyId` de rutas tenant-scoped nuevas y migrar rutas legacy esta registrada en `docs/technical/api/tenant-route-technical-debt.md`

### 4.8 Education Catalogs

Core (solo lectura, usuarios autenticados):

- `GET /api/v1/education-catalogs/{catalogKey}` — lista paginada de items activos del catalogo indicado
- `GET /api/v1/education-catalogs/{catalogKey}/{id}` — item activo individual por `publicId`

Platform Backoffice (CRUD completo, `PlatformOperator`):

- `GET /api/platform/education-catalogs/{catalogKey}` — lista paginada con filtros `isActive`, `search`
- `GET /api/platform/education-catalogs/{catalogKey}/{id}` — detalle completo del item
- `POST /api/platform/education-catalogs/{catalogKey}` — crea item nuevo
- `PUT /api/platform/education-catalogs/{catalogKey}/{id}` — actualiza item existente
- `PATCH /api/platform/education-catalogs/{catalogKey}/{id}/activate` — activa item
- `PATCH /api/platform/education-catalogs/{catalogKey}/{id}/inactivate` — inactiva item

Catalog keys validas: `education-statuses`, `study-types`, `careers`, `shifts`, `modalities`.

Comportamiento observable:

- los catalogos de educacion son de sistema global y no tienen scope por pais ni por tenant
- el Core solo puede leer los items activos del catalogo; no puede crear, modificar ni inactivar items
- el Backoffice administra el ciclo de vida completo de cada catalogo
- los filtros `isActive` y `search` del Backoffice no estan disponibles en el Core
- las operaciones de escritura usan `concurrencyToken` para deteccion de conflictos
- un `catalogKey` inexistente devuelve `404 Not Found`
- el codigo unico por tipo de catalogo se valida en creacion y actualizacion con `409 Conflict`

### 4.9 Report export jobs

Endpoints:

- `POST /api/v1/companies/{companyPublicId}/report-export-jobs`
- `GET /api/v1/companies/{companyPublicId}/report-export-jobs?pageNumber&pageSize&status`
- `GET /api/v1/report-export-jobs/{jobPublicId}`
- `GET /api/v1/report-export-jobs/{jobPublicId}/download`
- `POST /api/v1/report-export-jobs/{jobPublicId}/cancel`

Request de creacion:

```json
{
  "resourceKey": "PERSONNEL_FILES",
  "format": "xlsx",
  "parameters": {
    "isActive": true,
    "sortBy": "createdUtc"
  }
}
```

Recurso `JOB_PROFILE_PDF` (documental): el unico parametro de cliente es `jobProfileId` (el public id del perfil de cargo; se acepta el alias `jobProfilePublicId`), el `format` debe ser `pdf`, y `includeCompensation` es server-controlled — el backend lo estampa segun el RBAC del solicitante (solo embebe datos salariales/PII en el documento si el usuario puede gestionar perfiles, el mismo nivel que las escrituras de compensacion) e ignora cualquier valor enviado por el cliente, incluso con casing distinto.

```json
{
  "resourceKey": "JOB_PROFILE_PDF",
  "format": "pdf",
  "parameters": {
    "jobProfileId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
}
```

Comportamiento observable:

- `POST` valida formato, recurso, permisos de lectura del recurso y tenant activo; responde `202` con `ReportExportJobResponse`.
- `GET` lista solo jobs del tenant activo con paginacion y filtro opcional `status`.
- `GET /download` descarga solo jobs `Succeeded`; si sigue en cola o proceso responde `409 REPORT_EXPORT_JOB_NOT_READY`, y si expiro responde `410 REPORT_EXPORT_JOB_EXPIRED`.
- `POST /cancel` cancela jobs `Queued` o `Running`; jobs ya finalizados conservan su estado.
- La respuesta de estado no devuelve filtros crudos ni payload sensible; solo metadata operacional como recurso, formato, estado, fechas, intentos, filas, archivo y error ultimo.

Errores relevantes:

- `REPORT_EXPORT_TOO_LARGE`: `413`, overflow en export sincrono.
- `REPORT_EXPORT_LIMIT_EXCEEDED`: `413`, overflow en job asincrono o diagrama.
- `REPORT_EXPORT_JOB_NOT_FOUND`: `404`.
- `REPORT_EXPORT_JOB_NOT_READY`: `409`.
- `REPORT_EXPORT_JOB_EXPIRED`: `410`.
- `REPORT_EXPORT_STORAGE_NOT_CONFIGURED`: `503`.

## 5. Profundizacion por modulo

La profundizacion modular se ira completando por iteraciones. Esta version ya documenta varios modulos base del sistema y sigue avanzando por bloques funcionales.

### 5.1 Authorization

#### 5.1.1 Alcance

Este bloque cubre la administracion de acceso del tenant activo e incluye:

- `CompanyUsersController` con base `/api/company/users`
- `AccountCompaniesController` para `access-context`, `role-builder-catalog` y `resource-policies`
- `AccountCompanyAuthorizationController` con base `/api/account/companies/{companyPublicId}/authorization`

`CompanyUsersController` sigue resolviendo el lifecycle operativo del usuario del tenant. La administracion de roles, grants y policies ya no se publica como `IAM/RBAC` legacy, sino como superficie company-scoped de `authorization`.

#### 5.1.2 Proposito funcional en CLARIHR

El modulo de autorizacion sirve para:

- administrar quien puede entrar y operar dentro de la compania activa
- resolver el `access-context` de la compania activa
- definir roles del tenant y sus grants
- exponer un `role-builder-catalog` filtrado para frontend
- resolver `resource-policies` para recursos sensibles
- separar claramente lifecycle de usuarios de la administracion de autorizacion

En la practica, `api/company/users` resuelve la administracion funcional de usuarios del tenant, mientras que `/api/account/companies/{companyPublicId}/authorization/*` resuelve el plano tecnico de roles, grants y policies.

#### 5.1.3 Modelo operativo y reglas transversales del modulo

- El lifecycle de usuarios (`/api/company/users`) sigue tenant-scoped implicito por el tenant activo del token despues de `company switch`.
- La nueva superficie de autorizacion es company-scoped explicita y exige `companyPublicId` en la ruta.
- `access-context` expone capacidades efectivas, modulos efectivos, roles, permisos y scopes del usuario para esa compania.
- `role-builder-catalog` devuelve solo el catalogo filtrado y util para la compania activa.
- Los roles de sistema (`IsSystemRole = true`) siguen protegidos y no pueden modificarse desde Core.
- `resource-policies` son responsabilidad del backend y se consultan por `resourceKey`.
- El tenant siempre debe conservar al menos un administrador activo en las operaciones que cambian roles o usuarios.

#### 5.1.4 Errores observables relevantes en authorization

- `UNAUTHENTICATED`: `401`, autenticacion requerida.
- `RBAC_DENIED`: `403`, el usuario autenticado no tiene permiso para la accion pedida.
- `TENANT_MISMATCH`: `403`, el recurso existe pero pertenece a otro tenant o no hay tenant valido para resolverlo.
- `FIELD_EDIT_FORBIDDEN`: `403`, la pantalla esta permitida pero uno o mas campos del payload no pueden editarse.
- `iam.roles.protected_role.forbidden` o `iam.roles.protected_role.delete_forbidden`: `403`, el rol es de sistema.
- `iam.roles.last_administrator_required` o `company_users.last_admin_required`: `409`, el cambio dejaria al tenant sin administrador activo.
- `iam.roles.name_conflict`: `409`, ya existe el valor unico dentro del tenant.
- `iam.roles.not_found`, `company_users.user.not_found`: `404`, el recurso no existe en el tenant actual.

#### 5.1.5 `CompanyUsersController` - usuarios operativos del tenant

Base route: `/api/company/users`

Usar este controlador cuando el frontend necesita administrar usuarios reales de la compania activa: invitarlos, cambiarles rol, desactivarlos o reenviar su invitacion. Es el entry point funcional para administracion de usuarios del tenant.

Contratos principales:

- `CompanyUserSummaryResponse`: `id`, `email`, `firstName`, `lastName`, `roleId`, `role`, `status`
- `CompanyUserResponse`: mismo shape para detalle/escritura
- `CompanyUserInvitationResponse`: `{ user, invitationExpiresUtc }`

Reglas comunes:

- Todas las rutas exigen `RBAC_USERS` con la accion correspondiente.
- Las lecturas y respuestas de escritura se filtran por field permissions; algunos campos pueden salir ocultos o enmascarados.
- El campo `status` usa `UserStatus` (`Active`, `Inactive`, `PendingActivation`).
- Este bloque sincroniza el usuario funcional (`User`), la membresia a compania y su proyeccion IAM (`IamUser`).

##### `GET /api/company/users`

- Proposito: listar usuarios de la compania activa.
- Autorizacion: `RBAC_USERS:Read`.
- Query: `page`, `pageSize`, `status`, `roleId`, `search`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `roleId` no puede ser `Guid.Empty`, `search` maximo `100`.
- Response: `PagedResponse<CompanyUserSummaryResponse>`.
- Observaciones: la lista ya sale filtrada por permisos de campo del usuario autenticado.

##### `GET /api/company/users/{userPublicId}`

- Proposito: obtener un usuario operativo puntual de la compania activa por `userPublicId`.
- Autorizacion: `RBAC_USERS:Read`.
- Response: `CompanyUserResponse`.
- Observaciones: mantiene tenant scope implicito y el campo `id` del response representa el `publicId` externo del usuario.

##### `POST /api/company/users`

- Proposito: invitar un usuario a la compania activa y asignarle un rol inicial.
- Autorizacion: `RBAC_USERS:Create`.
- Request body: `email`, `firstName`, `lastName`, `roleId`.
- Response: `201 Created` con `CompanyUserInvitationResponse`.
- Errores relevantes: `company_users.role.not_found`, `company_users.user_already_in_company`, `company_users.user_in_another_company`, `FIELD_EDIT_FORBIDDEN`.
- Observaciones: si el email ya existe como usuario local reutiliza el usuario; si no existe, crea un usuario invitado local, crea membresia, sincroniza `IamUser`, genera token de invitacion, revoca invitaciones activas previas para esa combinacion usuario/compania y envia correo.

##### `PUT /api/company/users/{userId}`

- Proposito: actualizar nombre/apellido del usuario y cambiar su rol dentro de la compania activa.
- Autorizacion: `RBAC_USERS:Update`.
- Request body: `firstName`, `lastName`, `roleId`.
- Response: `200 OK` con `CompanyUserResponse`.
- Errores relevantes: `company_users.role.not_found`, `company_users.last_admin_required`, `FIELD_EDIT_FORBIDDEN`, `TENANT_MISMATCH`.
- Observaciones: la autorizacion por campo se evalua solo para los campos realmente cambiados; tambien sincroniza el `IamUser` asociado para que el rol efectivo del tenant quede consistente.

##### `PATCH /api/company/users/{userId}/deactivate`

- Proposito: desactivar al usuario en la compania activa.
- Autorizacion: `RBAC_USERS:Update`.
- Response: `200 OK` con `CompanyUserResponse`.
- Errores relevantes: `company_users.last_admin_required`, `TENANT_MISMATCH`.
- Observaciones: desactiva el usuario funcional, la membresia y el `IamUser` vinculado; ademas revoca refresh tokens con razon `company-user-deactivated`.

##### `PATCH /api/company/users/{userId}/reactivate`

- Proposito: reactivar al usuario en la compania activa.
- Autorizacion: `RBAC_USERS:Update`.
- Response: `200 OK` con `CompanyUserResponse`.
- Errores relevantes: `TENANT_MISMATCH`, `company_users.user.not_found`.
- Observaciones: reactiva membresia y vuelve a marcar activo el `IamUser` si el estado funcional del usuario queda `Active`.

##### `POST /api/company/users/{userId}/reset-invitation`

- Proposito: emitir una nueva invitacion para un usuario pendiente o reenviable.
- Autorizacion: `RBAC_USERS:Update`.
- Response: `200 OK` con `CompanyUserInvitationResponse`.
- Errores relevantes: `company_users.reset_invitation.external_user_not_supported`, `TENANT_MISMATCH`.
- Observaciones: solo aplica a usuarios `Local`; revoca tokens de invitacion previos, emite uno nuevo y envia correo con `CompanyUserInvitationEmailKind.ResetInvitation`.

#### 5.1.6 `AccountCompanyAuthorizationController` - roles y grants del tenant

Base route: `/api/account/companies/{companyPublicId}/authorization`

Este controlador administra el catalogo operativo de roles del tenant, sus grants y la asignacion de roles a usuarios, siempre en el contexto de una compania explicita.

Contratos principales:

- `AuthorizationRoleSummaryResponse`: `id`, `name`, `description`, `isSystemRole`, `grantCount`, `userCount`
- `AuthorizationRoleResponse`: `id`, `name`, `description`, `isSystemRole`, `userCount`, `grants[]`
- `AuthorizationRoleGrantsResponse`: `roleId`, `roleName`, `isSystemRole`, `grants[]`
- `AuthorizationUserRolesResponse`: `userId`, `email`, `firstName`, `lastName`, `isActive`, `roles[]`

Reglas comunes:

- CRUD base usa la superficie `authorization/roles`.
- Los grants se sincronizan por `permissionIds` del catalogo filtrado visible para esa compania.
- Los nombres de rol son unicos por tenant.
- Los roles de sistema no pueden modificarse ni borrarse.
- El sistema no permite dejar al tenant sin administrador activo.

##### `GET /api/account/companies/{companyPublicId}/authorization/roles`

- Proposito: listar roles del tenant para la compania indicada.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Query: `pageNumber`, `pageSize`, `search`, `includeAllowedActions`.
- Response: `PagedResponse<AuthorizationRoleSummaryResponse>`.

##### `POST /api/account/companies/{companyPublicId}/authorization/roles`

- Proposito: crear un rol nuevo con permisos iniciales opcionales.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Request body: `name`, `description`, `permissionPublicIds`.
- Compatibilidad: tambien acepta `permissionIds` en integraciones legacy de frontend.
- Response: `201 Created` con `AuthorizationRoleResponse`.
- Errores relevantes: `iam.roles.name_conflict`, `iam.permissions.collection_not_found`.

##### `GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`

- Proposito: obtener el detalle de un rol.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Response: `AuthorizationRoleResponse`.
- Errores relevantes: `iam.roles.not_found`, `TENANT_MISMATCH`.

##### `PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`

- Proposito: actualizar nombre y descripcion del rol.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Request body: `name`, `description`.
- Response: `AuthorizationRoleResponse`.
- Errores relevantes: `iam.roles.name_conflict`, `iam.roles.protected_role.forbidden`, `TENANT_MISMATCH`.

##### `DELETE /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}`

- Proposito: eliminar un rol.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Response: `204 No Content`.
- Errores relevantes: `iam.roles.protected_role.delete_forbidden`, `iam.roles.in_use`, `TENANT_MISMATCH`.
- Observaciones: no se puede borrar un rol que aun tenga usuarios asignados.

##### `GET /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`

- Proposito: obtener el set actual de grants asignados al rol.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Response: `AuthorizationRoleGrantsResponse`.

##### `PUT /api/account/companies/{companyPublicId}/authorization/roles/{rolePublicId}/grants`

- Proposito: sincronizar el set de grants asignados al rol por `permissionPublicId`.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Request body: `{ "permissionPublicIds": [...] }`.
- Compatibilidad: tambien acepta `{ "permissionIds": [...] }` para clientes legacy.
- Response: `AuthorizationRoleGrantsResponse`.
- Errores relevantes: `iam.permissions.collection_not_found`, `iam.roles.protected_role.forbidden`, `iam.roles.last_administrator_required`.

##### `PUT /api/account/companies/{companyPublicId}/authorization/users/{userPublicId}/roles`

- Proposito: sincronizar el conjunto completo de roles asignados a un usuario dentro de la compania activa.
- Autorizacion: tenant activo coincidente con `companyPublicId`.
- Request body: `{ "rolePublicIds": [...] }`.
- Compatibilidad: tambien acepta `{ "roleIds": [...] }` para clientes legacy.
- Response: `AuthorizationUserRolesResponse`.
- Errores relevantes: `iam.roles.collection_not_found`, `iam.roles.last_administrator_required`, `TENANT_MISMATCH`.

#### 5.1.7 `AccountCompaniesController` - contexto y catalogos de autorizacion

Rutas relevantes:

- `GET /api/account/companies/{companyPublicId}/access-context`
- `GET /api/account/companies/{companyPublicId}/authorization/role-builder-catalog`
- `GET /api/account/companies/{companyPublicId}/authorization/resource-policies/{resourceKey}`

Comportamiento observable:

- `access-context` devuelve el contexto de acceso efectivo para la compania solicitada.
- `role-builder-catalog` devuelve el catalogo que el frontend puede usar para construir roles del tenant.
- `resource-policies` expone la policy resuelta por recurso sensible para consumo de frontend.

#### 5.1.8 `AuditController` - auditoria funcional del tenant

Base route: `/api/audit/logs`

Este controlador expone la auditoria funcional del tenant activo para entidades del negocio y de seguridad. Aqui el sujeto principal es la entidad auditada y no la configuracion de grants o policies.

Contratos principales:

- `PagedResponse<AuditLogSummaryResponse>` para listado
- `AuditLogDetailResponse` para detalle

Reglas comunes:

- Todas las rutas usan el recurso `AUDIT_LOGS` con `Read`.
- El listado es tenant-scoped implicito y nunca mezcla registros de otro tenant.
- Los identificadores publicos siguen el estandar transversal del backend: cuando se filtra por GUID publico se usa `EntityPublicId`, no `EntityId`.
- `EntityType` acepta los valores normalizados del catalogo de auditoria, por ejemplo `User`, `Role`, `JobProfile`, `OrgUnit`, `PositionSlot`, `CostCenter`.
- `totalCount` se calcula despues de aplicar todos los filtros server-side, incluido `EntityPublicId`, para que la paginacion del frontend quede alineada con la misma consulta.
- Por compatibilidad, el backend tambien acepta `ActorUserId` y `EntityId` en query string y los normaliza al contrato publico `ActorUserPublicId` y `EntityPublicId` antes de consultar.

##### `GET /api/audit/logs`

- Proposito: listar logs de auditoria del tenant activo.
- Autorizacion: `AUDIT_LOGS:Read`.
- Query: `fromUtc`, `toUtc`, `ActorUserPublicId`, `EntityPublicId`, `entityType`, `eventType`, `search`, `page`, `pageSize`.
- Compatibilidad: tambien se aceptan `ActorUserId` y `EntityId` como aliases legacy del query string.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `ActorUserPublicId`/`ActorUserId` y `EntityPublicId`/`EntityId` no pueden ser `Guid.Empty`, `entityType` y `eventType` deben existir en sus catalogos, `fromUtc <= toUtc`.
- Response: `PagedResponse<AuditLogSummaryResponse>`.
- Observaciones: `EntityPublicId` filtra por el sujeto auditado; combinarlo con `entityType` evita ambiguedad entre tipos distintos que pudieran compartir el mismo GUID publico en ambientes sinteticos o seeds. Si se reciben aliases legacy, el backend los normaliza al mismo filtro efectivo antes de ejecutar la consulta.

##### `GET /api/audit/logs/{publicId}`

- Proposito: obtener el detalle completo de un log de auditoria.
- Autorizacion: `AUDIT_LOGS:Read`.
- Response: `AuditLogDetailResponse`.
- Errores relevantes: `TENANT_MISMATCH`, `AUDIT_LOG_NOT_FOUND`.
- Observaciones: el detalle incluye `before`, `after`, `diff`, metadatos del actor y contexto tecnico (`ipAddress`, `userAgent`) cuando existen.

#### 5.1.9 Relacion entre superficies de usuarios y authorization

- `api/company/users` resuelve membresia, invitacion y lifecycle operativo del usuario dentro de una compania.
- `api/account/companies/{companyPublicId}/authorization/*` resuelve roles, grants, policies y asignacion de roles a usuarios.
- `api/account/companies/{companyPublicId}/access-context` resuelve el contexto efectivo de acceso.

No existe ya una superficie publica separada legacy para frontend Core fuera del espacio `authorization`.

### 5.2 Auth

#### 5.2.1 Alcance

Este bloque cubre la autenticacion publica y la gestion de sesiones iniciales a traves de `AuthController`, con base `/api/auth`.

Incluye:

- `POST /api/auth/register`
- `POST /api/auth/external`
- `POST /api/auth/login`
- `POST /api/auth/refresh`
- `POST /api/auth/company-user-invitations/accept`
- `POST /api/auth/logout`

#### 5.2.2 Proposito funcional en CLARIHR

El modulo `Auth` sirve para:

- registrar cuentas locales
- autenticar usuarios locales
- autenticar o registrar usuarios via proveedor externo
- emitir y rotar pares `access token` + `refresh token`
- activar usuarios de compania invitados con contrasena temporal
- cerrar sesion invalidando tokens de refresco

Este modulo no administra companias ni permisos RBAC. Su responsabilidad termina en emitir identidad autenticada. El contexto tenant llega despues, cuando el usuario ya tiene membresias y selecciona compania activa.

#### 5.2.3 Modelo operativo y reglas transversales del modulo

- `register`, `external`, `login`, `refresh` y `company-user-invitations/accept` son publicos (`AllowAnonymous`).
- `logout` requiere `Bearer` valido.
- La respuesta canonica del modulo es `AuthResponse`: `accessToken`, `refreshToken`, `expiresIn`, `user`.
- `user` devuelve `id`, `email`, `firstName`, `lastName` y `authProvider`.
- El contrato permite `refreshToken` nulo, pero la implementacion actual con `JwtTokenService` emite refresh token en `register`, `external`, `login` y `refresh`.
- El `access token` siempre incluye identidad basica (`sub`, email, nombre, `auth_provider`, `user_status`). Cuando el token queda asociado a un tenant, tambien incluye `tid` y el claim `role` tenant-scoped con el nombre normalizado del rol activo para esa compania.
- Un usuario recien registrado que aun no tiene compania recibe token sin tenant activo.
- `login` solo permite usuarios `Local` y `Active`.
- `company-user-invitations/accept` solo aplica a usuarios `Local` pendientes de activacion con un token de invitacion activo.
- `external` hoy solo soporta `Google` en runtime; `Microsoft` y `Apple` existen en el enum `AuthProvider`, pero hoy no tienen provider service configurado.
- `refresh` rota el refresh token en cada uso valido. Si detecta reutilizacion de un token ya rotado, revoca la familia completa.
- `company-user-invitations/accept` reemplaza la contrasena temporal, activa la cuenta local, reactiva la membresia tenant-scoped y devuelve una sesion ya emitida para la compania de la invitacion.
- `logout` revoca todos los refresh tokens del usuario autenticado, no solo uno.

#### 5.2.4 Errores observables relevantes en Auth

- `auth.user_already_exists`: `409`, el email ya existe.
- `auth.login.invalid_credentials`: `401`, email/password invalidos o usuario local no elegible para login.
- `auth.external.invalid_token`: `401`, el `idToken` externo no pudo validarse.
- `auth.external.email_missing`: `422`, el proveedor externo no devolvio email.
- `auth.external.provider_not_supported`: `400`, se pidio un proveedor externo que el backend no soporta.
- `auth.external.provider_configuration_invalid`: `500`, la configuracion del proveedor externo es invalida.
- `auth.external.provider_link_conflict`: `409`, el usuario ya esta vinculado a otro proveedor externo.
- `auth.external.email_link_not_allowed`: `409`, el backend no puede autovincular por email el usuario existente.
- `auth.user_not_active`: `401`, la cuenta existe pero no esta activa para flujo externo.
- `auth.refresh.invalid_token`: `401`, el refresh token es invalido, expiro o fue revocado.
- `auth.invitation.invalid_token`: `401`, el token de invitacion no existe, expiro, fue revocado o ya se uso.
- `auth.logout.invalid_current_user`: `401`, el contexto autenticado no puede resolverse a un usuario valido.
- `auth.token_configuration_invalid`: `500`, JWT no esta configurado correctamente.

#### 5.2.5 `AuthController`

Base route: `/api/auth`

Contratos principales:

- `RegisterUserRequest`: `firstName`, `lastName`, `email`, `password`, `country`, `source`
- `RegisterExternalRequest`: `provider`, `idToken`, `country`, `source`
- `LoginRequest`: `email`, `password`
- `RefreshTokenRequest`: `refreshToken`
- `AcceptCompanyUserInvitationRequest`: `token`, `password`
- `AuthResponse`: `accessToken`, `refreshToken`, `expiresIn`, `user`

##### `POST /api/auth/register`

- Proposito: registrar una cuenta local nueva y devolver sesion autenticada.
- Autenticacion: publica.
- Request body: `firstName`, `lastName`, `email`, `password`, `country`, `source`.
- Validaciones: nombres maximo `100`; email valido maximo `320`; password entre `12` y `100` con mayuscula, minuscula, numero y caracter especial, sin espacios y sin incluir nombre, apellido o correo del usuario; `country` y `source` tienen regex controlada.
- Response: `201 Created` con `AuthResponse`.
- Errores relevantes: `common.validation`, `auth.user_already_exists`.
- Observaciones: hace hash del password, crea `User` local, guarda en transaccion y emite un par de tokens inmediatamente.

##### `POST /api/auth/external`

- Proposito: autenticar via proveedor externo y registrar o vincular usuario segun corresponda.
- Autenticacion: publica.
- Request body: `provider`, `idToken`, `country`, `source`.
- Validaciones: `provider` no puede ser `Local`; `idToken` obligatorio y maximo `8000`.
- Response: `201 Created` con `AuthResponse` si el usuario se crea en ese flujo; `200 OK` con `AuthResponse` si el usuario ya existia y solo se autentico/vinculo.
- Errores relevantes: `auth.external.invalid_token`, `auth.external.email_missing`, `auth.external.provider_not_supported`, `auth.external.provider_configuration_invalid`, `auth.external.provider_link_conflict`, `auth.external.email_link_not_allowed`, `auth.user_not_active`.
- Observaciones: hoy valida Google ID tokens; si el usuario no existe se crea como externo; si existe y esta activo puede vincularse automaticamente por email solo cuando el proveedor lo permite, por ejemplo email verificado de Gmail o hosted domain en Google Workspace.

##### `POST /api/auth/login`

- Proposito: autenticar un usuario local con email y password.
- Autenticacion: publica.
- Request body: `email`, `password`.
- Validaciones: email valido maximo `320`; password obligatorio maximo `100`.
- Response: `200 OK` con `AuthResponse`.
- Errores relevantes: `auth.login.invalid_credentials`.
- Observaciones: devuelve el mismo error para email inexistente, usuario inactivo, proveedor no local o password incorrecto.

##### `POST /api/auth/refresh`

- Proposito: rotar la sesion usando un refresh token valido.
- Autenticacion: publica.
- Request body: `refreshToken`.
- Validaciones: `refreshToken` obligatorio y maximo `2048`.
- Response: `200 OK` con `AuthResponse`.
- Errores relevantes: `auth.refresh.invalid_token`, `auth.token_configuration_invalid`.
- Observaciones: cada refresh exitoso invalida el refresh token anterior y emite uno nuevo; si se intenta reutilizar un token ya rotado, el backend revoca toda la familia de refresh tokens asociada.

##### `POST /api/auth/company-user-invitations/accept`

- Proposito: activar un usuario de compania invitado y definir su contrasena final.
- Autenticacion: publica.
- Request body: `token`, `password`.
- Validaciones: `token` obligatorio maximo `500`; `password` usa la misma politica fuerte del registro local.
- Response: `200 OK` con `AuthResponse`.
- Errores relevantes: `common.validation`, `auth.invitation.invalid_token`, `auth.token_configuration_invalid`.
- Observaciones: el backend marca el token como usado, activa al usuario local e `IamUser` tenant-scoped asociado, reactiva la membresia y devuelve una sesion ya contextualizada al tenant de la invitacion.

##### `POST /api/auth/logout`

- Proposito: cerrar la sesion del usuario autenticado.
- Autenticacion: `Bearer` requerido.
- Request body: ninguno.
- Response: `204 No Content`.
- Errores relevantes: `auth.logout.invalid_current_user`.
- Observaciones: revoca todos los refresh tokens del usuario actual con motivo `logout`; no devuelve body.

### 5.3 Account companies

#### 5.3.1 Alcance

Este bloque cubre la administracion de companias propias del usuario autenticado mediante `AccountCompaniesController`, con base `/api/account/companies`.

Incluye:

- `GET /api/account/companies/company-types`
- `GET /api/account/companies/legal-representative-representation-types`
- `GET /api/account/companies`
- `GET /api/account/companies/{companyId}`
- `POST /api/account/companies`
- `PUT /api/account/companies/{companyId}`
- `PATCH /api/account/companies/{companyId}/archive`
- `PATCH /api/account/companies/{companyId}/reactivate`
- `POST /api/account/companies/{companyId}/switch`

#### 5.3.2 Proposito funcional en CLARIHR

El modulo `Account companies` sirve para:

- resolver catalogos necesarios antes de crear compania cuando aun no existe contexto tenant
- listar las companias creadas por la cuenta autenticada
- crear una nueva compania con provisionamiento inicial
- consultar y actualizar metadata basica de una compania propia
- archivar o reactivar companias propias
- cambiar el contexto tenant activo y recibir un nuevo token con ese `tid`

Este modulo es el puente entre `Auth` y `api/v1`. Primero autentica la cuenta, luego crea o selecciona una compania, y a partir de ese cambio de contexto el usuario puede operar los modulos tenant-scoped.

#### 5.3.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El scope aqui es por ownership del usuario autenticado, no por RBAC tenant-scoped.
- Las consultas y mutaciones principales solo operan sobre companias cuyo `CreatedByUserPublicId` coincide con el usuario autenticado.
- `switch` agrega una regla adicional: ademas de ser owner, el usuario debe tener membresia activa en la compania destino.
- `list` y `detail` exponen `IsActiveContext`, calculado desde el `tenantId` actual del token.
- `create` no cambia automaticamente la compania activa ni devuelve tokens nuevos; solo provisiona y devuelve detalle de la nueva compania.
- `create` provisiona la compania con una suscripcion activa al plan de sistema `FREE`, crea representante legal inicial, siembra locations por pais, crea roles de sistema iniciales, genera permisos admin/matrix RBAC y vincula al owner como administrador con el rol `Admin de Empresa` sincronizado contra todos los permisos tenant-scoped existentes.
- `update` solo cambia `name` y `companyTypeId`; no cambia pais ni representantes legales.
- `archive` no permite archivar la compania activa ni la compania primaria actual del usuario.
- `reactivate` respeta el limite de companias activas/suspended permitido para la cuenta.
- `switch` marca la compania destino como primaria para el usuario y devuelve un nuevo par de tokens ya emitido para ese tenant.

#### 5.3.4 Errores observables relevantes en Account companies

- `account_companies.current_user.invalid`: `401`, el token no resuelve un usuario actual valido.
- `account_companies.user.not_found`: `404`, el usuario autenticado ya no existe en persistencia.
- `COMPANY_NOT_FOUND`: `404`, la compania pedida no existe.
- `COMPANY_OWNERSHIP_FORBIDDEN`: `403`, la compania existe pero no pertenece al usuario autenticado.
- `COMPANY_LIMIT_REACHED`: `409`, la cuenta no puede crear otra compania activa/suspended.
- `COMPANY_REACTIVATION_LIMIT_REACHED`: `409`, no puede reactivarse porque se excederia el limite de companias activas/suspended.
- `COMPANY_ALREADY_ARCHIVED`: `409`, la compania ya estaba archivada.
- `COMPANY_ALREADY_ACTIVE`: `409`, la compania ya estaba activa.
- `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN`: `409`, primero debe cambiarse la compania activa/primaria antes de archivar.
- `ACTIVE_COMPANY_SWITCH_FORBIDDEN`: `409`, la compania no puede convertirse en contexto activo.
- `COMPANY_TYPE_NOT_FOUND`: `404`, el tipo de compania seleccionado no existe o esta inactivo.
- `provisioning.country_not_found`: `400`, el `countryCode` enviado no existe en el catalogo global activo de paises.

#### 5.3.5 `AccountCompaniesController`

Base route: `/api/account/companies`

Contratos principales:

- `AccountCompanySummaryResponse`: `companyPublicId`, `name`, `slug`, `countryCode`, `status`, `planCode`, `isActiveContext`, `isOwnedByCurrentUser`, `createdAtUtc`, `companyType`
- `AccountCompanyDetailResponse`: agrega `modifiedAtUtc`, `activeLegalRepresentatives` y metadata completa del tipo de compania
- `SwitchActiveCompanyResponse`: `accessToken`, `refreshToken`, `expiresIn`, `activeCompany`
- `CountryCatalogItemResponse`: `id`, `code`, `name`, `sortOrder`
- `CompanyTypeCatalogItemResponse`: `id`, `code`, `name`, `description`, `sortOrder`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`
- `LegalRepresentativePositionTitleCatalogItemResponse`: `publicId`, `code`, `normalizedCode`, `name`, `sortOrder`
- `LegalRepresentativeRepresentationTypeCatalogItemResponse`: `publicId`, `code`, `normalizedCode`, `name`, `sortOrder`
- `CreateAccountCompanyRequest`: `name`, `countryCode`, `companyTypePublicId`, `initialLegalRepresentative`

##### `GET /api/account/companies/countries`

- Proposito: obtener el catalogo global activo de paises para el onboarding de companias.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<CountryCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; el frontend debe usar `code` como valor estable para `countryCode` al crear la compania y puede usar `name` solo para display.

##### `GET /api/account/companies/company-types`

- Proposito: obtener el catalogo global activo de tipos de compania permitido para el pais seleccionado durante el onboarding o la edicion basica de companias.
- Autenticacion: `Bearer` requerido.
- Query: `countryCode`.
- Validaciones: `countryCode` obligatorio, de `2` o `3` letras.
- Response: `IReadOnlyCollection<CompanyTypeCatalogItemResponse>`.
- Observaciones: devuelve solo items activos del pais solicitado; el catalogo es global por pais, no por tenant ni por owner. El frontend debe obtener primero `countryCode` desde `GET /api/account/companies/countries` y luego usar el `id` de este endpoint como `companyTypePublicId`.

##### `GET /api/account/companies/legal-representative-position-titles`

- Proposito: obtener el catalogo activo de cargos iniciales permitido para el representante legal durante la creacion de compania.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<LegalRepresentativePositionTitleCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; `code` es una clave estable de frontend y `name` es el valor string que debe enviarse en `initialLegalRepresentative.positionTitle` al crear la compania.

##### `GET /api/account/companies/legal-representative-representation-types`

- Proposito: obtener el catalogo activo de tipos de representacion permitido para el representante legal inicial durante la creacion de compania.
- Autenticacion: `Bearer` requerido.
- Response: `IReadOnlyCollection<LegalRepresentativeRepresentationTypeCatalogItemResponse>`.
- Observaciones: devuelve items globales, ordenados por `sortOrder`; `code` coincide con el valor string que debe enviarse en `initialLegalRepresentative.representationType` al crear la compania.

##### `GET /api/account/companies`

- Proposito: listar las companias propias del usuario autenticado.
- Autenticacion: `Bearer` requerido.
- Query: `status`, `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`.
- Response: `PagedResponse<AccountCompanySummaryResponse>`.
- Observaciones: no tiene busqueda libre; la lista se ordena por `name` y luego `companyPublicId`, y marca con `isActiveContext` la compania alineada al tenant del token actual.

##### `GET /api/account/companies/{companyPublicId}`

- Proposito: obtener el detalle de una compania propia.
- Autenticacion: `Bearer` requerido.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: devuelve solo representantes legales activos, ordenados con los marcados como primarios primero, mas metadata del tipo de compania si existe. `activeLegalRepresentatives[].isPrimary` puede venir en `true`, `false` o `null`; `null` indica que el registro fue creado sin marcar prioridad inicial.

##### `POST /api/account/companies`

- Proposito: crear una nueva compania para la cuenta autenticada.
- Autenticacion: `Bearer` requerido.
- Request body: `name`, `countryCode`, `companyTypePublicId`, `initialLegalRepresentative`.
- Validaciones base: `name` obligatorio maximo `150`; `countryCode` de `2` o `3` letras y existente en el catalogo global activo de paises; `companyTypePublicId` no puede ser `Guid.Empty` y, si se envia, debe pertenecer al catalogo global activo del pais seleccionado.
- Validaciones del representante inicial: `firstName`, `lastName`, `documentNumber`, `positionTitle`, `effectiveFromUtc`; `effectiveToUtc >= effectiveFromUtc`; `email` opcional maximo `320`; `phone` opcional maximo `40`; `isPrimary` es opcional.
- Response: `201 Created` con `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_LIMIT_REACHED`, `COMPANY_TYPE_NOT_FOUND`, `provisioning.country_not_found`.
- Observaciones: provisiona una suscripcion activa al plan `FREE`, crea representante legal inicial, crea rol de sistema `Admin de Empresa`, crea rol de sistema `Usuario Estandar`, vincula al owner como admin, sincroniza ese rol admin contra todos los permisos tenant-scoped existentes, siembra locations segun el pais y deja la nueva compania sin hacer auto-switch. `SV` conserva una plantilla estructurada de locations; el resto de paises recibe una jerarquia minima generica de un solo nivel para permitir el arranque del tenant. Para `countryCode`, el frontend debe usar el `code` devuelto por `GET /api/account/companies/countries`. Para `initialLegalRepresentative.documentType`, el frontend debe usar el `code` devuelto por `GET /api/v1/companies/{companyId}/reference-catalogs/identification-types`. Para `initialLegalRepresentative.representationType`, el frontend debe usar el `code` devuelto por `GET /api/account/companies/legal-representative-representation-types`. Para `initialLegalRepresentative.positionTitle`, el frontend debe usar el `name` devuelto por el endpoint de catalogo de cargos. Si `initialLegalRepresentative.isPrimary` se omite o se envia `null`, el registro se persiste con `isPrimary = null`.

##### `PUT /api/account/companies/{companyPublicId}`

- Proposito: actualizar nombre y tipo de compania de una compania propia.
- Autenticacion: `Bearer` requerido.
- Request body: `name`, `companyTypePublicId`.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `COMPANY_TYPE_NOT_FOUND`.
- Observaciones: no cambia pais, plan ni representantes legales; solo metadata basica. Si se envia `companyTypePublicId`, debe pertenecer al catalogo global activo del pais ya asociado a la compania.

#### 5.3.6 `AccountCompanySubscriptionsController`

Base route: `/api/account/companies/{publicId}/subscription`

Contratos principales:

- `AccountCompanySubscriptionOverviewResponse`: `companyPublicId`, `companyName`, `companySlug`, `planCode`, `currentPlan`, `activeAddons`, `effectiveModules`
- `AccountCompanySubscriptionPlanResponse`: `commercialPlanPublicId`, `code`, `normalizedCode`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `currentVersionNumber`, `currencyCode`, `moduleCount`, `moduleKeys`, `isCurrent`
- `AccountCompanySubscriptionAddonResponse`: `companyAddonPublicId`, `commercialAddonPublicId`, `code`, `normalizedCode`, `name`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `status`, `moduleCount`, `moduleKeys`
- `AccountCompanyEffectiveModuleResponse`: `moduleKey`, `displayName`, `description`, `source`, `grantedByPlan`, `grantedByAddon`
- `AccountCompanySubscriptionPlanPreviewResponse`: `companyPublicId`, `currentPlan`, `targetPlan`, `addedModuleKeys`, `removedModuleKeys`, `addonDeactivationWarnings`, `isEligible`, `ineligibilityReasons`
- `AccountCompanyMarketplaceAddonResponse`: `commercialAddonPublicId`, `code`, `normalizedCode`, `name`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `moduleCount`, `moduleKeys`, `isOwned`, `canAcquire`, `blockedReason`
- `AccountCompanyAddonChangePreviewResponse`: `companyPublicId`, `commercialAddonPublicId`, `addonCode`, `addonName`, `action`, `addedModuleKeys`, `removedModuleKeys`, `isEligible`, `ineligibilityReasons`, `warnings`

##### `GET /api/account/companies/{publicId}/subscription`

- Proposito: obtener la vista comercial efectiva de una compania owned por el usuario autenticado.
- Autenticacion: `Bearer` requerido con token `core`.
- Response: `AccountCompanySubscriptionOverviewResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`.
- Observaciones: `effectiveModules` es la union viva entre modulos del plan activo y modulos de add-ons activos; `source` puede ser `plan`, `addon` o `plan+addon`.

##### `GET /api/account/companies/{publicId}/subscription/plans`

- Proposito: listar los planes comerciales activos visibles para el owner.
- Autenticacion: `Bearer` requerido con token `core`.
- Response: `IReadOnlyCollection<AccountCompanySubscriptionPlanResponse>`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`.
- Observaciones: incluye `FREE` para downgrade controlado, expone `moduleKeys` resueltos para UI y oculta `MASTER` salvo que el owner autenticado tambien tenga un `PlatformOperator` activo.

##### `POST /api/account/companies/{publicId}/subscription/preview`

- Proposito: previsualizar un cambio inmediato de plan iniciado por el owner.
- Autenticacion: `Bearer` requerido con token `core`.
- Request body: `commercialPlanPublicId`.
- Response: `AccountCompanySubscriptionPlanPreviewResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE`, `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN`.
- Observaciones: devuelve diffs de modulos agregados/removidos, advierte si un downgrade a `FREE` desactivaria add-ons activos y rechaza `MASTER` para owners que no sean operadores activos de CLARI.

##### `PUT /api/account/companies/{publicId}/subscription`

- Proposito: aplicar de inmediato un cambio de plan iniciado por el owner.
- Autenticacion: `Bearer` requerido con token `core`.
- Request body: `commercialPlanPublicId`, `observations`.
- Response: `AccountCompanySubscriptionOverviewResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_MISSING_LEGAL_REPRESENTATIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_MISSING_ADMINISTRATOR`, `ACCOUNT_COMPANY_SUBSCRIPTION_MASTER_FORBIDDEN`.
- Observaciones: un downgrade a `FREE` desactiva en la misma transaccion todos los add-ons activos de la empresa; `MASTER` solo puede aplicarse desde owner cuando el actor tambien es `PlatformOperator` activo.

##### `GET /api/account/companies/{publicId}/subscription/addons`

- Proposito: listar los add-ons activos que el owner ya tiene adquiridos para su compania.
- Autenticacion: `Bearer` requerido con token `core`.
- Response: `IReadOnlyCollection<AccountCompanySubscriptionAddonResponse>`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`.

##### `GET /api/account/companies/{publicId}/subscription/addons/marketplace`

- Proposito: listar el marketplace de add-ons disponible para la compania del owner.
- Autenticacion: `Bearer` requerido con token `core`.
- Response: `IReadOnlyCollection<AccountCompanyMarketplaceAddonResponse>`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: cuando el plan activo es `FREE`, `canAcquire=false` y `blockedReason` explica el bloqueo comercial.

##### `POST /api/account/companies/{publicId}/subscription/addons/preview`

- Proposito: previsualizar la activacion o desactivacion inmediata de un add-on desde el marketplace owner.
- Autenticacion: `Bearer` requerido con token `core`.
- Request body: `commercialAddonPublicId`, `action`.
- Response: `AccountCompanyAddonChangePreviewResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_FORBIDDEN_FOR_FREE_PLAN`.
- Observaciones: expone diffs de modulos y las razones de inelegibilidad antes de aplicar el cambio.

##### `POST /api/account/companies/{publicId}/subscription/addons`

- Proposito: aplicar de inmediato una activacion o desactivacion de add-on iniciada por el owner.
- Autenticacion: `Bearer` requerido con token `core`.
- Request body: `commercialAddonPublicId`, `action`, `observations`.
- Response: `AccountCompanySubscriptionOverviewResponse`.
- Errores relevantes: `COMPANY_NOT_FOUND`, `COMPANY_OWNERSHIP_FORBIDDEN`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_FORBIDDEN_FOR_FREE_PLAN`.
- Observaciones: si la activacion procede, los modulos del add-on aparecen inmediatamente dentro de `effectiveModules`.

#### 5.3.7 `AccountInternalCatalogsController`

Base route: `/api/account/internal-catalogs`

Contratos principales:

- `InternalCatalogDefinitionResponse`: `context`, `identifier`, `label`, `renderType`, `catalogKey`, `allowCreate`, `minQueryLength`
- `InternalCatalogValueSuggestionResponse`: `id`, `value`, `score`
- `CreateInternalCatalogValueRequest`: `value`

##### `GET /api/account/internal-catalogs?context=job-profile.requirements`

- Proposito: devolver el manifest de catalogos internos globales que el frontend puede usar para renderizar campos `search`, `select` o `freeText`.
- Autenticacion: `Bearer` requerido con token `core`.
- Response: `IReadOnlyCollection<InternalCatalogDefinitionResponse>`.
- Errores relevantes: `UNAUTHENTICATED`, `internal_catalogs.context_not_found`.
- Observaciones: esta superficie no requiere `tenantId` activo ni ownership de compania; hoy publica el contexto `job-profile.requirements` con `Education`, `Knowledge` y `Certification` como `search`, mientras `Experience` y `Other` siguen en `freeText`.

##### `GET /api/account/internal-catalogs/{catalogKey}/values?q=...&limit=...`

- Proposito: buscar sugerencias globales por similitud dentro de un catalogo interno puntual.
- Autenticacion: `Bearer` requerido con token `core`.
- Response: `IReadOnlyCollection<InternalCatalogValueSuggestionResponse>`.
- Errores relevantes: `UNAUTHENTICATED`, `internal_catalogs.catalog_key_not_found`.
- Observaciones: la busqueda usa `normalized_value`, threshold minimo `0.70`, ordena por exact match, prefijo, similitud, uso y nombre, y no separa resultados por tenant o compania.

##### `POST /api/account/internal-catalogs/{catalogKey}/values`

- Proposito: crear un nuevo valor global dentro de un catalogo `search` cuando el usuario autenticado no encuentra una opcion util.
- Autenticacion: `Bearer` requerido con token `core`.
- Request body: `value`.
- Response: `InternalCatalogValueSuggestionResponse`.
- Errores relevantes: `UNAUTHENTICATED`, `internal_catalogs.catalog_key_not_found`, `internal_catalogs.create_not_allowed`, `internal_catalogs.similar_value_conflict`.
- Observaciones: si el valor ya existe por coincidencia exacta se reutiliza y responde `200`; si es nuevo responde `201`; si existe otra fila con similitud `>= 0.90`, la API responde `409` y adjunta `suggestions` en el `ProblemDetails` para evitar duplicados casi identicos.

##### `PATCH /api/account/companies/{companyPublicId}/archive`

- Proposito: archivar una compania propia.
- Autenticacion: `Bearer` requerido.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_ALREADY_ARCHIVED`, `ACTIVE_COMPANY_ARCHIVE_FORBIDDEN`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: no puede archivarse si la compania coincide con el `tenantId` actual del token o con la compania primaria actual del usuario.

##### `PATCH /api/account/companies/{companyPublicId}/reactivate`

- Proposito: reactivar una compania propia archivada.
- Autenticacion: `Bearer` requerido.
- Response: `AccountCompanyDetailResponse`.
- Errores relevantes: `COMPANY_ALREADY_ACTIVE`, `COMPANY_REACTIVATION_LIMIT_REACHED`, `COMPANY_OWNERSHIP_FORBIDDEN`.
- Observaciones: la reactivacion vuelve a estado `Active`, pero no cambia automaticamente el tenant activo del usuario.

##### `PATCH /api/account/companies/{companyPublicId}/switch`

- Proposito: cambiar la compania activa del usuario autenticado.
- Autenticacion: `Bearer` requerido.
- Request body: ninguno.
- Response: `200 OK` con `SwitchActiveCompanyResponse`.
- Errores relevantes: `ACTIVE_COMPANY_SWITCH_FORBIDDEN`, `COMPANY_OWNERSHIP_FORBIDDEN`, `COMPANY_NOT_FOUND`.
- Observaciones: solo funciona si la compania destino esta `Active` y el usuario tiene membresia activa en ella; marca esa membresia como primaria, emite un nuevo token con `tid` de la compania destino y el claim `role` correspondiente a esa membresia, y devuelve `activeCompany` con `companyPublicId`, `name`, `slug`, `countryCode` y `status`.

#### 5.3.6 Relacion con `Auth` y onboarding

- `POST /api/auth/register` crea la cuenta y devuelve identidad autenticada, normalmente aun sin tenant.
- `GET /api/account/companies/countries` resuelve el catalogo global de paises requerido por el formulario.
- `GET /api/account/companies/company-types` resuelve el catalogo global de tipos de compania filtrado por pais.
- `GET /api/v1/companies/{companyId}/reference-catalogs/identification-types` resuelve el catalogo de tipos documentales desde la fuente unificada por pais de compania.
- `GET /api/account/companies/legal-representative-position-titles` resuelve el catalogo de cargos requerido por el formulario.
- `POST /api/account/companies` crea la primera o siguiente compania propiedad de esa cuenta.
- `PATCH /api/account/companies/{companyPublicId}/switch` emite el token ya tenant-scoped para operar `api/v1`.

Esa secuencia explica por que `Account companies` no usa `/api/v1`: todavia esta resolviendo el paso previo al contexto tenant estable del resto del sistema.

### 5.3A Platform backoffice

#### 5.3A.1 Alcance

Este bloque cubre la administracion global de plataforma mediante:

Incluye:

- `POST /api/platform/auth/login`
- `POST /api/platform/auth/refresh`
- `POST /api/platform/auth/logout`
- `GET /api/platform/commercial-addons`
- `GET /api/platform/commercial-addons/{publicId}`
- `POST /api/platform/commercial-addons`
- `PUT /api/platform/commercial-addons/{publicId}`
- `PATCH /api/platform/commercial-addons/{publicId}/activate`
- `PATCH /api/platform/commercial-addons/{publicId}/inactivate`
- `GET /api/platform/commercial-plans`
- `GET /api/platform/commercial-plans/{publicId}`
- `POST /api/platform/commercial-plans`
- `PUT /api/platform/commercial-plans/{publicId}`
- `PATCH /api/platform/commercial-plans/{publicId}/activate`
- `PATCH /api/platform/commercial-plans/{publicId}/inactivate`
- `GET /api/platform/companies/{companyPublicId}/subscription`
- `GET /api/platform/companies/{companyPublicId}/subscriptions`
- `PUT /api/platform/companies/{companyPublicId}/subscription`

#### 5.3A.2 Proposito funcional en CLARIHR

El backoffice de plataforma sirve para:

- aislar las capacidades globales del plano tenant-scoped de RH
- autenticar operadores globales con un token propio de plataforma
- definir el catalogo comercial global de add-ons que luego podran activarse por empresa
- definir el catalogo comercial base que luego podran consumir las futuras suscripciones a empresas
- estandarizar el `fee` mensual base y el precio por empleado activo por plan
- estandarizar el pricing comercial de add-ons masivos y especializados sin acoplar todo el catalogo al cobro por empleado activo
- mantener limites incluidos por plan de forma reutilizable
- administrar el estado operativo de planes y add-ons (`Draft`, `Active`, `Inactive`)
- reemplazar la suscripcion activa de una empresa manteniendo historial
- preservar un plan canonico `FREE` alineado al provisioning actual y al provisioning inicial de companias

No resuelve aun versionamiento de precios, activacion de add-ons por empresa, descuentos corporativos, billing real ni calculo de cobros.

#### 5.3A.3 Modelo operativo y reglas transversales del modulo

- `login` y `refresh` son publicos dentro del backoffice, pero solo emiten tokens si el usuario local esta ligado a un `PlatformOperator` activo.
- Todas las demas rutas requieren `Bearer` emitido con `client_type=platform`; esos tokens no incluyen `tid`.
- La autorizacion global no reutiliza `platform_admin`, allow-lists de correo ni RBAC tenant-scoped; depende de `PlatformOperator` y sus roles `Admin` o `ReadOnly`.
- `ReadOnly` puede consultar add-ons, planes y suscripciones; `Admin` puede crear, actualizar, activar, inactivar y reemplazar suscripciones.
- `list` de add-ons soporta paginacion, filtros por `type`, `billingModel`, `status` y busqueda libre `q` sobre codigo y nombre.
- `create` de add-on registra `code`, `name`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity` y `status`.
- `update` de add-on modifica datos comerciales editables; no actualiza `status`.
- `list` de planes soporta paginacion, filtro por `status` y busqueda libre `q` sobre codigo y nombre.
- `create` registra `code`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `status`, `moduleKeys` y `limits`.
- `update` reemplaza completamente `moduleKeys` y `limits`; no actualiza `status`.
- Las transiciones de estado se hacen solo con `activate` e `inactivate`.
- `FREE` existe sembrado como `system plan`, con precios `0`, lista de limites vacia y el mismo catalogo modular completo que `MASTER` en la semilla actual.
- `MASTER` existe sembrado como `system plan` interno, con precios `0`, lista de limites vacia y todos los modulos comerciales conocidos.
- Los planes de sistema no pueden cambiar `code` o `name`, y tampoco pueden inactivarse.
- Si se intenta actualizar `MASTER` con un subconjunto de `moduleKeys`, el backend persiste igualmente el catalogo completo.
- La suscripcion activa de una empresa nunca se edita in-place: el backoffice cancela la fila activa y crea una nueva con snapshot de `planCode`, `planName`, `baseMonthlyFee` y `pricePerActiveEmployee`.
- `PlanEntitlement` y la resolucion de modulos siguen viviendo a nivel del plan comercial relacionado; no hay versionamiento comercial en esta fase.
- Las escrituras globales registran `PlatformAuditLog` durable con actor, entidad y payload antes/despues cuando aplica.

#### 5.3A.4 Errores observables relevantes en Platform backoffice

- `UNAUTHENTICATED`: `401`, autenticacion requerida.
- `PLATFORM_ACCESS_FORBIDDEN`: `403`, el usuario autenticado no tiene un `PlatformOperator` activo o no posee nivel `Admin` para la mutacion solicitada.
- `COMMERCIAL_PLAN_NOT_FOUND`: `404`, el plan solicitado no existe.
- `COMMERCIAL_PLAN_CODE_CONFLICT`: `409`, ya existe otro plan con ese `code`.
- `CONCURRENCY_CONFLICT`: `409`, el recurso fue modificado por otra solicitud.
- `COMMERCIAL_PLAN_ALREADY_ACTIVE`: `409`, el plan ya estaba activo.
- `COMMERCIAL_PLAN_ALREADY_INACTIVE`: `409`, el plan ya estaba inactivo.
- `COMMERCIAL_PLAN_SYSTEM_RENAME_FORBIDDEN`: `409`, un plan de sistema no puede cambiar `code` o `name`.
- `COMMERCIAL_PLAN_SYSTEM_INACTIVATION_FORBIDDEN`: `409`, un plan de sistema no puede inactivarse.
- `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`: `404`, la compania objetivo no existe.
- `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`: `404`, no existe suscripcion vigente o historica para la consulta pedida.
- `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`: `404`, el plan comercial enviado no existe.
- `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE`: `409`, no puede asignarse un plan inactivo.
- `PLATFORM_COMPANY_SUBSCRIPTION_ALREADY_ASSIGNED`: `409`, la compania ya usa ese plan como suscripcion activa.

#### 5.3A.5 `PlatformAuthController`

Base route: `/api/platform/auth`

Contratos principales:

- `PlatformLoginRequest`: `email`, `password`
- `PlatformRefreshTokenRequest`: `refreshToken`
- `AuthResponse`: `accessToken`, `refreshToken`, `expiresIn`, `user`
- `UserDto`: `publicId`, `email`, `firstName`, `lastName`, `authProvider`

##### `POST /api/platform/auth/login`

- Proposito: autenticar un operador de plataforma usando credenciales locales.
- Autenticacion: publica.
- Request body: `email`, `password`.
- Response: `AuthResponse`.
- Errores relevantes: `INVALID_CREDENTIALS`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: solo emite token si el usuario local esta activo y ligado a un `PlatformOperator` activo.

##### `POST /api/platform/auth/refresh`

- Proposito: rotar una sesion de plataforma usando un refresh token valido de `client_type=platform`.
- Autenticacion: publica.
- Request body: `refreshToken`.
- Response: `AuthResponse`.
- Errores relevantes: errores estandar de refresh token y `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: si el `PlatformOperator` deja de estar activo, el refresh revoca la familia de tokens de plataforma del usuario.

##### `POST /api/platform/auth/logout`

- Proposito: revocar la sesion de plataforma vigente.
- Autenticacion: `Bearer` requerido con token `platform`.
- Response: `204 No Content`.

#### 5.3A.6 `CommercialPlansController`

Base route: `/api/platform/commercial-plans`

Contratos principales:

- `CommercialPlanSummaryResponse`: `publicId`, `code`, `normalizedCode`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `status`, `isSystemPlan`, `moduleCount`, `createdAtUtc`, `modifiedAtUtc`
- `CommercialPlanResponse`: agrega `moduleKeys`, `moduleCount`, `concurrencyToken` y `limits`
- `CommercialPlanLimitInput`: `code`, `value`
- `CommercialPlanLimitResponse`: `code`, `normalizedCode`, `value`

##### `GET /api/platform/commercial-plans`

- Proposito: listar planes comerciales globales.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `status`, `q`, `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `q` maximo `150`.
- Response: `PagedResponse<CommercialPlanSummaryResponse>`.
- Observaciones: no exige tenant activo; ordena por `name` y luego `code`; expone tanto `FREE` como `MASTER` como planes de sistema activos.

##### `GET /api/platform/commercial-plans/{publicId}`

- Proposito: obtener el detalle de un plan comercial global.
- Autenticacion: `Bearer` requerido con token `platform`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`.
- Observaciones: `moduleKeys` y `limits` siempre se devuelven como colecciones, aunque esten vacias.

##### `POST /api/platform/commercial-plans`

- Proposito: registrar un nuevo plan comercial global.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `code`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `status`, `moduleKeys`, `limits`.
- Validaciones base: `code` obligatorio maximo `40`; `name` obligatorio maximo `150`; `description` maximo `500`; montos y limites no negativos; maximo `2` decimales; `limits[].code` obligatorio maximo `80`.
- Response: `201 Created` con `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_CODE_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: `moduleKeys` y `limits` pueden venir vacios; el `status` inicial puede registrarse como `Draft`, `Active` o `Inactive`.

##### `PUT /api/platform/commercial-plans/{publicId}`

- Proposito: actualizar datos base de un plan comercial existente.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `code`, `name`, `description`, `baseMonthlyFee`, `pricePerActiveEmployee`, `moduleKeys`, `limits`, `concurrencyToken`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`, `COMMERCIAL_PLAN_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_PLAN_SYSTEM_RENAME_FORBIDDEN`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: las colecciones `moduleKeys` y `limits` reemplazan completamente la configuracion anterior, salvo en `MASTER`, donde `moduleKeys` siempre se normaliza al catalogo completo; `status` no se modifica por esta ruta.

##### `PATCH /api/platform/commercial-plans/{publicId}/activate`

- Proposito: activar un plan comercial existente.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `concurrencyToken`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_PLAN_ALREADY_ACTIVE`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: no existe ruta para volver un plan a `Draft`.

##### `PATCH /api/platform/commercial-plans/{publicId}/inactivate`

- Proposito: inactivar un plan comercial existente.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `concurrencyToken`.
- Response: `CommercialPlanResponse`.
- Errores relevantes: `COMMERCIAL_PLAN_NOT_FOUND`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_PLAN_ALREADY_INACTIVE`, `COMMERCIAL_PLAN_SYSTEM_INACTIVATION_FORBIDDEN`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: `FREE` y `MASTER` no pueden inactivarse mientras sigan siendo planes de sistema del catalogo.

#### 5.3A.7 `CommercialAddonsController`

Base route: `/api/platform/commercial-addons`

Contratos principales:

- `CommercialAddonSummaryResponse`: `publicId`, `code`, `name`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `status`, `createdAtUtc`, `modifiedAtUtc`
- `CommercialAddonResponse`: agrega `concurrencyToken`

##### `GET /api/platform/commercial-addons`

- Proposito: listar add-ons comerciales globales con pricing masivo o especializado.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `type`, `billingModel`, `status`, `q`, `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`, `q` maximo `150`.
- Response: `PagedResponse<CommercialAddonSummaryResponse>`.
- Observaciones: no exige tenant activo; ordena por `name` y luego `code`.

##### `GET /api/platform/commercial-addons/{publicId}`

- Proposito: obtener el detalle de un add-on comercial global.
- Autenticacion: `Bearer` requerido con token `platform`.
- Response: `CommercialAddonResponse`.
- Errores relevantes: `COMMERCIAL_ADDON_NOT_FOUND`.
- Observaciones: la auditoria basica visible en contrato se limita a `createdAtUtc` y `modifiedAtUtc`.

##### `POST /api/platform/commercial-addons`

- Proposito: registrar un nuevo add-on comercial global.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `code`, `name`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `status`.
- Validaciones base: `code` obligatorio maximo `40`; `name` obligatorio maximo `150`; `description` maximo `500`; `measurementUnit` obligatorio maximo `80`; `unitPrice` y `minimumMonthlyFee` con maximo `2` decimales; `minimumQuantity` entero no negativo si se envia.
- Response: `201 Created` con `CommercialAddonResponse`.
- Errores relevantes: `COMMERCIAL_ADDON_CODE_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: los add-ons `Massive` usan `billingModel=PerActiveEmployee`, unidad `active employee` y no aceptan `minimumQuantity`; los `Specialized` usan `PerSeat` o `PerVolume`, no aceptan `minimumMonthlyFee` y exigen una unidad comercial coherente con la modalidad elegida.

##### `PUT /api/platform/commercial-addons/{publicId}`

- Proposito: actualizar datos base de un add-on comercial existente.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `code`, `name`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `concurrencyToken`.
- Response: `CommercialAddonResponse`.
- Errores relevantes: `COMMERCIAL_ADDON_NOT_FOUND`, `COMMERCIAL_ADDON_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: `status` no se modifica por esta ruta; la coherencia entre `type`, `billingModel` y la unidad comercial se revalida en cada actualizacion.

##### `PATCH /api/platform/commercial-addons/{publicId}/activate`

- Proposito: activar un add-on comercial existente.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `concurrencyToken`.
- Response: `CommercialAddonResponse`.
- Errores relevantes: `COMMERCIAL_ADDON_NOT_FOUND`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_ADDON_ALREADY_ACTIVE`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: no existe ruta para volver un add-on a `Draft`.

##### `PATCH /api/platform/commercial-addons/{publicId}/inactivate`

- Proposito: inactivar un add-on comercial existente.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `concurrencyToken`.
- Response: `CommercialAddonResponse`.
- Errores relevantes: `COMMERCIAL_ADDON_NOT_FOUND`, `CONCURRENCY_CONFLICT`, `COMMERCIAL_ADDON_ALREADY_INACTIVE`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: no existe delete fisico; la baja operativa del catalogo se resuelve por estado.

#### 5.3A.8 `BankCatalogsController`

Base route: `/api/platform/bank-catalogs`

Contratos principales:

- `BankCatalogItemResponse`: `publicId`, `countryCode`, `code`, `name`, `alias`, `swiftCode`, `routingCode`, `isActive`, `sortOrder`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`

##### `GET /api/platform/bank-catalogs`

- Proposito: listar el catalogo global de bancos filtrado por pais.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `countryCode`, `isActive`, `q`, `page`, `pageSize`.
- Validaciones: `countryCode` obligatorio; `page > 0`; `pageSize` entre `1` y `100`; `q` maximo `150`.
- Response: `PagedResponse<BankCatalogItemResponse>`.
- Observaciones: la busqueda aplica sobre `code`, `name`, `alias`, `swiftCode` y `routingCode`.

##### `GET /api/platform/bank-catalogs/{publicId}`

- Proposito: obtener el detalle de un banco del catalogo global.
- Autenticacion: `Bearer` requerido con token `platform`.
- Response: `BankCatalogItemResponse`.
- Errores relevantes: `BANK_CATALOG_NOT_FOUND`.

##### `POST /api/platform/bank-catalogs`

- Proposito: crear un banco global country-scoped.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `countryCode`, `code`, `name`, `alias`, `swiftCode`, `routingCode`, `sortOrder`.
- Validaciones base: `countryCode` activo; `code` obligatorio maximo `80`; `name` obligatorio maximo `200`; `alias` maximo `120`; `swiftCode` y `routingCode` maximo `40`; `sortOrder >= 0`.
- Response: `201 Created` con `BankCatalogItemResponse`.
- Errores relevantes: `BANK_CATALOG_COUNTRY_NOT_FOUND`, `BANK_CATALOG_CODE_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `PUT /api/platform/bank-catalogs/{publicId}`

- Proposito: actualizar un banco existente del catalogo global.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `countryCode`, `code`, `name`, `alias`, `swiftCode`, `routingCode`, `sortOrder`, `concurrencyToken`.
- Response: `BankCatalogItemResponse`.
- Errores relevantes: `BANK_CATALOG_NOT_FOUND`, `BANK_CATALOG_COUNTRY_CHANGE_FORBIDDEN`, `BANK_CATALOG_CODE_CONFLICT`, `CONCURRENCY_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `PATCH /api/platform/bank-catalogs/{publicId}/activate`

- Proposito: reactivar un banco del catalogo global.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `concurrencyToken`.
- Response: `BankCatalogItemResponse`.
- Errores relevantes: `BANK_CATALOG_NOT_FOUND`, `BANK_CATALOG_ALREADY_ACTIVE`, `CONCURRENCY_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `PATCH /api/platform/bank-catalogs/{publicId}/inactivate`

- Proposito: inactivar un banco del catalogo global.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `concurrencyToken`.
- Response: `BankCatalogItemResponse`.
- Errores relevantes: `BANK_CATALOG_NOT_FOUND`, `BANK_CATALOG_ALREADY_INACTIVE`, `CONCURRENCY_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: los bancos inactivos siguen pudiendo verse en cuentas bancarias ya asociadas, pero dejan de salir en el lookup activo del core.

#### 5.3A.9 `PlatformCompanySubscriptionsController`

Base route: `/api/platform/companies/{companyPublicId}/subscription*`

Contratos principales:

- `PlatformCompanySubscriptionResponse`: `subscriptionPublicId`, `companyPublicId`, `commercialPlanPublicId`, `commercialPlanVersionId`, `planCode`, `planName`, `planVersionNumber`, `baseMonthlyFee`, `pricePerActiveEmployee`, `periodicity`, `currencyCode`, `status`, `startDateUtc`, `expiresAtUtc`, `endDateUtc`, `statusChangedAtUtc`, `currentStatusReasonCode`, `currentStatusObservations`, `currentStatusOrigin`, `canOperate`, `canGenerateCharges`, `pendingStatusChange`, `activatedByUserId`, `activatedAtUtc`, `createdAtUtc`, `modifiedAtUtc`
- `PlatformCompanySubscriptionOverviewResponse`: `companyPublicId`, `companyName`, `companySlug`, `companyStatus`, `isBillable`, `billableSinceUtc`, `currentSubscription`, `scheduledReplacement`
- `PlatformCompanySubscriptionPreviewResponse`: `companyPublicId`, `commercialPlanPublicId`, `commercialPlanVersionId`, `planCode`, `planName`, `planVersionNumber`, `baseMonthlyFee`, `pricePerActiveEmployee`, `periodicity`, `currencyCode`, `resolvedStatus`, `startDateUtc`, `isEligible`, `ineligibilityReasons`
- `PlatformCompanySubscriptionPendingStatusChangeResponse`: `targetStatus`, `effectiveDateUtc`, `reasonCode`, `observations`, `requestedAtUtc`, `requestedByUserPublicId`
- `PlatformCompanySubscriptionStatusChangePreviewResponse`: `companyPublicId`, `companyName`, `companySlug`, `companyStatus`, `subscriptionPublicId`, `currentStatus`, `targetStatus`, `effectiveDateUtc`, `planCode`, `planName`, `planVersionNumber`, `expiresAtUtc`, `canOperate`, `canGenerateCharges`, `isEligible`, `ineligibilityReasons`
- `UpsertPlatformCompanySubscriptionRequest`: `commercialPlanId`, `startDateUtc`, `periodicity`
- `ChangePlatformCompanySubscriptionStatusRequest`: `targetStatus`, `reasonCode`, `observations`, `effectiveDateUtc`
- `PlatformCompanySubscriptionPlanChangePreviewResponse`: snapshot del plan actual y del plan objetivo, `mode`, `effectiveDateUtc`, `activeEmployeeCount`, `estimatedNextCharge`, `isEligible`, `ineligibilityReasons`, `addonCompatibilityWarnings`
- `PlatformCompanySubscriptionPlanChangeResponse`: `planChangePublicId`, snapshot actual/objetivo, `mode`, `status`, `reasonCode`, fechas de solicitud y aplicacion, `estimatedNextCharge`, metadata de cancelacion o rechazo
- `PreviewPlatformCompanySubscriptionPlanChangeRequest`: `commercialPlanId`, `mode`, `requestedEffectiveDateUtc`
- `CreatePlatformCompanySubscriptionPlanChangeRequest`: `commercialPlanId`, `mode`, `requestedEffectiveDateUtc`, `reasonCode`, `observations`
- `CancelPlatformCompanySubscriptionPlanChangeRequest`: `observations`
- `PlatformCompanyAddonResponse`: `companyAddonPublicId`, `companyPublicId`, `companySubscriptionPublicId`, `commercialAddonPublicId`, `addonCode`, `addonName`, `addonType`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `currencyCode`, `status`, `statusEffectiveDateUtc`
- `PlatformCompanyEligibleAddonResponse`: `commercialAddonPublicId`, `addonCode`, `addonName`, `description`, `addonType`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity`, `catalogStatus`
- `PlatformCompanyAddonChangePreviewResponse`: empresa, suscripcion actual, add-on seleccionado, `action`, `mode`, `currentStatus`, `resultingStatus`, `effectiveDateUtc`, `quantityBasis`, `estimatedNextChargeImpact`, `isEligible`, `warnings`
- `PlatformCompanyAddonChangeResponse`: `addonChangePublicId`, snapshot del add-on, `action`, `mode`, `status`, `reasonCode`, `previousStatus`, `resultingStatus`, fechas de solicitud/aplicacion/cancelacion/rechazo y metadata de actor
- `PreviewPlatformCompanyAddonChangeRequest`: `commercialAddonId`, `action`, `mode`, `requestedEffectiveDateUtc`
- `CreatePlatformCompanyAddonChangeRequest`: `commercialAddonId`, `action`, `mode`, `requestedEffectiveDateUtc`, `reasonCode`, `observations`
- `CancelPlatformCompanyAddonChangeRequest`: `observations`

##### `GET /api/platform/companies/{companyPublicId}/subscription`

- Proposito: obtener el overview comercial de una compania, incluyendo la suscripcion vigente y una programada si existe.
- Autenticacion: `Bearer` requerido con token `platform`.
- Response: `PlatformCompanySubscriptionOverviewResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `GET /api/platform/companies/{companyPublicId}/subscriptions`

- Proposito: listar el historial paginado de suscripciones de una compania.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`.
- Response: `PagedResponse<PlatformCompanySubscriptionResponse>`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: la pagina se ordena por fecha de inicio descendente y conserva filas canceladas para historial.

##### `POST /api/platform/companies/{companyPublicId}/subscription/preview`

- Proposito: resolver la version comercial efectiva, el estado inicial (`Active` o `Scheduled`) y la elegibilidad antes de confirmar la activacion.
- Autenticacion: `Bearer` requerido con token `platform`.
- Request body: `commercialPlanId`, `startDateUtc`, `periodicity`.
- Response: `PlatformCompanySubscriptionPreviewResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_VERSION_NOT_AVAILABLE`, `PLATFORM_COMPANY_SUBSCRIPTION_START_DATE_IN_PAST`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `PUT /api/platform/companies/{companyPublicId}/subscription`

- Proposito: activar de inmediato o programar una suscripcion empresarial ligada a una version explicita del plan comercial.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `commercialPlanId`, `startDateUtc`, `periodicity`.
- Response: `PlatformCompanySubscriptionResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_VERSION_NOT_AVAILABLE`, `PLATFORM_COMPANY_SUBSCRIPTION_START_DATE_IN_PAST`, `PLATFORM_COMPANY_SUBSCRIPTION_MISSING_LEGAL_REPRESENTATIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_MISSING_ADMINISTRATOR`, `PLATFORM_COMPANY_SUBSCRIPTION_SCHEDULED_CONFLICT`, `PLATFORM_COMPANY_SUBSCRIPTION_ALREADY_ASSIGNED`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: si la fecha es hoy, la fila activa anterior se cancela y se crea una nueva activa; si la fecha es futura, la fila se registra como `Scheduled` y se promueve automaticamente cuando llega su vigencia.

##### `POST /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status/preview`

- Proposito: previsualizar una reactivacion manual de una suscripcion suspendida sin mutar el estado actual.
- Autenticacion: `Bearer` requerido con token `platform`.
- Request body: `targetStatus`, `reasonCode`, `observations`, `effectiveDateUtc`.
- Response: `PlatformCompanySubscriptionStatusChangePreviewResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_EFFECTIVE_DATE_REQUIRED`, `PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_EFFECTIVE_DATE_IN_PAST`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: en este MVP solo soporta `Suspended -> Active`; si la fecha es futura, el preview conserva el plan/version vigentes y expone las razones de inelegibilidad sin crear una solicitud aun.

##### `PATCH /api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status`

- Proposito: aplicar un cambio manual inmediato de estado o registrar una reactivacion programada de una suscripcion suspendida.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `targetStatus`, `reasonCode`, `observations`, `effectiveDateUtc`.
- Response: `PlatformCompanySubscriptionResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_INVALID_STATUS_TRANSITION`, `PLATFORM_COMPANY_SUBSCRIPTION_INVALID_STATUS_REASON`, `PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_EFFECTIVE_DATE_REQUIRED`, `PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_EFFECTIVE_DATE_IN_PAST`, `PLATFORM_COMPANY_SUBSCRIPTION_STATUS_CHANGE_PENDING_CONFLICT`, `PLATFORM_COMPANY_SUBSCRIPTION_REACTIVATION_REQUIRES_SUSPENDED_STATUS`, `PLATFORM_COMPANY_SUBSCRIPTION_REACTIVATION_PAST_EXPIRATION`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: `effectiveDateUtc` solo aplica a reactivaciones `Suspended -> Active`; si es hoy, la suscripcion vuelve a `Active` en la misma transaccion; si es futura, la respuesta conserva `status = Suspended` y expone `pendingStatusChange` hasta que el lifecycle processor aplique o rechace la solicitud.

##### `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes/preview`

- Proposito: previsualizar un cambio de plan sobre la suscripcion actual sin mutar estado.
- Autenticacion: `Bearer` requerido con token `platform`.
- Request body: `commercialPlanId`, `mode`, `requestedEffectiveDateUtc`.
- Response: `PlatformCompanySubscriptionPlanChangePreviewResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_INVALID_MODE`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_EFFECTIVE_DATE_REQUIRED`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_EFFECTIVE_DATE_IN_PAST`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_ALREADY_PENDING`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `POST /api/platform/companies/{companyPublicId}/subscription/plan-changes`

- Proposito: crear un cambio de plan inmediato o programado, preservando historial comercial y auditoria durable.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `commercialPlanId`, `mode`, `requestedEffectiveDateUtc`, `reasonCode`, `observations`.
- Response: `PlatformCompanySubscriptionPlanChangeResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_INACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_INVALID_MODE`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_REASON_REQUIRED`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_ALREADY_PENDING`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: `Immediate` aplica el swap en la misma transaccion; `SpecificDate` y `NextBillingCycle` crean una fila pendiente o programada que luego consume el lifecycle processor.

##### `GET /api/platform/companies/{companyPublicId}/subscription/plan-changes`

- Proposito: listar el historial paginado de cambios de plan solicitados para la empresa.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`.
- Response: `PagedResponse<PlatformCompanySubscriptionPlanChangeResponse>`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `PATCH /api/platform/companies/{companyPublicId}/subscription/plan-changes/{planChangePublicId}/cancel`

- Proposito: cancelar un cambio de plan aun no aplicado.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `observations`.
- Response: `PlatformCompanySubscriptionPlanChangeResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_PLAN_CHANGE_CANCELLATION_NOT_ALLOWED`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `GET /api/platform/companies/{companyPublicId}/subscription/addons`

- Proposito: listar el estado actual y comercial de los add-ons asociados a la empresa.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `status`, `q`, `page`, `pageSize`.
- Validaciones: `status` debe ser un `CompanyAddonStatus` valido; `q` maximo `150` caracteres; `page > 0`; `pageSize` entre `1` y `100`.
- Response: `PagedResponse<PlatformCompanyAddonResponse>`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `GET /api/platform/companies/{companyPublicId}/subscription/addons/eligible`

- Proposito: listar los add-ons del catalogo global que pueden evaluarse para contratacion en la empresa.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `type`, `q`, `page`, `pageSize`.
- Validaciones: `type` debe ser un `CommercialAddonType` valido; `q` maximo `150` caracteres; `page > 0`; `pageSize` entre `1` y `100`.
- Response: `PagedResponse<PlatformCompanyEligibleAddonResponse>`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes/preview`

- Proposito: previsualizar la activacion o desactivacion comercial de un add-on sin mutar el estado actual.
- Autenticacion: `Bearer` requerido con token `platform`.
- Request body: `commercialAddonId`, `action`, `mode`, `requestedEffectiveDateUtc`.
- Response: `PlatformCompanyAddonChangePreviewResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_INACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_INVALID_MODE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_EFFECTIVE_DATE_REQUIRED`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_EFFECTIVE_DATE_IN_PAST`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_ALREADY_ACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_ACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_PENDING_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: la respuesta incluye `quantityBasis`, `estimatedNextChargeImpact`, `ineligibilityReasons` y `warnings`; la estimacion es informativa y no representa el cobro final.

##### `POST /api/platform/companies/{companyPublicId}/subscription/addon-changes`

- Proposito: registrar una activacion o desactivacion inmediata o programada de add-on preservando historial comercial, estado actual por empresa y auditoria durable.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `commercialAddonId`, `action`, `mode`, `requestedEffectiveDateUtc`, `reasonCode`, `observations`.
- Response: `PlatformCompanyAddonChangeResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_INACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_INVALID_ACTION`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_INVALID_MODE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_REASON_REQUIRED`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_ALREADY_ACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_NOT_ACTIVE`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_PENDING_CONFLICT`, `PLATFORM_ACCESS_FORBIDDEN`.
- Observaciones: si existe un cambio pendiente incompatible del mismo add-on, el backend resuelve el conflicto antes de crear otro; la funcionalidad no altera todavia entitlements ni cobro final.

##### `GET /api/platform/companies/{companyPublicId}/subscription/addon-changes`

- Proposito: listar el historial paginado de cambios comerciales de add-ons por empresa.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `page`, `pageSize`.
- Validaciones: `page > 0`, `pageSize` entre `1` y `100`.
- Response: `PagedResponse<PlatformCompanyAddonChangeResponse>`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_ACCESS_FORBIDDEN`.

##### `PATCH /api/platform/companies/{companyPublicId}/subscription/addon-changes/{addonChangePublicId}/cancel`

- Proposito: cancelar un cambio de add-on aun no aplicado.
- Autenticacion: `Bearer` requerido con token `platform` y `PlatformOperatorRole.Admin`.
- Request body: `observations`.
- Response: `PlatformCompanyAddonChangeResponse`.
- Errores relevantes: `PLATFORM_COMPANY_SUBSCRIPTION_COMPANY_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_NOT_FOUND`, `PLATFORM_COMPANY_SUBSCRIPTION_ADDON_CHANGE_CANCELLATION_NOT_ALLOWED`, `PLATFORM_ACCESS_FORBIDDEN`.

#### 5.3A.9 `PlatformSubscriptionsController`

Base route: `/api/platform/company-subscriptions`

##### `GET /api/platform/company-subscriptions`

- Proposito: listar globalmente empresas con suscripciones creadas, activas o programadas desde el backoffice.
- Autenticacion: `Bearer` requerido con token `platform`.
- Query: `status`, `search`, `page`, `pageSize`.
- Validaciones: `status` debe ser un `SubscriptionStatus` valido; `page > 0`; `pageSize` entre `1` y `100`.
- Response: `PagedResponse<PlatformCompanySubscriptionListItemResponse>`.
- Observaciones: permite filtrar por estado y buscar por nombre de empresa, slug, codigo o nombre del plan.

#### 5.3A.10 Relacion con provisioning y entitlements

- `CompanyProvisioningService` resuelve formalmente el plan comercial `FREE` y crea la suscripcion inicial desde ese agregado global.
- `PlanEntitlementService` resincroniza `FREE` y `MASTER` como planes de sistema, resuelve modulos y limites desde el plan comercial relacionado a la suscripcion activa y garantiza que `MASTER` cubra siempre todo el catalogo comercial conocido.
- `AccountCompanySummaryResponse` y `AccountCompanyDetailResponse` mantienen `planCode` como compatibilidad de contrato, pero ese valor sale del snapshot de la suscripcion activa.
- `Company.IsBillable` solo se activa cuando la empresa tiene una suscripcion comercial `Active` no sistema; una fila `Scheduled` no la vuelve facturable antes de tiempo.

### 5.4 Org structure catalogs

#### 5.4.1 Alcance

Este bloque cubre `OrgStructureCatalogsController` y hoy expone tres familias de catalogos:

- `unit-types` a nivel tenant
- `functional-areas` a nivel tenant

Familias de rutas:

- `/api/v1/companies/{companyPublicId}/org-structure-catalogs/unit-types`
- `/api/v1/org-structure-catalogs/unit-types/{publicId}`
- `/api/v1/companies/{companyPublicId}/org-structure-catalogs/functional-areas`
- `/api/v1/org-structure-catalogs/functional-areas/{publicId}`

#### 5.4.2 Proposito funcional en CLARIHR

El modulo `Org structure catalogs` sirve para mantener catalogos base que luego usan otros modulos:

- `unit-types` alimenta la estructura organizativa y clasificaciones relacionadas
- `functional-areas` alimenta org units y otras definiciones organizativas

Es un modulo fundacional: no modela organigramas ni companias completas, sino los catalogos que esas piezas consumen.

#### 5.4.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- `unit-types` y `functional-areas` dependen del `tenantId` y de claims tenant-scoped.
- En `unit-types` y `functional-areas`, `search/create` usan `companyPublicId` en la ruta y exigen que coincida con el tenant del token.
- En `unit-types` y `functional-areas`, `get/update/activate/inactivate` usan solo `{publicId}` y resuelven el tenant desde el token actual. Si el item existe en otro tenant, la API devuelve `TENANT_MISMATCH` en vez de `404` plano.
- Ambos catalogos comparten el mismo shape observable: `publicId`, `code`, `normalizedCode`, `name`, `description`, `sortOrder`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc`.
- Todas las escrituras validan `code` con regex alfanumerica mas `_` o `-`, longitud maxima `50`, `name` maximo `150`, `description` maximo `500` y `sortOrder >= 0`.
- Los endpoints de `update`, `activate` e `inactivate` usan concurrencia optimista con `ConcurrencyToken`.
- Los endpoints de busqueda usan `isActive`, `q`, `page` y `pageSize`; el `pageSize` maximo es `100` y el default es `20`.
- La inactivacion esta protegida por reglas de uso: no puede inactivarse un item que siga referenciado por recursos dependientes.

#### 5.4.4 Autorizacion observable

- `unit-types` y `functional-areas` en lectura aceptan alguno de estos permisos: `OrgStructureCatalogs.Read`, `OrgStructureCatalogs.Admin`, `OrgUnits.Read`, `OrgUnits.Admin` o `iam.administration.manage`.
- `unit-types` y `functional-areas` en escritura exigen alguno de estos permisos: `OrgStructureCatalogs.Admin`, `OrgUnits.Admin` o `iam.administration.manage`.
- Si el `companyPublicId` de la ruta no coincide con el tenant actual, la respuesta observable es `TENANT_MISMATCH`.
- Si el usuario esta autenticado pero no cumple permisos tenant-scoped, la respuesta observable es `ORG_STRUCTURE_CATALOG_FORBIDDEN`.

#### 5.4.5 Errores observables relevantes en Org structure catalogs

- `UNAUTHENTICATED`: `401`, autenticacion requerida o no hay tenant valido en rutas tenant-scoped.
- `ORG_STRUCTURE_CATALOG_FORBIDDEN`: `403`, el usuario no tiene permisos sobre catalogos de estructura.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `ORG_STRUCTURE_CATALOG_NOT_FOUND`: `404`, el item solicitado no existe en el scope correcto.
- `ORG_STRUCTURE_CATALOG_CODE_CONFLICT`: `409`, ya existe otro item con el mismo `code` en el mismo scope.
- `ORG_STRUCTURE_CATALOG_IN_USE`: `409`, no puede inactivarse porque sigue referenciado.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `400` de validacion: `page/pageSize` invalidos, `code` invalido, campos obligatorios vacios o `ConcurrencyToken` ausente.

#### 5.4.6 `unit-types` - catalogo tenant-scoped

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-structure-catalogs/unit-types`
- `GET /api/v1/org-structure-catalogs/unit-types/{publicId}`
- `POST /api/v1/companies/{companyPublicId}/org-structure-catalogs/unit-types`
- `PUT /api/v1/org-structure-catalogs/unit-types/{publicId}`
- `PATCH /api/v1/org-structure-catalogs/unit-types/{publicId}/activate`
- `PATCH /api/v1/org-structure-catalogs/unit-types/{publicId}/inactivate`

Uso principal:

- alimentar tipos de unidad organizativa que luego usa `OrgUnits`
- servir como catalogo base para clasificaciones relacionadas con puestos/estructura

Observaciones funcionales:

- `search` y `create` usan `companyPublicId` en la ruta y exigen que coincida con el tenant del token.
- `get/update/activate/inactivate` usan el `tenantId` del token actual y el `publicId` del item.
- Si el item existe pero pertenece a otro tenant, la API devuelve `TENANT_MISMATCH`.
- `create` asigna `TenantId = companyPublicId` al nuevo item.
- `update`, `activate` e `inactivate` exigen `ConcurrencyToken`.
- `inactivate` falla con `ORG_STRUCTURE_CATALOG_IN_USE` si el item esta siendo usado por org units o por position category classifications.
- Todas las escrituras generan auditoria de tenant.

#### 5.4.7 `functional-areas` - catalogo tenant-scoped

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-structure-catalogs/functional-areas`
- `GET /api/v1/org-structure-catalogs/functional-areas/{publicId}`
- `POST /api/v1/companies/{companyPublicId}/org-structure-catalogs/functional-areas`
- `PUT /api/v1/org-structure-catalogs/functional-areas/{publicId}`
- `PATCH /api/v1/org-structure-catalogs/functional-areas/{publicId}/activate`
- `PATCH /api/v1/org-structure-catalogs/functional-areas/{publicId}/inactivate`

Uso principal:

- mantener areas funcionales del tenant para la estructura organizativa

Observaciones funcionales:

- comparte el mismo patron de autorizacion y scoping que `unit-types`.
- `search` y `create` trabajan con `companyPublicId` en la ruta; `get/update/activate/inactivate` operan por `publicId` usando el tenant actual del token.
- `create` asigna `TenantId = companyPublicId`.
- `update`, `activate` e `inactivate` exigen `ConcurrencyToken`.
- `inactivate` falla con `ORG_STRUCTURE_CATALOG_IN_USE` si alguna org unit sigue usando el area funcional.
- Todas las escrituras generan auditoria de tenant.

#### 5.4.9 Relacion con otros modulos

- `OrgUnits` consume `unit-types` y `functional-areas`.
- Modulos posteriores de diseno organizativo y de puestos dependen indirectamente de estos catalogos.

Por eso este modulo aparece temprano en el flujo documental: define catalogos base que otros modulos reutilizan, pero no reemplaza la administracion de organigramas ni de companias.

### 5.5 OrgUnits

#### 5.5.1 Alcance

Este bloque cubre `OrgUnitsController` y expone la administracion operativa de unidades organizativas del tenant.

Familias de rutas:

- `/api/v1/companies/{companyPublicId}/org-units`
- `/api/v1/org-units/{publicId}`
- `/api/v1/companies/{companyPublicId}/org-units/tree`
- `/api/v1/companies/{companyPublicId}/org-units/graph`
- `/api/v1/companies/{companyPublicId}/org-units/export`
- `/api/v1/companies/{companyPublicId}/org-units/diagram-export`
- `/api/v1/org-units/{publicId}/move`
- `/api/v1/org-units/{publicId}/activate`
- `/api/v1/org-units/{publicId}/inactivate`

#### 5.5.2 Proposito funcional en CLARIHR

El modulo `OrgUnits` modela el organigrama operativo del tenant. Su funcion es crear y mantener las unidades que componen la estructura organizativa real de la compania, con relaciones padre-hijo, tipo de unidad, area funcional, centro de costo, responsable y estado activo.

No es un catalogo base como `Org structure catalogs`; aqui ya se administran nodos concretos del arbol organizativo que luego consumen modulos laborales, reportes y vistas de estructura.

#### 5.5.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El modulo es tenant-scoped por defecto. En rutas con `companyPublicId`, ese valor debe coincidir con el tenant activo del token.
- `search`, `tree`, `graph`, `export`, `diagram-export` y `create` usan `companyPublicId` en la ruta.
- `get by id`, `update`, `move`, `activate` e `inactivate` usan solo `{publicId}` y resuelven el tenant desde el token actual.
- Si una org unit existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver un `404` plano.
- El shape principal de lectura es `OrgUnitResponse`: `publicId`, `code`, `normalizedCode`, `name`, `orgUnitType`, `functionalArea`, `parent`, `sortOrder`, `description`, `costCenterCode`, `managerEmployeePublicId`, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc` y opcionalmente `allowedActions`.
- `code` es obligatorio, maximo `50`, y debe cumplir regex alfanumerica con `_` o `-`.
- `name` es obligatorio y maximo `150`.
- `description` acepta hasta `500` caracteres.
- `sortOrder` debe ser `>= 0`.
- `pageSize` maximo es `100`, el default es `20`.
- La profundidad maxima soportada para arbol y grafo es `15`.
- `update`, `move`, `activate` e `inactivate` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras y ambos exportes generan auditoria.

#### 5.5.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `OrgUnits.Read`, `OrgUnits.Admin` o `iam.administration.manage`.
- Escritura acepta alguno de estos permisos: `OrgUnits.Admin` o `iam.administration.manage`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyPublicId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `ORG_UNITS_FORBIDDEN`.

#### 5.5.5 Errores observables relevantes en OrgUnits

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido para rutas tenant-scoped.
- `ORG_UNITS_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de org units.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `ORG_UNIT_NOT_FOUND`: `404`, la org unit o `rootId` solicitado no existe en el scope correcto.
- `ORG_UNIT_PARENT_NOT_FOUND`: `404`, el `parentPublicId` o `newParentPublicId` solicitado no existe en el tenant correcto.
- `ORG_UNIT_TYPE_NOT_FOUND`: `404`, el `orgUnitTypePublicId` no existe o esta inactivo en los catalogos del tenant correcto.
- `FUNCTIONAL_AREA_NOT_FOUND`: `404`, el `functionalAreaPublicId` no existe o esta inactivo en los catalogos del tenant correcto.
- `ORG_UNIT_CODE_CONFLICT`: `409`, otra org unit del mismo tenant ya usa ese `code`.
- `ORG_UNIT_CYCLE_DETECTED`: `409`, el movimiento solicitado crearia un ciclo en la jerarquia.
- `ORG_UNIT_DEPTH_LIMIT_EXCEEDED`: `409`, la nueva ubicacion excede los `15` niveles soportados.
- `ORG_UNIT_HAS_ACTIVE_CHILDREN`: `409`, no puede inactivarse porque todavia tiene hijos activos.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `ORG_UNIT_COST_CENTER_INVALID`: `422`, el `costCenterCode` no existe o esta inactivo para la compania.
- `REPORT_FORMAT_NOT_SUPPORTED`: `400`, `format` no soportado en export o diagram-export.
- `400` de validacion: `page/pageSize` invalidos, `depth` fuera de `1..15`, `code` invalido, ids vacios o `ConcurrencyToken` ausente.

#### 5.5.6 Search y detalle de org units

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-units`
- `GET /api/v1/org-units/{publicId}`

Uso principal:

- listar org units para administracion operativa del organigrama
- abrir el detalle canonical de una org unit concreta

Observaciones funcionales:

- `search` soporta filtros `isActive`, `orgUnitTypePublicId`, `functionalAreaPublicId`, `parentPublicId`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `code`, `name`, tipo de unidad, area funcional y nombre del padre.
- el orden observable del listado es `sortOrder`, luego `name`, luego `code`.
- `search` devuelve `PagedResponse<OrgUnitResponse>`.
- `get by id` devuelve una sola `OrgUnitResponse`.
- `search` y `get by id` incluyen `parent` con `publicId`, `code`, `normalizedCode` y `name` cuando la unidad tiene padre.
- `get by id` siempre calcula `allowedActions` usando el estado activo y la existencia de hijos activos.
- `search` solo calcula `allowedActions` si `includeAllowedActions=true`.

#### 5.5.7 Jerarquia y visualizacion estructural

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-units/tree`
- `GET /api/v1/companies/{companyPublicId}/org-units/graph`

Uso principal:

- renderizar arboles de navegacion del organigrama
- alimentar vistas graficas o diagramas de relaciones padre-hijo

Observaciones funcionales:

- ambos endpoints aceptan `rootPublicId` opcional y `depth` opcional.
- si no se envian `rootPublicId` ni `depth`, el resultado cubre toda la jerarquia del tenant.
- si se envia `rootPublicId`, la respuesta se acota a ese subarbol.
- `depth` debe estar entre `1` y `15`.
- `tree` devuelve nodos anidados con `children`.
- `graph` devuelve `nodes` y `edges`.
- en `graph`, cada edge va de `parentPublicId` a `childPublicId`.
- el orden observable de nodos y ramas sigue `sortOrder`, luego `name`, luego `code`.
- si `rootPublicId` existe en otro tenant, la API devuelve `TENANT_MISMATCH`; si no existe, devuelve `ORG_UNIT_NOT_FOUND`.

#### 5.5.8 Exportes

Route family:

- `GET /api/v1/companies/{companyPublicId}/org-units/export`
- `GET /api/v1/companies/{companyPublicId}/org-units/diagram-export`

Uso principal:

- exportar la estructura para analisis, intercambio o respaldo operativo

Observaciones funcionales:

- `export` soporta `format=csv|xlsx` y reutiliza los mismos filtros de busqueda operativa, salvo paginacion.
- `export` devuelve filas planas con columnas de tipo de unidad, area funcional, padre, centro de costo, responsable y timestamps.
- `diagram-export` soporta `format=graphml|json|dot`.
- `diagram-export` reutiliza `rootPublicId` y `depth` del endpoint `graph`.
- ambos endpoints generan auditoria de tipo `ReportExported`.
- si `format` no es soportado, la respuesta observable es `REPORT_FORMAT_NOT_SUPPORTED`.

#### 5.5.9 Escrituras: create, update, move, activate e inactivate

Route family:

- `POST /api/v1/companies/{companyPublicId}/org-units`
- `PUT /api/v1/org-units/{publicId}`
- `PATCH /api/v1/org-units/{publicId}/move`
- `PATCH /api/v1/org-units/{publicId}/activate`
- `PATCH /api/v1/org-units/{publicId}/inactivate`

Uso principal:

- crear nuevas unidades del organigrama
- corregir datos operativos
- reorganizar la jerarquia
- controlar el estado activo de cada nodo

Observaciones funcionales:

- `create` devuelve `201 Created` con la `OrgUnitResponse` creada.
- `create` exige `code`, `name`, `orgUnitTypePublicId` y valida opcionalmente `functionalAreaPublicId`, `parentPublicId`, `sortOrder`, `description`, `costCenterCode` y `managerEmployeePublicId`.
- `create` exige que `orgUnitTypePublicId` y `functionalAreaPublicId` apunten a catalogos activos del tenant.
- `create` valida unicidad de `code` por tenant.
- `create` valida que `costCenterCode`, si se envia, exista y este activo en la compania.
- `create` valida `parentPublicId` dentro del tenant correcto y rechaza profundidades mayores a `15`.
- `update` modifica datos escalares de la org unit, pero no cambia el padre; para eso existe `move`.
- `update` exige `ConcurrencyToken`.
- `move` permite cambiar `newParentPublicId` y opcionalmente `sortOrder`.
- `move` exige `ConcurrencyToken`, rechaza mover una unidad bajo si misma y rechaza ciclos indirectos.
- `move` tambien valida que la nueva ubicacion no exceda la profundidad maxima soportada.
- `activate` exige `ConcurrencyToken` y reactiva la org unit.
- `inactivate` exige `ConcurrencyToken` y falla si la unidad todavia tiene hijos activos.

#### 5.5.10 Relacion con otros modulos

- `Org structure catalogs` provee `unit-types` y `functional-areas`, dependencias directas de este modulo.
- `Cost centers` se integra via `costCenterCode`; una org unit no puede guardar un centro de costo inexistente o inactivo.
- Modulos posteriores de puestos, personal y reporting consumen la estructura resultante para asignaciones y visualizacion.

`OrgUnits` es el primer modulo donde el tenant deja de configurar catalogos y pasa a modelar su estructura organizativa real.

### 5.6 CostCenters

#### 5.6.1 Alcance

Este bloque cubre `CostCentersController` y expone la administracion tenant-scoped de centros de costo.

Familias de rutas:

- `/api/v1/companies/{companyId}/cost-centers`
- `/api/v1/cost-centers/{id}`
- `/api/v1/cost-centers/{id}/usage`
- `/api/v1/companies/{companyId}/cost-centers/export`
- `/api/v1/cost-centers/{id}/activate`
- `/api/v1/cost-centers/{id}/inactivate`

#### 5.6.2 Proposito funcional en CLARIHR

El modulo `CostCenters` mantiene el catalogo operativo de centros de costo del tenant. Su funcion es clasificar costos laborales y organizativos para que otros modulos puedan referenciar un codigo valido al asignar organigramas, perfiles y plazas derivadas del perfil, ademas de movimientos relacionados con gasto.

No modela jerarquias; modela un catalogo operativo con estado, tipo y codigos contables auxiliares.

#### 5.6.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El modulo es tenant-scoped por defecto. En rutas con `companyId`, ese valor debe coincidir con el tenant activo del token.
- `search`, `export` y `create` usan `companyId` en la ruta.
- `get by id`, `usage`, `update`, `activate` e `inactivate` usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un centro de costo existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver `404` plano.
- `CostCenterListItemResponse` devuelve `id`, `code`, `name`, `type`, codigos contables opcionales, `isActive`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc` y opcionalmente `allowedActions`.
- `CostCenterResponse` agrega `companyId` y `description`.
- `CostCenterUsageResponse` devuelve contadores de referencias activas e inactivas desde `OrgUnits` y `PositionSlots`, mas `hasActiveReferences`.
- El uso de `PositionSlots` se calcula por derivacion `PositionSlot -> JobProfile -> OrgUnit -> CostCenterCode`; la plaza ya no conserva un `costCenterCode` propio.
- `code` es obligatorio, maximo `50`, y debe cumplir regex alfanumerica con `_` o `-`.
- `name` es obligatorio y maximo `150`.
- `description` acepta hasta `500` caracteres.
- Los tres account codes opcionales aceptan hasta `100` caracteres y usan regex alfanumerica con `_`, `.` o `-`.
- `pageSize` maximo es `100`, el default es `20`.
- `update`, `activate` e `inactivate` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras y el export generan auditoria.
- El campo `type` usa el enum `CostCenterType`: `SalaryExpense`, `EmployerContribution`, `ProvisionReserve` y `Mixed`.

#### 5.6.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `CostCenters.Read`, `CostCenters.Admin` o `iam.administration.manage`.
- Escritura acepta alguno de estos permisos: `CostCenters.Admin` o `iam.administration.manage`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `COST_CENTERS_FORBIDDEN`.

#### 5.6.5 Errores observables relevantes en CostCenters

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido para rutas tenant-scoped.
- `COST_CENTERS_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de centros de costo.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `COST_CENTER_NOT_FOUND`: `404`, el centro de costo solicitado no existe en el scope correcto.
- `COST_CENTER_CODE_CONFLICT`: `409`, otro centro de costo del mismo tenant ya usa ese `code`.
- `COST_CENTER_IN_USE`: `409`, no puede inactivarse porque sigue referenciado por org units activas o position slots activas.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `COST_CENTER_EXPORT_FORMAT_INVALID`: `400`, `format` no soportado en el export.
- `400` de validacion: `page/pageSize` invalidos, `code` invalido, account codes invalidos, ids vacios o `ConcurrencyToken` ausente.

#### 5.6.6 Search y detalle de cost centers

Route family:

- `GET /api/v1/companies/{companyId}/cost-centers`
- `GET /api/v1/cost-centers/{id}`

Uso principal:

- listar el catalogo operativo de centros de costo
- abrir el detalle canonical de un centro de costo concreto

Observaciones funcionales:

- `search` soporta filtros `type`, `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca solo por `code` y `name`.
- el orden observable del listado es `name`, luego `code`.
- `search` devuelve `PagedResponse<CostCenterListItemResponse>`.
- `get by id` devuelve `CostCenterResponse`.
- `get by id` siempre calcula `allowedActions` usando el estado activo y la existencia de referencias activas.
- `search` solo calcula `allowedActions` si `includeAllowedActions=true`.

#### 5.6.7 Uso y dependencias activas

Route family:

- `GET /api/v1/cost-centers/{id}/usage`

Uso principal:

- inspeccionar antes de inactivar si un centro de costo sigue siendo usado por otros modulos

Observaciones funcionales:

- devuelve conteos separados para `OrgUnits` activas e inactivas.
- devuelve conteos separados para `PositionSlots` activas e inactivas.
- `hasActiveReferences` se vuelve `true` si existe al menos una referencia activa en cualquiera de esos dos modulos.
- la logica de bloqueo de `inactivate` se basa en referencias activas, no en referencias historicas inactivas.
- los conteos de `PositionSlots` se resuelven desde la `OrgUnit` del `JobProfile` asociado a cada plaza, no desde una columna propia de la plaza.

#### 5.6.8 Exportes

Route family:

- `GET /api/v1/companies/{companyId}/cost-centers/export`

Uso principal:

- exportar el catalogo de centros de costo para analisis, conciliacion o trabajo operativo fuera del sistema

Observaciones funcionales:

- soporta `format=csv|xlsx`.
- reutiliza los mismos filtros de busqueda operativa, salvo paginacion.
- devuelve filas planas con `code`, `name`, `type`, account codes, `description`, `isActive` y timestamps.
- genera auditoria de tipo `ReportExported`.
- si `format` no es soportado, la respuesta observable es `COST_CENTER_EXPORT_FORMAT_INVALID`.

#### 5.6.9 Escrituras: create, update, activate e inactivate

Route family:

- `POST /api/v1/companies/{companyId}/cost-centers`
- `PUT /api/v1/cost-centers/{id}`
- `PATCH /api/v1/cost-centers/{id}/activate`
- `PATCH /api/v1/cost-centers/{id}/inactivate`

Uso principal:

- crear centros de costo del tenant
- corregir datos operativos y contables
- controlar el estado activo del catalogo

Observaciones funcionales:

- `create` devuelve `201 Created` con la `CostCenterResponse` creada.
- `create` exige `code`, `name` y `type`; acepta `payrollExpenseAccountCode`, `employerContributionAccountCode`, `provisionAccountCode` y `description`.
- `create` valida unicidad de `code` por tenant.
- `update` modifica datos escalares del centro de costo, exige `ConcurrencyToken` y puede cambiar `type` y account codes.
- `activate` exige `ConcurrencyToken` y reactiva el centro de costo.
- `inactivate` exige `ConcurrencyToken` y falla si existe uso activo en `OrgUnits` o en `PositionSlots` que lleguen a ese centro de costo por derivacion.

#### 5.6.10 Relacion con otros modulos

- `OrgUnits` puede referenciar centros de costo por `costCenterCode`.
- `JobProfiles` fija la `OrgUnit` obligatoria del puesto, y esa `OrgUnit` puede cargar `costCenterCode`.
- `PositionSlots` ya no almacena `CostCenterCode`; el detalle, los exportes y `usage` lo resuelven desde `JobProfile -> OrgUnit -> CostCenterCode`.
- La respuesta `usage` existe precisamente para hacer visible esa dependencia cruzada antes de desactivar un codigo.

`CostCenters` cierra el bloque organizacional base junto con `Org structure catalogs` y `OrgUnits`: primero se definen catalogos, luego la estructura y luego las claves de imputacion de costo que esa estructura y los perfiles/plazas reutilizan por derivacion.

### 5.7 Legal representatives

#### 5.7.1 Alcance

Este bloque cubre `LegalRepresentativesController` y expone la administracion tenant-scoped de representantes legales de la compania.

Familias de rutas:

- `/api/v1/companies/{companyId}/legal-representatives`
- `/api/v1/legal-representatives/{id}`
- `/api/v1/legal-representatives/{id}/usage`
- `/api/v1/companies/{companyId}/legal-representatives/export`
- `/api/v1/legal-representatives/{id}/activate`
- `/api/v1/legal-representatives/{id}/inactivate`
- `/api/v1/legal-representatives/{id}/set-primary`

#### 5.7.2 Proposito funcional en CLARIHR

El modulo `Legal representatives` mantiene las personas que representan legalmente a la compania dentro del tenant. Su funcion es registrar identidad, documento, cargo, tipo de representacion, vigencia, datos de contacto y cual representante activo es el principal.

Es un modulo administrativo sensible porque afecta onboarding de compania, datos corporativos y futuras referencias documentales o contractuales.

#### 5.7.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- El modulo es tenant-scoped por defecto. En rutas con `companyId`, ese valor debe coincidir con el tenant activo del token.
- `search`, `export` y `create` usan `companyId` en la ruta.
- `get by id`, `usage`, `update`, `activate`, `inactivate` y `set-primary` usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un representante legal existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver `404` plano.
- `LegalRepresentativeListItemResponse` devuelve `companyId`, nombre, documento, cargo, `representationType`, `isPrimary`, `isActive`, vigencias, `concurrencyToken`, timestamps y opcionalmente `allowedActions`.
- `LegalRepresentativeResponse` agrega `authorityDescription`, `appointmentInstrument`, `appointmentDateUtc`, `email` y `phone`.
- `isPrimary` es nullable en respuestas. `true` significa primario vigente; `false`, no primario; `null`, prioridad no especificada en el alta original.
- `LegalRepresentativeUsageResponse` hoy devuelve `legalRepresentativeId`, `activeDocumentReferencesCount` y `canInactivate`.
- `firstName` y `lastName` son obligatorios, maximo `100`, y usan validacion de nombre.
- `documentNumber` es obligatorio, maximo `80`, y usa regex alfanumerica con `_`, `.`, `/` o `-`.
- `positionTitle` es obligatorio, maximo `150`, y usa regex controlada.
- `authorityDescription` y `appointmentInstrument` aceptan hasta `500` caracteres.
- `email` es opcional, pero si se envia debe ser email valido y maximo `320`.
- `phone` es opcional y maximo `40`.
- `effectiveFromUtc` es obligatorio.
- `effectiveToUtc` es opcional, pero no puede ser menor que `effectiveFromUtc`.
- `pageSize` maximo es `100`, el default es `20`.
- `update`, `activate`, `inactivate` y `set-primary` usan concurrencia optimista con `ConcurrencyToken`.
- Todas las escrituras y el export generan auditoria.
- El sistema exige que siempre exista al menos un representante legal activo por compania.
- Solo puede existir un representante activo marcado como primario a la vez.

#### 5.7.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `LegalRepresentatives.Read`, `LegalRepresentatives.Admin` o `iam.administration.manage`.
- Escritura acepta alguno de estos permisos: `LegalRepresentatives.Admin` o `iam.administration.manage`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `LEGAL_REPRESENTATIVES_FORBIDDEN`.

#### 5.7.5 Errores observables relevantes en Legal representatives

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido para rutas tenant-scoped.
- `LEGAL_REPRESENTATIVES_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de representantes legales.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `LEGAL_REPRESENTATIVE_NOT_FOUND`: `404`, el representante solicitado no existe en el scope correcto.
- `LEGAL_REPRESENTATIVE_DOCUMENT_CONFLICT`: `409`, otro representante del mismo tenant ya usa esa combinacion de tipo y numero de documento.
- `LEGAL_REPRESENTATIVE_ACTIVE_MIN_REQUIRED`: `409`, no puede inactivarse el ultimo representante activo de la compania.
- `CONCURRENCY_CONFLICT`: `409`, el `concurrencyToken` ya no coincide con la version actual.
- `LEGAL_REPRESENTATIVE_EFFECTIVE_DATES_INVALID`: `422`, `effectiveToUtc` es menor que `effectiveFromUtc`.
- `LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION`: `422`, la operacion no es valida para el estado actual, por ejemplo marcar primario un registro inactivo.
- `LEGAL_REPRESENTATIVE_EXPORT_FORMAT_INVALID`: `400`, `format` no soportado en el export.
- `400` de validacion: `page/pageSize` invalidos, nombres invalidos, `documentNumber` invalido, `positionTitle` invalido, email invalido, ids vacios o `ConcurrencyToken` ausente.

#### 5.7.6 Search y detalle de legal representatives

Route family:

- `GET /api/v1/companies/{companyId}/legal-representatives`
- `GET /api/v1/legal-representatives/{id}`

Uso principal:

- listar representantes legales de la compania
- abrir el detalle canonical de un representante concreto

Observaciones funcionales:

- `search` soporta filtros `isActive`, `isPrimary`, `representationType`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `fullName`, `positionTitle` y `documentNumber`.
- el orden observable del listado prioriza los registros con `isPrimary = true` y luego `fullName`; `false` y `null` no se tratan como primarios.
- `search` devuelve `PagedResponse<LegalRepresentativeListItemResponse>`.
- `get by id` devuelve `LegalRepresentativeResponse`.
- `search` solo calcula `allowedActions` si `includeAllowedActions=true`.
- `search` aplica acciones permitidas basicas por estado y tipo, pero no incorpora la regla de minimo activo.
- `get by id` si incorpora la regla de minimo activo y puede devolver `CanInactivate = false` en `allowedActions` con la razon `At least one active legal representative is required.`.

#### 5.7.7 Uso y restriccion de inactivacion

Route family:

- `GET /api/v1/legal-representatives/{id}/usage`

Uso principal:

- verificar si el representante puede inactivarse sin romper la invariante minima del modulo

Observaciones funcionales:

- hoy `activeDocumentReferencesCount` se devuelve en `0`; el contrato existe, pero la implementacion actual no contabiliza referencias documentales activas.
- `canInactivate` depende de una sola regla observable hoy: si el registro ya esta inactivo, devuelve `true`; si esta activo, solo devuelve `true` cuando la compania tiene mas de un representante activo.
- la inactivacion no se bloquea por ser primario; al inactivarse, el dominio limpia `IsPrimary`.

#### 5.7.8 Exportes

Route family:

- `GET /api/v1/companies/{companyId}/legal-representatives/export`

Uso principal:

- exportar el registro de representantes legales para trabajo operativo, soporte legal o auditoria documental

Observaciones funcionales:

- soporta `format=csv|xlsx`.
- reutiliza los mismos filtros de busqueda operativa, salvo paginacion.
- devuelve filas planas con nombre, documento, cargo, tipo de representacion, vigencias, contacto, `isPrimary`, `isActive` y timestamps. Cuando `isPrimary` es `null`, el valor sale vacio en el export.
- genera auditoria de tipo `ReportExported`.
- si `format` no es soportado, la respuesta observable es `LEGAL_REPRESENTATIVE_EXPORT_FORMAT_INVALID`.

#### 5.7.9 Escrituras: create, update, activate, inactivate y set-primary

Route family:

- `POST /api/v1/companies/{companyId}/legal-representatives`
- `PUT /api/v1/legal-representatives/{id}`
- `PATCH /api/v1/legal-representatives/{id}/activate`
- `PATCH /api/v1/legal-representatives/{id}/inactivate`
- `PATCH /api/v1/legal-representatives/{id}/set-primary`

Uso principal:

- crear y mantener representantes legales de la compania
- controlar vigencia operativa
- designar el representante principal activo

Observaciones funcionales:

- `create` devuelve `201 Created` con la `LegalRepresentativeResponse` creada.
- `create` exige identidad, documento, cargo, tipo de representacion y `effectiveFromUtc`; acepta descripcion de autoridad, instrumento, fecha de nombramiento, contacto y `isPrimary`.
- `create` valida unicidad por `documentType + documentNumber` dentro del tenant.
- si `create` recibe `isPrimary=true`, el sistema quita la marca primaria al representante activo actual en la misma transaccion; no devuelve conflicto por primario duplicado.
- `update` exige `ConcurrencyToken` y puede cambiar todos los datos escalares, incluida la bandera `isPrimary`.
- `update` falla con `LEGAL_REPRESENTATIVE_STATE_RULE_VIOLATION` si se intenta dejar como primario un representante inactivo.
- `activate` exige `ConcurrencyToken` y reactiva el registro, pero no lo convierte automaticamente en primario.
- `inactivate` exige `ConcurrencyToken`, limpia `isPrimary` y falla si dejaria a la compania sin representantes activos.
- `set-primary` exige `ConcurrencyToken`, solo funciona sobre registros activos y desplaza al primario activo anterior en la misma transaccion.

#### 5.7.10 Relacion con otros modulos

- `Account companies` expone el catalogo auxiliar de `representation type` en `/api/account/companies/legal-representative-representation-types`.
- los tipos documentales de representantes legales y personal se unifican en `GET /api/v1/companies/{companyId}/reference-catalogs/identification-types`.
- `Account companies` tambien consume `InitialLegalRepresentativeInput` durante la creacion de una compania, donde `isPrimary` ahora es opcional.
- Los detalles de compania reutilizan resumenes de representantes activos para mostrar la representacion vigente del tenant.

`Legal representatives` queda asi como el modulo operativo posterior al onboarding: los catalogos se consultan desde `Account companies`, pero la administracion continua del registro legal ya ocurre aqui.

### 5.8 Locations

#### 5.8.1 Alcance

Este bloque cubre cinco controladores que juntos modelan la estructura geografica y operativa del tenant:

- `LocationHierarchyController`
- `LocationLevelsController`
- `LocationGroupsController`
- `WorkCenterTypesController`
- `WorkCentersController`

Familias de rutas:

- `/api/v1/companies/{companyId}/location-hierarchy`
- `/api/v1/companies/{companyId}/location-levels`
- `/api/v1/location-levels/{id}`
- `/api/v1/companies/{companyId}/location-groups`
- `/api/v1/companies/{companyId}/location-groups/tree`
- `/api/v1/location-groups/{id}`
- `/api/v1/companies/{companyId}/work-center-types`
- `/api/v1/work-center-types/{id}`
- `/api/v1/companies/{companyId}/work-centers`
- `/api/v1/work-centers/{id}`

#### 5.8.2 Proposito funcional en CLARIHR

El modulo `Locations` define donde existe operativamente la compania dentro del tenant. Su funcion es configurar:

- si la jerarquia territorial es multi-nivel o no
- que niveles geografico-operativos existen
- que grupos de ubicacion concretos se registran en cada nivel
- que tipos de work center existen
- y en que grupo se ubica cada work center

Es el modulo base de localizacion del tenant. Primero define la estructura, luego los nodos geografico-organizativos y finalmente los work centers utilizables por otros modulos.

#### 5.8.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Todo el modulo es tenant-scoped.
- Las rutas de coleccion usan `companyId` en la ruta y exigen que coincida con el tenant activo del token.
- Las rutas por recurso usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un recurso existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de devolver `404` plano.
- Todos los listados usan `page/pageSize` con maximo `100` y default `20` cuando aplican.
- Los `code` de grupos, tipos y work centers usan regex alfanumerica con `_` o `-`, longitud maxima `50`.
- Las operaciones de `update/activate/inactivate/move/reassign-group` usan concurrencia optimista con `ConcurrencyToken`.
- Todo el modulo comparte una sola superficie de permisos: `Locations.Read`, `Locations.Admin` o `iam.administration.manage`.
- La jerarquia admite una unica regla fuerte para work centers: solo el ultimo nivel activo puede tener `AllowsWorkCenters = true`.
- El modulo soporta configuracion `defaultGroupCode/defaultGroupName` en la jerarquia y protege grupos marcados como `IsDefault` si existen.

#### 5.8.4 Autorizacion observable

- Lectura acepta alguno de estos permisos: `Locations.Read`, `Locations.Admin` o `iam.administration.manage`.
- Escritura acepta alguno de estos permisos: `Locations.Admin` o `iam.administration.manage`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado dentro del tenant correcto pero no cumple permisos, la API responde `LOCATIONS_FORBIDDEN`.

#### 5.8.5 Errores observables relevantes en Locations

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido.
- `LOCATIONS_FORBIDDEN`: `403`, el usuario no tiene permisos sobre administracion de ubicaciones.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `LOCATION_HIERARCHY_NOT_FOUND`: `404`, no existe configuracion de jerarquia para la compania.
- `LOCATION_LEVEL_NOT_FOUND`: `404`, el nivel solicitado no existe o no esta disponible para la operacion.
- `LOCATION_GROUP_NOT_FOUND`: `404`, el grupo solicitado no existe en el scope correcto.
- `WORK_CENTER_TYPE_NOT_FOUND`: `404`, el tipo de work center solicitado no existe en el scope correcto.
- `WORK_CENTER_NOT_FOUND`: `404`, el work center solicitado no existe en el scope correcto.
- `LOCATION_LEVEL_ORDER_CONFLICT`: `409`, otro nivel ya usa ese `LevelOrder`.
- `LOCATION_GROUP_CODE_CONFLICT`: `409`, otro grupo ya usa ese `code`.
- `WORK_CENTER_TYPE_CODE_CONFLICT`: `409`, otro tipo ya usa ese `code`.
- `WORK_CENTER_CODE_CONFLICT`: `409`, otro work center ya usa ese `code`.
- `LOCATION_GROUP_PARENT_REQUIRED`: `409`, un grupo no root no puede existir sin padre.
- `LOCATION_GROUP_INVALID_PARENT`: `409`, el padre no corresponde al nivel esperado.
- `LOCATION_GROUP_CYCLE_DETECTED`: `409`, el movimiento solicitado crearia un ciclo.
- `LOCATION_GROUP_HAS_ACTIVE_CHILDREN`: `409`, no puede inactivarse porque tiene hijos activos.
- `LOCATION_GROUP_HAS_ACTIVE_WORK_CENTERS`: `409`, no puede inactivarse porque tiene work centers activos.
- `DEFAULT_GROUP_PROTECTED`: `409`, un grupo marcado como default no puede renombrarse, recodificarse ni inactivarse.
- `LAST_ACTIVE_LEVEL_REQUIRED`: `409`, no puede dejarse al tenant sin niveles activos.
- `LOCATION_LEVEL_REQUIRED_ACTIVE`: `409`, un nivel requerido debe permanecer activo.
- `WORK_CENTERS_ALLOWED_ONLY_ON_LAST_LEVEL`: `409`, solo el ultimo nivel activo puede permitir work centers.
- `LOCATION_LEVEL_HAS_ACTIVE_GROUPS`: `409`, no puede inactivarse un nivel con grupos activos.
- `LOCATION_LEVEL_ALLOWS_WORK_CENTERS_IN_USE`: `409`, no puede quitarse `AllowsWorkCenters` a un nivel si grupos activos de ese nivel sostienen work centers activos.
- `WORK_CENTER_TYPE_IN_USE`: `409`, no puede inactivarse un tipo si work centers activos aun lo usan.
- `WORK_CENTER_TYPE_INACTIVE`: `409`, no puede asignarse un tipo inactivo a un work center.
- `LOCATION_GROUP_INACTIVE`: `409`, no puede asignarse un grupo inactivo a un work center.
- `LOCATION_GROUP_PARENT_INACTIVE`: `409`, no puede crearse o moverse bajo un padre inactivo.
- `LOCATION_GROUP_LEVEL_NOT_ALLOWED_FOR_WORK_CENTER`: `409`, el grupo seleccionado pertenece a un nivel que no permite work centers.
- `WORK_CENTER_ADDRESS_REQUIRED`: `400`, el tipo seleccionado exige `Address`.
- `WORK_CENTER_GEO_REQUIRED`: `400`, el tipo seleccionado exige `GeoLat` y `GeoLong`.
- `WORK_CENTER_INVALID_COORDINATES`: `400`, latitud o longitud fuera de rango.
- `LOCATION_SINGLE_LEVEL_REQUIRES_ONE_ACTIVE_LEVEL`: `409`, no puede activarse modo single-level si no existe exactamente un nivel activo.
- `400` de validacion: `page/pageSize` invalidos, `LevelOrder <= 0`, `code` invalido, ids vacios, email invalido o `ConcurrencyToken` ausente.

#### 5.8.6 Jerarquia de ubicacion

Route family:

- `GET /api/v1/companies/{companyId}/location-hierarchy`
- `PUT /api/v1/companies/{companyId}/location-hierarchy`

Uso principal:

- consultar o cambiar la configuracion global de jerarquia de ubicaciones del tenant

Observaciones funcionales:

- `get` devuelve `LocationHierarchyConfigResponse` con `isMultiLevel`, `defaultGroupCode`, `defaultGroupName` y `concurrencyToken`.
- `update` solo modifica `isMultiLevel`.
- `update` exige `ConcurrencyToken`.
- si se intenta pasar a single-level, la API exige que exista exactamente un nivel activo.

#### 5.8.7 Location levels

Route family:

- `GET /api/v1/companies/{companyId}/location-levels`
- `POST /api/v1/companies/{companyId}/location-levels`
- `PUT /api/v1/location-levels/{id}`
- `PATCH /api/v1/location-levels/{id}/activate`
- `PATCH /api/v1/location-levels/{id}/inactivate`

Uso principal:

- definir y mantener los niveles del modelo territorial del tenant

Observaciones funcionales:

- `list` devuelve todos los niveles ordenados por `LevelOrder`.
- `create` exige `LevelOrder > 0`, `DisplayName`, `IsActive`, `IsRequired` y `AllowsWorkCenters`.
- un nivel requerido debe estar activo.
- un nivel que permite work centers tambien debe estar activo.
- `create` y `update` bloquean configuraciones donde mas de un nivel activo permita work centers o donde ese nivel no sea el ultimo activo.
- `update` exige `ConcurrencyToken`.
- `update/inactivate` bloquean desactivar el ultimo nivel activo.
- `update/inactivate` bloquean desactivar niveles requeridos.
- `update/inactivate` bloquean desactivar niveles con grupos activos.
- `update` tambien puede bloquear quitar `AllowsWorkCenters` si grupos activos de ese nivel sostienen work centers activos.

#### 5.8.8 Location groups

Route family:

- `GET /api/v1/companies/{companyId}/location-groups/tree`
- `GET /api/v1/companies/{companyId}/location-groups`
- `POST /api/v1/companies/{companyId}/location-groups`
- `PUT /api/v1/location-groups/{id}`
- `PATCH /api/v1/location-groups/{id}/move`
- `PATCH /api/v1/location-groups/{id}/activate`
- `PATCH /api/v1/location-groups/{id}/inactivate`

Uso principal:

- registrar los nodos concretos de la jerarquia territorial

Observaciones funcionales:

- `tree` devuelve la jerarquia completa anidada.
- `search` soporta `levelOrder`, `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- `search` ordena por `LevelOrder` y luego por `Name`.
- `tree` tambien ordena por `LevelOrder` y luego por `Name`.
- `create` exige que el `LevelOrder` exista y este activo.
- grupos de `LevelOrder = 1` no pueden tener padre.
- grupos de niveles superiores exigen padre activo del nivel inmediatamente anterior.
- `move` mantiene fijo el `LevelOrder` del grupo; solo cambia el padre.
- `move` detecta ciclos y bloquea mover bajo un descendiente.
- `update` protege grupos `IsDefault` contra cambio de `code` o `name`.
- `inactivate` protege grupos `IsDefault` y ademas bloquea si hay hijos activos o work centers activos colgando del grupo.

#### 5.8.9 Work center types

Route family:

- `GET /api/v1/companies/{companyId}/work-center-types`
- `POST /api/v1/companies/{companyId}/work-center-types`
- `PUT /api/v1/work-center-types/{id}`
- `PATCH /api/v1/work-center-types/{id}/activate`
- `PATCH /api/v1/work-center-types/{id}/inactivate`

Uso principal:

- mantener el catalogo de tipos de work center y sus requerimientos operativos

Observaciones funcionales:

- `list` soporta `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- el orden observable del listado es `name`, luego `code`.
- el response incluye `RequiresAddress`, `RequiresGeo` y `AllowsBiometric`.
- `create` y `update` validan unicidad de `code` por tenant.
- `update/activate/inactivate` exigen `ConcurrencyToken`.
- `inactivate` falla si work centers activos siguen usando ese tipo.

#### 5.8.10 Work centers

Route family:

- `GET /api/v1/companies/{companyId}/work-centers`
- `GET /api/v1/work-centers/{id}`
- `POST /api/v1/companies/{companyId}/work-centers`
- `PUT /api/v1/work-centers/{id}`
- `PATCH /api/v1/work-centers/{id}/reassign-group`
- `PATCH /api/v1/work-centers/{id}/activate`
- `PATCH /api/v1/work-centers/{id}/inactivate`

Uso principal:

- registrar sedes, oficinas o puntos operativos concretos del tenant

Observaciones funcionales:

- `search` soporta `groupId`, `typeId`, `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code`, `name`, nombre del grupo y nombre del tipo.
- el orden observable del listado es `name`, luego `code`.
- `get by id` devuelve tipo y grupo resueltos junto con `LocationGroupLevelOrder`.
- `create` y `update` exigen `Code`, `Name`, `WorkCenterTypeId` y `LocationGroupId`.
- `create` y `update` validan unicidad de `code` por tenant.
- el tipo asignado debe existir y estar activo.
- el grupo asignado debe existir, estar activo y pertenecer a un nivel activo que permita work centers.
- si el tipo exige direccion, `Address` pasa a ser obligatorio.
- si el tipo exige geo, `GeoLat` y `GeoLong` pasan a ser obligatorios.
- `GeoLat` debe estar en `[-90, 90]` y `GeoLong` en `[-180, 180]`.
- `reassign-group` solo cambia el grupo y revalida que el nuevo grupo pueda alojar work centers.
- `update/reassign-group/activate/inactivate` exigen `ConcurrencyToken`.

#### 5.8.11 Seed y relacion con onboarding

- el flujo de provisioning de compania inicializa este modulo automaticamente al crear la compania.
- hoy la semilla visible soporta un solo template de pais: `SV`.
- para `SV`, el sistema crea jerarquia multi-nivel con niveles `Pais`, `Departamento` y `Municipio`.
- en esa semilla, solo `Municipio` permite work centers.
- tambien se siembran grupos iniciales desde la plantilla del pais; por eso `Locations` no nace vacio tras el onboarding.
- `OrgUnits` y modulos posteriores reutilizan esta base geografico-operativa para ubicar estructuras y centros de trabajo.

`Locations` es el modulo que convierte el tenant de una compania nueva en una compania ubicable: primero se define la jerarquia, luego los nodos de ubicacion y finalmente los work centers operativos.

### 5.9 Job and competency design

#### 5.9.1 Alcance

Este bloque cubre nueve controladores que juntos definen el diseno formal de puestos, competencias y plazas operativas del tenant:

- `JobCatalogsController`
- `JobProfilesController`
- `JobProfileRequirementsController`
- `JobProfileFunctionsController`
- `JobProfileRelationsController`
- `JobProfileCompetenciesController`
- `CompetencyFrameworkController`
- `PositionDescriptionCatalogItemsController`
- `PositionCategoryClassificationsController`
- `PositionCategoriesController`
- `PositionSlotsController`

Familias de rutas:

- `/api/v1/companies/{companyId}/job-catalogs/{category}`
- `/api/v1/companies/{companyId}/job-catalogs/{category}/{jobCatalogPublicId}`
- `/api/v1/companies/{companyId}/job-profiles`
- `/api/v1/job-profiles/{publicId}`
- `/api/v1/job-profiles/{publicId}/vacancy-template`
- `/api/v1/job-profiles/{publicId}/print`
- `/api/v1/job-profiles/{publicId}/export`
- `/api/v1/job-profiles/{publicId}/publish`
- `/api/v1/job-profiles/{publicId}/archive`
- `/api/v1/job-profiles/{jobProfilePublicId}/requirements`
- `/api/v1/job-profiles/{jobProfilePublicId}/requirements/{requirementPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/functions`
- `/api/v1/job-profiles/{jobProfilePublicId}/functions/{functionPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/relations`
- `/api/v1/job-profiles/{jobProfilePublicId}/relations/{relationPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/competencies`
- `/api/v1/job-profiles/{jobProfilePublicId}/competencies/{competencyPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/trainings`
- `/api/v1/job-profiles/{jobProfilePublicId}/trainings/{trainingPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/benefits`
- `/api/v1/job-profiles/{jobProfilePublicId}/benefits/{benefitPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/working-conditions`
- `/api/v1/job-profiles/{jobProfilePublicId}/working-conditions/{workingConditionPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/dependent-positions`
- `/api/v1/job-profiles/{jobProfilePublicId}/dependent-positions/{dependentPositionPublicId}`
- `/api/v1/job-profiles/{jobProfilePublicId}/compensations`
- `/api/v1/job-profiles/{jobProfilePublicId}/compensations/{compensationPublicId}`
- `/api/v1/companies/{companyId}/occupational-pyramid-levels`
- `/api/v1/occupational-pyramid-levels/{id}`
- `/api/v1/companies/{companyId}/competency-conducts`
- `/api/v1/competency-conducts/{id}`
- `/api/v1/competency-conducts/{id}/behaviors`
- `/api/v1/job-profiles/{publicId}/competency-matrix`
- `/api/v1/job-profiles/{publicId}/competency-matrix/export`
- `/api/v1/companies/{companyPublicId}/position-description-catalogs/{catalogType}/items`
- `/api/v1/position-description-catalogs/{catalogType}/items/{positionDescriptionCatalogItemPublicId}`
- `/api/v1/companies/{companyPublicId}/position-category-classifications`
- `/api/v1/position-category-classifications/{positionCategoryClassificationPublicId}`
- `/api/v1/companies/{companyPublicId}/position-categories`
- `/api/v1/position-categories/{positionCategoryPublicId}`
- `/api/v1/companies/{companyId}/position-slots`
- `/api/v1/position-slots/{id}`
- `/api/v1/companies/{companyId}/position-slots/graph`
- `/api/v1/companies/{companyId}/position-slots/diagram-export`
- `/api/v1/companies/{companyId}/position-slots/export`
- `/api/v1/position-slots/{id}/status`
- `/api/v1/position-slots/{id}/dependencies`
- `/api/v1/position-slots/{id}/occupancy`

#### 5.9.2 Proposito funcional en CLARIHR

Este bloque convierte la estructura organizacional en un modelo formal de puestos y plazas:

- `Job catalogs` mantiene diccionarios reutilizables para conocimientos, competencias, capacitaciones, niveles conductuales y otros ejes de diseno.
- `Job profiles` define el puesto tipo: objetivo, dependencias, requisitos, funciones, relaciones, compensacion referenciada al tabulador salarial, beneficios y condiciones.
- `Competency framework` modela la piramide ocupacional, los conductos esperados y la matriz de expectativas por perfil.
- `Position description catalogs` provee el vocabulario formal del descriptor de puestos y sus clasificaciones.
- `Position slots` aterriza ese diseno en plazas concretas dentro de la empresa: una posicion real asociada a un `JobProfile`, que hereda `OrgUnit` y `CostCenter` desde ese perfil, opcionalmente en un `WorkCenter`, con capacidad, ocupacion y dependencias.

En terminos funcionales, este bloque es la base del diseno organizacional y del gobierno del talento: sin estos endpoints no hay perfiles de puesto consistentes, matrices de competencia por perfil ni plazas concretas para dotacion.

#### 5.9.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Todo el bloque es tenant-scoped.
- Las rutas de coleccion usan `companyId` en la ruta y exigen que coincida con el tenant activo del token.
- Las rutas por recurso usan solo `{id}` y resuelven el tenant desde el token actual.
- Si el recurso existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de un `404` plano.
- Los listados paginados usan `page/pageSize`, con maximo `100` y default `20`.
- `PositionSlots /graph` acepta `depth` opcional, pero si se envia debe estar entre `1` y `15`.
- Los codigos del bloque usan regex alfanumerica con `_` o `-`, longitud maxima `50`.
- `q/search` admite hasta `150` caracteres.
- Las mutaciones de `JobProfiles`, sus sub-recursos granulares y `JobCatalogs` usan concurrencia HTTP con `If-Match: "<concurrencyToken>"` en `PUT`, `PATCH` y `DELETE`; el token ya no se envia en body ni como operacion `/concurrencyToken` del JSON Patch.
- La validacion de `If-Match` esta centralizada en el binder `[FromIfMatch]`: los controllers reciben un `Guid concurrencyToken` ya validado y los errores de ausencia/formato usan el `ProblemDetails` estandar de model state.
- Las respuestas exitosas `200 OK` y `201 Created` de esas mutaciones incluyen `ETag: "<nuevoConcurrencyToken>"` para encadenar operaciones sin hacer un `GET` adicional.
- Los `GET` que devuelven recursos o colecciones con `concurrencyToken` emiten `ETag`. Si el DTO expone `modifiedAtUtc` o `createdAtUtc`, tambien emiten `Last-Modified`. El cliente puede enviar `If-None-Match` y la API responde `304 Not Modified` cuando el ETag calculado no cambio.
- En listados paginados, el `ETag` es agregado: incluye metadata de pagina y los `concurrencyToken` de los items devueltos. Por eso `Search JobProfiles` y los sub-recursos paginados soportan cache condicional sin introducir tokens de pagina en Application.
- Otros submodulos del bloque que aun no fueron migrados mantienen su contrato vigente con `concurrencyToken` en body o patch segun corresponda.
- Todas las escrituras generan auditoria.
- `print` y `export` de `JobProfiles`, `export` de matriz de competencias y `export/diagram-export` de `PositionSlots` tambien generan auditoria.
- No existen endpoints de borrado fisico en este bloque.
- Las escrituras complejas por `PUT` son de reemplazo, no incrementales:
- `PUT /job-profiles/{publicId}` reemplaza todas las colecciones anidadas del perfil; si una coleccion llega `null`, el controller la convierte en `[]` y el handler limpia la seccion existente.
- `PATCH /job-profiles/{publicId}` aplica JSON Patch desde Application sobre el agregado cargado en transaccion; si el patch no toca `/compensation`, preserva la referencia de compensacion existente y evita reconstruirla desde una proyeccion parcial.
- `POST/PUT /job-profiles` usan `compensation` (singular, opcional) como referencia canonica a `salaryTabulatorLineId`; si llega `null`, se limpia la referencia de compensacion del perfil.
- Los sub-recursos granulares de `JobProfiles` (`requirements`, `functions`, `relations`, `competencies`, `trainings`, `benefits`, `working-conditions`, `dependent-positions` y `compensations`) devuelven solo el sub-recurso mutado, no el `JobProfileResponse` completo ni wrappers `{ item, parentConcurrencyToken }`.
- En requests y responses de sub-recursos de `JobProfiles`, el identificador externo expuesto sigue el canon `*PublicId`: `trainingPublicId`, `benefitPublicId`, `workingConditionPublicId`, `dependentPositionPublicId` y `compensationPublicId`. Las referencias relacionadas tambien usan `*PublicId`: `catalogItemPublicId`, `workConditionTypeCatalogItemPublicId`, `dependentJobProfilePublicId` y `salaryTabulatorLinePublicId`.
- `POST`, `PUT` y `PATCH` de cada sub-recurso granular comparten un unico DTO de mutacion por sub-recurso (`Mutate*Request` en API), porque `PUT` ya recibe concurrencia por `If-Match` y no necesita un request body distinto al de `POST`.
- `PATCH` granular de `trainings`, `benefits`, `working-conditions`, `dependent-positions` y `compensations` tambien usa JSON Pointer con nombres `*PublicId`; por ejemplo `/catalogItemPublicId`, `/workConditionTypeCatalogItemPublicId`, `/dependentJobProfilePublicId` y `/salaryTabulatorLinePublicId`.
- `POST` de sub-recursos responde `201 Created` con `Location` apuntando al `*PublicId` creado y `ETag` del sub-recurso creado; `PUT` y `PATCH` retornan `200 OK` con el snapshot del sub-recurso y `ETag` del nuevo token del sub-recurso; `DELETE` retorna `200 OK` con `{ parentConcurrencyToken }` y `ETag` del nuevo token del `JobProfile` padre.
- `JobProfileRequirements` ya usa concurrencia granular por entidad hija: `GET /api/v1/job-profiles/{jobProfilePublicId}/requirements` ahora acepta paging opcional con `page` y `pageSize` y responde `PagedResponse<JobProfileRequirementResponse>`; `POST` crea un requisito sin pedir token y responde `201 Created` con `JobProfileRequirementResponse`; `PUT` y `PATCH` retornan la entidad del requisito y validan `If-Match` con el token del requisito; `DELETE` valida `If-Match` con el token del requisito y retorna el token actualizado del padre.
- `PATCH /api/v1/job-profiles/{jobProfilePublicId}/requirements/{requirementPublicId}` consume `application/json-patch+json` con array RFC 6902 como body raiz. No debe incluir `/concurrencyToken`; el token actual viaja exclusivamente en `If-Match`.
- `DELETE /api/v1/job-profiles/{jobProfilePublicId}/requirements/{requirementPublicId}` requiere `If-Match: "<ConcurrencyToken-del-requisito>"` y responde `200 OK` con `{ parentConcurrencyToken }`, no con el snapshot eliminado.
- `JobProfileFunctions` aplica el mismo patron granular que `requirements`: `GET /api/v1/job-profiles/{jobProfilePublicId}/functions` ahora acepta paging opcional con `page` y `pageSize` y responde `PagedResponse<JobProfileFunctionResponse>`; `POST` crea una funcion sin pedir token y responde `201 Created` con `JobProfileFunctionResponse`; `PUT` y `PATCH` retornan la entidad de la funcion y validan `If-Match` con el token de la funcion; `DELETE` retorna `{ parentConcurrencyToken }`. Cambios de contrato vs. la version anterior basada en wrapper: el campo `id` pasa a `functionPublicId`, `frequencyCatalogItemId` pasa a `frequencyCatalogItemPublicId`, `POST` ya no recibe `concurrencyToken` ni devuelve `{ item, parentConcurrencyToken }`, y `DELETE` ya no expone el header `Parent-Concurrency-Token`.
- `PATCH /api/v1/job-profiles/{jobProfilePublicId}/functions/{functionPublicId}` consume `application/json-patch+json` con array RFC 6902 como body raiz. No debe incluir `/concurrencyToken`; el token actual viaja exclusivamente en `If-Match`.
- `DELETE /api/v1/job-profiles/{jobProfilePublicId}/functions/{functionPublicId}` requiere `If-Match: "<ConcurrencyToken-de-la-funcion>"` y responde `200 OK` con `{ parentConcurrencyToken }`, no con el snapshot eliminado.
- `JobProfileRelations` aplica el mismo patron granular que `requirements` y `functions`: `GET /api/v1/job-profiles/{jobProfilePublicId}/relations` ahora acepta paging opcional con `page` y `pageSize` y responde `PagedResponse<JobProfileRelationResponse>`; `POST` crea una relacion sin pedir token y responde `201 Created` con `JobProfileRelationResponse`; `PUT` y `PATCH` retornan la entidad de la relacion y validan `If-Match` con el token de la relacion; `DELETE` retorna `{ parentConcurrencyToken }`. Cambios de contrato vs. la version anterior basada en wrapper: el campo `id` pasa a `relationPublicId`, `catalogItemId` pasa a `catalogItemPublicId`, `POST` ya no recibe `concurrencyToken` ni devuelve `{ item, parentConcurrencyToken }`, y `DELETE` ya no expone el header `Parent-Concurrency-Token`.
- `PATCH /api/v1/job-profiles/{jobProfilePublicId}/relations/{relationPublicId}` consume `application/json-patch+json` con array RFC 6902 como body raiz. No debe incluir `/concurrencyToken`; el token actual viaja exclusivamente en `If-Match`.
- `DELETE /api/v1/job-profiles/{jobProfilePublicId}/relations/{relationPublicId}` requiere `If-Match: "<ConcurrencyToken-de-la-relacion>"` y responde `200 OK` con `{ parentConcurrencyToken }`, no con el snapshot eliminado.
- `JobProfileCompetencies` (competencias legacy, independiente de la matriz de `CompetencyFrameworkController`) aplica el mismo patron granular: `GET /api/v1/job-profiles/{jobProfilePublicId}/competencies` ahora acepta paging opcional con `page` y `pageSize` y responde `PagedResponse<JobProfileLegacyCompetencyResponse>`; `POST` crea una competencia sin pedir token y responde `201 Created` con `JobProfileLegacyCompetencyResponse`; `PUT` y `PATCH` retornan la entidad de la competencia y validan `If-Match` con el token de la competencia; `DELETE` retorna `{ parentConcurrencyToken }`. Cambios de contrato vs. la version anterior basada en wrapper: el campo `id` pasa a `competencyPublicId`, `catalogItemId` pasa a `catalogItemPublicId`, `POST` ya no recibe `concurrencyToken` ni devuelve `{ item, parentConcurrencyToken }`, y `DELETE` ya no expone el header `Parent-Concurrency-Token`. Si `name` llega vacio en `POST`/`PUT`, se toma del `JobCatalogItem` referenciado; si no hay catalogo y `name` queda vacio se responde `JOB_PROFILE_COMPETENCY_NAME_REQUIRED` (`400`).
- `PATCH /api/v1/job-profiles/{jobProfilePublicId}/competencies/{competencyPublicId}` consume `application/json-patch+json` con array RFC 6902 como body raiz. No debe incluir `/concurrencyToken`; el token actual viaja exclusivamente en `If-Match`.
- `DELETE /api/v1/job-profiles/{jobProfilePublicId}/competencies/{competencyPublicId}` requiere `If-Match: "<ConcurrencyToken-de-la-competencia>"` y responde `200 OK` con `{ parentConcurrencyToken }`, no con el snapshot eliminado.
- `GET /api/v1/job-profiles/{jobProfilePublicId}/trainings`, `/benefits`, `/working-conditions` y `/dependent-positions` aceptan paging opcional con `page` y `pageSize`, default `20`, maximo `100`, y responden `PagedResponse<JobProfileTrainingResponse>`, `PagedResponse<JobProfileBenefitResponse>`, `PagedResponse<JobProfileWorkingConditionResponse>` y `PagedResponse<JobProfileDependentPositionResponse>` respectivamente.
- `GET /api/v1/job-profiles/{jobProfilePublicId}/compensations` permanece sin paginar porque el modelo actual permite una sola compensacion por perfil; el handler aplica cap duro de `1` item antes de responder `IReadOnlyCollection<JobProfileCompensationItemResponse>`.
- `PUT /job-profiles/{publicId}` y `PATCH /job-profiles/{publicId}` tambien requieren `If-Match` y responden con `ETag`; `PUT` ya no acepta `concurrencyToken` en body y `PATCH` ya no acepta `/concurrencyToken`.
- Todos los endpoints `PATCH` que consumen `application/json-patch+json` en este bloque aplican hardening uniforme: maximo `50` operaciones RFC 6902 por documento y limite de body de `64 KiB`. Si el cliente excede `50` operaciones responde `400 common.validation`; si excede el tamano permitido el servidor rechaza la peticion con `413`.
- `PUT /job-profiles/{publicId}/competency-matrix`, `PATCH /job-profiles/{publicId}/publish`, `PATCH /job-profiles/{publicId}/archive` y `GET /job-profiles/{publicId}` no cambian contrato por este ajuste.
- `PUT /competency-conducts/{id}/behaviors` reemplaza el conjunto completo de behaviors del conducto.
- `PUT /job-profiles/{publicId}/competency-matrix` reemplaza la matriz completa del perfil; una lista vacia limpia la matriz.
- `PATCH /position-slots/{id}/dependencies` sobrescribe tanto la dependencia directa como la funcional; `null` limpia la relacion.
- `JobProfileStatus` hoy expone `Draft`, `Published` y `Archived`.
- `PositionSlotStatus` hoy expone `Vacant`, `Occupied` y `Suspended`.
- `POST/PUT /job-profiles` exige `OrgUnitPublicId`; ya no existen perfiles de puesto sin unidad organizativa.
- `POST/PUT /position-slots` ya no aceptan `OrgUnitPublicId` ni `CostCenterCode`; ambos valores se infieren desde `JobProfile -> OrgUnit`.
- `POST/PUT /position-slots` aceptan `RolePublicId` opcional; si se envia debe resolver un rol valido del catalogo IAM del mismo tenant.
- En `PositionSlots`, el tipo de contrato no lo envia el cliente: se deriva desde `JobProfile -> PositionCategory -> PositionCategoryClassification -> PositionContractType`.
- `PATCH /salary-tabulator/change-requests/{id}/approve` ahora aplica un guardrail de cobertura: si el cambio deja `JobProfiles` referenciando una combinacion `salaryClass + salaryScale` sin linea activa para su fecha efectiva, responde `SALARY_TABULATOR_JOB_PROFILE_COVERAGE_CONFLICT` (`409`) y revierte la aprobacion.
- `POST /salary-tabulator/change-requests` crea una solicitud con `items[]`; cada item usa el mismo contrato de linea que el `PUT`, incluyendo `changeType`. No recibe `reason`; el backend conserva una razon interna por defecto.
- `allowedActions` conserva `canEdit`, `canDelete`, `canArchive`, `canActivate`, `canInactivate` y `reasons`, y agrega flags de workflow `canSubmit`, `canApprove`, `canReject`, `canCancel`, `canPublish` y `canFinalize`.
- En tabulador salarial, `allowedActions.actionPermissions[]` expone por accion `{ action, permissionCode, allowed, reasons }`; `SalaryTabulator.Request` respalda `edit`, `submit` y `cancel`, mientras `SalaryTabulator.Approve` respalda `approve` y `reject`.
- En `change requests`, `Draft` habilita `canSubmit/canCancel` para usuarios con `SalaryTabulator.Request`; `Submitted` habilita `canApprove/canReject` para usuarios con `SalaryTabulator.Approve`, pero `canApprove=false` cuando el usuario actual es el solicitante.

#### 5.9.4 Autorizacion observable

- Lectura de `JobProfiles` y `JobCatalogs` acepta alguno de estos permisos: `JobProfiles.Read`, `JobProfiles.Admin`, `JobCatalogs.Admin` o `iam.administration.manage`.
- Escritura de perfiles acepta alguno de estos permisos: `JobProfiles.Admin` o `iam.administration.manage`.
- Escritura de job catalogs acepta alguno de estos permisos: `JobCatalogs.Admin` o `iam.administration.manage`.
- Lectura de `CompetencyFramework` acepta alguno de estos permisos: `CompetencyFramework.Read`, `CompetencyFramework.Admin` o `iam.administration.manage`.
- Escritura de `CompetencyFramework` acepta alguno de estos permisos: `CompetencyFramework.Admin` o `iam.administration.manage`.
- Lectura de `PositionDescriptionCatalogs` acepta alguno de estos permisos: `PositionDescriptionCatalogs.Read`, `PositionDescriptionCatalogs.Admin` o `iam.administration.manage`.
- Escritura de `PositionDescriptionCatalogs` acepta alguno de estos permisos: `PositionDescriptionCatalogs.Admin` o `iam.administration.manage`.
- Lectura de `PositionSlots` acepta alguno de estos permisos: `PositionSlots.Read`, `PositionSlots.Admin` o `iam.administration.manage`.
- Escritura de `PositionSlots` acepta alguno de estos permisos: `PositionSlots.Admin` o `iam.administration.manage`.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado en el tenant correcto pero no cumple permisos, la API responde el `FORBIDDEN` especifico del submodulo:
- `JOB_PROFILES_FORBIDDEN`
- `COMPETENCY_FRAMEWORK_FORBIDDEN`
- `POSITION_DESCRIPTION_CATALOG_FORBIDDEN`
- `POSITION_SLOTS_FORBIDDEN`

#### 5.9.5 Errores observables relevantes en Job and competency design

Errores transversales:

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido.
- `TENANT_MISMATCH`: `403`, el recurso existe o se intenta operar otro tenant distinto al activo.
- `CONCURRENCY_CONFLICT`: `409`, el `ConcurrencyToken` ya no coincide con la version actual.

Errores relevantes en `JobProfiles` y `JobCatalogs`:

- `common.validation`: `400`, request body invalido, `model binding` invalido o validaciones de entrada; responde con `code`, `traceId` y `errors` por campo usando nombres del contrato publico. En `create/update`, `orgUnitPublicId` ahora es obligatorio.
- `JOB_PROFILE_NOT_FOUND`: `404`, el perfil solicitado no existe en el scope correcto.
- `JOB_CATALOG_ITEM_NOT_FOUND`: `404`, el catalog item solicitado no existe en el scope correcto.
- `JOB_PROFILE_ORG_UNIT_NOT_FOUND`: `404`, el `OrgUnit` referenciado no se pudo resolver.
- `JOB_PROFILE_REPORTS_TO_NOT_FOUND`: `404`, el perfil superior referenciado no se pudo resolver.
- `JOB_PROFILE_CODE_CONFLICT`: `409`, otro perfil ya usa ese `code`.
- `JOB_CATALOG_ITEM_CODE_CONFLICT`: `409`, otro item de la misma categoria ya usa ese `code`.
- `JOB_PROFILE_DEPENDENCY_CYCLE`: `409`, `reportsTo` o `dependentPositions` crearian una dependencia circular entre perfiles; el usuario debe revisar el perfil superior y las posiciones dependientes.
- `JOB_PROFILE_STATE_CONFLICT`: `409`, la operacion no aplica al estado actual, por ejemplo editar un perfil archivado.
- `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING`: `422`, faltan requisitos minimos para publicar.
- `JOB_PROFILE_COMPENSATION_TABULATOR_LINE_NOT_FOUND`: `422`, el `salaryTabulatorLineId` enviado en `compensation` no existe en el tenant o no esta activo para la fecha efectiva del perfil.
- `JOB_CATALOG_INLINE_CREATE_FORBIDDEN`: `403`, el payload quiso crear catalogos inline sin permisos de catalog admin.
- `JOB_PROFILE_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `json|csv`.

Errores relevantes en `CompetencyFramework`:

- `OCCUPATIONAL_PYRAMID_LEVEL_NOT_FOUND`: `404`, el nivel solicitado no existe en el scope correcto.
- `OCCUPATIONAL_PYRAMID_LEVEL_CODE_CONFLICT`: `409`, otro nivel ya usa ese `code`.
- `OCCUPATIONAL_PYRAMID_LEVEL_ORDER_CONFLICT`: `409`, otro nivel ya usa ese `LevelOrder`.
- `OCCUPATIONAL_PYRAMID_LEVEL_IN_USE`: `409`, no puede inactivarse porque matrices activas aun lo usan.
- `COMPETENCY_CONDUCT_NOT_FOUND`: `404`, el conducto solicitado no existe en el scope correcto.
- `COMPETENCY_CONDUCT_DUPLICATE`: `409`, ya existe un conducto con la misma combinacion de competencia, tipo, behavior level y descripcion.
- `RESOURCE_IN_USE`: `409`, no puede inactivarse un conducto usado por expectativas activas.
- `COMPETENCY_NOT_FOUND`: `404`, la competencia referenciada no existe o esta inactiva.
- `COMPETENCY_TYPE_NOT_FOUND`: `404`, el tipo de competencia referenciado no existe o esta inactivo.
- `BEHAVIOR_LEVEL_NOT_FOUND`: `404`, el behavior level referenciado no existe o esta inactivo.
- `BEHAVIOR_NOT_FOUND`: `404`, el behavior referenciado no existe o esta inactivo.
- `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`: `409`, la matriz pedida tiene duplicados, cruces invalidos, un conducto no corresponde al eje declarado o el perfil esta archivado.
- `COMPETENCY_FRAMEWORK_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `json|csv|xlsx`.

Errores relevantes en `PositionDescriptionCatalogs`:

- `POSITION_DESCRIPTION_CATALOG_ITEM_NOT_FOUND`: `404`, no existe el catalog item solicitado.
- `POSITION_CATEGORY_CLASSIFICATION_NOT_FOUND`: `404`, no existe la clasificacion solicitada.
- `POSITION_CATEGORY_NOT_FOUND`: `404`, no existe la categoria solicitada.
- `POSITION_DESCRIPTION_CATALOG_CODE_CONFLICT`: `409`, otro item del mismo tipo ya usa ese `code`.
- `POSITION_CATEGORY_CLASSIFICATION_CODE_CONFLICT`: `409`, otra clasificacion ya usa ese `code`.
- `POSITION_CATEGORY_CODE_CONFLICT`: `409`, otra categoria ya usa ese `code`.
- `POSITION_CATEGORY_CLASSIFICATION_DUPLICATE_AXES`: `409`, ya existe una clasificacion con la misma combinacion de `positionFunctionType`, `positionContractType` y `orgUnitType`.
- `POSITION_DESCRIPTION_CATALOG_IN_USE`: `409`, no puede inactivarse el item porque aun esta referenciado.
- `POSITION_CATEGORY_CLASSIFICATION_IN_USE`: `409`, no puede inactivarse la clasificacion porque aun tiene categorias activas asociadas.
- `POSITION_CATEGORY_IN_USE`: `409`, no puede inactivarse la categoria porque aun la usan job profiles.
- `POSITION_DESCRIPTION_CATALOG_RELATED_ITEM_NOT_FOUND`: `404`, un catalogo relacionado requerido no existe o esta inactivo.
- `ORG_UNIT_TYPE_NOT_FOUND`: `404`, el `OrgUnitType` usado por la clasificacion no existe o esta inactivo.
- `SALARY_CLASS_NOT_FOUND`: `404`, la salary class referenciada no existe.
- `REQUIREMENT_TYPE_NOT_FOUND`: `404`, el requirement type referenciado no existe.
- `FREQUENCY_NOT_FOUND`: `404`, la frecuencia referenciada no existe.
- `WORK_CONDITION_TYPE_NOT_FOUND`: `404`, el tipo de condicion de trabajo referenciado no existe.

Errores relevantes en `PositionSlots`:

- `POSITION_SLOT_NOT_FOUND`: `404`, la plaza solicitada no existe en el scope correcto.
- `POSITION_SLOT_JOB_PROFILE_NOT_FOUND`: `404`, el job profile referenciado no se pudo resolver.
- `POSITION_SLOT_WORK_CENTER_NOT_FOUND`: `404`, el `WorkCenter` referenciado no se pudo resolver.
- `POSITION_SLOT_ROLE_NOT_FOUND`: `404`, el rol referenciado para la plaza no existe en el tenant activo.
- `POSITION_SLOT_DEPENDENCY_NOT_FOUND`: `404`, la plaza usada como dependencia no se pudo resolver.
- `POSITION_SLOT_JOB_PROFILE_ORG_UNIT_NOT_CONFIGURED`: `422`, el job profile referenciado no resuelve una `OrgUnit`; es un guardrail defensivo para datos legacy o inconsistentes.
- `POSITION_SLOT_CONTRACT_TYPE_NOT_RESOLVED`: `422`, el job profile no resuelve un tipo de contrato activo.
- `POSITION_SLOT_COST_CENTER_INVALID`: `422`, el `costCenterCode` inferido desde la `OrgUnit` del job profile no existe o esta inactivo en la compania.
- `POSITION_SLOT_CODE_CONFLICT`: `409`, otra plaza ya usa ese `code`.
- `POSITION_SLOT_DEPENDENCY_CYCLE`: `409`, la dependencia directa crearia un ciclo.
- `POSITION_SLOT_DEPENDENCY_SELF_REFERENCE`: `409`, la plaza no puede depender de si misma.
- `POSITION_SLOT_STATUS_CONFLICT`: `409`, el estado solicitado no es consistente con la ocupacion actual.
- `POSITION_SLOT_SUSPENDED_OCCUPANCY_CONFLICT`: `409`, una plaza suspendida no puede cambiar ocupacion.
- `POSITION_SLOT_CAPACITY_RULE_VIOLATION`: `422`, `occupiedEmployees` esta fuera del rango permitido.
- `POSITION_SLOT_EFFECTIVE_DATES_INVALID`: `422`, rango de fechas invalido.
- `POSITION_SLOT_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `csv|xlsx`.
- `POSITION_SLOT_DIAGRAM_FORMAT_INVALID`: `400`, `format` distinto de `graphml|json|dot`.

Errores `400` de validacion frecuentes:

- `page/pageSize` fuera de rango.
- `depth` fuera de `1..15`.
- ids vacios.
- `code` invalido.
- `If-Match` ausente en mutaciones migradas, o `ConcurrencyToken` ausente en endpoints legacy no migrados.
- `EffectiveToUtc < EffectiveFromUtc`.

#### 5.9.6 Job catalogs

Route family:

- `GET /api/v1/companies/{companyId}/job-catalogs/{category}`
- `POST /api/v1/companies/{companyId}/job-catalogs/{category}`
- `PUT /api/v1/companies/{companyId}/job-catalogs/{category}/{jobCatalogPublicId}`
- `PATCH /api/v1/companies/{companyId}/job-catalogs/{category}/{jobCatalogPublicId}`
- `DELETE /api/v1/companies/{companyId}/job-catalogs/{category}/{jobCatalogPublicId}`

Uso principal:

- mantener catalogos reutilizables del diseno de puestos y del framework de competencias

Observaciones funcionales:

- `category` es enum-driven y hoy expone: `EducationLevel`, `KnowledgeArea`, `Competency`, `Training`, `SalaryClass`, `BenefitType`, `WorkingCondition`, `RelationType`, `DecisionLevel`, `CompetencyType`, `BehaviorLevel` y `Behavior`.
- `search` soporta `isActive`, `q`, `page` y `pageSize`.
- `q` busca por `code` y `name`.
- el orden observable del listado es `name`, luego `code`.
- el response incluye `IsSystem`, `IsActive` y `ConcurrencyToken`.
- `POST` responde `201 Created` con `ETag` del item creado.
- `PUT`, `PATCH` y `DELETE` requieren `If-Match: "<ConcurrencyToken-del-item>"`.
- `PUT` recibe `code`, `name` e `isActive`; ya no recibe `concurrencyToken` en body.
- `PATCH` consume JSON Patch para `/code`, `/name` e `/isActive`; ya no acepta `/concurrencyToken`.
- El `PATCH` de `job-catalogs` admite maximo `50` operaciones y body de hasta `64 KiB`. Excesos de operaciones responden `400 common.validation`; requests mas grandes son rechazados con `413`.
- `PUT`, `PATCH` y `DELETE` responden `200 OK` con `JobCatalogItemResponse` y header `ETag` del token devuelto.
- no existe `get by id`.
- no existe endpoint `update`.
- `create` exige unicidad de `code` por tenant + categoria.
- `activate` e `inactivate` usan solo `id + ConcurrencyToken`; no revalidan categoria en la ruta.
- la API no aplica chequeos de uso antes de inactivar un item de `job-catalogs`.
- el efecto observable de esa decision es que los historicos ya enlazados siguen existiendo, pero futuras resoluciones "activas" para `JobProfiles` o `CompetencyFramework` ya no aceptaran el item inactivo.

#### 5.9.7 Job profiles

Route family:

- `GET /api/v1/companies/{companyId}/job-profiles`
- `GET /api/v1/job-profiles/{publicId}`
- `GET /api/v1/job-profiles/{publicId}/vacancy-template`
- `GET /api/v1/job-profiles/{publicId}/print`
- `GET /api/v1/job-profiles/{publicId}/export`
- `POST /api/v1/companies/{companyId}/job-profiles`
- `PUT /api/v1/job-profiles/{publicId}`
- `PATCH /api/v1/job-profiles/{publicId}`
- `PATCH /api/v1/job-profiles/{publicId}/publish`
- `PATCH /api/v1/job-profiles/{publicId}/archive`

Uso principal:

- crear y mantener el descriptor maestro de un puesto dentro del tenant
- preparar una version imprimible o exportable del perfil
- publicar o archivar el perfil formal

Observaciones funcionales:

- `search` soporta filtros `status`, `orgUnitId`, `salaryClass`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `code` y `title`.
- el orden observable del listado es `title`, luego `code`.
- `search` devuelve `PagedResponse<JobProfileListItemResponse>`.
- `get by id` devuelve solo la entidad base del `JobProfile`: campos escalares propios, referencias publicas directas, estado, version, `ConcurrencyToken` y `allowedActions`.
- `get by id` ya no expone el agregado enriquecido ni reconstruye compensacion legacy desde `Salary Tabulator`; para vistas enriquecidas se usan endpoints especializados como `print`, `vacancy-template` y `competency-matrix`.
- `vacancy-template` devuelve una vista resumida para reclutamiento: objetivo, responsabilidades, resumen de condiciones/beneficios y las colecciones mas relevantes del perfil.
- `print` devuelve `JobProfilePrintResponse` con `Profile + GeneratedAtUtc` y registra auditoria `ReportPrinted`.
- `export` soporta solo `json|csv`.
- el payload de `create/update` mezcla campos escalares y colecciones anidadas.
- `update` es de reemplazo total sobre las colecciones; no es un merge parcial y no acepta cambios de `status`.
- `patch` es parcial sobre campos escalares, `status` y `compensation`; soporta operaciones `add`, `replace` y `remove`. `status` no es obligatorio, pero si llega como `add` o `replace /status` aplica la transicion de estado sobre la entidad. `remove /compensation` limpia la referencia, pero cualquier patch que no toque `/compensation` conserva la compensacion actual.
- `compensation` no es coleccion: es una sola referencia opcional (`salaryTabulatorLineId`) y el backend valida que la linea exista y este activa en `Salary Tabulator` para la fecha efectiva.
- `create/update` resuelven referencias a `OrgUnit`, `ReportsToJobProfile`, `PositionCategory`, `StrategicObjective`, `AssignedWorkEquipment` y `Responsibility`.
- `OrgUnitPublicId` es obligatoria en `create/update`; incluso un borrador debe quedar asociado a una unidad organizativa valida.
- `PositionCategory` debe existir y estar activa para poder asociarse al perfil.
- las referencias a `StrategicObjective`, `AssignedWorkEquipment`, `Responsibility`, `RequirementType`, `Frequency` y `WorkConditionType` deben existir y estar activas.
- el sistema detecta ciclos tanto en `reportsTo` como en `dependentPositions`.
- `Published` no es un estado inmutable: un job profile publicado todavia puede editarse y volver a publicarse mientras no este archivado.
- `PUT /api/v1/job-profiles/{publicId}` permite guardar borradores incompletos, pero si el perfil ya esta `Published` no puede remover `objective`, `responsibilities`, `requirements` o `functions`; en ese caso responde `JOB_PROFILE_PUBLISH_REQUIREMENTS_MISSING` (`422`). Esa flexibilidad ya no aplica a `OrgUnit`: siempre debe existir. Las transiciones de estado se hacen exclusivamente con `PATCH /api/v1/job-profiles/{publicId}` usando `/status`.
- `Archived` si es terminal para edicion: cualquier `update` o `publish` sobre un perfil archivado falla con `JOB_PROFILE_STATE_CONFLICT`.
- `publish` exige al menos estas precondiciones: `Objective`, minimo un `Requirement`, minimo una `Function` y `Responsibilities`.
- `publish` no exige competencias, trainings, beneficios, compensacion ni categoria de puesto.
- `publish` usa el `ConcurrencyToken` del perfil y, si el estado es valido, incrementa `Version`.
- `archive` es practicamente idempotente: si el perfil ya estaba archivado y el `ConcurrencyToken` coincide, devuelve la representacion actual sin volver a mutar.
- cambiar la `OrgUnit` del `JobProfile` cambia automaticamente la `OrgUnit` y el `CostCenterCode` derivados que exponen todas sus `PositionSlots`.
- `AllowInlineCatalogCreate=true` permite que el mismo payload cree items faltantes en ciertas categorias de `job-catalogs`.
- ese inline create solo funciona si el usuario tambien tiene permisos de catalog admin; de lo contrario responde `JOB_CATALOG_INLINE_CREATE_FORBIDDEN`.
- para inline create se requieren `code + name`; si ya existe un item activo con el mismo `name` normalizado, la API lo reutiliza en vez de crear duplicado.
- para requisitos `Education`, `Knowledge` y `Certification`, `create/update` ahora tambien resuelven un catalogo interno global no tenant-scoped a partir de `description`; si no existe un valor suficientemente parecido, lo agregan y dejan el texto canonico resultante en el perfil.
- `Experience` y `Other` siguen siendo texto libre y no alimentan el catalogo interno global.

#### 5.9.8 Competency framework

Route family:

- `GET /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `GET /api/v1/occupational-pyramid-levels/{id}`
- `POST /api/v1/companies/{companyId}/occupational-pyramid-levels`
- `PUT /api/v1/occupational-pyramid-levels/{id}`
- `PATCH /api/v1/occupational-pyramid-levels/{id}/activate`
- `PATCH /api/v1/occupational-pyramid-levels/{id}/inactivate`
- `GET /api/v1/companies/{companyId}/competency-conducts`
- `GET /api/v1/competency-conducts/{id}`
- `POST /api/v1/companies/{companyId}/competency-conducts`
- `PUT /api/v1/competency-conducts/{id}`
- `PATCH /api/v1/competency-conducts/{id}/activate`
- `PATCH /api/v1/competency-conducts/{id}/inactivate`
- `PUT /api/v1/competency-conducts/{id}/behaviors`
- `PUT /api/v1/job-profiles/{publicId}/competency-matrix`
- `GET /api/v1/job-profiles/{publicId}/competency-matrix/export`

Uso principal:

- definir niveles ocupacionales y conductos de comportamiento
- asociar behaviors a cada conducto
- mantener la matriz de expectativas por job profile

Observaciones funcionales:

- `occupational-pyramid-levels` soporta `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- el orden observable de niveles es `LevelOrder`, luego `Code`.
- `create/update` de niveles exigen unicidad de `code` y unicidad de `LevelOrder`.
- `inactivate` de nivel falla si matrices activas aun lo estan usando.
- `competency-conducts` soporta filtros `competencyId`, `competencyTypeId`, `behaviorLevelId`, `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- Si se usa cualquiera de `competencyId`, `competencyTypeId` o `behaviorLevelId`, deben enviarse los tres para evitar resultados ambiguos entre niveles de comportamiento.
- `q` busca por descripcion del conducto, por `code/name` de la competencia y por `code` de `competencyType` y `behaviorLevel`.
- el orden observable de conductos es `SortOrder`, luego `Description`.
- `create/update` de conductos resuelven `Competency`, `CompetencyType` y `BehaviorLevel` desde `job-catalogs`, siempre como referencias activas.
- un conducto es unico por `competency + competencyType + behaviorLevel + description normalizada`.
- `PUT /competency-conducts/{id}/behaviors` reemplaza todo el set de behaviors.
- en ese endpoint no se permiten `BehaviorId` duplicados dentro de la misma solicitud.
- cada `BehaviorId` debe existir como item activo de `JobCatalogCategory.Behavior`.
- `inactivate` de conducto falla si el conducto sigue vinculado a expectativas activas de perfiles.
- `GET /job-profiles/{publicId}` devuelve la matriz de competencias en la propiedad `competencies`.
- `PUT /job-profiles/{publicId}/competency-matrix` es de reemplazo total; una lista vacia limpia la matriz del perfil.
- `PUT /job-profiles/{publicId}` tambien acepta `competencies` como matriz de competencias; cada item usa IDs publicos y no requiere `name`.
- cada item de matriz debe ser unico por combinacion `OccupationalPyramidLevelId + CompetencyId + CompetencyTypeId + BehaviorLevelId`.
- dentro de un item, `ConductIds` tambien deben ser unicos.
- cada conducto referenciado debe pertenecer exactamente al mismo eje `competency + competencyType + behaviorLevel` declarado en el item.
- perfiles archivados no pueden mutar su matriz y responden `JOB_PROFILE_COMPETENCY_MATRIX_CONFLICT`.
- al actualizar la matriz, el sistema incrementa la `Version` del `JobProfile` y regenera su `ConcurrencyToken`, aun si no cambio ningun otro campo escalar del perfil.
- `competency-matrix/export` soporta `json|csv|xlsx` y registra auditoria de export.

#### 5.9.9 Position description catalogs

Route family:

- catalogos simples: `/api/v1/companies/{companyPublicId}/position-description-catalogs/{catalogType}/items` y `/api/v1/position-description-catalogs/{catalogType}/items/{positionDescriptionCatalogItemPublicId}`
- clasificaciones: `/api/v1/companies/{companyPublicId}/position-category-classifications` y `/api/v1/position-category-classifications/{positionCategoryClassificationPublicId}`
- categorias: `/api/v1/companies/{companyPublicId}/position-categories` y `/api/v1/position-categories/{positionCategoryPublicId}`

Uso principal:

- mantener el vocabulario formal del descriptor de puestos
- clasificar puestos por funcion, contrato y tipo organizacional
- definir categorias de puesto que luego usan `JobProfiles` y `PositionSlots`

Observaciones funcionales de los catalogos simples:

- los catalogos simples usan un contrato generico por `catalogType`; los slugs vigentes son `position-function-types`, `position-contract-types`, `strategic-objectives`, `frequencies`, `requirement-types`, `requirements`, `general-functions`, `salary-classes`, `work-equipments`, `responsibilities-catalog`, `benefits-catalog`, `work-condition-types` y `work-conditions`.
- cada catalogo simple expone solo `GET` de coleccion, `GET` por entidad, `POST` y `PATCH` unico por entidad.
- `GET` de coleccion es el unico endpoint que retorna el array paginado de items; soporta `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `code` y `name`.
- el orden observable es `SortOrder`, luego `Name`, luego `Code`.
- `POST` responde `201 Created` con la entidad creada.
- `PATCH` consume `application/json-patch+json` con array RFC 6902 y debe incluir `add` o `replace` sobre `/concurrencyToken`.
- `PATCH` permite administrar `/code`, `/name`, `/description`, `/sortOrder` e `/isActive`; `/isActive` reemplaza los endpoints historicos `activate` e `inactivate`.
- El `PATCH` de catalogos simples admite maximo `50` operaciones y body de hasta `64 KiB`. Excesos de operaciones responden `400 common.validation`; requests mas grandes son rechazados con `413`.
- todo endpoint por entidad retorna la entidad afectada para que el frontend no tenga que recargar la coleccion completa.
- no existen exportes para estos catalogos.
- el bloqueo por uso para inactivar depende del tipo:
- `position-function-types` y `position-contract-types` se bloquean si alguna `PositionCategoryClassification` los usa.
- `frequencies` se bloquea si alguna funcion de job profile la usa.
- `requirement-types` se bloquea si algun requirement de job profile la usa.
- `work-condition-types` se bloquea si alguna condicion de job profile la usa.
- el resto de catalogos simples se bloquea si algun `JobProfile` los usa en sus referencias principales.
- el `catalogType` de la ruta se valida contra el item; si el `positionDescriptionCatalogItemPublicId` existe pero pertenece a otro tipo, responde `POSITION_DESCRIPTION_CATALOG_ITEM_NOT_FOUND`.
- las rutas por entidad usan `positionDescriptionCatalogItemPublicId` y las respuestas exponen `catalogType`.

Observaciones funcionales de `position-category-classifications`:

- `GET` de coleccion es el unico endpoint que retorna el array paginado de clasificaciones.
- `search` soporta filtros `positionFunctionTypePublicId`, `positionContractTypePublicId`, `orgUnitTypePublicId`, `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- el orden observable es `SortOrder`, luego `Name`, luego `Code`.
- `POST` responde `201 Created` con la clasificacion creada.
- `PATCH` consume `application/json-patch+json`, debe incluir `/concurrencyToken` y puede administrar `/code`, `/name`, `/description`, `/positionFunctionTypePublicId`, `/positionContractTypePublicId`, `/orgUnitTypePublicId`, `/sortOrder` e `/isActive`.
- El `PATCH` de clasificaciones admite maximo `50` operaciones y body de hasta `64 KiB`. Excesos de operaciones responden `400 common.validation`; requests mas grandes son rechazados con `413`.
- las modificaciones escalares exigen referencias activas a `PositionFunctionType`, `PositionContractType` y `OrgUnitType`.
- la clasificacion es unica por `code`.
- ademas la combinacion `PositionFunctionType + PositionContractType + OrgUnitType` tambien debe ser unica.
- `PATCH /isActive=false` falla si alguna `PositionCategory` activa sigue usando la clasificacion.

Observaciones funcionales de `position-categories`:

- `GET` de coleccion es el unico endpoint que retorna el array paginado de categorias.
- `search` soporta filtros `classificationPublicId`, `isActive`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- el orden observable es `SortOrder`, luego `Name`, luego `Code`.
- `POST` responde `201 Created` con la categoria creada.
- `PATCH` consume `application/json-patch+json`, debe incluir `/concurrencyToken` y puede administrar `/code`, `/name`, `/description`, `/classificationPublicId`, `/sortOrder` e `/isActive`.
- El `PATCH` de categorias admite maximo `50` operaciones y body de hasta `64 KiB`. Excesos de operaciones responden `400 common.validation`; requests mas grandes son rechazados con `413`.
- las modificaciones escalares exigen `classificationPublicId`.
- la categoria es unica por `code`.
- `PATCH /isActive=false` falla si algun `JobProfile` sigue usando la categoria.

#### 5.9.10 Position slots

Route family:

- `GET /api/v1/companies/{companyId}/position-slots`
- `GET /api/v1/position-slots/{id}`
- `GET /api/v1/companies/{companyId}/position-slots/graph`
- `GET /api/v1/companies/{companyId}/position-slots/diagram-export`
- `GET /api/v1/companies/{companyId}/position-slots/export`
- `POST /api/v1/companies/{companyId}/position-slots`
- `PUT /api/v1/position-slots/{id}`
- `PATCH /api/v1/position-slots/{id}/status`
- `PATCH /api/v1/position-slots/{id}/dependencies`
- `PATCH /api/v1/position-slots/{id}/occupancy`

Uso principal:

- materializar plazas reales a partir de job profiles
- controlar estado, jerarquia operativa y ocupacion de cada plaza
- exportar la vision tabular o de grafo de la estructura de puestos

Observaciones funcionales:

- `search` soporta filtros `status`, `jobProfileId`, `orgUnitId`, `workCenterId`, `contractTypeId`, `q`, `page`, `pageSize` e `includeAllowedActions`.
- `q` busca por `slot code`, `slot title`, `job profile`, `org unit` y `work center`.
- el orden observable del listado es `Code`, luego `Title`.
- el detail devuelve referencias resueltas a `JobProfile`, `OrgUnit`, `WorkCenter`, `DirectDependency`, `FunctionalDependency`, `PositionCategory`, `PositionCategoryClassification` y `ContractType`.
- el detail y los listados tambien exponen `RoleId` y `RoleName` cuando la plaza tiene un rol configurado.
- `OrgUnit` y `CostCenterCode` visibles en listados, detalle, grafo y exportes se derivan siempre desde `JobProfile -> OrgUnit`.
- `create` exige `Code`, `JobProfileId`, `Status`, `MaxEmployees`, `OccupiedEmployees` y `EffectiveFromUtc`.
- `WorkCenterId`, `RoleId`, dependencias directas/funcionales, `EffectiveToUtc` y `Notes` son opcionales.
- `OrgUnitId` y `CostCenterCode` ya no forman parte del request de `create/update`.
- `JobProfileId` se resuelve por tenant, no por estado; el API no exige que el perfil este publicado para crear una plaza.
- la validacion critica real no es el estado del perfil sino que ese perfil resuelva una `OrgUnit` valida y, si la `OrgUnit` deriva `CostCenterCode`, que ese centro de costo exista activo.
- `ContractType` no viene del cliente y se infiere desde la clasificacion del job profile cuando esa relacion existe.
- la bandera interna `IsFixedTerm` se infiere automaticamente por heuristica de `contractTypeCode/contractTypeName` con tokens como `TEMP`, `FIXED`, `PLAZO` o `FIJO`.
- si el `JobProfile` no resuelve `ContractType`, la plaza igual se crea o actualiza y expone `ContractType = null`; internamente `IsFixedTerm` cae a `false`.
- si la `OrgUnit` del job profile tiene `CostCenterCode`, ese codigo debe existir activo dentro del tenant; si no existe o esta inactivo, `create/update` responde `POSITION_SLOT_COST_CENTER_INVALID` (`422`).
- `Vacant` exige `OccupiedEmployees = 0`.
- `Occupied` exige `OccupiedEmployees > 0`.
- `Suspended` no cambia la ocupacion existente, pero pone `IsActive = false`.
- `PUT /position-slots/{id}` actualiza solo el nucleo de la plaza; no cambia ni `Status` ni `OccupiedEmployees`. Si cambia `JobProfileId`, tambien cambian la `OrgUnit` y el `CostCenterCode` derivados.
- si cambia el rol configurado de la plaza, el backend resincroniza el rol tenant-scoped de los usuarios ya vinculados a expedientes completados que usan esa plaza.
- `PATCH /position-slots/{id}/status` cambia solo el estado y revalida consistencia contra la ocupacion actual.
- `PATCH /position-slots/{id}/occupancy` cambia solo `OccupiedEmployees` y recalcula automaticamente el estado a `Vacant` u `Occupied`.
- una plaza `Suspended` no puede usar el endpoint de `occupancy`.
- `PATCH /position-slots/{id}/dependencies` sobrescribe ambas dependencias.
- la dependencia directa si se valida contra ciclos.
- la dependencia funcional solo se protege contra autorreferencia; el API no aplica una deteccion general de ciclos funcionales.
- `graph` devuelve nodos y edges de la estructura de plazas.
- si se envia `rootId`, el grafo se acota a ese subarbol/subgrafo; si no, parte de las raices de dependencia directa.
- `includeFunctional=true` agrega traversal y edges funcionales.
- `diagram-export` reutiliza exactamente esos filtros (`rootId`, `depth`, `includeFunctional`) y soporta `graphml|json|dot`.
- `export` reutiliza los filtros del listado sin paginacion y soporta `csv|xlsx`.
- este controller no tiene `activate/inactivate`; el estado operativo de la plaza se controla con `PATCH /status`.

#### 5.9.11 Relacion con otros modulos

- `OrgUnits` aporta la estructura organizacional sobre la que se ubican `JobProfiles`; `PositionSlots` la hereda desde el perfil.
- `Locations` aporta `WorkCenters`, que `PositionSlots` puede usar como ubicacion operativa concreta.
- `CostCenters` valida el `CostCenterCode` configurado en la `OrgUnit` del perfil; `PositionSlots` lo expone por derivacion.
- `Org structure catalogs` aporta `OrgUnitTypes`, que son obligatorios para construir `PositionCategoryClassifications`.
- `PositionDescriptionCatalogs` alimenta a `JobProfiles` con tipos de funcion, contratos, frecuencias, salary classes, work equipments, responsabilidades, benefits y work conditions.
- `Job catalogs` alimenta tanto a `JobProfiles` como a `CompetencyFramework`.
- `CompetencyFramework` se apoya en `JobProfiles`: la matriz siempre cuelga de un `job-profile`.
- `PositionSlots` se apoya en `JobProfiles`: la plaza es la instancia operativa de un perfil, no un recurso independiente del diseno ni con overrides propios de `OrgUnit` o `CostCenter`.

`Job and competency design` es asi el modulo que une diseno, clasificacion y operacion: primero se definen catalogos y clasificaciones, luego el perfil de puesto, despues el framework de competencias y finalmente la plaza concreta dentro de la empresa.

### 5.10 Personnel files

#### 5.10.1 Alcance

Este bloque cubre ocho controladores que juntos administran el expediente de personal del tenant:

- `PersonnelFilesController`
- `PersonnelFileProfileController`
- `PersonnelFileEmploymentController`
- `PersonnelFileCompensationController`
- `PersonnelFileTalentController`
- `PersonnelFileDocumentsController`
- `PersonnelFileAdministrationController`
- `PersonnelFileReportingController`

Familias de rutas:

- `/api/v1/companies/{companyId}/personnel-files`
- `/api/v1/personnel-files/{id}`
- `/api/v1/personnel-files/{id}/personal-info`
- `/api/v1/personnel-files/{publicId}/identifications`
- `/api/v1/personnel-files/{publicId}/addresses`
- `/api/v1/personnel-files/{publicId}/emergency-contacts`
- `/api/v1/personnel-files/{publicId}/family-members`
- `/api/v1/personnel-files/{id}/hobbies`
- `/api/v1/personnel-files/{id}/employee-relations`
- `/api/v1/personnel-files/{id}/associations`
- `/api/v1/personnel-files/{id}/educations`
- `/api/v1/personnel-files/{id}/languages`
- `/api/v1/personnel-files/{id}/trainings`
- `/api/v1/personnel-files/{id}/previous-employments`
- `/api/v1/personnel-files/{id}/references`
- `/api/v1/personnel-files/{id}/finalize/preview`
- `/api/v1/personnel-files/{id}/finalize`
- `/api/v1/personnel-files/{id}/employee-profile`
- `/api/v1/personnel-files/{id}/employment-assignments`
- `/api/v1/personnel-files/{id}/contract-history`
- `/api/v1/personnel-files/{id}/position-hierarchy`
- `/api/v1/personnel-files/{id}/authorization-substitutions`
- `/api/v1/personnel-files/{id}/personnel-actions`
- `/api/v1/personnel-files/{id}/personnel-actions/export`
- `/api/v1/personnel-files/{id}/assets-accesses`
- `/api/v1/personnel-files/{id}/salary-items`
- `/api/v1/personnel-files/{id}/additional-benefits`
- `/api/v1/personnel-files/{id}/payment-methods`
- `/api/v1/personnel-files/{id}/payroll-transactions`
- `/api/v1/personnel-files/{id}/payroll-transactions/export`
- `/api/v1/personnel-files/{id}/insurances`
- `/api/v1/personnel-files/{id}/medical-claims`
- `/api/v1/personnel-files/{id}/bank-accounts`
- `/api/v1/personnel-files/{publicId}/evaluations`
- `/api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `/api/v1/personnel-files/{publicId}/position-competency-results`
- `/api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `/api/v1/personnel-files/{publicId}/selection-contests`
- `/api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `/api/v1/personnel-files/{publicId}/curricular-competencies`
- `/api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `/api/v1/personnel-files/{id}/documents`
- `/api/v1/personnel-files/{id}/observations`
- `/api/v1/personnel-files/{id}/print`
- `/api/v1/companies/{companyId}/personnel-files/dynamic-query`
- `/api/v1/companies/{companyId}/personnel-files/export`
- `/api/v1/companies/{companyId}/personnel-files/analytics/summary`
- `/api/v1/companies/{companyId}/general-catalogs/{catalogKey}`
- `/api/v1/companies/{companyId}/reference-catalogs/{catalogKey}`
- `/api/v1/companies/{companyId}/personnel-custom-field-definitions`
- `/api/v1/personnel-custom-field-definitions/{id}`

#### 5.10.2 Proposito funcional en CLARIHR

`PersonnelFiles` es el expediente maestro de personas del tenant. Su rol funcional es cubrir todo el ciclo base alrededor de una persona dentro de RRHH:

- alta y consulta del expediente
- llenado del perfil personal, familiar y curricular
- finalizacion formal de expedientes de empleado y aprovisionamiento de usuario
- mantenimiento de informacion laboral, compensacion y talento
- almacenamiento de documentos y observaciones
- catalogos propios del modulo
- reportes, exportes y consulta dinamica

En terminos de negocio, este bloque une dos mundos:

- el expediente personal base, que sirve para datos biograficos, antecedentes y documentos
- la capa operativa de RRHH, donde el mismo expediente se convierte en empleado, recibe asignaciones, salario, historial de acciones y evidencias

#### 5.10.3 Modelo operativo y reglas transversales del modulo

- Todas las rutas requieren autenticacion.
- Todo el bloque es tenant-scoped.
- Las rutas de coleccion usan `companyId` en la ruta y exigen que coincida con el tenant activo del token.
- Las rutas por recurso usan solo `{id}` y resuelven el tenant desde el token actual.
- Si un expediente o documento existe pero pertenece a otro tenant, la API responde `TENANT_MISMATCH` en vez de un `404` plano.
- La separacion de permisos es binaria:
- lectura usa `EnsureCanReadAsync`
- escritura usa `EnsureCanManageAsync`
- Los listados paginados del modulo usan `page/pageSize`, con default `20` y maximo `100`.
- No existen endpoints de borrado fisico.
- Todas las escrituras generan auditoria.
- `print`, `export`, `personnel-actions/export` y `payroll-transactions/export` tambien generan auditoria.
- La mayoria de escrituras por subseccion son de reemplazo total, no de merge parcial.
- `GET /api/v1/personnel-files/{id}` ya no devuelve el agregado base completo; responde un shell liviano del expediente con metadatos, estado y `AllowedActions`.
- Las escrituras por subseccion de `Profile`, `Employment` y `Compensation` usan el `ConcurrencyToken` del expediente padre y responden `PersonnelFileSectionResult<T>` con `data`, `personnelFileConcurrencyToken` y `modifiedAtUtc`. `Talent` ya no usa este patron: sus cuatro subrecursos exponen CRUD+PATCH atomico por item con `concurrencyToken` propio via `If-Match` (igual que `PersonalInfo`/`Background`/`Interests`).
- El cliente ya no necesita releer `/api/v1/personnel-files/{id}` para encadenar mutaciones seccionales; puede usar el token devuelto por cada escritura.
- Todo expediente nuevo nace con `LifecycleStatus = Draft`.
- Si `RecordType = Employee`, `AssignedPositionSlotId` es obligatorio desde `create` y `personal-info`; si `RecordType = Candidate`, ese campo no se permite.
- `RecordType` no puede cambiarse dentro de `personnel files`; la transicion funcional del modulo es `finalize`, no una conversion `Candidate -> Employee`.
- `finalize` solo aplica a expedientes `Employee` en `Draft`, exige `InstitutionalEmail` y plaza asignada; el rol IAM valido de la plaza solo se exige cuando `createUserAccount = true`.
- Despues de `Completed`, `InstitutionalEmail` y `AssignedPositionSlotId` quedan bloqueados en `personal-info`.
- Los endpoints de `Employment`, `Compensation` y `Talent` son de uso exclusivo para expedientes `Employee` ya completados.
- `GET /print` solo cubre el agregado base del expediente:
- datos personales y curriculares
- cuentas bancarias
- documentos
- observaciones
- no incluyen `employee profile`, asignaciones, acciones de personal, transacciones de planilla, seguros, evaluaciones ni otros subrecursos del bloque laboral
- `bank-accounts` se lee y se muta desde `PersonnelFileProfileController`; sigue viviendo sobre el agregado base y por eso aparece en `GetById` y en `print`. Las otras secciones del expediente base se distribuyen en `PersonnelFilePersonalInfoController` (datos personales, identificaciones, direcciones, contactos de emergencia, familiares), `PersonnelFileBackgroundController` (educaciones, idiomas, capacitaciones, empleos previos, referencias) y `PersonnelFileInterestsController` (pasatiempos, asociaciones, relaciones laborales).
- `search` y `dynamic-query` soportan busqueda libre por nombre completo y por numero de identificacion.
- `export` y `analytics/summary` reutilizan la ruta de export rows, por lo que su `q` solo opera sobre nombre completo; no busca por identificaciones.

#### 5.10.4 Autorizacion observable

- Lectura de `PersonnelFiles` acepta alguno de estos permisos: `PersonnelFiles.Read`, `PersonnelFiles.Admin` o `iam.administration.manage`.
- Escritura de `PersonnelFiles` acepta alguno de estos permisos: `PersonnelFiles.Admin` o `iam.administration.manage`.
- No existe bypass de plataforma sobre el chequeo tenant-permission del core; un token `platform` ni siquiera es valido en estas rutas.
- Si los claims directos no bastan, la API tambien resuelve permisos a traves de la membresia activa de compania y del rol asignado en ese tenant.
- Si el usuario no esta autenticado o no tiene tenant valido, la API responde `UNAUTHENTICATED`.
- Si el `companyId` de la ruta no coincide con el tenant activo, la API responde `TENANT_MISMATCH`.
- Si el usuario esta autenticado en el tenant correcto pero no cumple permisos, la API responde `PERSONNEL_FILES_FORBIDDEN`.
- `search` y `dynamic-query` pueden devolver `AllowedActions` cuando el cliente lo pide con `includeAllowedActions=true`.
- `get by id` siempre aplica `AllowedActions` sobre el shell del expediente.
- `print` reutiliza la misma lectura autorizada del expediente y devuelve el expediente filtrado por secciones, no un bypass de autorizacion.

#### 5.10.5 Errores observables relevantes en Personnel files

Errores transversales:

- `UNAUTHENTICATED`: `401`, autenticacion requerida o token sin tenant valido.
- `TENANT_MISMATCH`: `403`, se intenta operar un recurso de otro tenant.
- `PERSONNEL_FILES_FORBIDDEN`: `403`, el usuario no cumple permisos de lectura o administracion del modulo.
- `PERSONNEL_FILE_NOT_FOUND`: `404`, el expediente solicitado no existe en el scope correcto.
- `CONCURRENCY_CONFLICT`: `409`, el `ConcurrencyToken` del expediente o del documento ya no coincide.

Errores funcionales del expediente:

- `PERSONNEL_FILE_IDENTIFICATION_CONFLICT`: `409`, otra persona del tenant ya usa la misma identificacion.
- `PERSONNEL_FILE_STATE_RULE_VIOLATION`: `422`, la operacion no aplica al estado o tipo actual del expediente.
- `PERSONNEL_FILE_RECORD_TYPE_TRANSITION_NOT_ALLOWED`: `422`, se intento cambiar `RecordType` dentro del modulo.
- `PERSONNEL_FILE_PROVISIONING_FIELDS_LOCKED`: `422`, se intento cambiar `InstitutionalEmail` o `AssignedPositionSlotId` despues de completar el expediente.
- `PERSONNEL_FILE_FINALIZE_REQUIRES_INSTITUTIONAL_EMAIL`: `422`, falta el correo institucional requerido para aprovisionar el usuario.
- `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT`: `422`, falta la plaza asignada requerida para finalizar.
- `PERSONNEL_FILE_FINALIZE_REQUIRES_POSITION_SLOT_ROLE`: `422`, la plaza asignada no tiene un rol valido configurado cuando `finalize` intenta crear cuenta de usuario.
- `PERSONNEL_FILE_FINALIZE_ONLY_EMPLOYEE`: `422`, solo un expediente `Employee` puede finalizarse.
- `PERSONNEL_FILE_LINKED_USER_CONFLICT`: `409`, el correo institucional ya esta vinculado a otro expediente.
- `PERSONNEL_FILE_EFFECTIVE_DATES_INVALID`: `422`, hay rangos de fechas invalidos en identifications, associations, trainings, previous employments u otras subsecciones con vigencias.
- `PERSONNEL_FILE_FAMILY_MEMBER_RULE_VIOLATION`: `422`, campos condicionales de familiares no cumplen las reglas visibles del dominio.
- `PERSONNEL_CUSTOM_DATA_INVALID`: `400`, el JSON de custom data no es valido o no coincide con los tipos configurados.
- `PERSONNEL_CUSTOM_FIELD_KEY_CONFLICT`: `409`, otra definicion custom del tenant ya usa la misma key.
- `PERSONNEL_CUSTOM_FIELD_DEFINITION_NOT_FOUND`: `404`, la definicion custom solicitada no existe.

Errores de documentos y reporting:

- `PERSONNEL_FILE_DOCUMENT_NOT_FOUND`: `404`, el documento solicitado no existe.
- `PERSONNEL_FILE_DOCUMENT_FILE_REQUIRED`: `400`, el upload no trajo archivo.
- `PERSONNEL_FILE_DOCUMENT_CONTENT_TYPE_UNSUPPORTED`: `400`, el archivo no usa extension, MIME declarado o firma basica permitida.
- `PERSONNEL_FILE_DOCUMENT_TOO_LARGE`: `413`, el archivo excede `10 MiB`.
- `PERSONNEL_FILE_DOCUMENT_DATES_INVALID`: `422`, las fechas de prestamo y devolucion del documento no son consistentes.
- `PERSONNEL_FILE_EXPORT_FORMAT_INVALID`: `400`, `format` distinto de `csv|xlsx` en exportes de expedientes, acciones de personal o transacciones de planilla.

Errores `400` de validacion frecuentes:

- `q` mayor a `150` caracteres en `search`, `dynamic-query`, `export` o `analytics`.
- `page` o `pageSize` fuera de rango.
- `sortBy` o campos dinamicos no soportados.
- `groupBy` con mas de `3` campos o con campos no groupables.
- `Field/Operator` dinamicos no soportados o con payload incompleto.
- `sections` de `print` con valores no soportados.
- nombres, telefonos, codigos, keys o identificaciones con formato invalido.
- `customDataJson` sin los campos requeridos configurados.
- codigos curriculares inactivos o inexistentes, devueltos como errores de validacion por campo y categoria.

#### 5.10.6 Core

Route family:

- `POST /api/v1/companies/{companyPublicId}/personnel-files`
- `GET /api/v1/companies/{companyPublicId}/personnel-files`
- `GET /api/v1/personnel-files/{publicId}`
- `PUT /api/v1/personnel-files/{publicId}`
- `PATCH /api/v1/personnel-files/{publicId}` (JSON Patch RFC 6902; reemplaza a `/activate` e `/inactivate` vía `op replace /isActive`)

Uso principal:

- abrir un expediente nuevo
- listar expedientes del tenant
- consultar el shell liviano del expediente
- actualizar los datos del nucleo (`PUT`) y activar/inactivar logicamente (`PATCH` con `op replace /isActive`)

Observaciones funcionales:

- `RecordType` hoy expone `Candidate` y `Employee`.
- `LifecycleStatus` hoy expone `Draft` y `Completed`.
- `create` ya no recibe identificaciones; el expediente se abre solo con los datos base y queda en `Draft`.
- si el cliente sigue enviando `items` legacy en `create`, la API responde `400` y obliga a usar `POST /api/v1/personnel-files/{publicId}/identifications`.
- `create` valida formato de nombres, emails y telefonos, valida custom fields activos y valida codigos activos de catalogo del bloque personal; ya no valida identifications en esta ruta.
- `create` y `personal-info` reciben codigos de catalogo en `maritalStatusCode`, `professionCode`, `birthCountryCode`, `birthDepartmentCode` y `birthMunicipalityCode`; `identificationTypeCode` se valida en los endpoints propios de identifications.
- `create` y `personal-info` exigen consistencia geografica por jerarquia: `birthDepartmentCode` requiere `birthCountryCode`; `birthMunicipalityCode` requiere `birthDepartmentCode`; en esta fase `Department/Municipality` solo aplica para `birthCountryCode=SV`.
- `create` acepta `AssignedPositionSlotId`; es obligatorio para `Employee` y no se permite para `Candidate`.
- `search` soporta filtros `isActive`, `recordType`, `orgUnitId`, `minAge`, `maxAge`, `maritalStatus`, `nationality`, `profession`, `createdFromUtc`, `createdToUtc`, `q`, `sortBy`, `sortDirection`, `page`, `pageSize` e `includeAllowedActions`.
- `search` usa como orden default `FullName ASC`.
- `search` acepta estos `sortBy`: `fullname`, `firstname`, `lastname`, `birthdate`, `age`, `recordtype`, `maritalstatus`, `nationality`, `profession`, `orgunitid`, `isactive`, `createdatutc`, `modifiedatutc`.
- `search` busca por nombre completo normalizado y por numero de identificacion.
- `search` devuelve una proyeccion de tabla y ya no expone `birthDate` ni `concurrencyToken`; para mutaciones el cliente debe resolver primero el shell (`GET /api/v1/personnel-files/{publicId}`) o el detalle/seccion correspondiente.
- listados y detalle exponen pares `Code + Name` resueltos para `MaritalStatus` y `Profession`; los nombres geograficos e identifications viven en endpoints de detalle o secciones.
- listados, shell y exportes exponen `LifecycleStatus`, `AssignedPositionSlotId` y `LinkedUserId`.
- `get by id` devuelve solo el shell del expediente: `id`, `companyId`, `recordType`, `lifecycleStatus`, `fullName`, `photoFilePublicId`, `isActive`, `orgUnitId`, `assignedPositionSlotId`, `linkedUserId`, `concurrencyToken`, `createdAtUtc`, `modifiedAtUtc` y `allowedActions`.
- `activate` e `inactivate` son transiciones soft-state, requieren `ConcurrencyToken` y devuelven el shell refrescado con el token nuevo.
- `create`, `search`, `activate` e `inactivate` tienen rate limiting especifico del modulo y pueden devolver `429 Too Many Requests` con `ProblemDetails`.

#### 5.10.7 Datos personales, antecedentes, intereses y perfil financiero

Este bloque agrupa cuatro controladores independientes que cubren el expediente base no laboral del empleado.

##### Datos personales — `PersonnelFilePersonalInfoController`

Route family:

- `GET /api/v1/personnel-files/{publicId}/personal-info` (el `PUT` se reubicó al shell: `PUT /api/v1/personnel-files/{publicId}`)
- `GET /api/v1/personnel-files/{publicId}/identifications`
- `GET /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/identifications`
- `PUT /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/identifications/{identificationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/addresses`
- `GET /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `POST /api/v1/personnel-files/{publicId}/addresses`
- `PUT /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/addresses/{addressPublicId}`
- `GET /api/v1/personnel-files/{publicId}/emergency-contacts`
- `GET /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `POST /api/v1/personnel-files/{publicId}/emergency-contacts`
- `PUT /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/emergency-contacts/{emergencyContactPublicId}`
- `GET /api/v1/personnel-files/{publicId}/family-members`
- `GET /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`
- `POST /api/v1/personnel-files/{publicId}/family-members`
- `PUT /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/family-members/{familyMemberPublicId}`

##### Antecedentes — `PersonnelFileBackgroundController`

Route family:

- `GET /api/v1/personnel-files/{publicId}/educations`
- `GET /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/educations`
- `PUT /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/educations/{educationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/languages`
- `GET /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `POST /api/v1/personnel-files/{publicId}/languages`
- `PUT /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/languages/{languagePublicId}`
- `GET /api/v1/personnel-files/{publicId}/trainings`
- `GET /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `POST /api/v1/personnel-files/{publicId}/trainings`
- `PUT /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/trainings/{trainingPublicId}`
- `GET /api/v1/personnel-files/{publicId}/previous-employments`
- `GET /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `POST /api/v1/personnel-files/{publicId}/previous-employments`
- `PUT /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/previous-employments/{previousEmploymentPublicId}`
- `GET /api/v1/personnel-files/{publicId}/references`
- `GET /api/v1/personnel-files/{publicId}/references/{referencePublicId}`
- `POST /api/v1/personnel-files/{publicId}/references`
- `PUT /api/v1/personnel-files/{publicId}/references/{referencePublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/references/{referencePublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/references/{referencePublicId}`

##### Intereses — `PersonnelFileInterestsController`

Route family:

- `GET /api/v1/personnel-files/{publicId}/hobbies`
- `GET /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `POST /api/v1/personnel-files/{publicId}/hobbies`
- `PUT /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/hobbies/{hobbyPublicId}`
- `GET /api/v1/personnel-files/{publicId}/associations`
- `GET /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/associations`
- `PUT /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/associations/{associationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/employee-relations`
- `GET /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/employee-relations`
- `PUT /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/employee-relations/{employeeRelationPublicId}`

##### Perfil financiero — `PersonnelFileProfileController`

Route family:

- `GET /api/v1/personnel-files/{publicId}/bank-accounts`
- `POST /api/v1/personnel-files/{publicId}/bank-accounts`
- `PUT /api/v1/personnel-files/{publicId}/bank-accounts/{itemPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/bank-accounts/{itemPublicId}`

Uso principal:

- completar y mantener el contenido personal, familiar, curricular e intereses del expediente

Observaciones funcionales:

- `PersonnelFilesController` conserva el recurso raiz: create, search, shell y lifecycle. Las secciones editables del expediente base se distribuyen en cuatro controladores semanticos: `PersonnelFilePersonalInfoController` (datos de identidad y contacto), `PersonnelFileBackgroundController` (antecedentes curriculares), `PersonnelFileInterestsController` (intereses y vinculos relacionales) y `PersonnelFileProfileController` (cuentas bancarias).
- Los endpoints `GET` de cada seccion devuelven el bloque solicitado sin exigir `ConcurrencyToken`; `personal-info` responde con el bloque escalar del expediente y las demas rutas responden la coleccion completa del subrecurso.
- Todos los subrecursos de estos cuatro controladores usan CRUD atomico a nivel de item: `POST` crea un item, `PUT /{itemPublicId}` actualiza uno, `DELETE /{itemPublicId}` elimina uno; en todos los casos el `ConcurrencyToken` del expediente padre viaja en el body del request.
- `POST` item-level responde `201` con el item creado. `PUT` item-level responde `200` con el item actualizado. Solo `personal-info` devuelve `PersonnelFileSectionResult<T>`.
- `personal-info` actualiza los campos escalares del expediente, valida custom data contra definiciones activas y no permite transiciones de `RecordType`.
- `personal-info` tambien actualiza `AssignedPositionSlotId` mientras el expediente sigue en `Draft`; al completar el expediente, `AssignedPositionSlotId` e `InstitutionalEmail` quedan bloqueados.
- `create` y `personal-info` aceptan `photoFilePublicId` como `Guid?` que referencia un `StoredFile` previamente subido a traves de la API de file management (`POST /api/v1/files/upload-session` → upload directo → `PATCH /api/v1/files/{filePublicId}/complete`). El campo acepta `null` para borrar la foto o el `publicId` de un archivo activo con `purpose = profile-photo`.
- el backend valida que el `StoredFile` referenciado exista, pertenezca al tenant y este en estado `Active`; si la foto anterior era otro `StoredFile`, el sistema marca el anterior para limpieza.
- las respuestas de expediente exponen `photoFilePublicId` como `string?`; el result filter de la API resuelve internamente el `Guid` a una URL SAS temporal de lectura para que frontend pueda renderizar la imagen sin acceso directo al contenedor.
- si `RecordType = Employee`, `AssignedPositionSlotId` sigue siendo obligatorio; si `RecordType = Candidate`, no puede enviarse.
- `personal-info` valida que `maritalStatusCode`, `professionCode`, `birthCountryCode`, `birthDepartmentCode` y `birthMunicipalityCode` existan y esten activos segun catalogo global de referencia.
- `identifications` valida que `identificationTypeCode` exista y este activo, y revalida unicidad tenant-wide por `IdentificationTypeCode + IdentificationNumber` normalizado.
- `family-members` exige consistencia entre banderas condicionales y datos dependientes:
  - si `IsStudying=true`, se esperan `StudyPlace` y `AcademicLevel`
  - si `IsWorking=true`, se esperan `Workplace` y `JobTitle`
  - si `IsDeceased=true`, se espera `DeceasedDate`
- `family-members` usa `kinshipCode` (ya no texto libre) y valida contra `GET /api/v1/companies/{companyId}/reference-catalogs/kinships`.
- `educations`, `languages`, `trainings`, `previous-employments` y `references` validan codigos contra catalogos activos del modulo.
- Los catalogos curriculares observables usados desde este bloque incluyen `CurriculumEducationStatus`, `CurriculumStudyType`, `CurriculumShift`, `CurriculumModality`, `CurriculumLanguage`, `CurriculumLanguageLevel`, `CurriculumTrainingType`, `CurriculumDurationUnit`, `CurriculumReferenceType`, `Country` y `Currency`.
- `educations` exige `EndDate` cuando `IsCurrentlyStudying=false` y evita `ApprovedSubjects > TotalSubjects`.
- `bank-accounts` ya no recibe `bankCode`; la escritura usa `bankPublicId` y valida que el banco este activo para el pais de la compania.
- `bank-accounts` responde `bankPublicId`, `bankCode`, `bankName`, `bankAlias`, `swiftCode` y `routingCode` ademas de los datos propios de la cuenta.
- si un banco queda inactivo despues de haber sido asociado, la cuenta bancaria existente sigue siendo legible y renderizable.
- `languages` exige que al menos uno de `Speaks`, `Writes` o `Reads` sea `true`.
- `trainings` valida pais, tipo, unidad de duracion y moneda de costo contra catalogos activos.
- `employee-relations` exige que cada item referencie otro expediente existente del mismo tenant mediante `RelatedEmployeePublicId`; la respuesta devuelve tambien `RelatedEmployeeFullName`.

#### 5.10.8 Employment

Route family:

- `GET /api/v1/personnel-files/{id}/finalize/preview`
- `POST /api/v1/personnel-files/{id}/finalize`
- `GET /api/v1/personnel-files/{id}/employee-profile`
- `PUT /api/v1/personnel-files/{id}/employee-profile`
- `GET /api/v1/personnel-files/{id}/employment-assignments`
- `PUT /api/v1/personnel-files/{id}/employment-assignments`
- `GET /api/v1/personnel-files/{id}/contract-history`
- `PUT /api/v1/personnel-files/{id}/contract-history`
- `GET /api/v1/personnel-files/{id}/position-hierarchy`
- `GET /api/v1/personnel-files/{id}/authorization-substitutions`
- `PUT /api/v1/personnel-files/{id}/authorization-substitutions`
- `POST /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions`
- `GET /api/v1/personnel-files/{id}/personnel-actions/export`
- `GET /api/v1/personnel-files/{id}/assets-accesses`
- `PUT /api/v1/personnel-files/{id}/assets-accesses`

Uso principal:

- completar el expediente de empleado y aprovisionar su usuario
- mantener la capa laboral del expediente
- consultar y exportar movimientos laborales

Observaciones funcionales:

- `finalize/preview` usa `createUserAccount` como query param opcional (default `true`) y devuelve `isEligible` + `issues` para prerevisar bloqueos antes de ejecutar `finalize`; cada issue expone `section`, `fieldKey` y `navigationKey`.
- `finalize` exige que el expediente sea `Employee`, siga en `Draft`, tenga `InstitutionalEmail` y plaza asignada; la validacion de rol IAM de la plaza aplica solo cuando `createUserAccount = true`.
- `finalize` cambia el expediente a `Completed`; cuando `createUserAccount = true` crea o reutiliza el usuario de compania, deja la cuenta local en `PendingActivation`, emite invitacion y vincula el usuario al expediente, y cuando `createUserAccount = false` completa sin aprovisionar usuario.
- Todo el resto del bloque exige que el expediente ya sea un `Employee` completado; si no, responde `PERSONNEL_FILE_STATE_RULE_VIOLATION`.
- Los endpoints `GET` del bloque devuelven el subrecurso solicitado sin exigir `ConcurrencyToken`.
- Los endpoints `PUT` del bloque devuelven `PersonnelFileSectionResult<T>` para que frontend pueda encadenar mutaciones usando el nuevo `personnelFileConcurrencyToken`.
- `employee-profile` hace upsert del perfil laboral y permite vincular `PositionSlotId`, `JobProfileId`, `OrgUnitId`, `WorkCenterId`, `CostCenterId`, vigencias contractuales y `VacationConfigurationJson`.
- la creacion del usuario ya no depende del `employee-profile`; ese subrecurso queda para datos laborales posteriores a `finalize`.
- `employment-assignments`, `contract-history`, `authorization-substitutions` y `assets-accesses` reemplazan la coleccion completa de ese subrecurso.
- `employee-relations` ya no acepta nombres libres: cada item debe referenciar otro expediente existente del mismo tenant mediante `RelatedEmployeePublicId`, y la respuesta devuelve tambien `RelatedEmployeeFullName`.
- `position-hierarchy` devuelve `ImmediateSupervisorPersonnelFileId`, `ImmediateSupervisorName` y la coleccion de subordinados.
- `personnel-actions` agrega un evento individual de personal con fechas efectivas, monto opcional, moneda, descripcion y referencia.
- `search personnel-actions` soporta `fromUtc`, `toUtc`, `type`, `status`, `q`, `sortBy`, `sortDirection`, `page` y `pageSize`.
- `search personnel-actions` busca por `Description`, `Reference`, `ActionTypeCode` y `ActionStatusCode`.
- `search personnel-actions` usa `ActionDateUtc DESC` por defecto.
- `personnel-actions/export` soporta `csv|xlsx`.
- `personnel-actions/export` admite `sortBy` observable en `actionDateUtc`, `createdAtUtc`, `type/actionTypeCode`, `status/actionStatusCode` y `amount`.
- Las escrituras de `Employment` tocan deliberadamente el expediente padre, por lo que rotan su `ConcurrencyToken` aunque la respuesta sea de tipo laboral y no `PersonnelFileResponse`.

#### 5.10.9 Compensation

Route family:

- `GET /api/v1/personnel-files/{id}/salary-items`
- `PUT /api/v1/personnel-files/{id}/salary-items`
- `GET /api/v1/personnel-files/{id}/additional-benefits`
- `PUT /api/v1/personnel-files/{id}/additional-benefits`
- `GET /api/v1/personnel-files/{id}/payment-methods`
- `PUT /api/v1/personnel-files/{id}/payment-methods`
- `PUT /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions`
- `GET /api/v1/personnel-files/{id}/payroll-transactions/export`
- `GET /api/v1/personnel-files/{id}/insurances`
- `PUT /api/v1/personnel-files/{id}/insurances`
- `GET /api/v1/personnel-files/{id}/medical-claims`
- `PUT /api/v1/personnel-files/{id}/medical-claims`

Uso principal:

- mantener salario, beneficios, medios de pago, historico de planilla, seguros y reclamos

Observaciones funcionales:

- Todo el bloque exige que el expediente sea `Employee`.
- Los endpoints `GET` del bloque devuelven la coleccion completa del subrecurso y no exigen `ConcurrencyToken`.
- Todos los `PUT` son de reemplazo total de la subseccion y responden `PersonnelFileSectionResult<T>`.
- `salary-items` mantiene rubros salariales con vigencias, tipo de ingreso, rubrica, moneda y periodo de pago.
- `additional-benefits` mantiene beneficios adicionales activos o historicos.
- `payment-methods` usa vigencias `EffectiveFromUtc/EffectiveToUtc` y puede apuntar a un `BankAccountId`.
- `payroll-transactions` mantiene el historico detallado de movimientos de planilla, con metadatos de integracion (`SourceSystem`, `SourceReference`, `SourceSyncedUtc`).
- `search payroll-transactions` soporta `fromUtc`, `toUtc`, `type`, `status`, `q`, `sortBy`, `sortDirection`, `page` y `pageSize`.
- en `payroll-transactions`, el filtro `status` no representa un campo persistido; hoy mapea a polaridad del movimiento:
- `DEBIT` o `DISCOUNT` filtra `IsDebit=true`
- `CREDIT` o `EARNING` filtra `IsDebit=false`
- `search payroll-transactions` usa `TransactionDateUtc DESC` por defecto.
- `payroll-transactions/export` soporta `csv|xlsx`.
- `payroll-transactions/export` admite `sortBy` observable en `transactionDateUtc`, `createdAtUtc`, `type/transactionTypeCode` y `amount`.
- `insurances` reemplaza tambien el conjunto de beneficiarios de cada seguro.
- Las escrituras de `Compensation` tambien tocan el expediente padre y por eso hacen rotar su `ConcurrencyToken`.

#### 5.10.10 Talent

Route family:

- `GET /api/v1/personnel-files/{publicId}/evaluations`
- `GET /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `POST /api/v1/personnel-files/{publicId}/evaluations`
- `PUT /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/evaluations/{evaluationPublicId}`
- `GET /api/v1/personnel-files/{publicId}/position-competency-results`
- `GET /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `POST /api/v1/personnel-files/{publicId}/position-competency-results`
- `PUT /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/position-competency-results/{positionCompetencyResultPublicId}`
- `GET /api/v1/personnel-files/{publicId}/selection-contests`
- `GET /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `POST /api/v1/personnel-files/{publicId}/selection-contests`
- `PUT /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/selection-contests/{selectionContestPublicId}`
- `GET /api/v1/personnel-files/{publicId}/curricular-competencies`
- `GET /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `POST /api/v1/personnel-files/{publicId}/curricular-competencies`
- `PUT /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `PATCH /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`
- `DELETE /api/v1/personnel-files/{publicId}/curricular-competencies/{curricularCompetencyPublicId}`

Uso principal:

- mantener evidencias de desempeno, brechas de competencia, concursos y competencias curriculares

Observaciones funcionales:

- Todo el bloque exige que el expediente sea `Employee` completado.
- Los cuatro subrecursos (`evaluations`, `position-competency-results`, `selection-contests`, `curricular-competencies`) exponen el contrato CRUD+PATCH canonico por item, identico en forma a `PersonalInfo`/`Background`/`Interests`: `GET` lista, `GET /{entityPublicId}`, `POST`, `PUT /{entityPublicId}`, `PATCH /{entityPublicId}` y `DELETE /{entityPublicId}`.
- Cada item lleva su propio `concurrencyToken`. `PUT`, `PATCH` y `DELETE` exigen `If-Match: "<concurrencyToken>"` del item (validado por el binder `[FromIfMatch]`); el token ya no viaja en el body.
- `GET` de lista devuelve la coleccion completa del subrecurso (unico endpoint de listado, sin paginacion); `GET /{entityPublicId}` devuelve un item individual con su `concurrencyToken`.
- `POST` responde `201 Created` con el item creado, `Location` apuntando al recurso y `ETag: "<concurrencyToken>"` inicial.
- `PUT` y `PATCH` responden `200 OK` con el item actualizado y el nuevo `ETag`. `PATCH` consume `application/json-patch+json` (JSON Patch RFC 6902) y re-ejecuta la misma validacion de `Add`/`Update` (solo `NotEmpty`/longitud/normalizacion de fechas; no hay validacion de catalogo en `Talent`).
- `DELETE` es hard delete (sin soft-delete): responde `200 OK` con `PersonnelFileParentConcurrencyResult` (`parentConcurrencyToken`), el token refrescado del expediente padre, para encadenar mutaciones sin un `GET` adicional.
- Si el item referenciado por `{entityPublicId}` no existe, `GET /{entityPublicId}`, `PUT`, `PATCH` y `DELETE` responden `ITEM_NOT_FOUND` (`404`); el chequeo de existencia precede al de concurrencia, de modo que un item ausente nunca produce `CONCURRENCY_CONFLICT`.
- `evaluations` mantiene resultados de evaluacion con score cuantitativo, score cualitativo y comentario.
- `position-competency-results` mantiene resultados observados por `CompetencyCode`; el endpoint lee el estado persistido del expediente, no una recomputacion en vivo desde `CompetencyFramework`.
- `selection-contests` registra resultados de concursos internos o externos.
- `curricular-competencies` registra requerimientos, dominio y experiencia observada en el expediente.
- Todos los subrecursos de `Talent` exponen metadatos de integracion (`SourceSystem`, `SourceReference`, `SourceSyncedUtc`) cuando aplica.
- Las escrituras de `Talent` tambien tocan el expediente padre y rotan su `ConcurrencyToken`.

#### 5.10.11 Documents

Route family:

- `GET /api/v1/personnel-files/{id}/documents`
- `PUT /api/v1/personnel-files/{id}/documents`
- `POST /api/v1/personnel-files/{id}/documents`
- `GET /api/v1/personnel-files/{id}/observations`
- `POST /api/v1/personnel-files/{id}/observations`

Uso principal:

- adjuntar evidencias documentales al expediente
- sincronizar documentos nuevos y existentes desde una sola escritura
- reemplazar archivo y metadatos en una misma operacion cuando aplique
- inactivar logicamente documentos omitidos del payload de sincronizacion
- registrar observaciones internas

Observaciones funcionales:

- `GET /documents` devuelve una lista liviana de `PersonnelFileDocumentMetadataResponse` con `fileUrl` resuelto para el frontend; la API ya no expone `FileData` ni un endpoint separado de descarga.
- `GET /documents` es la vista recomendada para renderizar listados o abrir adjuntos directamente desde la URL firmada del documento.
- `GET /documents` ordena por `CreatedAtUtc` descendente.
- `PUT /documents` usa `multipart/form-data` con `concurrencyToken` del expediente + `manifestJson` y partes binarias adicionales cuyo nombre debe coincidir con el `fileKey` de cada item.
- `PUT /documents` es el contrato canonico de actualizacion para frontend: crea items sin `documentPublicId`, actualiza items con `documentPublicId` y deja inactivos los documentos activos omitidos del manifiesto.
- En `PUT /documents`, un item existente sin `fileKey` actualiza solo metadatos y conserva el blob actual; no debe generar un upload nuevo a Azure Blob Storage.
- En `PUT /documents`, un item existente con `fileKey` reemplaza el archivo binario y actualiza sus metadatos en la misma operacion.
- En `PUT /documents`, un item nuevo exige archivo y siempre crea `publicId` nuevo para el documento.
- `PUT /documents` responde `PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>` y rota el `ConcurrencyToken` del expediente padre.
- `upload document` usa `multipart/form-data`.
- `upload document` exige archivo no vacio, maximo `10 MiB`, extension/MIME permitido y firma basica valida antes de leer el binario completo.
- formatos permitidos para `upload document`: `.pdf`, `.jpg`, `.jpeg`, `.png`, `.docx`.
- `upload document` usa el `ConcurrencyToken` actual del expediente.
- `upload document` persiste la metadata en BD y sube el archivo a Azure Blob Storage privado; en BD queda la URL canonica del blob y el nombre interno (`blobName`).
- `upload document` calcula y persiste `sha256` del binario cargado.
- `upload document` valida fechas de entrega, prestamo y devolucion; rangos invalidos responden `PERSONNEL_FILE_DOCUMENT_DATES_INVALID`.
- `fileUrl` es una URL SAS temporal resuelta por la API para consumo directo del frontend.
- `upload document` devuelve `PersonnelFileDocumentMetadataResponse`, no el expediente completo.
- `PUT /documents` usa el `ConcurrencyToken` del expediente; los `ConcurrencyToken` de cada documento siguen visibles en response pero ya no son el contrato de escritura canonico.
- `PUT /documents` reactiva implicitamente un documento existente si vuelve a aparecer en el manifiesto.
- `PUT /documents` no borra fisicamente blobs ni filas por omision; la omision solo inactiva el documento.
- `add observation` usa el `ConcurrencyToken` del expediente y devuelve solo la observacion creada.

#### 5.10.12 Administration

Route family:

- `GET /api/v1/companies/{companyId}/general-catalogs/{catalogKey}`
- `GET /api/v1/companies/{companyId}/reference-catalogs/{catalogKey}`
- `GET /api/v1/companies/{companyId}/bank-catalogs`
- `GET /api/v1/companies/{companyId}/personnel-custom-field-definitions`
- `POST /api/v1/companies/{companyId}/personnel-custom-field-definitions`
- `PUT /api/v1/personnel-custom-field-definitions/{id}`

Uso principal:

- exponer catalogos funcionales del modulo
- administrar campos custom del expediente

Observaciones funcionales:

- `general-catalogs/{catalogKey}` y `reference-catalogs/{catalogKey}` son read-only y devuelven solo items activos.
- ambos endpoints filtran internamente por el pais de la compania (`Company.CountryCode`); el frontend no envia `countryCode`.
- `bank-catalogs` tambien resuelve internamente por el pais de la compania, pero usa busqueda paginada con `q`, `page` y `pageSize`.
- `bank-catalogs` devuelve solo bancos activos e incluye `publicId`, `code`, `name`, `alias`, `swiftCode` y `routingCode`.
- `reference-catalogs/municipalities` acepta `parentCode` para filtrar por departamento.
- `professions`, `marital-statuses`, `identification-types`, `kinships`, `departments` y `municipalities` salen de entidades separadas por pais.
- `general-catalogs/countries` sigue devolviendo paises globales activos.
- `nationality` permanece fuera de catalogo en esta HU y sigue como campo libre.
- `personnel-custom-field-definitions` soporta filtro opcional `isActive`.
- `PersonnelCustomFieldType` hoy expone `String`, `Number`, `Date`, `Bool` y `Select`.
- `create/update custom-field-definition` valida `Key` con la misma regex de codigos del modulo, exige `Label`, `SortOrder >= 0` y limita `OptionsJson` a `12000` caracteres.
- la key custom es unica por tenant en comparacion normalizada.
- `update custom-field-definition` requiere `ConcurrencyToken`.
- `create` y `personal-info update` validan `customDataJson` contra las definiciones activas del tenant.
- esa validacion exige presencia de campos requeridos y coherencia de tipo por key.
- la validacion actual no rechaza propiedades extra que no tengan definicion activa; simplemente valida las keys conocidas.

#### 5.10.13 Reporting

Route family:

- `GET /api/v1/personnel-files/{id}/print`
- `POST /api/v1/companies/{companyId}/personnel-files/dynamic-query`
- `GET /api/v1/companies/{companyId}/personnel-files/export`
- `GET /api/v1/companies/{companyId}/personnel-files/analytics/summary`

Uso principal:

- imprimir el expediente base
- ejecutar consultas dinamicas sobre expedientes
- exportar listados masivos
- resumir indicadores del modulo

Observaciones funcionales:

- `print` acepta `sections` como lista separada por comas.
- las secciones soportadas hoy son:
- `personal-info`
- `identifications`
- `addresses`
- `emergency-contacts`
- `family-members`
- `hobbies`
- `employee-relations`
- `bank-accounts`
- `associations`
- `educations`
- `languages`
- `trainings`
- `previous-employments`
- `references`
- `documents`
- `observations`
- si `sections` se omite, `print` incluye todas las secciones soportadas.
- `print` filtra el `PersonnelFileResponse` del agregado base; nunca imprime employment, compensation ni talent.
- Comportamiento observable actual de `print`:
- la seccion `personal-info` es aceptada en la API
- pero el filtro actual solo vacia colecciones
- por eso los campos escalares base del expediente siguen presentes aunque `personal-info` no se incluya explicitamente
- `dynamic-query` acepta body con `Filters`, `GroupBy`, `Sort`, `Q`, `Page`, `PageSize` e `IncludeAllowedActions`.
- `dynamic-query` permite como maximo `3` campos de agrupacion.
- cada campo agrupado devuelve como maximo `100` buckets.
- los campos groupables hoy son `recordtype`, `maritalstatus`, `nationality`, `orgunitid` e `isactive`.
- los campos sortables hoy son `fullname`, `firstname`, `lastname`, `birthdate`, `age`, `recordtype`, `maritalstatus`, `nationality`, `profession`, `orgunitid`, `isactive`, `createdatutc` y `modifiedatutc`.
- `dynamic-query` soporta estos filtros observables:
- `recordtype`: `eq`, `in`
- `maritalstatus`: `eq`, `in`
- `nationality`: `eq`, `in`
- `profession`: `eq`, `contains`
- `orgunitid`: `eq`, `in`
- `isactive`: `eq`
- `age`: `eq`, `gte`, `lte`, `between`
- `birthdate`: `eq`, `gte`, `lte`, `between`
- `createdatutc`: `eq`, `gte`, `lte`, `between`
- `firstname`: `eq`, `contains`
- `lastname`: `eq`, `contains`
- `fullname`: `eq`, `contains`
- `dynamic-query` tambien busca por numero de identificacion cuando se usa `Q`.
- `export` soporta `csv|xlsx` y expone filtros equivalentes al `search` core.
- `export` devuelve un dataset fino del expediente: no incluye employment, compensation, talent ni documentos.
- `analytics/summary` soporta `isActive`, `recordType`, `orgUnitId`, `minAge`, `maxAge` y `q`.
- `analytics/summary` devuelve `TotalCount`, `ActiveCount`, `InactiveCount` y breakdowns por `RecordType`, rango de edad y `OrgUnit`.
- `export` y `analytics/summary` no incluyen matching por identificacion en `q`; solo operan sobre nombre completo.

#### 5.10.14 Relacion con otros modulos

- `IAM` y `Auth` aportan autenticacion, roles, permisos y contexto tenant para todo el expediente.
- `Account companies` aporta la compania activa sobre la que se crea o consulta el expediente.
- `OrgUnits` alimenta `OrgUnitId` en el expediente base, en `employee-profile`, en asignaciones y en analytics.
- `Locations` alimenta `WorkCenterId` en `employee-profile` y en `employment-assignments`.
- `CostCenters` alimenta `CostCenterId` o `CostCenterCode` dentro de la capa laboral.
- `Job and competency design` aporta `JobProfiles`, `PositionSlots`, tipos de requerimiento y el contexto de competencias que luego se reflejan en `employee-profile`, jerarquia de posicion y resultados de competencias.
- `Reports` consume `PERSONNEL_FILES` como `resourceKey` para capacidades de export e impresion.

`PersonnelFiles` es asi el modulo que aterriza el resto del sistema sobre una persona real: primero nace el expediente, luego se completa el perfil, despues puede convertirse en empleado y finalmente se conecta con puestos, estructura, compensacion, talento y evidencia documental.

### 5.11 Education Catalogs

#### 5.11.1 Alcance

Este bloque cubre los catalogos de educacion gestionados como catalogos de sistema global, sin dependencia de pais o tenant. Incluye dos superficies diferenciadas:

- `EducationCatalogsController` en la Core API con base `/api/v1/education-catalogs` — solo lectura
- `EducationCatalogsController` en la Backoffice API con base `/api/platform/education-catalogs` — administracion completa

#### 5.11.2 Proposito funcional en CLARIHR

Los catalogos de educacion proveen los valores de referencia que los usuarios utilizan al registrar informacion educativa en expedientes de personal. Al ser globales de sistema, la plataforma garantiza consistencia sin requerir configuracion por compania o por pais.

Catalogos disponibles:

| Clave (`catalogKey`) | Descripcion |
|---|---|
| `education-statuses` | Estado del proceso educativo (ej. `GRADUATED`, `IN_PROGRESS`) |
| `study-types` | Tipo de estudio (ej. `BACHELOR`, `MASTER`, `TECHNICAL`) |
| `careers` | Carreras o especialidades academicas |
| `shifts` | Modalidad horaria (ej. `MORNING`, `AFTERNOON`) |
| `modalities` | Modalidad de estudio (ej. `ONSITE`, `REMOTE`) |

#### 5.11.3 Modelo operativo y reglas transversales

- los catalogos existen a nivel de sistema y no pertenecen a ningun tenant
- el Core solo expone items activos; un item inactivado desde Backoffice desaparece automaticamente del Core
- el Backoffice puede listar todos los items independientemente del estado con el filtro `isActive`
- las mutations en Backoffice requieren `concurrencyToken` para evitar escrituras en conflicto
- el `code` de cada item se normaliza a `UPPERCASE` y es unico por tipo de catalogo
- el route segment `{catalogKey}` actua como discriminador de tipo y es case-insensitive
- un `catalogKey` no reconocido devuelve `404` en ambas APIs

#### 5.11.4 Contratos principales

**Core — `EducationCatalogLookup`**

```json
{
  "id": "<uuid>",
  "code": "BACHELOR",
  "normalizedCode": "BACHELOR",
  "name": "Bachelor",
  "sortOrder": 10
}
```

**Backoffice — `EducationCatalogItemResponse`**

```json
{
  "id": "<uuid>",
  "code": "BACHELOR",
  "normalizedCode": "BACHELOR",
  "name": "Bachelor",
  "sortOrder": 10,
  "isActive": true,
  "concurrencyToken": "<uuid>"
}
```

**Backoffice — `CreateEducationCatalogItemRequest`**

```json
{ "code": "BACHELOR", "name": "Bachelor", "sortOrder": 10 }
```

**Backoffice — `UpdateEducationCatalogItemRequest`**

```json
{ "code": "BACHELOR", "name": "Bachelor", "sortOrder": 10, "concurrencyToken": "<uuid>" }
```

#### 5.11.5 Core API — lectura publica (usuario autenticado)

Base route: `/api/v1/education-catalogs`

Autorizacion: `Bearer` con `client_type=core`. No requiere permisos RBAC adicionales.

##### `GET /api/v1/education-catalogs/{catalogKey}`

- Proposito: listar items activos del catalogo para uso en formularios.
- Autorizacion: usuario autenticado core.
- Query: `isActive` (opcional, por defecto retorna solo activos), `search`, `pageNumber`, `pageSize`.
- Response: `PagedResponse<EducationCatalogLookup>`.
- Errores relevantes: `404` si `catalogKey` no es reconocido.

##### `GET /api/v1/education-catalogs/{catalogKey}/{id}`

- Proposito: obtener un item activo individual por `publicId`.
- Autorizacion: usuario autenticado core.
- Response: `EducationCatalogLookup`.
- Errores relevantes: `404` si `catalogKey` no existe o el item no esta activo.

#### 5.11.6 Backoffice API — administracion completa (`PlatformOperator`)

Base route: `/api/platform/education-catalogs`

Autorizacion: `Bearer` con `client_type=platform` y politica `PlatformOperator`.

##### `GET /api/platform/education-catalogs/{catalogKey}`

- Proposito: listar todos los items del catalogo con filtros de administracion.
- Query: `isActive`, `search`, `pageNumber`, `pageSize`.
- Response: `PagedResponse<EducationCatalogItemResponse>`.

##### `GET /api/platform/education-catalogs/{catalogKey}/{id}`

- Proposito: obtener un item completo por `publicId` independientemente de su estado.
- Response: `EducationCatalogItemResponse`.
- Errores relevantes: `404` si no existe.

##### `POST /api/platform/education-catalogs/{catalogKey}`

- Proposito: crear un item nuevo en el catalogo indicado.
- Request body: `code`, `name`, `sortOrder`.
- Response: `201 Created` con `EducationCatalogItemResponse`.
- Errores relevantes: `404` si `catalogKey` no es valido, `409` si `code` ya existe en ese catalogo.

##### `PUT /api/platform/education-catalogs/{catalogKey}/{id}`

- Proposito: actualizar un item existente.
- Request body: `code`, `name`, `sortOrder`, `concurrencyToken`.
- Response: `200 OK` con `EducationCatalogItemResponse`.
- Errores relevantes: `404` si no existe, `409` si `code` ya existe o hay conflicto de concurrencia.

##### `PATCH /api/platform/education-catalogs/{catalogKey}/{id}/activate`

- Proposito: activar un item inactivo.
- Request body: `{ "concurrencyToken": "<uuid>" }`.
- Response: `200 OK` con `EducationCatalogItemResponse`.

##### `PATCH /api/platform/education-catalogs/{catalogKey}/{id}/inactivate`

- Proposito: inactivar un item activo.
- Request body: `{ "concurrencyToken": "<uuid>" }`.
- Response: `200 OK` con `EducationCatalogItemResponse`.

#### 5.11.7 Errores observables relevantes

- `404 Not Found`: `catalogKey` invalido o item no encontrado.
- `409 Conflict`: codigo duplicado en el catalogo o conflicto de concurrencia en la escritura.
- `400 Bad Request`: validacion de campos (ej. `code` vacio, `sortOrder` invalido).
- `401 Unauthorized`: autenticacion requerida.
- `403 Forbidden`: rol de plataforma requerido (solo Backoffice).

### 5.12 File Management

#### 5.12.1 Alcance

Este bloque cubre el controlador `FilesController` que administra el ciclo de vida de archivos de forma provider-agnostica.

Route family:

- `POST /api/v1/files/upload-session`
- `PATCH /api/v1/files/{filePublicId}/complete`
- `GET /api/v1/files/{filePublicId}/read-url`
- `DELETE /api/v1/files/{filePublicId}`

#### 5.12.2 Proposito funcional en CLARIHR

`Files` centraliza la gestion de archivos binarios del sistema con subida directa del navegador al storage via URLs firmadas. Este modelo elimina la necesidad de enviar binarios a traves del backend y permite escalar a cualquier proveedor de almacenamiento.

El primer caso de uso implementado es la imagen de perfil del expediente de personal (`purpose = profile-photo`), pero la infraestructura soporta cualquier proposito futuro.

#### 5.12.3 Modelo operativo y reglas transversales

- Todas las rutas requieren autenticacion.
- Todo el bloque es tenant-scoped; el archivo se asocia al tenant activo del token.
- El archivo pasa por un ciclo de vida: `PendingUpload` → `Active` → `Deleted` / `Failed`.
- La subida es directa del navegador al storage: el backend genera una URL firmada de escritura y el frontend sube el binario directamente.
- Un background job limpia archivos `PendingUpload` que expiran sin completarse.
- Cada proposito (`purpose`) tiene reglas configurables de content type permitido, tamano maximo y contenedor de destino.

#### 5.12.4 Flujo de subida de archivos

1. Frontend llama `POST /api/v1/files/upload-session` con metadata (`fileName`, `contentType`, `sizeBytes`, `purpose`, `entityId` opcional).
2. Backend valida las reglas del proposito, crea el `StoredFile` en estado `PendingUpload`, genera la URL firmada de escritura y responde con `filePublicId`, `uploadUrl`, `expiresUtc` y `requiredHeaders`.
3. Frontend sube el binario directamente a `uploadUrl` usando los headers indicados.
4. Frontend llama `PATCH /api/v1/files/{filePublicId}/complete`.
5. Backend verifica que el objeto existe en storage, obtiene metadata real (tamano, content type) y marca el archivo como `Active`.
6. Frontend usa el `filePublicId` resultante para asociar el archivo a la entidad destino (ej. `photoFilePublicId` en `create` o `personal-info` de personnel files).

#### 5.12.5 Endpoints

##### `POST /api/v1/files/upload-session`

- Proposito: iniciar una sesion de subida directa.
- Request body:
  - `fileName` (string, requerido): nombre original del archivo.
  - `contentType` (string, requerido): MIME type del archivo.
  - `sizeBytes` (long, requerido): tamano en bytes.
  - `purpose` (string, requerido): proposito del archivo (ej. `profile-photo`).
  - `entityId` (uuid, opcional): referencia opcional a la entidad destino.
- Response `200 OK`:
  - `filePublicId` (uuid): identificador publico del archivo creado.
  - `uploadUrl` (string): URL firmada para subida directa.
  - `expiresUtc` (datetime): expiracion de la URL de subida.
  - `requiredHeaders` (object): headers que el frontend debe enviar con el upload.
- Errores: `400`, `401`, `413` (archivo demasiado grande), `422` (content type no permitido para el proposito).

##### `PATCH /api/v1/files/{filePublicId}/complete`

- Proposito: confirmar que la subida directa se completo exitosamente.
- Response `200 OK`:
  - `filePublicId` (uuid): identificador publico del archivo.
  - `status` (string): estado resultante (`Active`).
- Errores: `401`, `403` (no es el owner del archivo), `404`, `422` (archivo no en estado `PendingUpload` o no encontrado en storage).

##### `GET /api/v1/files/{filePublicId}/read-url`

- Proposito: obtener una URL temporal de solo lectura para acceder al archivo.
- Response `200 OK`:
  - `readUrl` (string): URL firmada de lectura.
  - `expiresUtc` (datetime): expiracion de la URL de lectura.
- Errores: `401`, `404`, `422` (archivo no activo).

##### `DELETE /api/v1/files/{filePublicId}`

- Proposito: marcar un archivo como eliminado logicamente.
- Response `200 OK`:
  - `filePublicId` (uuid): identificador publico del archivo.
  - `status` (string): estado resultante (`Deleted`).
- Errores: `401`, `403` (no es el owner del archivo), `404`.

#### 5.12.6 Errores observables relevantes

- `FILE_NOT_FOUND`: `404`, el archivo solicitado no existe.
- `FILE_OWNERSHIP_MISMATCH`: `403`, el usuario autenticado no es el creador del archivo.
- `FILE_NOT_PENDING_UPLOAD`: `422`, se intento completar un archivo que ya no esta en `PendingUpload`.
- `FILE_UPLOAD_NOT_FOUND_IN_STORAGE`: `422`, el objeto no se encontro en el storage despues de la sesion de subida.
- `FILE_NOT_ACTIVE`: `422`, se intento obtener read-url de un archivo no activo.
- `FILE_CONTENT_TYPE_NOT_ALLOWED`: `422`, el content type no esta permitido para el proposito.
- `FILE_SIZE_EXCEEDS_LIMIT`: `413`, el archivo excede el tamano maximo del proposito.
- `FILE_PURPOSE_UNKNOWN`: `422`, el proposito solicitado no esta configurado.

#### 5.12.7 Relacion con otros modulos

- `PersonnelFiles` usa `purpose = profile-photo` para la imagen de perfil del expediente; el campo `photoFilePublicId` en `create` y `personal-info` referencia un `StoredFile` activo.
- Futuros modulos pueden registrar nuevos propositos para documentos, evidencias u otros binarios sin cambiar la infraestructura de file management.

## 6. Reglas observables transversales

- las rutas autenticadas `api/v1` son tenant-scoped por defecto
- los listados usan normalmente paginacion, filtros o ambos
- muchas escrituras usan `concurrency token`
- export e impresion existen como endpoints explicitos, no como flags genericos
- los modulos administrativos sensibles usan RBAC y, en algunas areas, permisos por campo

## 7. Estado de OpenAPI

El repositorio ahora reserva [openapi.yaml](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/docs/technical/api/openapi.yaml), pero el contrato exhaustivo machine-readable sigue generandose en runtime por Swagger en `Development`.

Estado actual:

- `endpoint-reference.md` es el resumen humano canonico
- Swagger runtime es el contrato generado exhaustivo
- `openapi.yaml` es un placeholder bootstrap hasta automatizar export/versionado
