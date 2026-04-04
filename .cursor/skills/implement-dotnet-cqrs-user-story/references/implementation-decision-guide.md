# Guía de decisión de implementación por capa y tipo de cambio

## 1. Propósito

Esta guía ayuda a decidir **qué capas, artefactos y documentos deben tocarse** al implementar una historia de usuario o requerimiento backend en CLARIHR.

Debe usarse junto con:

- `.agents/skills/implement-dotnet-cqrs-user-story/SKILL.md`
- `docs/technical/overview/project-foundation.md`
- `/AGENTS.md`
- `docs/AGENTS.md`

Su objetivo es evitar:

- cambios en capas incorrectas,
- lógica mal ubicada,
- sobreingeniería,
- omisiones en seguridad, tenant o rendimiento,
- y falta de preparación para el cierre documental.

---

## 2. Regla principal

Antes de implementar, responder internamente:

1. ¿Este cambio modifica reglas de negocio puras?
2. ¿Este cambio representa un caso de uso?
3. ¿Este cambio solo toca persistencia o infraestructura?
4. ¿Este cambio afecta contrato HTTP?
5. ¿Este cambio requiere validación explícita?
6. ¿Este cambio afecta tenant, permisos, auditoría o rendimiento?
7. ¿Este cambio requiere pruebas?
8. ¿Este cambio tendrá impacto documental?

### Regla de decisión
- Si el cambio es **regla de negocio pura**, va a **Domain**.
- Si el cambio es **orquestación de caso de uso**, va a **Application**.
- Si el cambio es **persistencia o integración técnica**, va a **Infrastructure**.
- Si el cambio es **exposición HTTP**, va a **API**.
- Si el cambio es **verificación de comportamiento**, va a **Tests**.
- Si el cambio modifica estructura de datos, considerar **SQL / Data**.
- Si el cambio altera estado actual o trazabilidad, preparar impacto en **Docs**.

---

## 3. Cuándo tocar Domain

## 3.1 Cuándo sí
Tocar `Domain` cuando el cambio introduce o modifica:

- entidades,
- value objects,
- invariantes,
- reglas de negocio puras,
- enums del dominio,
- estados del negocio,
- domain events,
- comportamiento propio de una entidad o aggregate.

### Ejemplos
- una organización no puede tener dos códigos únicos iguales dentro del mismo tenant,
- un usuario no puede cambiar a cierto estado sin cumplir una condición previa,
- un empleado debe cumplir reglas de consistencia en datos esenciales,
- un aggregate debe proteger su integridad interna.

## 3.2 Cuándo no
No tocar `Domain` si el cambio es solo:

- DTO de request/response,
- validación de formato de entrada HTTP,
- detalle de EF Core,
- repositorio,
- controller,
- configuración,
- integración externa,
- paginación o filtros de consulta.

## 3.3 Regla práctica
Si la regla seguiría existiendo aunque cambies la base de datos, el framework web o el transporte, probablemente pertenece a `Domain`.

---

## 4. Cuándo tocar Application

## 4.1 Cuándo sí
Tocar `Application` cuando el cambio introduce o modifica:

- Commands,
- Queries,
- Handlers,
- Validators,
- DTOs del caso de uso,
- contratos de repositorio o servicios,
- autorización del caso de uso,
- orquestación de reglas entre dominio e infraestructura,
- Result / errores del caso de uso,
- mapping no trivial.

### Ejemplos
- crear empresa,
- editar organización,
- consultar listado paginado,
- aprobar solicitud,
- desactivar usuario,
- asignar rol,
- consultar permisos efectivos.

## 4.2 Cuándo no
No tocar `Application` para:

- detalles concretos de EF,
- wiring HTTP,
- middleware,
- configuración de autenticación,
- implementación técnica de caching o logging,
- reglas puras que pertenecen a `Domain`.

## 4.3 Regla práctica
Si el cambio representa “qué hace el sistema” ante una intención del usuario o del negocio, casi seguro vive en `Application`.

---

## 5. Cuándo tocar Infrastructure

## 5.1 Cuándo sí
Tocar `Infrastructure` cuando el cambio introduce o modifica:

- `DbContext`,
- configuraciones EF,
- repositorios concretos,
- servicios externos,
- almacenamiento de archivos,
- caché,
- auditoría técnica,
- integraciones,
- cifrado técnico,
- background jobs,
- lectura o escritura concreta sobre base de datos.

### Ejemplos
- agregar configuración EF de una entidad,
- implementar repositorio para consultar organizaciones,
- integrar proveedor externo,
- agregar caché de catálogos,
- persistir refresh tokens.

## 5.2 Cuándo no
No tocar `Infrastructure` para:

- reglas de negocio puras,
- controladores HTTP,
- validaciones funcionales del caso de uso,
- decisiones del flujo de aplicación que pertenecen a `Application`.

## 5.3 Regla práctica
Si el cambio responde a “cómo se implementa técnicamente”, normalmente toca `Infrastructure`.

---

## 6. Cuándo tocar API

## 6.1 Cuándo sí
Tocar `API` cuando el cambio introduce o modifica:

- controllers,
- endpoints,
- rutas,
- binding HTTP,
- autenticación / autorización de entrada,
- ProblemDetails mapping,
- políticas HTTP,
- versionado,
- configuración del pipeline web.

### Ejemplos
- agregar `POST /api/organizations`,
- ajustar respuesta HTTP de un endpoint,
- cambiar códigos de error,
- agregar atributo de autorización,
- exponer un nuevo query vía controller.

## 6.2 Cuándo no
No tocar `API` para:

- lógica de negocio,
- consultas directas a base de datos,
- validaciones principales del caso de uso,
- manejo de dominio,
- persistencia.

## 6.3 Regla práctica
Si el cambio existe solo porque hay HTTP de por medio, probablemente pertenece a `API`.

---

## 7. Cuándo crear un Command

Crear un **Command** cuando la intención sea:

- crear,
- actualizar,
- eliminar,
- activar,
- desactivar,
- aprobar,
- rechazar,
- cambiar estado,
- ejecutar una acción con efecto permanente.

### Un Command debe cubrir
- intención clara,
- validación,
- autorización,
- tenant scope,
- reglas de negocio,
- persistencia,
- resultado consistente,
- auditoría si aplica.

### Ejemplos
- `CreateOrganizationCommand`
- `UpdateOrganizationCommand`
- `DeactivateCompanyUserCommand`

---

## 8. Cuándo crear una Query

Crear una **Query** cuando el caso de uso solo necesite:

- consultar,
- listar,
- filtrar,
- paginar,
- obtener detalle,
- buscar por criterio,
- exponer información sin modificar estado.

### Una Query debe cubrir
- proyección a DTO,
- tenant scope,
- filtros,
- paginación si aplica,
- `AsNoTracking()` cuando corresponda,
- contrato claro.

### Ejemplos
- `GetOrganizationByIdQuery`
- `ListOrganizationsQuery`
- `SearchCompanyUsersQuery`

---

## 9. Cuándo crear o modificar un Validator

Crear o modificar un validator cuando existan reglas de entrada como:

- requeridos,
- longitudes,
- formatos,
- rangos,
- consistencia básica,
- relaciones obligatorias,
- reglas previas al caso de uso.

### Regla
Toda entrada relevante debe validarse explícitamente fuera del controller.

### Ejemplos
- nombre requerido,
- código único con formato específico,
- fecha válida,
- estado permitido,
- lista mínima o máxima.

### No hacer
- mover toda regla de negocio compleja a FluentValidation si pertenece realmente a dominio o al caso de uso.

---

## 10. Cuándo tocar SQL o Data

Tocar SQL / Data cuando el cambio implique:

- tablas nuevas,
- columnas nuevas,
- índices,
- constraints,
- relaciones,
- migraciones,
- backfill,
- seed,
- ajustes tenant-scoped de persistencia.

### Evaluar además
- impacto en performance,
- impacto en seguridad,
- impacto en auditoría,
- impacto en documentación técnica.

### Regla
No introducir cambios de base de datos sin pensar en:
- filtros por tenant,
- índices adecuados,
- consistencia de datos,
- trazabilidad del cambio.

---

## 11. Cuándo considerar seguridad de forma explícita

Debes evaluar seguridad con más profundidad si la HU toca:

- autenticación,
- autorización,
- roles,
- permisos,
- usuarios,
- field permissions,
- tenant isolation,
- auditoría,
- datos personales,
- datos salariales,
- aprobaciones,
- acciones administrativas.

### Preguntas mínimas
- ¿requiere usuario autenticado?
- ¿requiere permiso específico?
- ¿requiere validación de tenant?
- ¿requiere ownership?
- ¿expone PII?
- ¿requiere auditoría?
- ¿podría revelar datos de otro tenant?

---

## 12. Cuándo considerar rendimiento de forma explícita

Debes evaluar rendimiento con más profundidad si la HU toca:

- listados,
- búsquedas,
- filtros,
- catálogos grandes,
- joins múltiples,
- queries repetitivas,
- dashboards,
- reportes,
- exportaciones,
- operaciones masivas,
- procesos en alto volumen.

### Preguntas mínimas
- ¿lleva paginación?
- ¿usa proyección a DTO?
- ¿usa `AsNoTracking()`?
- ¿hay riesgo de N+1?
- ¿requiere índice nuevo?
- ¿debería salir del request path?
- ¿qué tanto crecerá por tenant?

---

## 13. Cuándo considerar auditoría

Evaluar auditoría si la HU implica:

- crear o editar entidades sensibles,
- cambios de roles o permisos,
- cambios salariales,
- aprobaciones o rechazos,
- activación o desactivación de usuarios,
- cambios administrativos críticos,
- acciones que negocio necesite rastrear.

### Regla
Si la acción sería relevante en una investigación, soporte o trazabilidad de negocio, probablemente requiere auditoría.

---

## 14. Cuándo crear pruebas

Crear o modificar pruebas unitarias cuando la HU agregue o cambie:

- reglas de negocio,
- handlers,
- validators,
- autorización funcional,
- tenant scope,
- errores esperados,
- mapping no trivial,
- decisiones sensibles del caso de uso.

### Cobertura mínima por HU
- happy path,
- validaciones,
- errores esperados,
- permisos,
- tenant scope,
- reglas críticas.

### No hacer
- depender de DB real en unit tests,
- probar controllers a profundidad como si fueran tests de integración,
- escribir tests frágiles atados a detalles internos irrelevantes.

---

## 15. Cuándo preparar impacto documental

Preparar impacto documental cuando la HU cambie:

- flujo de negocio,
- arquitectura,
- seguridad,
- performance,
- testing strategy,
- API,
- SQL / Data,
- decisiones técnicas duraderas.

### Regla
Aunque la implementación sea el foco principal, debes dejar identificados los documentos que después deberá actualizar la skill documental.

---

## 16. Secuencia recomendada de implementación

Seguir esta secuencia:

1. Entender la HU.
2. Clasificar si es Command, Query o ambos.
3. Identificar capas afectadas.
4. Diseñar el cambio mínimo correcto.
5. Implementar Domain si aplica.
6. Implementar Application.
7. Implementar Infrastructure.
8. Exponer vía API si aplica.
9. Agregar o actualizar tests.
10. Validar seguridad, tenant y rendimiento.
11. Dejar lista la trazabilidad documental.

---

## 17. Ejemplos rápidos de decisión

## Caso A: Crear organización
Probablemente toca:
- Domain
- Application
- Infrastructure
- API
- Tests
- Docs

## Caso B: Listar organizaciones con paginación
Probablemente toca:
- Application
- Infrastructure
- API
- Tests
- Docs si cambia referencia de API o performance

## Caso C: Corregir validación de nombre requerido
Probablemente toca:
- Application
- Tests
- Docs solo si requiere trazabilidad puntual

## Caso D: Cambiar estrategia de IDs públicas
Probablemente toca:
- Domain
- Application
- Infrastructure
- API
- Tests
- Docs
- posiblemente ADR

---

## 18. Checklist rápida

Antes de implementar, validar:

- [ ] Identifiqué si el cambio pertenece a Domain
- [ ] Identifiqué si el cambio pertenece a Application
- [ ] Identifiqué si el cambio pertenece a Infrastructure
- [ ] Identifiqué si el cambio pertenece a API
- [ ] Identifiqué si necesito Validator
- [ ] Identifiqué si necesito SQL / Data
- [ ] Evalué seguridad
- [ ] Evalué tenant scope
- [ ] Evalué rendimiento
- [ ] Evalué auditoría
- [ ] Identifiqué pruebas necesarias
- [ ] Identifiqué impacto documental posterior

---

## 19. Criterio rector

Si hay duda sobre dónde implementar algo, aplicar esta regla:

**la regla pura va al dominio, el caso de uso va a application, la implementación técnica va a infrastructure, la exposición HTTP va a API, y toda HU debe considerar seguridad, tenant, rendimiento, pruebas y trazabilidad documental.**