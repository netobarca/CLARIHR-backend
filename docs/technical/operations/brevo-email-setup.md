# Configuración de Brevo — envío de correos (manual)

| | |
|---|---|
| **Propósito** | Pasos **manuales** en la consola de Brevo y en el entorno para activar el envío real de correos. El código ya está listo; esto es lo único que falta. |
| **Fecha** | 2026-07-26 |
| **Aplica a** | `CLARIHR.Api` (y `CLARIHR.Backoffice.Api`, que comparte la misma configuración) |
| **Estado por defecto** | `Email:Provider = Logging` → **no se envía nada**. El envío real requiere activarlo explícitamente al final de esta guía. |

---

## 0. Antes de empezar

Nada de lo que sigue requiere desplegar código. El backend ya trae:

- el proveedor Brevo implementado y seleccionable por configuración,
- el enlace de aceptación de invitación (que antes no existía),
- entrega **después** del commit de la transacción,
- fallo tolerado: si Brevo está caído, la operación de negocio igual se completa y queda registrada en el log.

**Orden importante:** hacer §1 → §2 → §3 → §4 y activar (§5) **hasta el final**. Si se activa `Provider=Brevo` antes de verificar el dominio, los correos llegan a spam o son rechazados, y parece un fallo del sistema.

---

## 1. Cuenta y verificación del dominio

1. Crear la cuenta en <https://app.brevo.com> (o usar la existente de la empresa).
2. Ir a **Settings → Senders, Domains & Dedicated IPs → Domains** y agregar el dominio desde el que se enviará (ejemplo: `clarihr.com`).
3. Brevo entrega registros DNS. Publicarlos en el proveedor de DNS del dominio:
   - **SPF** (TXT)
   - **DKIM** (TXT, normalmente `mail._domainkey`)
   - **DMARC** (TXT en `_dmarc`) — recomendado, empezar con `p=none`
4. Esperar la propagación y pulsar **Verify** hasta que el dominio quede en verde.

> ⚠️ **Este paso no es opcional.** Sin SPF/DKIM el correo de invitación cae en spam de forma silenciosa: el backend registra "enviado", el usuario nunca lo ve, y el flujo parece roto sin ningún error.

---

## 2. Remitente verificado

**Settings → Senders** → agregar el remitente, por ejemplo:

- Nombre: `CLARIHR`
- Correo: `no-reply@clarihr.com` (debe pertenecer al dominio verificado en §1)

Brevo rechaza con **400** cualquier envío desde un remitente no verificado. En ese caso el log muestra el error con el detalle del proveedor (`Brevo rejected the message with 400 … Invalid sender`) y **no se reintenta**: reintentar un 400 solo repite el error.

---

## 3. Plantillas — **no se crean en Brevo**

**No hay nada que hacer en este paso.** Las plantillas viven en nuestro repositorio, no en la consola del proveedor:

```
src/CLARIHR.Infrastructure/Email/Templates/
  Invitation.html        Invitation.txt
  ResetInvitation.html   ResetInvitation.txt
  PasswordReset.html     PasswordReset.txt
  EmailVerification.html EmailVerification.txt
```

El backend envía el **contenido ya renderizado** (asunto + HTML + texto plano); Brevo solo lo transporta.

**Por qué**: si las plantillas viven en el proveedor, cambiar de proveedor obliga a recrearlas allá y el contenido queda fuera del control de versiones. Con este diseño, agregar un segundo proveedor no toca ni una plantilla.

| Plantilla | Cuándo se envía | Placeholders |
|---|---|---|
| `Invitation` | Alta de usuario de empresa; también al finalizar un expediente que crea cuenta | `FIRSTNAME`, `LASTNAME`, `COMPANYNAME`, `ACTIONURL`, `EXPIRESUTC` |
| `ResetInvitation` | Acción "reset invitation" sobre un usuario ya invitado | los mismos |
| `PasswordReset` | `POST /api/v1/auth/password-reset/request` | `FIRSTNAME`, `LASTNAME`, `ACTIONURL`, `EXPIRESUTC` |
| `EmailVerification` | Registro y reenvío de verificación | `FIRSTNAME`, `LASTNAME`, `ACTIONURL`, `EXPIRESUTC` |

**Para editar el copy**: se cambia el `.html` y el `.txt` y se despliega. Reglas al editarlas:

- El **asunto** es el `<title>` del archivo `.html`. No lo quites: el render lo lee de ahí para que un solo archivo sea dueño del mensaje completo.
- Los placeholders se escriben `{{NOMBRE}}`. Los valores se **codifican como HTML** al sustituir, así que un apellido con `<script>` queda inerte.
- Un placeholder desconocido se deja visible (`{{FOO}}`) en vez de vaciarse: un correo que dice `{{FOO}}` se reporta; uno al que le falta el enlace en silencio, no.
- El `.txt` no es opcional: los clientes de solo texto son justamente los que no pueden caer de vuelta al HTML.
- `EXPIRESUTC` sale como `2026-08-01 12:00:00Z` (UTC). Si prefieres no mostrar fecha, redacta "el enlace vence en 24 horas".

Hay tests que fallan si una plantilla pierde el `<title>`, deja un placeholder sin sustituir o no lleva el enlace en ambos cuerpos.

---

## 4. La API key

**Settings → SMTP & API → API Keys → Generate a new API key**. Copiarla (solo se muestra una vez).

Esta clave es el **único secreto** del stack de correo. Se define como variable de entorno, nunca en `appsettings.json`:

```bash
Email__Brevo__ApiKey=xkeysib-...
```

- **Local:** en el archivo `.env` (ver `.env.example`) o con `dotnet user-secrets`.
- **Azure App Service:** *Configuration → Application settings*, con ese nombre exacto (doble guion bajo).

Si falta, el primer intento de envío falla con un mensaje que nombra la variable; el resto de la API sigue funcionando.

---

## 5. Configuración de la aplicación

**Todo lo que cambia entre entornos va como variable de entorno**, no en `appsettings.json`. .NET las lee sin código adicional y **sobrescriben** el JSON; el separador de sección es `__` (doble guion bajo). En Azure son *Configuration → Application settings*; en local, el archivo `.env` (ver `.env.example`).

```bash
# --- Transporte -------------------------------------------------------------
Email__Provider=Brevo                     # ← activar SOLO al final
Email__Brevo__ApiKey=xkeysib-...          # secreto (§4)
Email__Brevo__SenderEmail=no-reply@clarihr.com   # el remitente verificado en §2

# --- Destinos del frontend (uno por entorno) --------------------------------
Authentication__Invitation__FrontendAcceptUrl=https://app.clarihr.com/accept-invitation
Authentication__PasswordReset__FrontendResetUrl=https://app.clarihr.com/reset-password
Authentication__EmailVerification__FrontendVerifyUrl=https://app.clarihr.com/verify-email
```

Con eso basta. **En `appsettings.json` solo quedan los valores que son iguales en todos lados** (`BaseUrl`, `TimeoutSeconds`, `MaxRetries`, `SenderName`) y el default seguro `Email:Provider = Logging`, que la variable de entorno sobrescribe cuando toca activar.

Regla práctica de dónde va cada cosa:

| | Dónde | Por qué |
|---|---|---|
| `Email__Brevo__ApiKey` | **Solo** variable de entorno | Es un secreto; `appsettings.json` se versiona |
| `Email__Provider`, `SenderEmail`, las 3 URLs | Variable de entorno | Cambian por entorno; ponerlas en el JSON obliga a mantener un `appsettings.Production.json` o a commitear valores de un entorno |
| `BaseUrl`, `TimeoutSeconds`, `MaxRetries`, `SenderName` | `appsettings.json` | Son iguales en todos los entornos; sirven de default documentado |

<details>
<summary>Equivalencia en JSON (solo como referencia de la forma de las claves)</summary>

En `appsettings.json` del entorno, si por alguna razón se prefiere el JSON:

```jsonc
"Authentication": {
  "Invitation": {
    // Página del frontend que recibe ?token=... — acordar la ruta con el equipo de front
    "FrontendAcceptUrl": "https://app.clarihr.com/accept-invitation"
  },
  "PasswordReset":     { "FrontendResetUrl":  "https://app.clarihr.com/reset-password" },
  "EmailVerification": { "FrontendVerifyUrl": "https://app.clarihr.com/verify-email" }
},
"Email": {
  "Provider": "Brevo",                       // ← activar SOLO al final
  "Brevo": {
    "BaseUrl": "https://api.brevo.com/",
    "SenderName": "CLARIHR",
    "SenderEmail": "no-reply@clarihr.com",   // el verificado en §2
    "TimeoutSeconds": 10,
    "MaxRetries": 2
  }
}
```

</details>

> No hay sección de plantillas: son nuestras (§3). Esta configuración tiene la misma forma que necesitaría cualquier otro proveedor — conexión e identidad del remitente, nada más.

> Las tres URLs de frontend vienen con `http://localhost:3000` por defecto. **Hay que cambiarlas por entorno**: si quedan en localhost, el correo llega pero el enlace no lleva a ninguna parte.

### 5.1 Comandos — Azure App Service

Reemplazar `<resource-group>`, `<app-service>` y el dominio. **Ojo con `Email__Provider`: déjalo en `Logging` en este comando** y cámbialo a `Brevo` hasta el final (§6), cuando el dominio ya esté verificado.

```bash
# Requiere: az login   (y az account set --subscription "<sub>" si hay más de una)
RG=<resource-group>
APP=<app-service>

az webapp config appsettings set \
  --resource-group "$RG" \
  --name "$APP" \
  --settings \
    "Email__Provider=Logging" \
    "Email__Brevo__SenderEmail=no-reply@clarihr.com" \
    "Authentication__Invitation__FrontendAcceptUrl=https://app.clarihr.com/accept-invitation" \
    "Authentication__PasswordReset__FrontendResetUrl=https://app.clarihr.com/reset-password" \
    "Authentication__EmailVerification__FrontendVerifyUrl=https://app.clarihr.com/verify-email"
```

La API key va en un comando aparte, **con un espacio al inicio** para que no quede en el historial del shell (con `HISTCONTROL=ignorespace`, que es el default en bash; en zsh usar `setopt HIST_IGNORE_SPACE`):

```bash
 az webapp config appsettings set \
  --resource-group "$RG" --name "$APP" \
  --settings "Email__Brevo__ApiKey=xkeysib-..."
```

Alternativa sin exponerla en la línea de comandos — leerla de un archivo y borrarlo después:

```bash
cat > /tmp/email-secret.json <<'JSON'
[{ "name": "Email__Brevo__ApiKey", "value": "xkeysib-...", "slotSetting": false }]
JSON
az webapp config appsettings set --resource-group "$RG" --name "$APP" --settings @/tmp/email-secret.json
shred -u /tmp/email-secret.json 2>/dev/null || rm -P /tmp/email-secret.json
```

Verificar que quedaron (los valores se muestran, así que no lo pegues en un ticket):

```bash
az webapp config appsettings list --resource-group "$RG" --name "$APP" \
  --query "[?starts_with(name,'Email__') || contains(name,'Frontend')].{name:name,value:value}" -o table
```

> `az webapp config appsettings set` **reinicia la aplicación**. Si hay slots de despliegue, aplícalo al slot correspondiente con `--slot <nombre>`; recuerda que las settings marcadas como *slot setting* no viajan en un swap.

Si prefieren el portal: *App Service → Configuration → Application settings → New application setting*, un renglón por variable, y **Save** (también reinicia).

### 5.2 Local — probar en tu máquina

**El correo es el único canal del enlace.** La consola nunca muestra un enlace usable: el proveedor `Logging` registra el destino con el token enmascarado (`?token=abcd...wxyz`), lo justo para detectar una URL mal configurada y nada más. Por lo tanto, **para ejercitar los flujos de invitación / reset / verificación en local hay que configurar Brevo también en local.**

#### Configurar Brevo en local

Brevo es una llamada HTTP, así que funciona igual desde tu máquina. Requiere haber hecho §1–§4.

```bash
dotnet user-secrets --project src/CLARIHR.Api set "Email:Provider" "Brevo"
dotnet user-secrets --project src/CLARIHR.Api set "Email:Brevo:ApiKey" "xkeysib-..."
dotnet user-secrets --project src/CLARIHR.Api set "Email:Brevo:SenderEmail" "no-reply@<dominio-verificado>"

# Los enlaces deben apuntar a TU frontend local
dotnet user-secrets --project src/CLARIHR.Api set "Authentication:Invitation:FrontendAcceptUrl" "http://localhost:5173/accept-invitation"
dotnet user-secrets --project src/CLARIHR.Api set "Authentication:PasswordReset:FrontendResetUrl" "http://localhost:5173/reset-password"
dotnet user-secrets --project src/CLARIHR.Api set "Authentication:EmailVerification:FrontendVerifyUrl" "http://localhost:5173/verify-email"

dotnet user-secrets --project src/CLARIHR.Api list      # verificar
```

Usa una dirección real tuya como destinatario. Cada envío consume cupo del plan Brevo (§8). Recibes el correo en tu bandeja, haces clic y completas el flujo contra tu API local.

Para volver al modo sin envío (`Logging`) mientras trabajas en otra cosa:

```bash
dotnet user-secrets --project src/CLARIHR.Api remove "Email:Provider"
```

> ⚠️ **El puerto 3000 ya está ocupado**: `docker compose` levanta **Gotenberg** ahí (el renderizador de PDF). Los defaults del proyecto apuntan a `localhost:3000` porque se escribieron pensando en un frontend en ese puerto. Ajusta las URLs al puerto real de tu frontend (arriba se usa 5173 como ejemplo) o tendrás enlaces que caen en Gotenberg.

#### Sobre `.env` vs `user-secrets`

Este proyecto **ya está configurado con `user-secrets`** (ahí viven la cadena de Postgres, la clave JWT y el ClientId de Google). Mantente en esa vía: la regla del repo es usar **una** de las dos, no mezclar. Si prefieres `.env`, `cp .env.example .env`, rellénalo y cárgalo con `set -a; source .env; set +a` antes de `dotnet run` — pero entonces mueve **toda** la configuración local ahí.

> Con `user-secrets` el separador es `:` (`Email:Brevo:ApiKey`). Con variables de entorno es `__` (`Email__Brevo__ApiKey`). Son la misma clave.

#### Levantar la app

```bash
docker compose up -d                          # postgres :5433, azurite :10000, gotenberg :3000
dotnet run --project src/CLARIHR.Api
```

Detalle completo del entorno local (usuario semilla `dev@clarihr.local` / `DevPassword123!`, troubleshooting de arranque) en [`local-environment-setup.md`](local-environment-setup.md).

### 5.3 Apagar el envío real

En cualquier entorno, sin desplegar ni borrar la key:

```bash
az webapp config appsettings set --resource-group "$RG" --name "$APP" --settings "Email__Provider=Logging"
```

---

## 6. Activación y prueba

1. Con todo lo anterior hecho, poner `Email:Provider = Brevo` y reiniciar la aplicación.
2. Probar el flujo completo:
   - `POST /api/v1/company/users` → debe llegar el correo de invitación.
   - Abrir el enlace → cae en `FrontendAcceptUrl?token=...`.
   - `POST /api/v1/auth/company-user-invitations/accept` → **200** y el usuario puede hacer login.
   - `POST /api/v1/auth/password-reset/request` → llega el correo de reset.
3. Verificar en Brevo → **Transactional → Logs** que aparezcan los envíos.

Para volver atrás en cualquier momento: `Email:Provider = Logging`. No hace falta desplegar.

---

## 7. Qué buscar en los logs

| Mensaje | Significado |
|---|---|
| `CompanyUserInvitationSent ... provider {…} messageId {…}` | Enviado. El `messageId` permite localizarlo en Transactional → Logs de Brevo |
| `AuthEmailSent template {PasswordReset\|EmailVerification} ...` | Enviado |
| `BrevoSendRetry attempt {n}/{max} ...` | Fallo transitorio (5xx o 429); reintentando |
| `CompanyUserInvitationDeliveryFailed ...` | No se pudo entregar la invitación. **El usuario y su token SÍ se crearon** → recuperar con *reset invitation* |
| `AuthEmailDeliveryFailed ...` | No se pudo entregar reset/verificación. El token existe; el usuario puede volver a pedirlo tras el cooldown |
| `EmailQueued to {…} subject {…} links {…}` | Provider en modo `Logging` (no se envió nada) |
| `PendingInvitationEmailsDropped count {n}` | **Bug**: un handler terminó sin hacer flush ni discard. Reportar |

Con `Provider=Logging` se registra el asunto real (el copy sirve verlo) pero los enlaces salen con el token **enmascarado** (`abcd...wxyz`). Es intencional: antes se escribían completos y cualquiera con acceso al log podía tomar la cuenta. Para recibir el correo de verdad hay que usar Brevo, no leer el log.

---

## 8. Límites y costos

- El plan gratuito de Brevo tiene un **cupo diario** de correos transaccionales. Un alta masiva de usuarios puede agotarlo: cuando pasa, Brevo responde **429** y el backend reintenta; si aun así falla, queda `...DeliveryFailed` en el log. **Confirmar el cupo del plan contratado antes de una carga masiva.**
- Los reintentos solo aplican a 5xx, timeout y 429. Un 400 (remitente no verificado, plantilla inexistente, dirección malformada) no se reintenta.

---

## 9. Agregar otro proveedor en el futuro

El sistema está construido para esto. Un proveedor nuevo (SendGrid, Mailgun, SES, un relay SMTP) es:

1. Una clase que implementa `IEmailSender` en `src/CLARIHR.Infrastructure/Email/Providers/<Proveedor>/`.
2. Su clase de opciones (conexión + remitente).
3. Una rama más en `EmailRegistration.AddTransport` y una constante en `EmailProviders`.

**No se toca nada más**: ni las plantillas, ni el render, ni `IEmailService`/`IAuthEmailService`, ni un solo handler. Hay tests de gobierno que fallan si un proveedor empieza a reimplementar el contenido o si algo fuera de `Providers/` referencia un tipo específico de proveedor.

---

## 10. Checklist

- [ ] Dominio verificado en Brevo (SPF + DKIM + DMARC en verde)
- [ ] Remitente `no-reply@<dominio>` verificado
- [ ] ~~Plantillas en Brevo~~ → **no aplica**: viven en el repo (§3)
- [ ] `Email__Brevo__ApiKey` en el entorno
- [ ] `Email:Brevo:SenderEmail` en configuración
- [ ] `Authentication:Invitation:FrontendAcceptUrl` + las otras dos URLs apuntando al frontend del entorno
- [ ] `Email:Provider = Brevo`
- [ ] Prueba end-to-end: invitar → recibir → aceptar → login
