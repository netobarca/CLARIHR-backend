# AGENTS.md — CLARIHR Backend

## 1. Propósito

Este archivo define **cómo debe trabajar Codex dentro de este repositorio**.

Su objetivo es asegurar que toda tarea de desarrollo, análisis, corrección o documentación se ejecute de forma:

- ordenada,
- consistente,
- segura,
- alineada con la arquitectura del proyecto,
- y sin generar caos documental.

Este archivo aplica a todo el repositorio, salvo que exista otro `AGENTS.md` más específico en un subdirectorio.

---

## 2. Documento rector del proyecto

Antes de implementar cualquier cambio, tomar como referencia principal:

- `docs/technical/overview/project-foundation.md`

Ese documento define la base canónica del proyecto en cuanto a:

- arquitectura,
- stack,
- multi-tenant,
- seguridad,
- rendimiento,
- pruebas,
- gobernanza documental,
- y definición de salida por historia de usuario.

Si existe contradicción entre documentación dispersa y el foundation document, usar como base el foundation document y evitar propagar documentación duplicada.

---

## 3. Naturaleza del proyecto

CLARIHR Backend es un backend **.NET 10 Web API** bajo:

- **Clean Architecture**
- **CQRS**
- **PostgreSQL**
- **EF Core**
- **JWT + Refresh Tokens**
- **RBAC**
- **tenant-scoped by default**

El sistema maneja información sensible de RRHH y debe proteger:

- aislamiento entre tenants,
- datos personales,
- datos salariales,
- permisos,
- auditoría,
- trazabilidad,
- rendimiento en operaciones de lectura y escritura.

---

## 4. Principios no negociables

## 4.1 Arquitectura
- Respetar Clean Architecture en todo cambio.
- No mover lógica de negocio a controllers.
- No usar infraestructura directamente desde API.
- No contaminar Domain con DTOs, EF, HTTP ni dependencias externas.

## 4.2 CQRS
- Commands cambian estado.
- Queries solo leen.
- Queries deben proyectar a DTOs.
- Commands y Queries deben mantener responsabilidades claras.

## 4.3 Multi-tenant
- Todo cambio debe respetar tenant scope.
- Nunca permitir acceso cross-tenant.
- Toda lectura y escritura debe considerar explícitamente `TenantId`.
- No asumir acceso global salvo que el caso de uso lo requiera y esté formalmente diseñado.

## 4.4 Seguridad
- No exponer datos sensibles innecesarios.
- No escribir secretos en código.
- Validar autenticación, autorización y pertenencia al tenant.
- Aplicar RBAC según el caso.
- Mantener trazabilidad cuando el flujo lo requiera.

## 4.5 Rendimiento
- No crear endpoints de listado sin paginación.
- No cargar entidades completas si solo se necesita una proyección.
- Usar `AsNoTracking()` en queries de lectura cuando aplique.
- Evitar N+1, full scans evitables y consultas sin estrategia.
- No introducir carga pesada en request/response si puede resolverse mejor.

## 4.6 Documentación
- No duplicar documentación existente.
- Antes de crear un archivo nuevo, buscar si existe una fuente canónica para ese tipo de contenido.
- La documentación viva se actualiza; no se clona por HU.
- Cada HU debe dejar salida documental ordenada.

---

## 5. Cómo trabajar ante una tarea o historia de usuario

Cada tarea debe ejecutarse en este orden lógico:

### Paso 1. Entender el requerimiento
Antes de tocar código:

- identificar el objetivo funcional,
- identificar el módulo y capas afectadas,
- identificar riesgos de tenant, seguridad, permisos, auditoría y rendimiento,
- identificar documentos que deberán actualizarse.

### Paso 2. Ubicar el impacto arquitectónico
Determinar si el cambio afecta:

- Domain,
- Application,
- Infrastructure,
- API,
- documentación viva,
- contratos de API,
- SQL,
- pruebas.

### Paso 3. Diseñar el cambio mínimo correcto
Implementar solo lo necesario para cumplir el requerimiento con calidad.

Evitar:
- sobreingeniería,
- duplicación,
- abstracciones prematuras,
- refactors no solicitados sin justificación,
- cambios masivos fuera del alcance.

### Paso 4. Implementar
El código debe quedar alineado con:

- Clean Architecture,
- CQRS,
- tenant isolation,
- Result + ProblemDetails,
- seguridad,
- rendimiento,
- convenciones del proyecto.

### Paso 5. Probar
Agregar o actualizar pruebas según el impacto.

Como mínimo cubrir:
- happy path,
- validaciones,
- permisos,
- tenant scope,
- errores esperados,
- reglas críticas del caso de uso.

### Paso 6. Documentar
Actualizar documentación viva impactada y registrar el cambio de la HU.

### Paso 7. Verificar
Antes de cerrar la tarea, validar:

- compilación,
- pruebas,
- consistencia de contratos,
- consistencia documental,
- no duplicación de archivos.

---

## 6. Reglas por capa

## 6.1 Domain
Aquí viven:
- entidades,
- value objects,
- invariantes,
- reglas de negocio puras.

No hacer aquí:
- EF Core,
- DTOs,
- HTTP,
- acceso a base de datos,
- dependencias de infraestructura.

## 6.2 Application
Aquí viven:
- Commands,
- Queries,
- Handlers,
- DTOs,
- Validators,
- contratos,
- flujos de caso de uso.

No hacer aquí:
- lógica de infraestructura concreta,
- acceso directo a persistencia concreta fuera de contratos definidos.

## 6.3 Infrastructure
Aquí viven:
- DbContext,
- configuraciones EF,
- repositorios,
- servicios externos,
- caché,
- integraciones,
- implementaciones técnicas.

No hacer aquí:
- reglas de negocio de dominio,
- orquestación funcional propia del caso de uso.

## 6.4 API
Aquí viven:
- controllers,
- middleware,
- wiring,
- autenticación/autorización de entrada,
- ProblemDetails mapping.

No hacer aquí:
- lógica de negocio,
- acceso directo a EF,
- reglas complejas del dominio.

---

## 7. Reglas de implementación

## 7.1 Controllers
- Deben ser delgados.
- Solo orquestan request/response.
- No deben contener lógica de negocio.
- No deben duplicar validaciones que ya existen en Application.

## 7.2 Handlers
- Deben ejecutar un solo caso de uso claramente definido.
- Deben ser claros, pequeños y testeables.
- Deben aplicar tenant scope, reglas y permisos requeridos.
- Deben retornar resultados consistentes.

## 7.3 Validadores
- Toda entrada relevante debe validarse explícitamente.
- Las reglas deben vivir fuera del controller.
- Los mensajes deben ser claros y consistentes.

## 7.4 Persistencia
- Diseñar consultas y escrituras con conciencia de rendimiento.
- Proteger integridad y aislamiento por tenant.
- Evitar consultas ambiguas o con filtros incompletos.

## 7.5 Errores
- Usar el patrón de `Result` definido por el proyecto.
- Mapear adecuadamente a `ProblemDetails`.
- No exponer stack traces ni datos internos sensibles.

---

## 8. Reglas de seguridad y permisos

Para cualquier cambio, revisar si aplica:

- autenticación,
- autorización,
- tenant scoping,
- field permissions,
- ownership,
- auditoría,
- rate limiting,
- protección ante abuso,
- exposición de PII.

Nunca asumir que un endpoint interno es automáticamente seguro.

Si un cambio toca autenticación, autorización, usuarios, roles, permisos, datos sensibles o auditoría, tratarlo como un cambio de alta sensibilidad.

---

## 9. Reglas de rendimiento

Al implementar endpoints, queries o procesos:

- paginar listados,
- proyectar a DTOs,
- usar `AsNoTracking()` en lecturas cuando aplique,
- evitar includes innecesarios,
- evitar N+1,
- evitar traer columnas no utilizadas,
- revisar índices si el patrón de acceso cambia,
- considerar impacto de tenant sobre consultas e índices.

Si el flujo es potencialmente pesado:
- evaluar asincronía,
- evaluar background processing,
- evitar ejecución costosa en el request path.

---

## 10. Reglas de pruebas

Toda tarea con impacto funcional relevante debe considerar pruebas.

### Probar como mínimo:
- caso exitoso,
- validaciones,
- permisos,
- aislamiento tenant,
- errores de negocio,
- comportamiento esperado del handler o regla.

### No usar unit tests para:
- probar middleware HTTP completo,
- probar base de datos real,
- probar integraciones reales,
- probar carga.

El enfoque unitario principal del proyecto está en:
- Domain,
- Application,
- validadores,
- lógica pura,
- handlers,
- reglas críticas.

---

## 11. Gobernanza documental

La documentación del proyecto tiene dos categorías:

## 11.1 Documentación viva
Representa el estado actual del sistema.

Debe actualizarse cuando cambie el sistema.

Ejemplos:
- flujos de negocio,
- análisis de arquitectura,
- análisis de seguridad,
- análisis de performance,
- análisis de testing,
- referencias técnicas vigentes,
- foundation del proyecto.

## 11.2 Registro de cambio por HU
Representa el impacto específico de una historia de usuario o requerimiento.

Debe resumir:
- qué cambió,
- qué documentos fueron actualizados,
- qué validaciones se hicieron,
- qué riesgos o pendientes quedan.

---

## 12. Regla de fuente canónica única

Para cada tipo de información debe existir una sola fuente oficial.

### Regla obligatoria
Antes de crear un archivo documental nuevo, validar:

1. ¿ya existe un documento canónico para este contenido?
2. ¿puedo actualizar el documento vivo existente en lugar de crear otro?
3. ¿esto debe ser un documento vivo o solo un registro de cambio por HU?

### Prohibido
- crear carpetas por HU con copias completas de análisis ya existentes,
- mantener dos documentos manuales con la misma información de API,
- duplicar arquitectura, performance, seguridad o testing por historia,
- crear documentación paralela “temporal” sin propósito claro.

---

## 13. Regla documental por historia de usuario

Toda HU completada debe dejar una salida ordenada.

## 13.1 Salida obligatoria
- código implementado,
- pruebas agregadas o actualizadas,
- documentación viva actualizada si fue impactada,
- registro documental de la HU,
- resumen claro de verificación.

## 13.2 Salida condicional
Si la HU lo requiere, también actualizar:
- flujo de negocio,
- análisis de arquitectura,
- análisis de seguridad,
- análisis de performance,
- análisis de testing,
- referencia de API,
- scripts SQL,
- decisiones técnicas formales,
- documentación operativa.

## 13.3 Regla de no caos documental
Una HU no debe crear una estructura nueva de documentos si ya existe una estructura objetivo para ese contenido.

---

## 14. Política sobre documentación técnica actual

Durante la transición documental del proyecto:

- favorecer siempre la actualización del documento más canónico disponible,
- evitar seguir alimentando árboles documentales duplicados,
- preferir una sola referencia principal de API,
- convertir análisis repetidos por HU en actualización de documentos vivos + registro puntual por HU.

Si existe una estructura antigua y una estructura nueva en paralelo, priorizar la nueva estructura objetivo definida por el proyecto.

---

## 15. Ubicaciones objetivo de documentación

Tomar como referencia objetivo:

```text
docs/
  business/
    current-system-business-flows.md

  analysis/
    current-state/
      architecture-analysis.md
      security-analysis.md
      performance-analysis.md
      testing-analysis.md
      remediation-plan.md
      validation-checklist.md
    changes/
      hu-index.md
      HU-XXXX.md

  technical/
    overview/
      project-foundation.md
    api/
      endpoint-reference.md
      openapi.yaml
    security/
    performance/
    operations/
    data/

  decisions/
    ADR-XXXX.md

  templates/
    hu-closeout-template.md
    adr-template.md