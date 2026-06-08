# Auditoría Técnica por Controlador — UserPreferencesController

> Nivel: **Controller** (controlador + su vertical directa). No certifica readiness productivo completo de la API.
> Fecha: 2026-06-07 · Rama: `master` · Auditor: Claude (skill `technical-audit-per-controller`)
> Contexto: alineado en "Ola 2" (commit `5d54281`); ver [[ola2-alignment-progress]].

## 1. Resumen ejecutivo

`UserPreferencesController` administra las **preferencias propias del usuario autenticado** (`/account/me/preferences`): idioma y enlaces sociales. Es un recurso **self-scoped** (resuelto desde el JWT, no por tenant ni por id en la ruta) con un singleton auto-provisionado en el primer acceso. 4 endpoints (GET, PUT idioma, PUT social-links, PATCH).

Veredicto: **APROBADO CON OBSERVACIONES** — y es el **controlador más limpio de la ola** (Location*/OrgUnits/OrgStructureCatalogs/PositionSlots/ReportExportJobs/SalaryTabulator/UserPreferences). 0 críticos / 0 altos / 0 medios / 0 bajos: **solo 3 observaciones triviales**. Totalmente canónico (`[ApiVersion]`/`[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]` con sets *scoped*/If-Match obligatorio/JSON-Patch hardened), **sin IDOR por construcción** (no hay userId en ruta ni body — todo se resuelve del JWT), validación en triple capa (validator+applier+dominio), URLs **https-absolutas** (anti-XSS/anti-esquemas peligrosos), y la ausencia de `[AuthorizationPolicySet]` es **por diseño documentado** (self-scoped, authn-only, 403/404 inalcanzables). Cobertura de pruebas sólida (76 unit incl. patch-applier + ~11 integración).

| Indicador | Resultado |
|---|---|
| Build (Release) | ✅ compila |
| Unit tests (UserPreference + guardrails, **ejecutados**) | ✅ 76/76 passed |
| Integration tests (revisados, no ejecutados) | ~11 (auto-provision, PUT/PATCH valid/no-If-Match/stale, social-links, GET) |
| Enrolamiento en guardrails de familia | concurrency ✓ (auto); authz ✗ (by-design); OpenAPI ✗ (canónico pero no enrolado) |
| Hallazgos | 0 Crít · 0 Alto · 0 Media · 0 Baja · **3 Observación** |

## 2. Alcance

**Incluido:** controlador `UserPreferencesController.cs`; aplicación `UserPreferenceAdministration.cs` (DTOs, 4 commands/queries, validators, 4 handlers, `UserPreferencePatchApplier`, helpers, mapper), `PreferenceCommon.cs` (errores); dominio `UserPreference.cs`, `UserSocialLink.cs`, normalization; persistencia `IUserPreferenceRepository` + `UserPreferenceRepository`, EF `UserPreferenceConfiguration`; dependencias directas `ICurrentUserService`, `IUserRepository`; pruebas (`UserPreferencePatchApplierTests` + integración).

**Excluido:** `CompanyPreferences` (controlador hermano, tenant-scoped, auditoría aparte); el subsistema de identidad/`User` salvo la resolución del usuario actual; auditoría integral; carga.

## 3. Metodología

Revisión estática de cada endpoint hasta SQL, con foco en el riesgo característico de un recurso "me" (**IDOR/self-scoping**), concurrencia (If-Match + auto-provisión), validación de entrada (idioma, URLs sociales) y adherencia canónica. Evidencia: 76 unit tests ejecutados (verde). Integración revisada por código (requiere DB; no ejecutada → limitación). **Nota:** edición en curso no relacionada en Locations rompió el build transitoriamente; estable al ejecutar.

## 4. Inventario de endpoints

| # | Método | Ruta | Propósito | Concurrencia |
|---|---|---|---|---|
| 1 | GET | `/account/me/preferences` | Leer preferencias propias (auto-provisiona) | — (token en body para el siguiente If-Match) |
| 2 | PUT | `/account/me/preferences` | Reemplazar idioma | **If-Match** + ETag |
| 3 | PUT | `/account/me/preferences/social-links` | Reemplazar enlaces sociales (max 10, https) | **If-Match** + ETag |
| 4 | PATCH | `/account/me/preferences` | Patch de `/language` (JSON Patch RFC-6902) | **If-Match** + ETag |

Todo self-scoped (sin userId en ruta/body). Auto-provisión del singleton en el primer acceso (If-Match ignorado sólo en esa rama, documentado).

## 5. Checklist de auditoría

| Categoría | Control | Estado | Evidencia |
|---|---|---|---|
| Arquitectura | Controller delgado / DTOs | PASS | Sólo despacha CQRS |
| Arquitectura | Aggregate design | PASS | `UserPreference` con `SocialLinks` por field-access + cascade; sin token en el hijo |
| Arquitectura | Transacciones / consistencia | PASS | Singleton por usuario; cambios atómicos (1 SaveChanges) |
| Seguridad | Autenticación | PASS | `[Authorize]` |
| Seguridad | **IDOR / self-scoping** | PASS | Sin userId en ruta/body; `ResolveCurrentUserAsync` del JWT → `GetByUserIdAsync`; imposible acceder a otro usuario |
| Seguridad | `[AuthorizationPolicySet]` | NO APLICA | Self-scoped + authn-only = **por diseño documentado** (comentario de cabecera) |
| Seguridad | Validación de entrada | PASS | Idioma `^[A-Za-z]{2,3}$` (validator+applier+dominio); provider code regex+≤50; URL |
| Seguridad | URL de enlaces sociales | PASS | **https absoluta obligatoria** (`Uri.TryCreate Absolute`+scheme https) → sin `javascript:`/`http:`/SSRF (no se fetchea server-side) |
| Seguridad | Caps de colección | PASS | Max 10 social links + provider codes únicos (validator + dominio) |
| Seguridad | DoS JSON Patch | PASS | `[RequestSizeLimit(64KB)]` + tope 50 ops; patch sólo `/language` |
| Seguridad | Mass assignment | PASS | DTOs cerrados; patch rechaza id/publicId/token/createdAt/modifiedAt/socialLinks |
| Contrato | Versionado / Tags / Swagger | PASS | `[ApiVersion]`+`[Route(v{version})]`+`[Tags("User Preferences")]`+`[SwaggerOperation]`×4 |
| Contrato | Error contract (sets scoped) | PASS | `[ProducesStandardErrors]` con sets precisos (GET=Unauthorized; writes=BadRequest\|Unauthorized\|Conflict — omite 403/404 inalcanzables) |
| Contrato | OpenAPI guardrail | OBS | **UP-C**: no enrolado en `Families` pese a ser totalmente canónico |
| Contrato | If-Match / ETag en updates | PASS | `[FromIfMatch]`+`ToActionResultWithETag` en los 3 writes (400 si falta, 409 stale) |
| Rendimiento | Índices | PASS | unique `(user_id)` + unique `(public_id)`; social links FK cascade |
| Rendimiento | N+1 / eager-load | PASS | `GetByUserIdAsync` `.Include(SocialLinks)` (sin N+1) |
| Rendimiento | Queries por request | OBS | **UP-B**: 2 (user lookup publicId→id + preference) |
| Rendimiento | Rate limit | NO APLICA | Self-scoped, sin search/export; perfil no abusable |
| Concurrencia | Optimista + If-Match + 409 | PASS | Token + `.IsConcurrencyToken()` (auto-guardrail); check manual→409; first-write ignora If-Match (documentado) |
| Concurrencia | Auto-provisión concurrente | OBS | **UP-A**: race en primera escritura → unique `(user_id)` → 500 (no atrapado; muy raro) |
| Observabilidad | Audit logs | NO APLICA | Preferencias self-service de bajo riesgo; no se auditan (por diseño) |
| Pruebas | Unit (patch applier) | PASS | `UserPreferencePatchApplierTests`; 76 verdes |
| Pruebas | Integración | PASS | ~11 (auto-provision, PUT/PATCH valid/no-If-Match/stale, social-links, socialLinks-patch-rejected→400) |
| Build | Compilación limpia | PASS | 0/0 |

## 6. Análisis técnico

### 6.1 Arquitectura
CQRS minimalista y correcta. Aggregate `UserPreference` (extiende `AuditableEntity`, **no** `TenantEntity` — las preferencias son del usuario, no del tenant) con `SocialLinks` encapsulados (field-access, cascade-delete). Helper `ResolveCurrentUserAsync` único reutilizado por los 4 handlers. Mapper ordena social links por `SortOrder`.

### 6.2 Seguridad — self-scoping correcto (riesgo central)
**Sin IDOR por construcción**: ningún endpoint recibe un userId (ni en ruta ni en body); todos resuelven el usuario desde `currentUserService.UserId` (JWT) y consultan `GetByUserIdAsync(internalId)`. Un usuario sólo puede leer/escribir SUS preferencias. La ausencia de `[AuthorizationPolicySet]`/RBAC es **correcta y documentada**: un recurso "me" sólo necesita autenticación. **URLs https-absolutas** (rechaza `http:`, `javascript:`, relativas) — protege al frontend de XSS/mixed-content por enlaces sociales almacenados; no hay SSRF porque el servidor nunca fetchea la URL. Validación en triple capa (FluentValidation + patch applier + invariantes de dominio) con caps (≤10 links, providers únicos, longitudes). JSON-Patch acotado a `/language`.

### 6.3 Contrato API
Totalmente canónico, con un detalle de calidad superior: `[ProducesStandardErrors]` usa **sets de error scoped** (sólo `Unauthorized` en GET; `BadRequest|Unauthorized|Conflict` en writes) porque 403/404 son inalcanzables para un singleton self-scoped auto-provisionado — más preciso que los controllers que listan todo. Única deuda: **no está enrolado en `OpenApiContractGuardrailsTests.Families`** (UP-C) pese a cumplir todos los requisitos; enrolarlo sería drift-proofing gratuito.

### 6.4 Rendimiento
Índices únicos en `user_id` y `public_id`. `GetByUserIdAsync` hace eager-load de social links (sin N+1). Cada request hace 2 queries (UP-B: resolver `User` por publicId para mapear a internal id + la preferencia); negligible para un endpoint "me", evitable sólo si la plataforma expusiera el internal id en `ICurrentUserService`. Sin search/export → rate-limit N/A.

### 6.5 Concurrencia y consistencia
If-Match obligatorio en los 3 writes; token rotado en cada mutación; `.IsConcurrencyToken()` (auto-guardrail, verificado verde); check manual→409. La **primera escritura auto-provisiona** e ignora intencionalmente el If-Match (no hay token previo) — documentado en los 3 handlers. **UP-A**: dos primeras escrituras concurrentes (o GET+write) para un usuario sin preferencia podrían colisionar en el unique `(user_id)` → `UniqueConstraintViolationException` no atrapada → 500; ventana minúscula (sólo primer acceso de un usuario nuevo).

### 6.6 Observabilidad
Sin audit logs — **apropiado** para preferencias self-service de bajo riesgo (idioma/enlaces propios); no es un dato regulado como salarios u org. No es un hallazgo.

### 6.7 Pruebas
**Ejecutadas: 76 unit tests verdes** (incl. `UserPreferencePatchApplierTests`: allow-list `/language`, rechazo de token/id/socialLinks, formato). **Revisadas: ~11 integration tests** cubriendo auto-provisión, PUT idioma (valid/sin-If-Match→400/stale→409), PUT social-links (valid + validación), PATCH (valid/stale/`/socialLinks`-rechazado→400), GET. Cobertura sólida; el self-scoping (sin IDOR) es estructural (no requiere test). 

### 6.8 Build / DevSecOps
Compila; sin secretos; localización vía resx.

## 7. Hallazgos

### UP-A — Race de auto-provisión concurrente → 500
**Severidad:** Observación · **Categoría:** Concurrencia · **Ubicación:** los 4 handlers (rama `preference is null` → `Create`+`Add`); EF unique `uq_user_preferences__user_id`.
**Condición:** dos primeras operaciones concurrentes (p.ej. GET y PUT, o dos PUT) para un usuario sin preferencia ejecutan ambas el `Create` → la segunda `SaveChanges` viola el unique `(user_id)` → `UniqueConstraintViolationException` no atrapada → 500.
**Impacto:** mínimo — sólo en el **primer** acceso de un usuario nuevo, en una ventana de carrera diminuta; tras la provisión nunca vuelve a ocurrir. Sin corrupción (el unique protege la unicidad).
**Recomendación:** en la rama de provisión, capturar `UniqueConstraintViolationException` y re-leer la preferencia recién creada por el otro request (o tratarlo como `ConcurrencyConflict` para que el cliente reintente).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

### UP-B — Dos queries por request (lookup de usuario + preferencia)
**Severidad:** Observación · **Categoría:** Rendimiento · **Ubicación:** `ResolveCurrentUserAsync` (`GetByPublicIdAsync`) + `GetByUserIdAsync`.
**Condición:** cada request resuelve el `User` por publicId (para mapear a internal id) y luego la preferencia por internal id.
**Impacto:** negligible para un endpoint "me".
**Recomendación:** si la plataforma expusiera el internal user id en `ICurrentUserService`, se ahorraría el primer query (cambio transversal, no local).
**Prioridad:** Baja · **Esfuerzo:** Bajo (plataforma) · **Estado:** Abierto

### UP-C — No enrolado en el OpenAPI guardrail pese a ser canónico
**Severidad:** Observación · **Categoría:** Contrato/Gobernanza · **Ubicación:** `OpenApiContractGuardrailsTests.Families`.
**Condición:** el controlador cumple `[Tags]`/`[SwaggerOperation]`/`[ProducesStandardErrors]` pero no está en la tabla `Families` → su contrato no está protegido contra drift por CI.
**Impacto:** un futuro cambio que rompa la canonicidad no fallaría en CI.
**Recomendación:** añadir `^UserPreferences`→"User Preferences" a `Families` (enrolamiento gratuito; ya pasa los invariantes).
**Prioridad:** Baja · **Esfuerzo:** Bajo · **Estado:** Abierto

## 8. Hallazgos fuera de alcance / trazabilidad

- **Decisión por-diseño:** sin `[AuthorizationPolicySet]`/RBAC (self-scoped, authn-only) — documentado en el comentario de cabecera del controlador. **No re-flaggear.**
- **Sin audit logs** es intencional (preferencias self-service de bajo riesgo) — no aplicar el patrón de auditoría de SalaryTabulator/OrgUnits aquí.
- **Rate-limiting N/A** (sin search/export; perfil no abusable).
- **`CompanyPreferences`** (hermano tenant-scoped) comparte el patrón If-Match/PATCH pero es una vertical separada; auditar aparte si se requiere.

## 9. Matriz de priorización

| ID | Severidad | Categoría | Hallazgo | Esfuerzo | Prioridad | Acción |
|---|---|---|---|---|---|---|
| UP-A | Obs | Concurrencia | Race de auto-provisión → 500 | Bajo | Baja | Catch `UniqueConstraintViolationException` + re-read |
| UP-B | Obs | Rendimiento | 2 queries/request | Bajo | Baja | Exponer internal id en plataforma (opcional) |
| UP-C | Obs | Contrato | No enrolado en OpenAPI guardrail | Bajo | Baja | Añadir a `Families` |

## 10. Veredicto del controlador

| Nivel evaluado | Resultado |
|---|---|
| Controller auditado (`UserPreferencesController`) | **Aprobado con observaciones** (el más limpio de la ola; sólo 3 observaciones triviales) |
| Seguridad (self-scoping / IDOR / validación / URL) | Aprobado |
| Arquitectura | Aprobado |
| Contrato | Aprobado (canónico; UP-C = enrolamiento pendiente) |
| Performance | Aprobado (UP-B negligible) |
| Concurrencia | Aprobado (UP-A = edge raro) |
| Pruebas | Aprobado (76 unit + ~11 integración) |
| Readiness productivo completo | No certificado (fuera de alcance de auditoría por controlador) |

**Controlador maduro, seguro y canónico. Puede avanzar a QA sin reservas;** los 3 hallazgos son observaciones no bloqueantes (mejoras opcionales). Esencialmente un "Aprobado" limpio.

## 11. Recomendaciones finales

1. **UP-A:** capturar el unique-violation de la auto-provisión y re-leer (robustez del primer acceso concurrente).
2. **UP-C:** enrolar en `OpenApiContractGuardrailsTests.Families` (drift-proofing gratuito).
3. **UP-B:** opcional/plataforma.
4. Mantener las fortalezas (modelo de referencia para recursos "me"): self-scoping sin IDOR, URLs https-only, validación triple-capa, If-Match obligatorio, `[ProducesStandardErrors]` con sets scoped, JSON-Patch hardened.

## 12. Anexos / Evidencia revisada

- Controller: `UserPreferencesController.cs` (4 endpoints + DTOs).
- Aplicación: `UserPreferenceAdministration.cs` (DTOs/validators/4 handlers + `UserPreferencePatchApplier` + helpers + mapper), `PreferenceCommon.cs`.
- Dominio: `UserPreference.cs`, `UserSocialLink.cs`, normalization.
- Persistencia: `IUserPreferenceRepository.cs`, `UserPreferenceRepository.cs`, `UserPreferenceConfiguration.cs`.
- Dependencias: `ICurrentUserService`, `IUserRepository` (resolución del usuario actual).
- Pruebas: `UserPreferencePatchApplierTests.cs`, `ApiIntegrationTests.cs` (~11 `UserPreferences_*` métodos, líneas ≈1534-1798).
- Ejecución: `dotnet test --filter ~UserPreference|~ConcurrencyTokenMappingGuardrails|~OpenApiContractGuardrails` → **76/76 passed** (sesión 2026-06-07).
