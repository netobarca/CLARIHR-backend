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
- Listados no calculan dependencias por ítem; `hasDependents` en detalle vía proyección SQL `EXISTS`. Ver `project-foundation.md §12.7`.
- Búsqueda free-text impone longitud mínima de `q` y declara supuesto de escala (LIKE '%x%' no-sargable). Ver `project-foundation.md §12.8`.

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

## 15. Ubicaciones objetivo de documentación (Híbrido)

Tomar como referencia la siguiente estructura. Las carpetas `technical` y `templates` se manejan dentro del repositorio, mientras que el resto se manejan externamente.
Para los documentos de explicación destinados al frontend, **NO** se crearán dentro del proyecto de forma automática; el usuario indicará cuándo y dónde crearlos manualmente.

```text
docs/
  technical/                  <-- (Manejado dentro del repositorio)
    overview/
      project-foundation.md
    api/
      endpoint-reference.md
      openapi.yaml
    security/
    performance/
    operations/
    data/

  templates/                  <-- (Manejado dentro del repositorio)
    hu-closeout-template.md
    adr-template.md

  business/                   <-- (Manejado externamente)
    current-system-business-flows.md

  analysis/                   <-- (Manejado externamente)
    current-state/
      architecture-analysis.md
      security-analysis.md
      performance-analysis.md
      testing-analysis.md
    changes/
      hu-index.md
      HU-XXXX.md

  decisions/                  <-- (Manejado externamente)
    ADR-XXXX.md
```

---

## 16. Estrategia de branching para sesiones Claude concurrentes

> **Propósito**: permitir que **más de una sesión de Claude** trabaje el backlog en paralelo (p. ej. doc `08` §5) **sin choques de trabajo ni conflictos de merge**. Esta sección es la fuente canónica del flujo git multi-sesión. Decidida 2026-05-19 (trunk-based · 1 finding = 1 PR · claim vía Issue+labels · `master` protegido).

### 16.1 Modelo
- **Trunk-based**. `master` = única rama de larga vida, **protegida**, siempre desplegable. Sin `develop`.
- **1 finding / HU = 1 rama corta = 1 PR** (coincide con el flujo PR #4/#5).
- Naming: `fix/<id>-<kebab-slug>` (deuda) · `feat/<id>-<slug>` (HU). Ej.: `fix/ps2-search-minlength-guard`. `<id>` en minúsculas sin `§`.

### 16.2 Claim atómico (anti doble-trabajo)
Cada ítem del backlog tiene **un GitHub Issue**. Ciclo de labels: `status:available` → `status:claimed` → `status:in-pr` → `status:done` (issue cerrado).

Protocolo **obligatorio** antes de tocar código:
1. `gh issue list --label status:available` → elegir uno.
2. `gh issue edit <n> --add-assignee @me --remove-label status:available --add-label status:claimed`.
3. **Releer** el issue (`gh issue view <n>`): si ya estaba claimed/asignado por otra sesión (carrera) → abandonar y elegir otro. El estado en GitHub es atómico: el primero que edita gana; la 2ª sesión ve el estado y se retira.
4. Solo entonces: `git fetch origin && git checkout -b fix/<id>-<slug> origin/master`.

Nunca crear rama ni editar código de un finding sin su issue en `status:claimed` asignado a la sesión.

### 16.3 Aislamiento por archivo-caliente (anti conflicto-de-merge)
- El **cuerpo del Issue DEBE listar el _file set_** (columna "Dónde" del doc `08`).
- **Exclusión mutua**: NO reclamar un finding cuyo file set **intersecte** el de cualquier issue actualmente `status:claimed` o `status:in-pr`. Elegir otro de set disjunto.
- Archivos de alta contención y política:
  - `PositionSlotsController.cs` → §X-OPENAPI, §X-VER, §PS7 → **serializar**; preferible una sola sesión haga §X-OPENAPI+§X-VER juntos (cluster doc `08`).
  - `PositionSlotAdministration.cs` → §PS2, §PS4, §PS5 → 1 a la vez.
  - `PositionSlotRepository.cs` → §PS3, §PS4.
  - `PositionSlot.cs` (Domain) → §PS5, §PS6.
  - `ApiIntegrationTests.cs` → casi todos añaden tests: bloque contiguo nombrado por el finding + **rebase de `origin/master` justo antes del push** (conflicto "ambos añadieron método" → conservar ambos).
- Ejemplo de sets disjuntos paralelizables hoy: §PS3 (repo) ‖ §PS6 (domain) ‖ §X-LOG (binder) ‖ §X-TEST2 (test nuevo).

### 16.4 Doc `08` (fuera de git → last-write-wins silencioso)
`docs/technical-debt/Position/08-…md` **no está en git**; dos sesiones editándolo se pisan sin aviso. Regla: lo edita **solo la sesión cuyo PR acaba de mergear**, **un finding a la vez**, inmediatamente tras el merge. Edits mínimos: flip de la fila en §5 + banner en §2/§3 + **append** de su subsección en §7 (nunca reescribir §7 ajeno). Si otra sesión está en su post-merge doc-update, esperar.

### 16.5 Reglas de PR
- Ramificar siempre de `origin/master` **fresco**; antes del PR: `git fetch && git rebase origin/master`, resolver.
- **Atómico**: solo los archivos del finding (disciplina §X-AUTHZ/§X-RATE/§PS1: diff mínimo, sin refactors no pedidos).
- **Verde local obligatorio** antes del PR: `dotnet build CLARIHR.slnx` 0/0 + unit suite + guardrails + integración dirigida del finding (+ sanity red→verde si añade guardrail).
- `gh pr create` enlazando el issue (`Closes #<n>`), título convencional, cuerpo con qué/verificación; issue → `status:in-pr`.
- Merge `--no-ff` (o squash) a `master`; issue → `status:done` + cerrar; rama borrada.
- `master` **protegido**: push directo prohibido; todo entra por PR que pase los checks.

### 16.6 Etiqueta multi-sesión
- Una sesión **nunca** toca archivos fuera del file set de su issue claimed.
- Sets relevantes ocupados → no forzar: reportar/esperar o tomar un finding disjunto.
- `git fetch` antes de cualquier rebase/push; **jamás** `push --force` a `master`; `--force-with-lease` solo a la propia rama de finding.
- Abandonar un finding → revertir la rama y devolver el issue a `status:available` (quitar assignee/label).

### 16.7 Bootstrap
Esta sección es el arranque de la estrategia y por necesidad se introduce sin PR previo (no se puede seguir una estrategia que aún no existe). A partir de su adopción, todo cambio —incluida la edición de este archivo— sigue §16.1–§16.6.

### 16.8 Checklist para APLICAR al remoto (pendiente de aprobación; no ejecutado aún)
1. Crear labels `status:available|claimed|in-pr|done`.
2. Crear 1 Issue por ítem abierto de doc `08` §5 (§PS2, §PS3, §PS4, §PS5, §PS6, §PS7, §1-bis, §X-OPENAPI, §X-VER, §X-ISP, §X-LOG, §X-TEST1, §X-TEST2) con su file set + `status:available`.
3. Añadir workflow CI mínimo (GitHub Actions): build + unit + guardrails (+ integración) — requisito para "required checks".
4. Habilitar branch protection en `master`: requerir PR, prohibir push directo y force-push, requerir el check de CI verde.
5. (Opcional) `CODEOWNERS` + plantilla de PR que enlace el issue.