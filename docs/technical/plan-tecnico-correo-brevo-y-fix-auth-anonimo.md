# Plan técnico — Fix de auth anónimo (500) + envío de correos con Brevo

| | |
|---|---|
| **Propósito** | Cerrar el bug que deja muerto el flujo de invitación/password-reset, y habilitar el envío real de correos con **Brevo**. |
| **Fecha** | 2026-07-26 |
| **Estado** | 🟢 **CÓDIGO COMPLETO 2026-07-26** — PR-1…PR-5 implementados; unit 2810/2810. Falta **solo configuración manual en Brevo** (cuenta, dominio, plantillas, API key) → [`operations/brevo-email-setup.md`](operations/brevo-email-setup.md) |
| **Origen** | Bug detectado 2026-07-26 en `apiclarihrdev` |
| **Relacionado** | `AGENTS.md` §7.5 · `project-foundation.md` §11 · `docs/technical/operations/production-deployment.md` |

---

## 0. Resumen

Son **dos entregables acoplados por dependencia, no por diseño**:

- **Parte A (bug):** tres endpoints `[AllowAnonymous]` devuelven **500 determinista**. Es un fix de ~5 líneas. **Sin él, conectar Brevo no sirve de nada**: el correo de invitación llegaría bien y el link seguiría reventando.
- **Parte B (Brevo):** hoy los tres servicios de correo son *no-ops* que solo escriben en el log. Al conectar un proveedor real salen a la luz tres problemas de diseño que hoy son inocuos y dejan de serlo (§2.1).

**Orden obligatorio: A antes que B.** La Parte A es independiente y mergeable de inmediato.

---

## 1. Parte A — Bug: 500 en endpoints anónimos de auth

### 1.1 Diagnóstico (verificado en código)

`AuditService.LogAsync` (`src/CLARIHR.Infrastructure/Auditing/AuditService.cs:22-26`) lanza:

```csharp
if (!tenantContext.TenantId.HasValue)
    throw new InvalidOperationException("Audit logging requires a tenant context.");
```

`HttpTenantContext` resuelve el tenant **solo** del claim `tid` del JWT o del `AmbientTenantContext`; ningún middleware lo setea por header. En un endpoint anónimo no hay JWT → `TenantId` es siempre `null` → **siempre 500 `common.unexpected`**. No es un problema de datos: es determinista.

### 1.2 Alcance exacto (auditado, no asumido)

Se revisaron **todos** los `auditService.LogAsync` de `Features/Auth` y `Features/CompanyUsers`. Los afectados son exactamente **tres**:

| # | Endpoint | Call site | Fix |
|---|---|---|---|
| 1 | `POST /api/v1/auth/company-user-invitations/accept` | `AcceptCompanyUserInvitationCommand.cs:109` | `LogForTenantAsync(resolution.CompanyPublicId, …)` — el tenant **ya está resuelto** en ese scope |
| 2 | `POST /api/v1/auth/password-reset/request` | `PasswordResetAdministration.cs:107` | `IPlatformAuditService` — no hay tenant natural |
| 3 | `POST /api/v1/auth/password-reset/redeem` | `PasswordResetAdministration.cs:219` | `IPlatformAuditService` |

Los demás `auditService.LogAsync` (`CreateCompanyUser:183`, `UpdateCompanyUser:161`, `DeactivateCompanyUser:106`, `ReactivateCompanyUser:91`, `ResetInvitation:100`) viven en endpoints **autenticados** bajo `/api/v1/company/users` → el tenant llega por JWT. **No se tocan.**

El patrón correcto ya existe en el repo y funciona: `LoginCommand:86,107`, `RegisterUserCommand:137`, `EmailVerificationAdministration:77`, `LogoutCommand:41` y `RegisterExternalUserCommand:146` usan `IPlatformAuditService` y **por eso sí funcionan anónimos**. Además está documentado como norma en el comentario de `CompanyUserProvisioningService.cs:214`.

### 1.3 Agravante: no es solo "no se audita"

En el caso 1 el `LogAsync` ocurre **dentro de la transacción abierta** (`AcceptCompanyUserInvitationCommand.cs:93`), **después** del `SaveChangesAsync` (:107) y **antes** del commit. La excepción hace **rollback de la activación completa**: password hash, `status='Active'`, membresía y `iam_users.is_active` se revierten. El usuario no queda "activado sin auditoría" — **queda sin activar**, y el token de invitación tampoco se consume. De ahí que el único workaround haya sido escribir el `password_hash` a mano en la base.

### 1.4 Por qué no lo atrapó la suite

Los tests unitarios inyectan un `IAuditService` falso que **no valida tenant**, así que el camino feliz pasa en verde. Y **no existen tests de integración de estos tres endpoints**. El fix sin tests de integración deja el mismo agujero abierto para el próximo endpoint anónimo.

### 1.5 Trabajo (PR-1)

1. Aplicar los tres cambios de §1.2.
2. **Tests de integración** de los 3 endpoints contra la base real (`CLARIHR.Api.IntegrationTests`, ya tiene infraestructura — ver `docs/technical/operations/local-environment-setup.md`): happy path + token inválido + token expirado.
3. **Test de gobierno anti-regresión**: que ningún handler alcanzable desde un endpoint `[AllowAnonymous]` dependa de `IAuditService.LogAsync`. Sin esto, el bug vuelve.
4. Endurecer el doble de test de `IAuditService` para que **replique la validación de tenant** (que falle igual que producción).

**Esfuerzo:** 0.5–1 día. **Riesgo:** bajo. **Migración:** ninguna. **Contrato:** sin cambios (`openapi.yaml` sin drift; hoy esos endpoints están documentados como 200 y hoy dan 500 — el fix los alinea con el contrato ya publicado).

---

## 2. Parte B — Envío de correos con Brevo

### 2.1 Tres hallazgos que hay que resolver **antes** de conectar el proveedor

Hoy `LoggingEmailService` y `LoggingAuthEmailService` son no-ops. Eso esconde tres problemas que aparecen el día que el envío sea una llamada HTTP real:

**H-1 · Tres de los seis envíos ocurren con una transacción de base de datos abierta.**

| Call site | ¿Transacción abierta? | |
|---|---|---|
| `CreateCompanyUser.cs:196` | **SÍ** (begin :97 → commit :207) | ⚠️ |
| `ResetInvitation.cs:131` | **SÍ** (begin :87 → commit :142) | ⚠️ |
| `CompanyUserProvisioningService.cs:196` | **SÍ** — heredada de `FinalizePersonnelFile.cs:134` | ⚠️ |
| `RegisterUserCommand.cs:151` | No (commit en :149) | ✅ |
| `RegisterUserCommand.cs:176` | No | ✅ |
| `EmailVerificationAdministration.cs:159` | No | ✅ |
| `PasswordResetAdministration.cs:125` | No | ✅ |

Con un no-op da igual. Con Brevo significa: **la conexión y los locks quedan tomados mientras dura el HTTP**, y si Brevo está lento o caído, **falla el alta del usuario y se hace rollback**. Peor aún en el orden inverso: correo enviado y commit fallido = invitación a un usuario que no existe. En particular, `CompanyUserProvisioningService` cuelga de la transacción de finalización de expediente, que ya escribe en varios módulos.

**H-2 · No existe configuración de URL de invitación.** `CompanyUserInvitationEmailMessage` transporta el **token crudo**, sin link. Existen `Authentication:PasswordReset:FrontendResetUrl` y `Authentication:EmailVerification:FrontendVerifyUrl`, pero **no hay equivalente para invitación**. Sin ese valor no se puede componer el correo. Hay que agregarlo y acordar la ruta con el frontend.

**H-3 · Hoy se loguea el link completo de reset y de verificación.** `LoggingAuthEmailService.cs:9-30` escribe `ResetLink` y `VerificationLink` **completos** en el log (el de invitación sí está enmascarado a `abcd...wxyz`). Es lo que hizo usable el entorno de desarrollo, y es **fuga de credenciales de un solo uso** en cuanto ese log salga a un agregador. Debe quedar restringido al proveedor `Logging` y explícitamente fuera de producción.

### 2.2 Decisiones de diseño (con recomendación)

| # | Decisión | Recomendación | Por qué |
|---|---|---|---|
| D-1 | API HTTP v3 vs. relay SMTP | **API HTTP** (`POST /v3/smtp/email`) | Azure App Service bloquea el puerto 25 y el 587 es frágil; el API devuelve `messageId` y errores tipados (clave para diagnosticar rebotes). Además ya hay molde de cliente HTTP tipado en el repo |
| D-2 | Plantillas en Brevo vs. HTML en el repo | **Plantillas en Brevo por `templateId`**, parametrizadas | Permite que el cliente ajuste copy y marca sin desplegar. Contra: los IDs son por entorno → van a configuración, nunca hardcodeados |
| D-3 | Síncrono vs. cola | **Síncrono, pero después del commit**, con timeout corto | Una cola bien hecha exige el outbox de `analisis-arquitectura-monolito-modular.md` F4. No lo bloqueemos por eso; el paso intermedio correcto es *enviar fuera de la transacción y no tumbar la operación si el correo falla* |
| D-4 | ¿Un fallo de correo tumba la operación? | **No.** Log + métrica + `Result` exitoso | El usuario ya quedó creado. Para password-reset el endpoint **debe seguir devolviendo 200 siempre** (no filtrar si el correo existe). Excepción a evaluar con negocio: si se decide que la invitación sin correo no tiene sentido, se compensa con `reset-invitation`, no con rollback |
| D-5 | Selector de proveedor | `Email:Provider = Logging \| Brevo`, con `Logging` por defecto | Mismo patrón que `Reporting:Pdf:Engine` (`DocumentPdfRenderingRegistration.cs:47-77`). CI, tests y dev local **nunca** deben poder mandar correo real |
| D-6 | Secreto | `Brevo__ApiKey` por variable de entorno | Convención ya establecida en `.env.example`; en Azure va como Application Setting. **Nunca en `appsettings.json`** |
| D-7 | Webhooks de rebote/spam | **Fuera de alcance ahora** | Requiere endpoint público, verificación de firma y una tabla de estado. Se registra como pendiente |

### 2.3 Arquitectura propuesta

Copiar el molde ya probado de Gotenberg (`DocumentPdfRenderingRegistration`):

- `Infrastructure/Email/BrevoOptions.cs` — `ApiKey`, `BaseUrl`, `SenderName`, `SenderEmail`, `TimeoutSeconds`, `Templates:{Invitation,PasswordReset,EmailVerification}`.
- `Infrastructure/Email/BrevoEmailClient.cs` — `HttpClient` tipado; una sola clase que compone el payload de Brevo y traduce errores.
- `Infrastructure/Email/BrevoEmailService.cs` + `BrevoAuthEmailService.cs` — implementan `IEmailService` e `IAuthEmailService` sobre el cliente. **Las dos interfaces existentes no se tocan** → cero impacto en los 6 call sites y cero cambio de contrato.
- `Infrastructure/Email/EmailRegistration.cs` — selector por `Email:Provider`; `AddHttpClient` con timeout y reintentos.

Resiliencia mínima: timeout 10 s, 2 reintentos con backoff solo para 5xx/timeout (**nunca** para 4xx: un `400` de Brevo es payload inválido y reintentar lo repite), y un log estructurado con `messageId` en éxito.

### 2.4 Trabajo por PR

| PR | Contenido | Esfuerzo |
|---|---|---|
| **PR-1** | **Fix del bug** (§1.5) — independiente, mergeable ya | 0.5–1 d |
| **PR-2** | **Higiene previa**: mover los 3 envíos fuera de la transacción (H-1); agregar `Authentication:Invitation:FrontendAcceptUrl` y componer el link (H-2); dejar de loguear links completos (H-3) | 1–2 d |
| **PR-3** | **Proveedor Brevo**: opciones, cliente HTTP tipado, las dos implementaciones, selector por configuración, resiliencia | 2–3 d |
| **PR-4** | **Plantillas y pruebas**: `templateId` por tipo de correo, parámetros, idioma; tests de integración contra un servidor HTTP falso (sin pegarle a Brevo); prueba manual end-to-end en `apiclarihrdev` | 2–3 d |
| **PR-5** | **Cierre**: métricas/logs de entrega, `docs/technical/operations/` con runbook y checklist de despliegue, actualización de `.env.example` | 1 d |

**Total: ~1.5–2 semanas.** PR-1 y PR-2 valen por sí solos aunque Brevo se posponga.

### 2.5 Configuración nueva

```jsonc
// appsettings.json — sin secretos
"Authentication": {
  "Invitation": { "FrontendAcceptUrl": "http://localhost:3000/accept-invitation" }   // H-2
},
"Email": {
  "Provider": "Logging",                    // Logging | Brevo  — default seguro
  "Brevo": {
    "BaseUrl": "https://api.brevo.com/",
    "SenderName": "CLARIHR",
    "SenderEmail": "no-reply@<dominio-verificado>",
    "TimeoutSeconds": 10,
    "Templates": { "Invitation": 0, "PasswordReset": 0, "EmailVerification": 0 }
  }
}
```

```bash
# .env / Application Settings — el único secreto
Email__Brevo__ApiKey=
```

---

## 3. Riesgos y checklist de despliegue

| Riesgo | Mitigación |
|---|---|
| Correos al spam | Verificar dominio en Brevo con **SPF + DKIM + DMARC** antes de la primera prueba real. Sin esto, la invitación llega a spam y el flujo "no funciona" sin error visible |
| Enviar correo real desde CI o dev | `Email:Provider` default `Logging`; el proveedor `Brevo` solo se activa por configuración explícita del entorno |
| Límite del plan Brevo | Confirmar el cupo diario del plan contratado (el gratuito es limitado). Un alta masiva de usuarios puede agotarlo en silencio → métrica de fallos obligatoria |
| API key filtrada | Solo por variable de entorno; rotable sin desplegar; nunca en `appsettings*.json` ni en logs |
| Tokens en logs | H-3 resuelto en PR-2 |
| Enumeración de usuarios | `password-reset/request` **debe** seguir devolviendo 200 aunque el correo falle o el usuario no exista |

**Checklist de despliegue (operación, no código):**
- [ ] Cuenta Brevo creada; dominio verificado (SPF/DKIM/DMARC propagados)
- [ ] Remitente `no-reply@<dominio>` verificado
- [ ] 3 plantillas creadas; sus IDs cargados por entorno
- [ ] `Email__Brevo__ApiKey` en Application Settings del entorno
- [ ] `Authentication:Invitation:FrontendAcceptUrl` y las otras dos URLs apuntando al frontend del entorno (hoy apuntan a `localhost:3000`)
- [ ] `Email:Provider=Brevo` activado **solo** después de lo anterior
- [ ] Prueba end-to-end: invitar → recibir → aceptar → login

---

## 4. Qué NO hacer

1. **No conectar Brevo antes del PR-1.** El correo llegaría y el link daría 500. Se vería como "falla el correo" cuando el problema es otro.
2. **No hacer rollback de la operación de negocio si falla el correo** (salvo decisión explícita del negocio). Para eso existe `reset-invitation`.
3. **No montar cola/outbox en este alcance.** Enviar después del commit resuelve el 90 % del riesgo; el outbox pertenece a F4 del plan de arquitectura.
4. **No usar SMTP** salvo que se compruebe un bloqueo del API HTTP; el puerto 25 está bloqueado en App Service.
5. **No dejar `Email:Provider=Brevo` como default en `appsettings.json`.** El default seguro es `Logging`.
6. **No hardcodear los `templateId`.** Cambian entre entornos.

---

## 5. Decisiones tomadas (2026-07-26)

| # | Decisión | Elegida |
|---|---|---|
| D-2 | Plantillas | ~~Gestionadas en Brevo por `templateId`~~ → **REVERTIDA 2026-07-26 (PR-6)**: las plantillas viven en nuestro repositorio (`Email/Templates`) y el backend envía el contenido ya renderizado. Motivo: habrá más de un proveedor, y con las plantillas en la consola del proveedor cambiar de proveedor obliga a recrearlas |
| D-4 | Fallo de envío | **Fail-open**: la operación de negocio se completa igual; log + métrica; se recupera con `reset-invitation` |
| D-5 | Agnosticismo | **`IEmailSender` es la única costura de proveedor.** Un proveedor nuevo = una clase + una rama en el registro. Verificado por tests de gobierno |
| — | Alcance | PR-1 … PR-6 completo |

---

## 6. Lo que se construyó

**PR-1 — Fix del bug**
- `AcceptCompanyUserInvitationCommand.cs` → `LogForTenantAsync(resolution.CompanyPublicId, …)`.
- `PasswordResetAdministration.cs` (request y redeem) → `IPlatformAuditService`.
- **El doble de test `TestAuditService` ahora replica producción**: `LogAsync` lanza si no hay tenant. Los flujos anónimos se construyen con `tenantId: null` (`CreateAnonymousAuditService()`), así que el fallo aparece en la suite y no solo en el servidor.
- `AnonymousEndpointAuditGovernanceTests`: deriva la superficie anónima de `AuthController` por reflexión y prohíbe que un handler de `Features.Auth` inyecte `IAuditService`, salvo allow-list justificada (hoy: solo el de aceptar invitación, que usa `LogForTenantAsync`). Incluye test de allow-list obsoleta.
- **Verificado que el guardrail no es vacío**: revertir el fix pone la suite en rojo.

**PR-2 — Higiene previa**
- `IPendingEmailDispatcher` (+ `PendingEmailDispatcher`): encolar dentro de la transacción, `FlushAsync` tras el commit, `Discard` en rollback. Si el scope muere con mensajes pendientes se registra error y se descartan — perder una invitación se recupera, inventarla no.
- Los 3 envíos que estaban dentro de transacción migrados: `CreateCompanyUser`, `ResetInvitation` y `CompanyUserProvisioningService` (este último encola; hacen flush sus dueños de transacción: `FinalizePersonnelFile` y `RehireEmployee`).
- `Authentication:Invitation:FrontendAcceptUrl` + `IInvitationLinkBuilder` — antes el mensaje llevaba el token crudo **sin enlace**.
- `SecretPreview`: los logs ya no imprimen enlaces de reset/verificación completos.

**PR-3/PR-4 — Brevo**
- `EmailRegistration` con selector `Email:Provider` (default `Logging`), transporte con HttpClient tipado y reintentos solo 5xx/timeout/429.
- Tests de transporte contra `HttpMessageHandler` stub + resolución real del contenedor DI.

**PR-5 — Cierre**
- `.env.example`, `appsettings.json`, y la guía de configuración manual.

**PR-6 — Agnosticismo de proveedor** (cambio de diseño pedido 2026-07-26)
- **Hallazgo**: el diseño anterior NO era agnóstico — el mapeo de contenido (`FIRSTNAME`, `ACTIONURL`…) vivía dentro de `BrevoEmailService`/`BrevoAuthEmailService`, así que un segundo proveedor habría duplicado el pipeline completo. La abstracción estaba al nivel equivocado: `IEmailService`/`IAuthEmailService` tienen forma de caso de uso y cada proveedor las implementaba enteras.
- **Nueva capa**: `IEmailSender` (única costura de proveedor) + `EmailMessage` (mensaje renderizado, sin conceptos de proveedor) + `IEmailTemplateSource` / `IEmailTemplateRenderer` (nuestros).
- `TemplatedEmailService` y `TemplatedAuthEmailService`: **una implementación para todos los proveedores**. Brevo queda reducido a `BrevoEmailSender`.
- **Plantillas en el repo** (`Email/Templates/`, 4 HTML + 4 texto, embebidas). El asunto es el `<title>` del propio archivo. Valores codificados como HTML al sustituir (un apellido `<script>` queda inerte).
- **Guardrails**: el código de proveedor solo puede implementar `IEmailSender`; nada fuera de `Providers/` puede referenciar un tipo de proveedor; una sola implementación de `IEmailService`/`IAuthEmailService`. **Verificado simulando un proveedor que reimplementa el contenido: 2 tests en rojo.**

---

## 7. Bitácora

| Fecha | Evento |
|---|---|
| 2026-07-26 | Bug detectado en `apiclarihrdev`; alcance auditado (3 call sites, no más); plan redactado. |
| 2026-07-26 | **PR-1…PR-5 implementados.** Unit 2810/2810 (línea base 2779 + 31 nuevos). Build `-warnaserror` limpio. Guardrail verificado revirtiendo el fix. |
| 2026-07-26 | **PR-6 — refactor a arquitectura agnóstica de proveedor** (D-2 revertida por decisión de diseño: habrá más de un proveedor). Unit **2829/2829**. Se eliminaron `BrevoEmailService`, `BrevoAuthEmailService`, `BrevoEmailClient`, `LoggingEmailService` y `LoggingAuthEmailService`; los reemplazan un pipeline de contenido compartido y dos transportes. Desapareció la sección `Email:Brevo:Templates` de la configuración. Pendiente: solo la configuración manual de Brevo (§ `operations/brevo-email-setup.md`). |
