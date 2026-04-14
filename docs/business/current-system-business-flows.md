# Flujos de negocio actuales del sistema

## 1. Proposito

Este documento resume el comportamiento funcional vigente del backend CLARIHR. Su objetivo es explicar los flujos principales del sistema sin mezclar detalle tecnico de implementacion.

## 2. Alcance

El estado actual del sistema cubre estos dominios funcionales:

- acceso y autenticacion
- onboarding y activacion de tenant
- catalogo comercial de suscripciones
- administracion de companias
- ubicaciones y estructura organizacional
- IAM, roles y permisos
- perfiles de puesto y posiciones
- expedientes de personal
- compensacion y tabulador salarial
- reportes y auditoria

## 3. Actores principales

- visitante o usuario sin autenticar
- usuario autenticado sin compania activa
- administrador de plataforma
- administrador de compania
- administrador de RRHH
- administrador de seguridad RBAC
- operador de estructura organizacional
- responsable de compensacion

## 4. Flujo de acceso y autenticacion

### 4.1 Registro local

1. El usuario se registra con nombre, apellido, correo, contrasena y pais.
2. El sistema crea la cuenta y devuelve `access token` y `refresh token`.
3. Si todavia no existe compania activa para ese usuario, el token inicial no incluye contexto tenant ni claim `role` tenant-scoped.

### 4.2 Registro externo

1. El usuario se autentica con un proveedor externo soportado.
2. El sistema valida el `id token`.
3. Si la cuenta es nueva, la crea; si ya existe, inicia sesion.
4. El sistema devuelve credenciales de sesion con el mismo modelo de tokens.

### 4.3 Login, refresh y logout

1. El usuario autentica credenciales locales o reutiliza el proveedor externo.
2. `login` devuelve un nuevo par de tokens; si el usuario ya tiene compania primaria, el `access token` sale con `tid` y `role` del tenant activo.
3. `refresh` renueva la sesion usando un refresh token valido.
4. `logout` revoca la sesion vigente del usuario autenticado.

## 5. Flujo de onboarding y activacion de tenant

### 5.1 Creacion de la primera compania

1. Un usuario autenticado sin contexto tenant consulta el catalogo global de paises desde `api/account/companies/countries`.
2. Con el `countryCode` seleccionado, consulta el catalogo global de tipos de compania permitido para ese pais desde `api/account/companies/company-types`.
3. Con el `countryCode`, el tipo de compania del pais y los catalogos auxiliares del representante legal, crea una compania desde `api/account/companies`.
4. El sistema registra la compania, la relaciona con el pais seleccionado y crea un representante legal inicial.
5. Durante el provisioning, el backend siembra la configuracion inicial dependiente del pais.

### 5.2 Provisioning inicial

El provisioning actual deja creada la base operativa minima del tenant:

- jerarquia de locations
- niveles de location
- grupos de locations por plantilla de pais cuando existe una plantilla detallada, o un nivel generico minimo cuando el pais solo requiere bootstrap basico
- metadatos iniciales requeridos para operar la estructura

Para `SV`, la plantilla estructurada vigente siembra `14` departamentos y `44` municipios para mantener consistencia entre onboarding y expedientes de personal.

### 5.3 Cambio de compania activa

1. El usuario selecciona una compania de las que posee o administra.
2. El sistema emite un nuevo `access token` con `tenant claim` y el `role` correspondiente a la membresia activa de esa compania.
3. A partir de ese momento, los endpoints `api/v1/companies/{companyId}` y los endpoints `tenant-scoped` operan sobre ese contexto activo.

### 5.4 Catalogo comercial base de suscripciones

1. Un operador de plataforma autenticado entra al backoffice por `api/platform/auth/login`.
2. Con ese token `platform`, administra planes globales desde `api/platform/commercial-plans`.
3. El sistema permite registrar `name`, `code`, `description`, `fee` mensual base, precio por empleado activo, estado, modulos y limites incluidos.
4. El catalogo mantiene dos planes de sistema con precio `0`: `FREE`, usado por el provisioning owner, y `MASTER`, reservado para CLARI como plan interno.
5. `FREE` conserva el codigo canonico del provisioning y ahora arranca con el mismo catalogo completo de modulos que `MASTER` para no bloquear operacion por plan durante el onboarding owner.
6. `MASTER` siempre se resincroniza con todo el catalogo de modulos conocido, de modo que nuevos modulos comerciales queden habilitados automaticamente para CLARI sin depender de seeds manuales adicionales.
7. Cualquier referencia legacy `Enterprise legacy` se normaliza a `FREE` durante la migracion de datos y el plan legacy deja de existir en el catalogo.

### 5.5 Catalogo comercial de add-ons globales

1. Un operador de plataforma autenticado entra al backoffice por `api/platform/auth/login`.
2. Con ese token `platform`, administra add-ons globales desde `api/platform/commercial-addons`.
3. El sistema permite registrar `name`, `code`, `description`, `type`, `billingModel`, `measurementUnit`, `unitPrice`, `minimumQuantity`, `minimumMonthlyFee`, `periodicity` y `status`.
4. Los add-ons `Massive` usan `billingModel=PerActiveEmployee`, la unidad reservada `active employee` y un `minimumMonthlyFee` opcional; los `Specialized` usan `PerSeat` o `PerVolume`, una unidad comercial propia y una cantidad minima opcional.
5. Los add-ons quedan disponibles como catalogo comercial reutilizable para futuras activaciones por empresa y para el motor de cobro posterior.
6. La Core API tenant-scoped no expone este catalogo; el acceso queda reservado al backoffice global.

### 5.6 Administracion backoffice de suscripciones empresariales

1. Un operador de plataforma consulta el overview comercial y el historial de una empresa desde `api/platform/companies/{companyPublicId}/subscription` y `.../subscriptions`, o usa `api/platform/company-subscriptions` para listar el universo global con el estado actual visible por fila.
2. Antes de confirmar, puede solicitar una vista previa en `POST api/platform/companies/{companyPublicId}/subscription/preview` para validar elegibilidad, version efectiva del plan, moneda `USD`, periodicidad, fecha de vencimiento opcional y estado inicial resuelto.
3. La activacion usa `PUT api/platform/companies/{companyPublicId}/subscription` con `commercialPlanId`, `startDateUtc`, `expiresAtUtc` opcional y `periodicity`.
4. Si la fecha inicia hoy, el backend cancela la fila viva anterior cuando corresponde y crea una nueva `Active`; si la fecha es futura, crea una fila `Scheduled` sin efectos comerciales inmediatos.
5. La suscripcion ahora mantiene un ciclo de vida comercial controlado con estados `Draft`, `Scheduled`, `Trial`, `Active`, `Suspended`, `Expired` y `Cancelled`, aunque en la operacion actual del backoffice se usan `Scheduled`, `Active`, `Suspended`, `Expired` y `Cancelled`.
6. Antes de aplicar una reactivacion, el operador puede solicitar `POST api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status/preview` para validar elegibilidad, fecha efectiva, estado destino y coherencia con el plan/version vigentes.
7. Los cambios manuales de estado se ejecutan desde `PATCH api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status`; solo `Admin` puede suspender, reactivar o cancelar, `reasonCode` siempre es obligatorio y `effectiveDateUtc` solo se usa para reactivaciones `Suspended -> Active`.
8. Si la reactivacion usa la fecha de hoy, la misma suscripcion suspendida vuelve a `Active` en una sola transaccion; si la fecha es futura, la suscripcion sigue `Suspended` y el overview expone un `pendingStatusChange` sin crear una suscripcion nueva ni alterar plan, version o add-ons.
9. El historial de transiciones se consulta desde `GET api/platform/companies/{companyPublicId}/subscriptions/{subscriptionPublicId}/status-history`, donde cada movimiento conserva estado anterior, nuevo estado, fecha, actor u origen del sistema, motivo y observaciones.
10. Cada suscripcion queda amarrada explicitamente a `CommercialPlanVersion`, conserva snapshot de precios, expone `canOperate` y `canGenerateCharges`, registra auditoria durable y actualiza `Company.IsBillable` solo cuando la empresa queda en una suscripcion comercial operable y cobrable segun la politica de estado.
11. Un proceso de fondo promueve automaticamente las filas `Scheduled`, aplica primero las reactivaciones programadas antes de los cambios de plan y add-ons del mismo dia, y vence filas `Active` cuando `expiresAtUtc` se cumple; los casos rechazados por perdida de elegibilidad tambien dejan auditoria durable.
12. Sobre una suscripcion activa elegible, el operador tambien puede gestionar cambios de plan desde `.../subscription/plan-changes`: preview, solicitud, historial y cancelacion de cambios programados. El backend conserva snapshot del plan actual y del plan objetivo, calcula la fecha efectiva (`Immediate`, `SpecificDate` o `NextBillingCycle`) y evita conflictos con cambios pendientes.
13. La misma superficie `platform` ahora administra add-ons por empresa desde `.../subscription/addons`, `.../subscription/addons/eligible` y `.../subscription/addon-changes`. El flujo permite consultar add-ons activos o pendientes, ver catalogo elegible, previsualizar una activacion o desactivacion, crear el cambio, revisar historial y cancelar cambios programados.
14. La gestion de add-ons es comercial-only en esta version: conserva estado por empresa (`Inactive`, `PendingActivation`, `Active`, `PendingDeactivation`), fecha efectiva, motivo, historial y auditoria durable, pero todavia no modifica entitlements operativos, seats, volumen, prorrateo ni cobro final. Un proceso de fondo aplica automaticamente las activaciones o desactivaciones programadas cuando vence su fecha.

### 5.7 Autoservicio owner sobre suscripciones

1. El owner consulta su overview comercial desde `GET /api/account/companies/{companyPublicId}/subscription` y siempre ve el plan realmente activo, incluso si la empresa ya esta en `MASTER`.
2. Cuando el owner pide `GET /api/account/companies/{companyPublicId}/subscription/plans`, el backend lista planes activos visibles para ese actor: `FREE` sigue disponible para downgrade y `MASTER` solo aparece si el usuario autenticado tambien tiene un `PlatformOperator` activo.
3. El owner puede usar `POST .../subscription/preview` y `PUT .../subscription` para cambios inmediatos, pero un intento directo hacia `MASTER` sin ese `PlatformOperator` activo falla con `403`.
4. El marketplace owner sigue bloqueado unicamente cuando el plan activo es `FREE`; `MASTER` no introduce un bloqueo adicional de add-ons por si mismo.
5. Si el owner baja a `FREE`, la misma transaccion desactiva todos los add-ons activos para mantener coherencia entre el inventario comercial owner y el estado efectivo de add-ons de la empresa.

## 6. Flujo de administracion de estructura organizacional

### 6.1 Catalogos base

1. La cuenta autenticada consulta un catalogo global de tipos de empresa filtrado por pais; ese catalogo ya no depende del owner ni del tenant activo.
2. El tenant administra catalogos de tipos de unidad, areas funcionales y catalogos de descripcion de puestos.
3. Cualquier usuario autenticado tambien puede consultar catalogos internos globales de requisitos desde `api/account/internal-catalogs`, sin tenant activo y sin separacion por empresa.
4. En `Job Profiles`, los requisitos `Education`, `Knowledge` y `Certification` usan ese catalogo global como fuente reusable de sugerencias; si el usuario no encuentra un valor, puede proponer uno nuevo y el backend evita casi duplicados por similitud.
5. Estos catalogos sirven como base para crear estructura, perfiles, posiciones y para clasificar companias.

### 6.2 Locations y work centers

1. El tenant consulta la jerarquia ya sembrada.
2. Puede administrar niveles, grupos y work centers segun permisos.
3. La estructura geografica alimenta la organizacion operativa y la ubicacion de puestos o personas.

### 6.3 Org units y cost centers

1. El tenant crea unidades organizativas.
2. Puede moverlas, activarlas, inactivarlas y exportarlas.
3. Tambien administra centros de costo y los consulta junto con uso, exportes y estados.

## 7. Flujo de IAM y gobierno de acceso

### 7.1 Usuarios y membresias

1. El tenant puede crear usuarios de compania de forma individual o aprovisionarlos automaticamente al finalizar un expediente de empleado.
2. En ambos casos el backend genera una invitacion con contrasena temporal y deja al usuario en estado pendiente de activacion.
3. El usuario debe definir su contrasena final antes de poder entrar al sistema.
4. El acceso queda siempre limitado al tenant activo y al rol tenant-scoped vigente para esa membresia.

### 7.2 Roles, permisos y permisos por campo

1. Seguridad administra roles y matrices de permisos.
2. El sistema soporta permisos por recurso, accion y tambien por campo.
3. Los perfiles de acceso por campo influyen en lectura y actualizacion de informacion sensible.

### 7.3 Auditoria

1. Acciones sensibles dejan rastro de auditoria.
2. El sistema conserva actor, tenant, accion, entidad, before/after y metadatos de request.

## 8. Flujo de diseno organizacional y de puestos

### 8.1 Perfiles de puesto

1. El tenant crea y mantiene `job profiles`.
2. Puede consultarlos, imprimirlos, exportarlos y publicar o archivar.

### 8.2 Competencias y conductas

1. El tenant administra niveles de piramide ocupacional y conductas por competencia.
2. Puede mantener matrices de competencias por perfil de puesto y exportarlas.

### 8.3 Position slots

1. El tenant crea posiciones reales dentro de la estructura.
2. Cada plaza puede configurarse con un rol tenant-scoped valido tomado del catalogo de roles.
3. Puede consultar grafo, exportes, dependencias y ocupacion.
4. Estas posiciones alimentan la asignacion laboral y el aprovisionamiento automatico de usuarios.

## 9. Flujo de expediente de personal

### 9.1 Alta del expediente

1. RRHH crea un `personnel file` para una persona dentro de una compania.
2. El expediente siempre nace en estado `Draft` con informacion base e identificaciones iniciales.
3. Los campos de profesion, estado civil, lugar de nacimiento e identificaciones del bloque personal se capturan por codigo de catalogo (`maritalStatusCode`, `professionCode`, `birthCountryCode`, `birthDepartmentCode`, `birthMunicipalityCode`, `identificationTypeCode`) y se validan contra catalogos read-only del sistema.
4. En geografia de nacimiento, `birthDepartmentCode` requiere `birthCountryCode` y `birthMunicipalityCode` requiere `birthDepartmentCode`; en esta fase la cascada departamento/municipio esta habilitada para `SV`.
5. `nationality` permanece fuera de catalogo en esta fase y sigue como campo libre.
6. Si el expediente es de tipo `Employee`, desde el alta debe quedar asociada una plaza; si es `Candidate`, la plaza no aplica dentro de este modulo.
7. Desde ese momento el sistema puede completar el resto de secciones.

### 9.2 Perfil del expediente

El expediente se completa por bloques:

- informacion personal
- identificaciones
- direcciones
- contactos de emergencia
- familiares
- hobbies
- relaciones con empleados
- asociaciones
- educacion
- idiomas
- capacitaciones
- empleos previos
- referencias

Para poblar selects del bloque personal e identificaciones, RRHH usa endpoints read-only de referencia por compania:

- `GET /api/v1/companies/{companyId}/personnel-reference-catalogs/professions`
- `GET /api/v1/companies/{companyId}/personnel-reference-catalogs/marital-statuses`
- `GET /api/v1/companies/{companyId}/personnel-reference-catalogs/identification-types`
- `GET /api/v1/companies/{companyId}/personnel-reference-catalogs/departments?countryCode=SV`
- `GET /api/v1/companies/{companyId}/personnel-reference-catalogs/municipalities?countryCode=SV&departmentCode=<CODE>`

### 9.3 Finalizacion del expediente y aprovisionamiento de usuario

1. Solo un expediente `Employee` en estado `Draft` puede finalizarse dentro de `personnel files`.
2. La finalizacion exige correo institucional y plaza asignada; si el operador decide crear cuenta en ese momento, la plaza ademas debe tener un rol valido configurado desde el catalogo IAM del tenant.
3. Antes de confirmar la transicion, RRHH puede consultar `GET /api/v1/personnel-files/{id}/finalize/preview` para validar readiness y recibir issues bloqueantes por campo.
4. La transicion formal se ejecuta por un comando o endpoint explicito de finalizacion, que cambia el expediente a `Completed`.
5. Durante esa finalizacion el operador puede decidir si se crea cuenta: si se activa la opcion, el backend crea o reutiliza automaticamente el usuario de compania, le asigna el rol de la plaza, genera contrasena temporal y emite la invitacion para activacion; si no se activa, se completa el expediente sin aprovisionar usuario.
6. El expediente puede quedar vinculado opcionalmente a un usuario; tambien siguen existiendo usuarios creados de forma individual sin expediente asociado.

### 9.4 Vida laboral del expediente

Una vez completado el expediente, RRHH puede mantener:

- perfil laboral
- asignaciones de empleo
- historial contractual
- jerarquia de posicion
- sustituciones de autorizacion
- acciones de personal
- activos y accesos asignados

El usuario aprovisionado desde el expediente solo puede ingresar despues de aceptar la invitacion y definir su contrasena final; a partir de ese momento ve las opciones del sistema segun el rol heredado de la plaza.

### 9.5 Compensacion del expediente

RRHH o compensacion puede mantener:

- rubros salariales
- beneficios adicionales
- cuentas bancarias
- metodos de pago
- transacciones de planilla
- seguros
- reclamos medicos

### 9.6 Talento y evidencias

El expediente soporta:

- evaluaciones
- resultados de competencias de posicion
- concursos de seleccion
- competencias curriculares
- documentos
- observaciones

### 9.7 Reporting del expediente

El sistema permite:

- consulta individual completa
- impresion filtrada por secciones
- exportes
- consultas dinamicas
- resumenes analiticos

## 10. Flujo de tabulador salarial

1. El tenant consulta lineas del tabulador.
2. Puede exportarlas y analizar impacto.
3. Los cambios se manejan como `change requests`.
4. Las solicitudes pasan por estados de envio, aprobacion, rechazo o cancelacion.

## 11. Flujo de reportes y capacidades

1. El frontend consulta capacidades de reporte por recurso.
2. Cada modulo expone sus propios exportes cuando el recurso lo soporta.
3. Los reportes siguen el alcance del tenant activo y de los permisos del usuario.

## 12. Reglas funcionales visibles

- El sistema es tenant-scoped por defecto.
- El backoffice de plataforma es una superficie global separada de la Core API, autenticada con tokens `platform` y sin tenant activo.
- La compania activa define el contexto operativo del usuario.
- Los listados operativos se consumen con paginacion.
- La mayoria de actualizaciones sobre bloques del expediente reemplazan la seccion completa.
- Las transiciones de estado relevantes dejan auditoria.
- Los cambios sensibles usan control de concurrencia optimista.
