# 30 · AuthController — Alineación canónica (2026-06-10)

| Campo | Valor |
|-------|-------|
| **Controlador** | `src/CLARIHR.Api/Controllers/AuthController.cs` (+ `SystemController.cs` como hermano de versionado) |
| **Tipo** | Superficie de **comandos de autenticación** (NO CRUD de entidad). 9 endpoints POST, anónimos salvo `logout`. |
| **Veredicto** | **APROBADO CON BRECHAS DE CONTRATO.** Núcleo seguro y cohesionado. 5 criterios ya cumplen, 6 no aplican por diseño (justificados), **3 brechas reales** — todas de la capa de contrato de API (versión, Swagger, tags). |
| **Migración** | **NINGUNA** — todos los cambios son a nivel de atributos de API; cero impacto de esquema. snake_case ya cumplía. |
| **Breaking** | ⚠️ **SÍ (ruta)** — `/api/auth/*` → `/api/v1/auth/*` y `/api/system/*` → `/api/v1/system/*`. Corte limpio (decisión del usuario; el frontend ajustará). |

---

## 1. Contexto y tesis

`AuthController` **no es un controlador CRUD de recurso** — es la superficie de **protocolo de autenticación**: `register`, `external`, `login`, `refresh`, `logout`, `password-reset/{request,validate,redeem}`, `company-user-invitations/accept`. Por eso ~la mitad de los 15 criterios canónicos (redactados para controladores de entidad CRUD) **no aplican por diseño**. Las entidades subyacentes (`User`, `RefreshToken`, `PasswordResetToken`) no se exponen como recursos REST; el controlador ejecuta transacciones de autenticación multi-agregado dentro de **un** bounded context.

---

## 2. Evaluación de los 15 criterios

| # | Criterio | Estado | Detalle |
|---|----------|--------|---------|
| 1 | Administra solo su entidad | ✅ Cumple | Cohesión en un bounded context (autenticación). Orquesta multi-agregado, pero no expone CRUD de `User`. |
| 2 | ConcurrencyToken en la entidad | ➖ No aplica | Sin superficie de edición granular; el modelo de concurrencia real es *token single-use + rotación + unique index* (ver §4). |
| 3 | Cada endpoint retorna su entidad | ✅ Cumple | Devuelve `AuthResponse` donde tiene sentido; 202/204 justificados por seguridad/semántica. |
| 4 | PATCH JSON Patch | ➖ No aplica | No existe recurso editable parcialmente. |
| 5 | IDs como `{Entidad}PublicId` | ✅ Cumple | No hay IDs de entidad en rutas; `UserDto.Id` = `User.PublicId`. |
| 6 | GET único que retorna el array | ➖ No aplica | No hay colección "auth" enumerable; exponerla sería fuga de identidades. |
| 7 | POST retorna creado + 201 | ✅ Cumple | `register`/`external` → 201; `login`/`refresh`/`accept` → 200 (correcto: no crean recurso). |
| 8 | Columnas snake_case | ✅ Cumple | Verificado: `auth_users`, `auth_refresh_tokens`, `auth_password_reset_tokens`, todas snake. |
| 9 | Ejecutar migración | ➖ No hay | Ningún ítem de la remediación toca el esquema. |
| 10 | Reemplazar PATCH de solo-estado | ➖ No aplica | No existe PATCH; logout/redeem son comandos de negocio (POST), no flips de estado. |
| 11 | Sin fallback en eliminaciones | ➖ No aplica | No hay DELETE; la revocación no es soft-delete-fallback (ver §4). |
| 12 | Evaluar delete vs soft-delete | ✅ Analizado | Revocación (soft) **obligatoria** por seguridad (ver §4). |
| 13 | Subrecursos / tags | ⚠️ **Brecha** | Sin `[Tags]`; el flujo *Password Reset* recibe su propio tag por-acción. **→ PR-A** |
| 14 | URL canónica `/v1/` | ❌ **Brecha** | `[Route("api/auth")]` sin versión. **→ PR-B (BREAKING)** |
| 15 | `[SwaggerOperation]` | ❌ **Brecha** | 0/9 endpoints documentados. **→ PR-A** |

---

## 3. Decisiones del usuario (2026-06-10)

1. **Versionado (criterio 14):** corte limpio a `v1`; el frontend ajustará. → `[Route("api/v1/auth")]` (estilo literal, espejando el frente canónico reciente docs 24–29: Audit/AccountCompany* — **sin** atributo `[ApiVersion]` ni token `v{version:apiVersion}`).
2. **Tags (criterio 13):** cohesión sin fragmentar. → **un** `AuthController`; clase con `[Tags("Authentication")]` + `[Tags("Password Reset")]` por-acción en los 3 endpoints de reset.
3. **`company-user-invitations/accept`:** se mantiene en Auth (anónimo, emite sesión; moverlo a `CompanyUsers` `[Authorize]` sería incorrecto). Queda bajo el tag `Authentication`.
4. **`SystemController`:** también pasa a `v1` → `[Route("api/v1/system")]` + `[Tags("System")]` + `[SwaggerOperation]`.

---

## 4. Justificación de las exclusiones (criterios 2, 4, 6, 10, 11, 12)

**Criterio 2 — ConcurrencyToken: deliberadamente NO se agrega.**
- No hay endpoint que haga *read-modify-write* granular sobre `User`/token donde un lost-update importe. El `xmin`/token de concurrencia en este código se expone al cliente vía **ETag/If-Match** para UIs de edición interactiva — y Auth no tiene esa superficie: el cliente porta secretos opacos (password, token), nunca un ETag de entidad.
- El control de concurrencia real ya existe y es más fuerte que `xmin`: `RefreshToken` → unique index en `token_hash` + rotación con `ReplacedByTokenHash` + guard idempotente en `Revoke()`. `PasswordResetToken` → flag `IsUsed` single-use + `MarkUsed` idempotente + unique `token_hash`. El token **es** el mecanismo de serialización.
- Evidencia de intención: la migración `AddConcurrencyTokensToPatchEntities` agregó tokens **solo a entidades PATCH-ables**; las 3 entidades de Auth fueron excluidas a propósito.

**Criterio 12 — Delete vs soft-delete: la revocación (soft) es obligatoria; hard delete sería regresión de seguridad.**
- `RefreshToken`: conservar filas revocadas/rotadas es **requisito** para *refresh-token reuse detection* (cadena `ReplacedByTokenHash` + `RevocationReason`, patrón OWASP). Hard-delete en logout/rotación destruiría la detección de robo+replay.
- `PasswordResetToken`: conservar filas usadas/revocadas previene *replay* y da rastro de auditoría. La purga de tokens expirados es un *background job* (otro contexto), no un DELETE REST.

**Criterios 4, 6, 10, 11 — No aplican (sin recurso CRUD):** no hay recurso parcial-editable (4); no hay colección enumerable y exponerla sería fuga de identidades (6); no existe PATCH viejo que reemplazar (10); no hay DELETE — la revocación es estado de seguridad persistente, no un fallback de borrado (11).

**Nota de producto (no brecha):** `password-reset/redeem` devuelve 204 sin auto-login. Forzar re-login tras reset es una postura de seguridad válida; auto-login sería decisión de producto, no de canon.

---

## 5. Plan de remediación

### PR-A — Documentación + Tags (criterios 15 y 13) · NO breaking
1. `[SwaggerOperation(Summary, Description)]` en los 9 endpoints de `AuthController` (+ `GetStatus` de `SystemController`).
2. `[Tags("Authentication")]` a nivel de clase + `[Tags("Password Reset")]` por-acción en `RequestPasswordReset`/`ValidatePasswordReset`/`RedeemPasswordReset`. `[Tags("System")]` en `SystemController`.
3. Comentario de cabecera estilo-canónico (modelo de authz anónimo, exclusión de `[AuthorizationPolicySet]`/`GovernedFamilyRegex` por diseño, enrolamiento OpenAPI, racional de inline-`ProducesResponseType`).
4. Enrolar en `OpenApiContractGuardrailsTests.Families`: `^Auth` → `Authentication`, `^System` → `System` (verificado sin colisión: `AccountCompanyAuthorization` empieza con "Account").

> **Decisión deliberada (documentada):** NO se migra a `[ProducesStandardErrors]`. Los sets estándar (`Query`/`Command`) inyectan 403/404 que estos endpoints anónimos nunca devuelven y omiten 202/429/422/500 que sí usan. Los `[ProducesResponseType<ProblemDetails>]` inline actuales son **más precisos** para la superficie anónima. El guardrail no exige `[ProducesStandardErrors]`.

### PR-B — Versionado de ruta (criterio 14) · ⚠️ BREAKING
5. `[Route("api/auth")]` → `[Route("api/v1/auth")]`; `[Route("api/system")]` → `[Route("api/v1/system")]`.
6. Actualizar refs de ruta en tests: `ApiIntegrationTests` (14), `PlatformAuthenticationIntegrationTests` (7 de `/api/auth/*`; los `/api/platform/auth/*` NO cambian), `AuthRegistrationSecurityTests` (1), `SecurityHeadersMiddlewareTests` (1). `api/system` no tiene refs en tests.
7. `openapi.yaml`: regenerar sección `/api/auth` + `/api/system` desde swagger live (per-sección, no-bloqueante — ver §7).

**Impacto en migración (criterio 9): ninguno.**

---

## 6. Verificación ✅ (2026-06-10)

- **build:** `dotnet build -c Release` → **0 warnings / 0 errors**.
- **unit:** `CLARIHR.Application.UnitTests` → **1795 / 1795** (incl. `OpenApiContractGuardrailsTests` **84/84** con las 2 familias nuevas `^Auth`→`Authentication` y `^System`→`System`; sin colisión de regex).
- **integración:** `ApiIntegrationTests` + `PlatformAuthenticationIntegrationTests` + `AuthRegistrationSecurityTests` (contra `clarihr-postgres:5433`) → **319 passed / 0 failed / 23 skipped** (skips IAM preexistentes, no relacionados). Valida el ruteo `/api/v1/auth/*` extremo-a-extremo (register/login/logout/refresh) y la separación core-vs-platform auth.
- **migración:** ninguna (sin cambio de esquema). Confirmado.

### Archivos tocados
- `src/CLARIHR.Api/Controllers/AuthController.cs` — `[Route("api/v1/auth")]`, `[Tags("Authentication")]` + `[Tags("Password Reset")]` ×3, `[SwaggerOperation]` ×9, cabecera de racional. Inline `[ProducesResponseType<ProblemDetails>]` conservado (deliberado).
- `src/CLARIHR.Api/Controllers/SystemController.cs` — `[Route("api/v1/system")]`, `[Tags("System")]`, `[SwaggerOperation]`.
- `tests/CLARIHR.Application.UnitTests/OpenApiContractGuardrailsTests.cs` — +2 familias.
- `tests/**` (4 archivos) — 23 refs `/api/auth/*` → `/api/v1/auth/*` (los `/api/platform/auth/*` intactos).
- `docs/technical/api/openapi.yaml` — 10 paths re-versionados + tags + summary/description (per-sección; YAML válido, 239 paths intactos, $refs OK).
- `docs/technical/api/endpoint-reference.md` — 32 refs de ruta re-versionadas.

---

## 7. Pendientes

- **Comunicar al frontend** la breaking de ruta (`/api/auth/*` y `/api/system/*` → `/api/v1/...`). Corte limpio (sin doble-ruta), por decisión del usuario.
- **Commit** (el usuario maneja commits/merges salvo delegación explícita).
