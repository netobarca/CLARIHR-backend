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

> **Propósito**: permitir que **varias sesiones de Claude** trabajen en paralelo **aisladas por feature/dominio** (p. ej. una sesión JobProfile, otra PersonnelFiles, otra PersonnelFileInterest) sin choques. Fuente canónica del flujo git. **Modelo v2 (2026-05-20): trunk-based, solo `master`, producción por tag/Release manual.** Reemplaza el v1 "1 finding = 1 PR + file-set" (ver §16.10).

### 16.1 Modelo
- **Trunk-based. Una sola rama de larga vida: `master`.** Sin `develop`, sin ramas de entorno (`dev/qa/uat/staging`): no aportan (no hay infra detrás) y generan merge-hell.
- `master` **= entorno DEV**: cada push a `master` auto-despliega a Azure `apiclarihrdev` (workflow `master_apiclarihrdev.yml` existente).
- **Producción = deploy manual desde un tag/Release inmutable** de un `master` verde (§16.7). El entorno es asunto de *deploy*, no de *rama*.
- **1 sesión = 1 feature/dominio = ramas cortas** → PR → `master`. Sesiones en dominios distintos tocan carpetas distintas → colisión ~nula por construcción.

### 16.2 Naming de ramas
`<tipo>/<dominio>/<slug-kebab>` — `<tipo>` ∈ `feat|fix|perf|refactor|chore|docs`; `<dominio>` ∈ `jobprofile | personnel-files | personnel-file-interest | position | reports | process | …`.
Ej.: `feat/personnel-files/interest-capture` · `fix/position/ps2-search-minlength` · `docs/process/branching-strategy-trunk`.

### 16.3 Claim por Issue (anti doble-trabajo entre sesiones)
Para trabajo de backlog rastreado (p. ej. doc `08`), cada ítem tiene **un GitHub Issue** con ciclo `status:available → status:claimed → status:in-pr → status:done`. Antes de ramificar:
1. `gh issue list --label status:available`.
2. `gh issue edit <n> --add-assignee @me --remove-label status:available --add-label status:claimed`.
3. **Releer** (`gh issue view <n>`): si otra sesión ganó la carrera (assignee/label ya puesto) → tomar otro. El estado GitHub es el lock atómico.
4. `git fetch origin && git checkout -b <tipo>/<dominio>/<slug> origin/master`.

Features nuevas sin issue de backlog: crear el issue (o usar el de la HU) y seguir igual. No iniciar trabajo de un ítem rastreado sin su issue `status:claimed` propio.

### 16.4 Aislamiento (guía blanda — degradado en v2)
Con sesiones por dominio distinto el solapamiento es casi nulo, así que **NO** se exige el "file-set por issue" del v1. Única verificación ligera: si tu cambio toca un **archivo cross-cutting compartido** (`Program.cs`, `AGENTS.md`, `Directory.*.props`, `src/**/Common/*`, `*GuardrailsTests*`, o el doc `08`), comprueba que ningún otro issue `status:claimed`/`status:in-pr` lo toque; si choca, coordina/serializa. Entre dominios distintos no aplica.

### 16.5 Doc `08` (fuera de git → last-write-wins silencioso)
`docs/technical-debt/Position/08-…md` **no está en git**; dos sesiones editándolo se pisan sin aviso. Regla: lo edita **solo la sesión cuyo PR acaba de mergear**, **un finding a la vez**, inmediatamente tras el merge. Edits mínimos: flip de la fila en §5 + banner en §2/§3 + **append** de su subsección en §7 (nunca reescribir §7 ajeno). Si otra sesión está en su post-merge doc-update, esperar.

### 16.6 Reglas de PR
- Ramificar siempre de `origin/master` **fresco**; antes del PR: `git fetch && git rebase origin/master`, resolver.
- **Atómico**: solo los archivos del finding (disciplina §X-AUTHZ/§X-RATE/§PS1: diff mínimo, sin refactors no pedidos).
- **Verde local obligatorio** antes del PR: `dotnet build CLARIHR.slnx` 0/0 + unit suite + guardrails + integración dirigida del finding (+ sanity red→verde si añade guardrail).
- `gh pr create` enlazando el issue de backlog si aplica (`Closes #<n>`), título convencional, cuerpo con qué/verificación; issue → `status:in-pr`.
- Merge `--no-ff` (o squash) a `master`; issue → `status:done` + cerrar; rama borrada.
- `master`: push directo **prohibido por convención**; todo entra por PR con CI verde. (En plan free privado GitHub no puede *forzar* esto server-side — ver §16.10; la regla es obligatoria igual.)

### 16.7 Promoción a Producción (manual, por tag/Release)
`master` despliega solo a DEV (`apiclarihrdev`). Producción la despliega **el usuario, manualmente** (no automatizado). Anclar SIEMPRE el deploy a un **tag inmutable**, nunca a "lo último de master":
1. Elegir un `master` verde (CI `build-and-unit` ✅): `git checkout master && git pull`.
2. Tag + Release: `git tag -a vX.Y.Z -m "prod: <resumen>" && git push origin vX.Y.Z` · `gh release create vX.Y.Z --notes "SHA · qué va · issues #…"`.
3. Desplegar manualmente **ese SHA/artefacto** a prod (proceso actual del usuario).
4. **Rollback** = re-desplegar el tag/Release anterior. No crear rama `production` (un tag es inmutable, trazable y sin merge/divergencia). Futuro opcional: `workflow_dispatch` que despliegue un tag elegido.

### 16.8 Etiqueta multi-sesión
- Una sesión **nunca** toca archivos fuera del file set de su issue claimed.
- Sets relevantes ocupados → no forzar: reportar/esperar o tomar un finding disjunto.
- `git fetch` antes de cualquier rebase/push; **jamás** `push --force` a `master`; `--force-with-lease` solo a la propia rama de finding.
- Abandonar un finding → revertir la rama y devolver el issue a `status:available` (quitar assignee/label).

### 16.9 Bootstrap
El **v1** de esta sección entró sin PR (no se puede seguir una estrategia inexistente). El **v2** (este modelo) entró **vía PR**, dogfooding §16.6. Todo cambio futuro —incluida esta sección— sigue §16.1–§16.8.

### 16.10 Estado de aplicación al remoto
0. ✅ Modelo revisado a **v2** 2026-05-20 (trunk · `<tipo>/<dominio>/<slug>` · prod por tag/Release manual · file-set v1 degradado a guía §16.4). v1 era "1 finding=1 PR + file-set exclusión".
1. ✅ Labels `status:available|claimed|in-pr|done` + `tech-debt` creados.
2. ✅ 14 Issues creados (1 por ítem abierto de doc `08` §5): `#6 §PS2`, `#7 §PS3`, `#8 §PS4`, `#9 §PS5`, `#10 §PS6`, `#11 §PS7`, `#12 §PS8`, `#13 §X-OPENAPI`, `#14 §X-VER`, `#15 §X-ISP`, `#16 §X-LOG`, `#17 §X-TEST1`, `#18 §X-TEST2`, `#19 §1-bis` — todos `status:available` + `tech-debt`, con file set.
3. ✅ Workflow CI `.github/workflows/ci.yml` (job `build-and-unit`: build + unit suite + guardrails) — verde en `master`. Integración NO es gate (testcontainers + el fallo pre-existente `JobProfiles_Compensation_…` la harían roja siempre); se corre local (§16.5). `.github/pull_request_template.md` añadido.
4. ❌ **Branch protection NO aplicable**: GitHub responde `403 Upgrade to GitHub Pro or make this repository public`. El repo es **privado en plan free** (owner tipo `User`) → la protección de ramas (clásica y rulesets) **no está disponible**. **No** se hará público (backend RRHH sensible). Decisión del usuario: (a) GitHub Pro/Team → entonces ejecutar el comando de §16.9; o (b) operar con **enforcement por convención + señal CI** (esta §16, no bloqueante server-side) hasta el upgrade.
5. ⏳ `CODEOWNERS` no añadido (un solo maintainer; sería ruido). Plantilla de PR ✅ (punto 3).

### 16.11 Comando para habilitar branch protection (cuando el plan lo permita)
```
gh api -X PUT repos/netobarca/CLARIHR-backend/branches/master/protection --input - <<'JSON'
{"required_status_checks":{"strict":true,"contexts":["build-and-unit"]},"enforce_admins":true,"required_pull_request_reviews":{"required_approving_review_count":0},"restrictions":null,"allow_force_pushes":false,"allow_deletions":false,"required_conversation_resolution":true}
JSON
```

### 16.12 Enforcement vigente (plan free privado)
Mientras no haya branch protection server-side, "1 PR por finding / no push directo a `master`" es **disciplina documentada + señal CI**, no un gate bloqueante. Toda sesión Claude DEBE seguir §16.1–§16.6 igual; el CI corre en cada push/PR y reporta verde/rojo (revisarlo antes de mergear) pero GitHub no impide técnicamente un push directo. Riesgo asumido y registrado hasta el upgrade de plan.