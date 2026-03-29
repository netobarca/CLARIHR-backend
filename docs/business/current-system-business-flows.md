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

### 5.3 Cambio de compania activa

1. El usuario selecciona una compania de las que posee o administra.
2. El sistema emite un nuevo `access token` con `tenant claim` y el `role` correspondiente a la membresia activa de esa compania.
3. A partir de ese momento, los endpoints `api/v1/companies/{companyId}` y los endpoints `tenant-scoped` operan sobre ese contexto activo.

### 5.4 Catalogo comercial base de suscripciones

1. Un administrador de plataforma autenticado administra planes globales desde `api/account/commercial-plans`.
2. El sistema permite registrar `name`, `code`, `description`, `fee` mensual base, precio por empleado activo, estado y limites incluidos.
3. Los planes quedan disponibles como catalogo reutilizable para futuras suscripciones empresariales.
4. El plan `FREE` existe como plan de sistema sembrado y permanece protegido porque el provisioning actual depende de ese codigo.
5. Este flujo no activa suscripciones, no calcula cobros y no administra descuentos ni add-ons.

## 6. Flujo de administracion de estructura organizacional

### 6.1 Catalogos base

1. La cuenta autenticada consulta un catalogo global de tipos de empresa filtrado por pais; ese catalogo ya no depende del owner ni del tenant activo.
2. El tenant administra catalogos de tipos de unidad, areas funcionales y catalogos de descripcion de puestos.
3. Estos catalogos sirven como base para crear estructura, perfiles, posiciones y para clasificar companias.

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

1. El tenant invita usuarios de compania.
2. Puede actualizar nombre, rol, reactivar, desactivar y reenviar invitaciones.
3. El acceso queda siempre limitado al tenant activo.

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
2. Puede consultar grafo, exportes, dependencias y ocupacion.
3. Estas posiciones alimentan la contratacion y asignacion laboral.

## 9. Flujo de expediente de personal

### 9.1 Alta del expediente

1. RRHH crea un `personnel file` para una persona dentro de una compania.
2. El expediente nace con informacion base e identificaciones iniciales.
3. Desde ese momento el sistema puede completar el resto de secciones.

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

### 9.3 Transicion a relacion laboral

1. Un expediente puede representar candidato o empleado segun `RecordType`.
2. El cambio funcional de candidato a empleado no ocurre por simple edicion del perfil.
3. La transicion formal se ejecuta por el endpoint `hire`, que crea la capa laboral del expediente.

### 9.4 Vida laboral del expediente

Una vez contratado, RRHH puede mantener:

- perfil laboral
- asignaciones de empleo
- historial contractual
- jerarquia de posicion
- sustituciones de autorizacion
- acciones de personal
- activos y accesos asignados

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
- El catalogo comercial de planes es una excepcion global, administrada solo por `platform_admin` y sin tenant activo.
- La compania activa define el contexto operativo del usuario.
- Los listados operativos se consumen con paginacion.
- La mayoria de actualizaciones sobre bloques del expediente reemplazan la seccion completa.
- Las transiciones de estado relevantes dejan auditoria.
- Los cambios sensibles usan control de concurrencia optimista.
