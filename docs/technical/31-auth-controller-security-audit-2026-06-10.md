# 31 · AuthController — Auditoría de seguridad (2026-06-10)

| Campo | Valor |
|-------|-------|
| **Controlador** | `src/CLARIHR.Api/Controllers/AuthController.cs` (superficie de autenticación) + delegados en `Application/Features/Auth/*`, `Infrastructure/Auth/*` |
| **Veredicto** | **APROBADO CON OBSERVACIONES · 0 crítico / 0 alto.** El núcleo criptográfico, de emisión/rotación de tokens, de validación JWT y de aislamiento es **SÓLIDO**. Las observaciones son de defensa-en-profundidad, observabilidad/cumplimiento y un vector de *pre-account-hijacking*. |
| **Hallazgos** | 4 × 🟠 MEDIA (AU-1..AU-4) · 6 × 🟡 BAJA (AU-5..AU-10). Ninguno es un quiebre de control de acceso, IDOR, forja de token ni fuga cross-tenant. |
| **Estado** | **PARCIAL (2026-06-10):** AU-1 + AU-2 + AU-5 ✅ **REMEDIADOS** (uncommitted). AU-6 + AU-7 **reclasificados** a decisión (§3.1). AU-3/AU-4/AU-8/AU-10 → decisiones §5 pendientes. **AU-1 = feature de verificación de email (§3.2), BREAKING en `register`, +1 migración.** |
| **Relación** | Complementa la alineación canónica [doc 30](30-auth-controller-canonical-alignment-2026-06-10.md). |

---

## 1. Alcance y metodología

Auditados los 9 endpoints de `AuthController` (register, external, login, refresh, logout, password-reset ×3, invitation-accept) y toda su vertical: handlers (`Application/Features/Auth`), entidades (`Domain/Auth`), y la infraestructura delegada (`BuiltInPasswordHasher`, `JwtTokenService`, `GoogleIdTokenValidator`, `RefreshTokenHasher`, repositorios de tokens, `Program.cs` auth+rate-limit+headers). Se revisó: fuerza de autenticación, gestión y rotación de tokens, validación de id-token externo, rate-limiting, enumeración de cuentas, side-channels de timing, condiciones de carrera, aislamiento (IDOR/cross-tenant) y cobertura de auditoría.

---

## 2. Núcleo SEGURO — confirmado, **no re-flag**

Verificado con cita directa; estas áreas son correctas y **no deben re-levantarse** en futuras auditorías salvo cambio de código:

- **Hashing de contraseña:** `BuiltInPasswordHasher` → ASP.NET Identity `PasswordHasher` = **PBKDF2-HMAC-SHA256** (V3, salt automático). `Verify` es **constant-time** (`CryptographicOperations.FixedTimeEquals` interno). No hay comparación byte a byte propia.
- **Entropía de tokens:** todos los secretos vía **CSPRNG** (`RandomNumberGenerator`): refresh **512 bits** (`JwtTokenService.cs:314`), reset/invitación **384 bits**. Cero `Guid`/`System.Random` para secretos. At-rest = SHA-256 sin sal (apropiado para tokens de alta entropía).
- **Rotación + reuse-detection de refresh:** el replay de un token rotado revoca **toda la familia** (`RevokeFamilyAsync` por `FamilyId`) — patrón OAuth de detección de robo correcto (`JwtTokenService.cs:142-159`).
- **Validación JWT (bearer):** `ValidateIssuer/Audience/IssuerSigningKey/Lifetime = true`, `ClockSkew = Zero`, `RequireHttpsMetadata = true`; **fail-closed** si la clave no está configurada (la API no arranca; `Program.cs:578-582`). `appsettings.json` base envía clave vacía.
- **id-token externo (Google) NO forjable:** `GoogleIdTokenValidator.cs:45-59` hace validación criptográfica real contra el **JWKS de Google** (well-known sobre HTTPS) con `iss`/`aud`(=ClientId)/`exp`/firma. Falla cerrado si `ClientId` no está configurado. Un token forjado o de otra audiencia es rechazado.
- **Flujo de invitación:** token 384-bit, single-use (`MarkUsed`), expiry 72 h, y cadena de guardas (token activo → user local → membership existe → **company Active**, AC-1) antes de acuñar sesión, todo atómico (`AcceptCompanyUserInvitationCommand.cs:65-127`).
- **Política de contraseña:** 12–100 chars, mayúscula/minúscula/dígito/especial, sin nombre/email (`AuthValidationRules.cs`).
- **Aislamiento:** sin `IgnoreQueryFilters` en la ruta de tokens; sin path de fuga cross-user; logout exige `[Authorize]`.
- **Anti-enumeración parcial:** login responde **401 uniforme** y password-reset/request responde **202 uniforme** (no revela existencia). (Ver AU-1/AU-5 por las fugas que sí quedan.)

---

## 3. Hallazgos

| ID | Sev | Título | Recomendación |
|----|-----|--------|---------------|
| **AU-1** | 🟠 MEDIA | Registro local sin verificación de email + auto-link por email verificado ⇒ **enumeración (409)** y **pre-account-hijacking** | ✅ **REMEDIADO** (§3.2): verificación de email (Opción A); residual sutil documentado |
| **AU-2** | 🟠 MEDIA | Sin rate-limit en `refresh`, `password-reset/validate`, `password-reset/redeem` | ✅ **REMEDIADO** (§3.1): 2 políticas nuevas + guardrail anti-drift |
| **AU-3** | 🟠 MEDIA | Sin auditoría de eventos de autenticación (login ok/fallo, logout, register, external) | Definir eventos auth + auditar; clave para detección y cumplimiento |
| **AU-4** | 🟠 MEDIA | Sin account-lockout / throttle por-cuenta (única defensa = rate-limit por-IP 5/min) | Throttle por-cuenta (partición por email) o backoff de intentos fallidos |
| **AU-5** | 🟡 BAJA | Timing side-channel en login (`user is null` corta antes de `Verify`) | ✅ **REMEDIADO** (§3.1): dummy-verify de timing constante + test |
| **AU-6** | 🟡 BAJA | Logout revoca solo tokens `Core`; sesión `Platform` sobrevive | 🔁 **Reclasificado a decisión** (§3.1): no es fix mecánico |
| **AU-7** | 🟡 BAJA | Reuse-detection solo en replay de rotación (no de token revocado por logout/reset); solo `LogWarning`, sin auditoría | 🔁 **Reclasificado** (§3.1): mitad mootada, mitad se pliega a AU-3 |
| **AU-8** | 🟡 BAJA | TOCTOU en rotación de refresh (sin lock/unique-constraint) | Lock por familia (`pg_advisory_xact_lock`, espejo PositionSlots RA-1) o índice único |
| **AU-9** | 🟡 BAJA | PII (email, nombre) en claims del JWT firmado | Aceptar por diseño (TTL 15 min + HTTPS) o minimizar claims |
| **AU-10** | 🟡 BAJA | Hardening de plataforma (HSTS/CSP/X-Frame-Options ausentes; sin `ValidAlgorithms` pin; clave dev commiteada; `external` comparte bucket `auth-register`) | Mayormente fuera del controlador; HSTS + pin de algoritmos |

### Detalle

**AU-1 🟠 — Registro local sin verificación de email + auto-link por email verificado.**
`RegisterUserCommandHandler` crea un usuario `Active` de inmediato sin verificar la propiedad del email, y devuelve **409 `user_already_exists`** si el email ya existe (`RegisterUserCommand.cs:83-88`) → **enumeración** del padrón de usuarios. Peor: `RegisterExternalUserCommandHandler` auto-linkea una identidad Google con email **verificado** hacia una cuenta **local preexistente** del mismo email (`RegisterExternalUserCommand.cs:85,110-114`). Como el registro local NO verifica email, un atacante puede **pre-sembrar** una cuenta local con el email de la víctima; cuando la víctima entre con "Sign in with Google", se la vincula silenciosamente a la cuenta del atacante (que conserva la contraseña) → *pre-account-hijacking*. Mitigantes: requiere conocer el email y que la víctima sea usuario nuevo vía Google. Fix estructural: verificación de email en registro local (o bloquear auto-link hacia cuentas locales sin email verificado).

**AU-2 🟠 — Endpoints anónimos sin rate-limit.** `password-reset/validate`, `password-reset/redeem` y `refresh` no llevan `[EnableRateLimiting]` (confirmado en el controlador). Aunque la entropía de 384/512 bits hace inviable el guessing, son superficies anónimas que ejecutan trabajo de DB + cripto sin throttle → abuso/DoS y brecha de defensa-en-profundidad. No hay limitador global (`Program.cs:145`): lo no anotado queda sin límite.

**AU-3 🟠 — Sin auditoría de autenticación.** Solo password-reset e invitation-accept auditan. **Login (éxito y fallo), logout, register y external NO auditan**, y el catálogo (`AuditCatalog.cs`) no define ningún evento `LOGIN/LOGOUT/AUTHENTICATION/SESSION`. No hay telemetría de fuerza bruta/credential-stuffing ni rastro para respuesta a incidentes — relevante para SOC2/ISO27001 en un SaaS de RRHH con PII.

**AU-4 🟠 — Sin account-lockout.** `User` no tiene contador de fallos ni lockout. Única defensa = `auth-login` rate-limit **particionado por IP** (5/min). No frena credential-stuffing distribuido (muchas IPs) ni low-and-slow (<5/min/IP contra una cuenta). Nota: lockout duro tiene su propio trade-off (DoS de la víctima); preferible throttle por-cuenta + AU-3 para detección.

**AU-5 🟡 — Timing oracle en login.** `LoginCommand.cs:42-45`: la cadena `user is null || Status!=Active || provider!=Local || !Verify(...)` cortocircuita antes de `Verify` cuando el user no existe/inelegible → las cuentas locales activas tardan un PBKDF2 más → oráculo de enumeración que socava el 401 uniforme. Fix: ejecutar un `Verify` dummy contra un hash fijo.

**AU-6 🟡 — Logout parcial.** `LogoutCommand.cs:32` revoca solo `AuthClientType.Core`; una sesión `Platform` del mismo usuario sobrevive al logout. (Redeem sí revoca ambos.)

**AU-7 🟡 — Reuse-detection estrecha + sin auditoría.** `HasBeenRotated` exige `ReplacedByTokenHash` (solo lo pone la rotación). El replay de un token revocado por **logout/reset** se rechaza (401) pero no dispara `RevokeFamilyAsync` ni señal de robo; y cuando sí dispara, solo emite `LogWarning` (sin registro de auditoría).

**AU-8 🟡 — TOCTOU de rotación.** `RefreshAsync` lee (`GetByTokenHashAsync`) y luego escribe sin row-lock/advisory-lock/índice único; dos refresh concurrentes con el mismo token válido pueden pasar ambos `IsActive` y acuñar dos miembros de familia. Impacto: dos sesiones válidas desde un refresh, no acceso cross-user. Espejo de fix: `pg_advisory_xact_lock` (PositionSlots RA-1).

**AU-9 🟡 — PII en JWT.** `email` (×2), `given_name`, `family_name` en cada access-token (`JwtTokenService.cs:238-241`). JWT firmado, no cifrado. Trade-off estándar; mitigado por TTL 15 min + HTTPS. Probable aceptar por diseño.

**AU-10 🟡 — Hardening de plataforma (mayormente fuera del controlador).** `SecurityHeadersMiddleware` pone `X-Content-Type-Options`/`Referrer-Policy`/`Cache-Control` pero **no** HSTS/CSP/X-Frame-Options/Permissions-Policy; sin `UseHsts()`. `TokenValidationParameters` no fija `ValidAlgorithms` (mitigado por clave simétrica + `RequireSignedTokens`). Clave dev HS256 commiteada en `appsettings.Development.json` (solo Development; prod fail-closed). `external` usa el bucket `auth-register` (no uno de login). Sin CORS configurado (la core API no setea `Access-Control-Allow-Origin`).

---

## 3.1 Remediación de quick-wins (2026-06-10, uncommitted)

**AU-2 ✅ — Rate-limiting de la superficie anónima.** Nuevo `AuthRateLimitPolicies` (single-source de los 6 nombres de política, mirror de los `*RateLimitPolicies` por-feature) usado por `Program.cs`, `AuthController` y el guardrail. 2 políticas nuevas (fixed-window por IP): `auth-password-reset-submit` (5/min — `validate` + `redeem`) y `auth-refresh` (60/min — deliberadamente generosa para no recortar NAT compartido; el anti-abuso real del refresh es el token 512-bit + rotación/reuse-detection). Las 4 políticas inline previas se migraron a las constantes. **Guardrail anti-drift** `AuthRateLimitingGovernanceTests`: todo endpoint `[AllowAnonymous]` de `AuthController` debe declarar `[EnableRateLimiting]` con una política canónica (identificado por atributo → sobrevive renames) → un endpoint anónimo nuevo sin límite rompe CI. Cobertura: los 8 anónimos; `logout` (`[Authorize]`) fuera de alcance.

**AU-5 ✅ — Timing-equalization en login.** `LoginCommandHandler` ahora ejecuta **siempre** una verificación PBKDF2: una cuenta no elegible (inexistente/inactiva/externa/sin password) se verifica contra un hash dummy fijo (calculado una vez vía el hasher inyectado, cacheado en estático), de modo que el costo cripto — y el tiempo de respuesta — es idéntico en todo path. Test de regresión `Handle_WhenEmailIsUnknown_ShouldStillRunPasswordVerificationToEqualizeTiming`.

**AU-6 🔁 — Reclasificado a decisión (NO es fix mecánico).** La core API solo recibe llamadas `client_type=Core` (lo exige la `FallbackPolicy`) e `ICurrentUserService` no expone `client_type`. El logout ya revoca **todos** los refresh tokens Core del usuario (todos sus dispositivos core). Revocar también Platform haría que el logout del core matara la sesión de backoffice → cambio de comportamiento/menor-sorpresa, no un fix mecánico. Decisión de producto: ¿logout por-cliente (actual, defendible) vs. global? (Reset sí revoca ambos porque un cambio de credencial amerita matar todo.)

**AU-7 🔁 — Reclasificado.** (a) Ampliar la revocación de familia a replays de tokens revocados por logout/reset es **moot**: logout/reset ya revocan **toda** la familia (en una familia solo hay un token activo a la vez) → no hay nada extra que revocar. (b) La parte con valor real — **registro de auditoría** del reuse-detected (hoy solo `LogWarning`) — depende del catálogo de eventos de auth de **AU-3**. Por eso AU-7 se **pliega a AU-3** (§5).

### Verificación
build 0/0 · unit **1798/1798** (incl. `AuthRateLimitingGovernanceTests` 2/2 + test AU-5) · integ auth (`ApiIntegrationTests`/`PlatformAuthenticationIntegrationTests`/`AuthRegistrationSecurityTests`) **319/0/23skip** contra `clarihr-postgres:5433` (mismo baseline pre-cambio → 0 regresión; valida que las 2 políticas nuevas están registradas y que login funciona con el timing-equalization). **Sin migración.**

### Archivos
- `src/CLARIHR.Application/Features/Auth/Common/AuthRateLimitPolicies.cs` (nuevo, single-source)
- `src/CLARIHR.Api/Program.cs` (4 políticas → constantes + 2 nuevas)
- `src/CLARIHR.Api/Controllers/AuthController.cs` (5 atributos → constantes + 3 nuevos)
- `src/CLARIHR.Application/Features/Auth/Login/LoginCommand.cs` (AU-5)
- `tests/CLARIHR.Application.UnitTests/AuthRateLimitingGovernanceTests.cs` (nuevo guardrail) + `LoginAndLogoutTests.cs` (test AU-5)

---

## 3.2 Remediación AU-1 — verificación de email en el registro (Opción A, 2026-06-10, uncommitted)

Fix estructural: el registro deja de acuñar sesión. Crea una cuenta **no usable** (`UserStatus.PendingEmailVerification`) y envía un link de verificación single-use; la cuenta se activa — y se emite sesión — solo al redimir el link.

**Flujo nuevo (espejo de password-reset):**
- `POST /api/v1/auth/register` → **202** (sin sesión), respuesta **uniforme** exista o no el email (cierra enumeración). Crea pending + token + email. Re-registrar un email pending reemite (con cooldown, sin cambiar password).
- `POST /api/v1/auth/email-verification/confirm` (token) → activa (`ConfirmEmail()`) + **200 + AuthResponse** (auto-login).
- `POST /api/v1/auth/email-verification/resend` (email) → **202** uniforme + cooldown.

**Qué cierra de raíz:**
- **Pre-account-hijacking federado:** el atacante ya no puede pre-sembrar una cuenta **usable** (no puede activar sin el link enviado al buzón real). Y un login de Google verificado que choca con una cuenta pending la **reclama**: `User.ActivateAsExternal(...)` activa + vincula Google + **borra el password no verificado** (`RegisterExternalUserCommand` rama nueva) → el registrante no verificado no conserva acceso.
- **Enumeración:** `register`/`resend` responden 202 uniforme.

**Decisión de seguridad (`UserStatus` allow-list):** el nuevo estado es rechazado por todos los gates de auth (todos chequean `== Active`); verificado en el audit de consumidores (login/refresh/reset/external/JWT issuance/permission-grants todos fail-closed).

**Residual documentado (bajo, no el vector titular):** *password-seeding then victim-verifies* — si un atacante pre-registra el email de la víctima (su password) y la víctima luego **verifica esa misma cuenta** (p. ej. vía resend de un email que no registró), se activaría con el password del atacante. Mitigantes: requiere targeting + que la víctima verifique una cuenta ajena; la recuperación natural (login con Google → limpia el password; o notar la cuenta) lo cubre. **Hardening de roadmap:** password atado al token (cada token lleva su propio candidato) o job de expiración de cuentas no verificadas. El vector titular (federado masivo) **sí queda cerrado**.

**Migración:** `20260610222727_AddEmailVerificationTokens` (solo crea `auth_email_verification_tokens` — el enum no genera cambio de esquema, es columna string). **Generada + verificada, NO aplicada** (el usuario la aplica).

**⚠️ BREAKING (frontend):** `register` ya no devuelve `{accessToken, refreshToken, user}` (era 201) → ahora **202 sin body**. El front debe: mostrar "revisa tu correo" tras registrar, y una landing `/verify-email?token=...` que llame a `email-verification/confirm` y loguee con la sesión devuelta. Config nueva: `Authentication:EmailVerification` (`FrontendVerifyUrl`, lifetime 60 min).

### Verificación
build 0/0 · unit **1799/1799** (incl. register-pending/confirm/claim/cooldown + guardrail + localization en+es del nuevo `auth.email_verification.invalid_token`) · integ auth (register→verify→login/logout/refresh end-to-end vía `CapturingAuthEmailService`) **en ejecución al cierre, confirmar**.

### Archivos (≈20)
- Domain: `UserStatus.PendingEmailVerification`, `User.{RegisterLocalPendingVerification,ConfirmEmail,ActivateAsExternal}`, `EmailVerificationToken`.
- Infra: `EmailVerificationTokenConfiguration` (+DbSet, +migración), `RefreshTokenHasher` (+`IEmailVerificationTokenHasher`), `EmailVerificationTokenGenerator`, `EmailVerificationOptions`, `EmailVerificationSupport`, `LoggingAuthEmailService.SendEmailVerificationAsync`, `EmailVerificationTokenRepository`, DI + appsettings(.Development).
- App: `RegisterUserCommand` (→`ICommand<bool>`, pending+verificación), `EmailVerificationAdministration` (confirm+resend+dispatch), `RegisterExternalUserCommand` (claim pending), `AuthErrors.EmailVerificationTokenInvalid` + resx en+es.
- API: `AuthController.Register`→202 + 2 endpoints `[Tags("Email Verification")]`, `AuthRateLimitPolicies` +2, `Program.cs` +2 políticas.
- Tests: `RegisterUserTests` reescrito, `CapturingAuthEmailService` + `AuthFlowTestExtensions.RegisterAndVerifyAsync` + factory wiring, `ApiIntegrationTests`/`PlatformAuthenticationIntegrationTests` actualizados.

---

## 4. Matriz de decisión

| Acción sugerida | Hallazgos |
|---|---|
| ✅ **Remediado (2026-06-10)** | **AU-1** (verificación de email, §3.2), **AU-2** (rate-limits + guardrail), **AU-5** (timing-equalization) |
| **Requiere decisión (trade-off producto/infra)** | **AU-3** (eventos de auditoría auth → catálogo nuevo; **AU-7** se pliega aquí), **AU-4** (throttle por-cuenta vs lockout), **AU-6** (logout por-cliente vs global), **AU-8** (advisory lock — ¿vale la complejidad?) |
| **Aceptar / fuera de alcance** | **AU-9** (PII en JWT, estándar), **AU-10** (plataforma: HSTS/headers/clave-dev — esfuerzo hermano de infra) |

---

## 5. Decisiones pendientes (del usuario)

> AU-1 + AU-2 + AU-5 ya remediados (§3.1/§3.2). Lo siguiente requiere tu decisión:

1. **AU-3 (auditoría auth) + AU-7:** ¿agregar eventos de autenticación al catálogo y auditar login/logout/register/external/reuse-detected (recomendado para cumplimiento)? AU-7 (auditar el reuse-detected) se incluye aquí.
3. **AU-4 (anti-brute-force):** ¿throttle por-cuenta (partición por email) además del por-IP, o se acepta el rate-limit por-IP actual?
4. **AU-6 (logout):** ¿logout global (revocar Core + Platform, más seguro) o se mantiene el actual por-cliente (Core; menor-sorpresa)?
5. **AU-8 / AU-10:** ¿se atienden ahora o se difieren a un esfuerzo de infra (junto con SystemController/headers)?
