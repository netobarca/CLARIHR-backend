---
name: implement-dotnet-cqrs-user-story
description: Usa esta skill cuando debas implementar una historia de usuario o requerimiento backend en .NET con Clean Architecture, CQRS, multi-tenant, seguridad, performance y unit testing. Úsala para crear o modificar código en Domain, Application, Infrastructure, API y Tests de forma ordenada. No usar para cierres documentales exclusivamente ni para reestructuraciones masivas fuera del alcance de una HU.
---

# Implement Dotnet CQRS User Story

## 1. Propósito

Esta skill existe para implementar historias de usuario backend en CLARIHR de forma consistente con la arquitectura, seguridad, rendimiento, testing y gobernanza documental del proyecto.

Su objetivo es asegurar que cada requerimiento backend:

- se implemente en la capa correcta,
- respete Clean Architecture y CQRS,
- mantenga tenant isolation,
- contemple seguridad y permisos,
- no introduzca problemas evidentes de performance,
- deje pruebas suficientes,
- y prepare una salida ordenada para documentación y cierre.

---

## 2. Cuándo usar esta skill

Usar esta skill cuando la tarea principal sea implementar o modificar backend relacionado con una HU o requerimiento.

### Casos típicos
- “Implementa esta HU en backend”
- “Crea el command/query/handler para este caso de uso”
- “Agrega endpoint, validaciones y persistencia para este requerimiento”
- “Desarrolla esta funcionalidad en .NET con CQRS”
- “Implementa el flujo completo backend de esta historia”

---

## 3. Cuándo NO usar esta skill

No usar esta skill para:

- solo documentar una HU ya implementada,
- solo actualizar `hu-index.md`,
- reorganizar documentación existente,
- crear ADRs sin implementación asociada,
- hacer refactors masivos sin requerimiento funcional claro,
- generar documentación para usuario final.

Si la tarea es cierre documental, usar la skill:

- `.agents/skills/close-user-story-docs/SKILL.md`

---

## 4. Fuentes de verdad obligatorias

Antes de implementar, revisar en este orden:

1. `docs/technical/overview/project-foundation.md`
2. `/AGENTS.md`
3. `docs/AGENTS.md`
4. La HU o requerimiento fuente
5. Documentación técnica y funcional relacionada ya existente
6. Convenciones reales del código actual del repositorio

Si el código existente contradice el estándar del proyecto, no propagues el error sin analizarlo.  
Alinea la solución al foundation document y al diseño correcto del sistema, minimizando ruptura innecesaria.

---

## 5. Principios no negociables

## 5.1 Arquitectura
- Respetar Clean Architecture.
- No poner lógica de negocio en controllers.
- No usar EF Core directamente desde API.
- No contaminar Domain con DTOs, HTTP o dependencias técnicas.

## 5.2 CQRS
- Commands cambian estado.
- Queries solo leen.
- Los handlers deben representar un caso de uso claro.
- Las lecturas deben proyectar a DTOs.

## 5.3 Multi-tenant
- Toda lectura y escritura debe respetar `TenantId`.
- Nunca asumir acceso cross-tenant.
- Toda lógica debe diseñarse tenant-scoped by default.

## 5.4 Seguridad
- Aplicar autenticación, autorización y permisos cuando corresponda.
- No exponer información sensible innecesaria.
- Considerar auditoría en flujos críticos.
- No confiar solo en validaciones de entrada; proteger también a nivel de caso de uso.

## 5.5 Rendimiento
- No crear listados sin paginación.
- Usar proyección a DTO.
- Usar `AsNoTracking()` en queries cuando aplique.
- Evitar N+1, includes innecesarios y cargas excesivas.
- Pensar en índices si el patrón de consulta cambia.

## 5.6 Testing
- Toda HU relevante debe dejar unit tests suficientes.
- Como mínimo cubrir happy path, validaciones, errores esperados, permisos y tenant scope cuando aplique.

## 5.7 Identificadores y codigos
- Toda entidad persistida nueva debe conservar `id` interno para PK/FK/joins y agregar `public_id` persistido como identificador externo.
- Ningún request, response, export o contrato público puede exponer `id` o `internalId`; hacia afuera se usa `publicId` o `<Entidad>PublicId`.
- Si la entidad tiene código de negocio, el contrato y la persistencia deben incluir `code` y `normalizedCode`.
- `code` y `normalizedCode` deben quedar normalizados en `UPPERCASE`; no se debe preservar mixed case.
- Las búsquedas, unicidad e integraciones internas deben resolver por `normalizedCode` y traducir a `Id` interno solo dentro del backend.

## 5.8 Cambios de schema (gestion manual)
- Si la tarea cambia entidades persistidas, configuraciones EF, relaciones, índices, `DbSet`, columnas, constraints o cualquier parte del modelo, debes identificar el impacto de schema.
- La ejecucion de pasos de schema y su procesamiento operativo es manual y la realiza el usuario; no es parte de los pasos automaticos de esta skill.
- El resultado esperado desde esta skill es dejar claro el handoff tecnico de impacto para que el usuario procese esos pasos manualmente.

---

## 6. Entradas mínimas esperadas

Para usar esta skill debes identificar o inferir:

- código HU o requerimiento,
- objetivo funcional,
- módulo afectado,
- actor o usuario involucrado,
- reglas de negocio,
- criterios de aceptación,
- entidades afectadas,
- si el caso es command o query,
- endpoints requeridos,
- validaciones,
- reglas de permisos,
- impacto en tenant,
- impacto en auditoría,
- impacto en performance,
- pruebas esperadas.

Si no todo está explícito, infiere con criterio técnico, pero no inventes reglas de negocio arbitrarias.

---

## 7. Flujo de implementación

## Paso 1. Entender la HU
Antes de escribir código, entender:

- qué problema resuelve,
- quién ejecuta el flujo,
- qué cambia en el sistema,
- qué reglas de negocio deben cumplirse,
- qué criterios de aceptación existen,
- qué riesgos de seguridad, permisos, tenant y rendimiento aparecen.

## Paso 2. Clasificar el caso de uso
Determinar si se trata de:

- Command
- Query
- cambio combinado con impacto en varios artefactos
- ajuste de soporte sin contrato visible

## Paso 3. Identificar capas afectadas
Definir si el requerimiento toca:

- Domain
- Application
- Infrastructure
- API
- Tests
- SQL / Data
- Documentation

## Paso 4. Diseñar el cambio mínimo correcto
Implementar solo lo necesario para cumplir el requerimiento bien.

Evitar:
- sobreingeniería,
- abstracciones prematuras,
- duplicación de lógica,
- refactors masivos fuera de alcance.

## Paso 5. Implementar por capa
Crear o modificar solo los artefactos necesarios en la capa correcta.

## Paso 6. Validar seguridad y tenant
Asegurarte de que la lógica:
- respete tenant scope,
- aplique permisos correctos,
- no exponga datos indebidos,
- considere auditoría si el flujo lo requiere.

## Paso 7. Validar rendimiento
Asegurarte de que:
- las lecturas estén optimizadas,
- las escrituras no carguen complejidad innecesaria,
- los listados sean paginados,
- no existan consultas obviamente deficientes.

## Paso 8. Registrar impacto de schema (sin ejecucion automatica)
Si hubo cambios de modelo persistido:

- identificar claramente qué tablas, columnas, relaciones o datos base quedan impactados,
- registrar el handoff tecnico para que el usuario procese manualmente los pasos de schema,
- no ejecutar pasos automaticos de schema desde esta skill.

## Paso 9. Agregar pruebas
Agregar o actualizar pruebas unitarias relevantes.

## Paso 10. Preparar salida documental
Identificar qué documentos vivos y qué archivo HU deberán actualizarse al cerrar la historia.  
No cerrar la HU sin trazabilidad mínima.

---

## 8. Guía por capa

## 8.1 Domain
Usar Domain para:
- entidades,
- value objects,
- invariantes,
- reglas de negocio puras,
- enums,
- domain events cuando apliquen.

No poner aquí:
- DTOs,
- EF,
- requests HTTP,
- repositorios concretos,
- lógica técnica de infraestructura.

## 8.2 Application
Usar Application para:
- Commands,
- Queries,
- Handlers,
- DTOs,
- Validators,
- contratos,
- autorización de caso de uso,
- flujos de aplicación,
- mapping no trivial.

No poner aquí:
- detalles técnicos de EF,
- wiring HTTP,
- infraestructura concreta.

## 8.3 Infrastructure
Usar Infrastructure para:
- DbContext,
- configuraciones EF,
- repositorios,
- servicios externos,
- auditoría técnica,
- caché,
- integraciones,
- persistencia.

No poner aquí:
- reglas de negocio puras del dominio,
- lógica del caso de uso que pertenece a Application.

## 8.4 API
Usar API para:
- controllers,
- middleware,
- wiring,
- auth config,
- ProblemDetails mapping,
- request/response HTTP.

No poner aquí:
- lógica de negocio,
- consultas directas a base,
- reglas complejas del caso de uso.

---

## 9. Reglas para Commands

Usar un Command cuando el caso de uso:

- crea,
- actualiza,
- elimina,
- aprueba,
- rechaza,
- activa,
- desactiva,
- o cambia estado.

### Un Command bien implementado debe:
- representar una intención clara,
- validar entrada,
- validar tenant,
- validar permisos,
- ejecutar reglas de negocio,
- persistir correctamente,
- auditar si aplica,
- devolver un resultado consistente.

---

## 10. Reglas para Queries

Usar una Query cuando el caso de uso solo lee información.

### Una Query bien implementada debe:
- no cambiar estado,
- usar proyección a DTO,
- usar `AsNoTracking()` cuando aplique,
- paginar listados,
- filtrar correctamente por tenant,
- evitar cargar más datos de los necesarios,
- devolver un contrato claro.

---

## 11. Reglas de validación

Toda entrada relevante debe validarse explícitamente.

### Validar como mínimo
- campos requeridos,
- formatos,
- longitudes,
- reglas de negocio básicas,
- pertenencia al tenant cuando aplique,
- estados válidos,
- relaciones requeridas.

### No hacer
- depender solo del controller para validar,
- mezclar validación técnica y de negocio sin claridad,
- dejar errores ambiguos o inconsistentes.

---

## 12. Reglas de seguridad

Si la HU toca usuarios, roles, permisos, datos sensibles, salarios, auditoría, aprobaciones o entidades críticas, tratarla como sensible.

### Validar como mínimo
- autenticación,
- autorización,
- tenant scope,
- ownership si aplica,
- exposición mínima de datos,
- auditoría para acciones críticas.

### Nunca hacer
- devolver datos de otro tenant,
- asumir que el usuario autenticado ya tiene permiso suficiente,
- exponer errores internos innecesarios,
- omitir controles porque “el frontend ya valida”.

---

## 13. Reglas de rendimiento

### En queries
- usar `AsNoTracking()` cuando aplique,
- proyectar a DTO,
- paginar,
- filtrar temprano,
- no traer columnas o relaciones innecesarias.

### En commands
- mantener transacciones cortas,
- evitar operaciones pesadas dentro del request path,
- no cargar agregados completos si no se necesitan.

### Evaluar especialmente
- índices,
- patrones de búsqueda,
- volumen esperado,
- impacto tenant-first en consultas.

---

## 14. Reglas de auditoría

Evaluar auditoría si la HU implica:

- creación o edición de entidades importantes,
- cambios de permisos,
- cambios salariales,
- aprobaciones,
- acciones administrativas,
- cambios sensibles o trazables por negocio.

Si aplica auditoría, no la dejes implícita.  
Debe quedar clara en la implementación y luego en el cierre documental.

---

## 15. Reglas de pruebas

Agregar o actualizar pruebas unitarias enfocadas en comportamiento.

### Cubrir como mínimo
- happy path,
- validaciones,
- errores esperados,
- permisos,
- tenant scope,
- reglas críticas del caso de uso.

### Enfoque esperado
- xUnit
- FluentAssertions
- mocks estandarizados del proyecto
- tests legibles y determinísticos

### No hacer
- tests acoplados a detalles internos innecesarios,
- tests que dependan de reloj real, red o DB real en unit tests.

---

## 16. Salida esperada de implementación

Una HU backend bien implementada debe dejar, según aplique:

- entidades o ajustes de Domain,
- Commands / Queries / Handlers,
- Validators,
- contratos o DTOs,
- repositorios o servicios necesarios,
- endpoints,
- ajustes de persistencia,
- pruebas,
- preparación para actualización documental.

No necesariamente todas las capas cambian en cada HU.  
Modificar solo las necesarias.

---

## 17. Qué revisar antes de cerrar la implementación

Antes de considerar la HU implementada, revisar:

- [ ] El requerimiento quedó cubierto
- [ ] La solución respeta Clean Architecture
- [ ] La solución respeta CQRS
- [ ] La solución respeta tenant isolation
- [ ] La seguridad requerida fue considerada
- [ ] El rendimiento básico fue considerado
- [ ] Las validaciones están completas
- [ ] Los errores están bien manejados
- [ ] Existen pruebas suficientes
- [ ] Si hubo cambios persistidos, quedó documentado el handoff manual de schema para el usuario
- [ ] Los flujos públicos resuelven recursos por `PublicId`
- [ ] Ningún contrato o export público expone `id` o `internalId`
- [ ] Todo `code`/`normalizedCode` observable está en `UPPERCASE`
- [ ] No se introdujo duplicación innecesaria
- [ ] La HU está lista para cierre documental

---

## 18. Verificación técnica mínima

Usar como base:

```bash
dotnet restore
dotnet build
dotnet test
```

Si hubo impacto de schema, esta skill solo debe dejar el handoff tecnico para procesamiento manual por parte del usuario.
