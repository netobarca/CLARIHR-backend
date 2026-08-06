# Playbook de pruebas de la API

Recorrido cronológico, endpoint por endpoint, para validar los flujos de CLARIHR contra un ambiente
desplegado. Cada paso dice qué se llama, qué debe devolver y qué prueba.

Los contratos están tomados de `docs/technical/api/openapi.yaml`, regenerado el 2026-08-03 contra el
swagger real.

**Estado del playbook**

| Sección | Estado |
|---|---|
| 1. Autenticación | ✅ **validada 2026-08-04** — registro, login, logout, reseteo de contraseña y recepción de correos funcionando. Google pendiente (problema abierto con el proveedor) |
| 2. Empresa y perfil legal | ✅ **validada 2026-08-04** — con ajustes reportados al frontend. Encontró dos `500`: `POST /legal-profile` y el cambio de representante principal, ambos arreglados y **pendientes de desplegar** |
| 3. Estructura organizativa | escrita — pendiente de ejecutar |
| 4. Puestos, plazas y tabulador | escrita — pendiente de ejecutar |
| 5. Nómina: definición, calendario y jornadas | escrita — pendiente de ejecutar |
| 6. Políticas operativas y usuarios | escrita — pendiente de ejecutar |
| 7. Expedientes de empleado | pendiente |
| 8. Transacciones y corrida de planilla | pendiente |

**Las secciones 3 a 6 son el requisito completo para poder crear expedientes y transaccionar.** Al final
de la 6 hay una lista de verificación única: si todo eso pasa, la empresa está lista.

---

## 0. Preparación

```bash
export API="https://<host-de-la-api>"        # sin barra final
export EMAIL="<correo de la cuenta de prueba>"
```

La contraseña **no** se exporta: cada paso la pide de forma interactiva con `read -s`, así no queda
en el historial del shell ni en el entorno.

Todas las respuestas de error siguen el mismo formato (`ProblemDetails`), con el código de negocio en
`code`:

```jsonc
{ "type": "...", "title": "...", "status": 401, "detail": "...", "code": "auth.invalid_credentials", "traceId": "..." }
```

Parámetros vigentes del ambiente (de `appsettings.json`; un ambiente puede sobrescribirlos):

| Parámetro | Valor |
|---|---|
| Vida del access token | 15 minutos |
| Vida del refresh token | 14 días |
| Intentos fallidos antes del bloqueo | 10 en 15 minutos → bloqueo de 15 minutos |
| Vida del token de reseteo de contraseña | 15 minutos |
| Espera entre solicitudes de reseteo | 2 minutos |
| Vida del token de verificación de correo | 60 minutos |

> **Nota de despliegue.** A la fecha de escritura, el arreglo del `POST /legal-profile` (que devolvía
> `500` aunque creaba el registro) **no está commiteado ni desplegado**. Afecta a la sección 2, no a
> esta.

---

## 1. Autenticación

### 1.1 · Registro local — `POST /api/v1/auth/register`

**No se ejecuta en esta corrida** (ya validado previamente). Queda documentado porque es el origen de
toda cuenta local y porque su comportamiento condiciona el paso 1.2.

```http
POST {API}/api/v1/auth/register
Content-Type: application/json

{ "firstName": "...", "lastName": "...", "email": "...", "password": "...", "country": "SV", "source": null }
```

| Respuesta | Significa |
|---|---|
| `202` | Aceptado. **No devuelve tokens**: manda correo de verificación y la cuenta queda sin verificar |
| `400` | Validación (correo mal formado, contraseña débil) |
| `429` | Demasiadas solicitudes |

**Lo importante para el resto del playbook:** el registro **no autentica**. Devuelve `202`, no una
sesión. Para obtener token hay que verificar el correo y luego hacer login.

---

### 1.2 · Login — `POST /api/v1/auth/login`

Es el punto de entrada real del playbook: de acá salen los tokens que usan todas las secciones.

```bash
read -s -p "Contraseña: " PASS; echo
curl -s -X POST "$API/api/v1/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL\",\"password\":\"$PASS\"}" \
  -o /tmp/login.json -w "status=%{http_code}\n"
unset PASS
```

**Esperado `200`:**

```jsonc
{
  "accessToken": "eyJ…",
  "refreshToken": "…",
  "expiresIn": 900,                 // 15 minutos, en segundos
  "user": { "publicId": "…", "email": "…", "firstName": "…", "lastName": "…", "authProvider": "…" }
}
```

Guardar para los pasos siguientes:

```bash
export TOKEN=$(python3 -c "import json;print(json.load(open('/tmp/login.json'))['accessToken'])")
export REFRESH=$(python3 -c "import json;print(json.load(open('/tmp/login.json'))['refreshToken'])")
```

**Verificar los claims del token** — esto es lo que decide a qué empresa se está entrando:

```bash
python3 -c "
import json,base64
t='$TOKEN'.split('.')[1]; t+='='*(-len(t)%4)
c=json.loads(base64.urlsafe_b64decode(t))
print('tid  (empresa):', c.get('tid'))
print('sub  (usuario):', c.get('sub'))
print('estado        :', c.get('user_status'))
print('proveedor     :', c.get('auth_provider'))
"
```

| Verificación | Esperado |
|---|---|
| `expiresIn` | `900` |
| Claim `tid` | El `publicId` de la empresa. **Es el `{companyPublicId}` de todas las rutas siguientes** |
| Claim `auth_provider` | `Local` para cuentas locales |

> **`tid` es el contexto de empresa.** Si la cuenta pertenece a varias empresas, el token trae una
> sola. Llamar un endpoint de empresa con un `companyPublicId` distinto al `tid` devuelve `403` de
> desajuste de tenant, no `404`.

**Casos negativos** (ejecutar con cuidado — cuentan para el bloqueo):

| Caso | Esperado |
|---|---|
| Contraseña incorrecta | `401` |
| Correo inexistente | `401` — mismo código, no revela si el correo existe |
| Correo mal formado | `400` |
| 10 fallos en 15 minutos | Bloqueo de 15 minutos |

> **Cuidado con el caso de bloqueo.** Diez intentos fallidos dejan la cuenta bloqueada 15 minutos y
> frenan el resto del playbook. Probarlo al final, o con una cuenta descartable.

---

### 1.3 · Refresh — `POST /api/v1/auth/refresh`

Prueba que la sesión se renueva sin volver a pedir contraseña.

```bash
curl -s -X POST "$API/api/v1/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH\"}" \
  -o /tmp/refresh.json -w "status=%{http_code}\n"
```

| Respuesta | Significa |
|---|---|
| `200` | Nueva `AuthResponse` completa |
| `400` | Falta el `refreshToken` o viene vacío |
| `401` | Token inválido, expirado o ya usado |

**Qué comprobar:** el `accessToken` devuelto debe ser **distinto** del anterior. Comprobar también si
el `refreshToken` rota:

```bash
python3 -c "
import json
a=json.load(open('/tmp/login.json')); b=json.load(open('/tmp/refresh.json'))
print('access token cambió :', a['accessToken']!=b['accessToken'])
print('refresh token rotó  :', a['refreshToken']!=b['refreshToken'])
"
```

Si el refresh rota, hay que actualizar `$REFRESH` antes de seguir. Reusar uno consumido debe dar
`401`; conviene confirmarlo:

```bash
curl -s -X POST "$API/api/v1/auth/refresh" -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH\"}" -o /dev/null -w "reuso=%{http_code}\n"
```

---

### 1.4 · Reseteo de contraseña — tres endpoints encadenados

Flujo completo: solicitar → validar el token → canjearlo.

**a) Solicitar** — `POST /api/v1/auth/password-reset/request`

```bash
curl -s -X POST "$API/api/v1/auth/password-reset/request" \
  -H "Content-Type: application/json" -d "{\"email\":\"$EMAIL\"}" \
  -o /dev/null -w "status=%{http_code}\n"
```

| Respuesta | Significa |
|---|---|
| `202` | Aceptado. **Devuelve `202` exista o no el correo** — deliberado, para no filtrar qué correos están registrados |
| `429` | Menos de 2 minutos desde la solicitud anterior |

**Repetir de inmediato debe dar `429`.** Es la forma de comprobar que el enfriamiento está activo.

**b) Revisar el enlace del correo.** Acá es donde apareció el incidente de los caracteres Unicode
invisibles: el enlace llegaba como
`…/%E2%81%A0%E2%80%AFreset-password?token=…`. Comprobar que el enlace recibido sea limpio:

```bash
# Pegar el enlace recibido entre comillas simples
python3 -c "
u=input('Enlace del correo: ')
raro=[(i,hex(ord(ch)),ch) for i,ch in enumerate(u) if ord(ch)>127 or ord(ch)<32]
print('caracteres no ASCII:', raro if raro else 'ninguno — OK')
print('contiene %E2%81%A0 o %E2%80%AF:', '%E2%81%A0' in u or '%E2%80%AF' in u)
"
```

**c) Validar el token** — `POST /api/v1/auth/password-reset/validate`

```bash
curl -s -X POST "$API/api/v1/auth/password-reset/validate" \
  -H "Content-Type: application/json" -d "{\"token\":\"<token-del-enlace>\"}" \
  -o /dev/null -w "status=%{http_code}\n"
```

`200` válido · `401` inválido o expirado (vida: 15 minutos) · `400` falta el token.

Sirve para que la pantalla decida si muestra el formulario o el mensaje de "enlace vencido" antes de
que la persona escriba una contraseña nueva.

**d) Canjear** — `POST /api/v1/auth/password-reset/redeem`

```bash
curl -s -X POST "$API/api/v1/auth/password-reset/redeem" \
  -H "Content-Type: application/json" \
  -d "{\"token\":\"<token>\",\"newPassword\":\"<nueva>\"}" \
  -o /dev/null -w "status=%{http_code}\n"
```

`204` cambiada · `401` token inválido/expirado · `400` contraseña que no cumple la política.

**Comprobaciones posteriores, en este orden:**

1. Reusar el mismo token → debe dar `401` (los tokens son de un solo uso).
2. Login con la contraseña **vieja** → `401`.
3. Login con la contraseña **nueva** → `200`.
4. Verificar si las sesiones anteriores siguen vivas: usar el `$REFRESH` de antes del cambio. Si
   devuelve `200`, cambiar la contraseña **no** cierra las sesiones abiertas — anotarlo, es una
   decisión de seguridad que conviene tener explícita.

---

### 1.5 · Verificación de correo — dos endpoints

**a) Reenviar** — `POST /api/v1/auth/email-verification/resend`

```bash
curl -s -X POST "$API/api/v1/auth/email-verification/resend" \
  -H "Content-Type: application/json" -d "{\"email\":\"$EMAIL\"}" \
  -o /dev/null -w "status=%{http_code}\n"
```

`202` aceptado (exista o no el correo, y esté o no ya verificado) · `429` menos de 2 minutos desde el
anterior.

**b) Confirmar** — `POST /api/v1/auth/email-verification/confirm`

```bash
curl -s -X POST "$API/api/v1/auth/email-verification/confirm" \
  -H "Content-Type: application/json" -d "{\"token\":\"<token-del-enlace>\"}" \
  -o /dev/null -w "status=%{http_code}\n"
```

`200` confirmado · `401` inválido o expirado (60 minutos) · `400` falta el token.

Confirmar dos veces con el mismo token: anotar si da `200` (idempotente) o `401` (un solo uso).

> Igual que en 1.4b, revisar que el enlace del correo de verificación no traiga caracteres invisibles.
> La variable de entorno es `Authentication__EmailVerification__FrontendVerifyUrl`.

---

### 1.6 · Autenticación externa (Google) — `POST /api/v1/auth/external`

**No se ejecuta en esta corrida: hay un problema abierto con el proveedor.** Queda documentado para
que el playbook esté completo y para que la prueba esté lista cuando se destrabe.

```http
POST {API}/api/v1/auth/external
Content-Type: application/json

{ "provider": "Google", "idToken": "<id_token de Google>", "country": "SV", "source": null }
```

| Respuesta | Significa |
|---|---|
| `201` | Primera vez: se creó la cuenta y se devuelve sesión |
| `200` | La cuenta ya existía: se devuelve sesión |
| `400` | Falta el `idToken` o el `provider` no es válido |
| `401` | El `idToken` no valida contra Google |
| `409` | Conflicto: el correo ya existe con otro proveedor |
| `422` | Falta un dato requerido para crear la cuenta (p. ej. `country`) |

**Es el único endpoint del grupo que distingue `201` de `200`**, y esa distinción es la señal de
"cuenta nueva" que el frontend necesita para decidir si manda a onboarding.

**Qué revisar cuando se destrabe:**

1. `Authentication__Google__ClientId` configurado en el ambiente — sin eso, todo `idToken` da `401`.
2. Que el `ClientId` del backend coincida con el que usa el frontend para pedir el token; si no
   coinciden, Google emite un token con otro `aud` y la validación falla.
3. El caso `409`: registrarse con Google usando un correo que ya existe como cuenta local.
4. El claim `auth_provider` del token resultante debe decir `Google`, no `Local`.

---

### 1.7 · Aceptar invitación a empresa — `POST /api/v1/auth/company-user-invitations/accept`

Cómo entra una persona invitada a una empresa existente. Se prueba junto con el alta de usuarios de
empresa (sección 2), pero el endpoint es de autenticación.

```http
POST {API}/api/v1/auth/company-user-invitations/accept
Content-Type: application/json

{ "token": "<token del correo de invitación>", "password": "<contraseña que define la persona>" }
```

`200` aceptada, devuelve sesión · `401` token inválido o expirado · `400` validación.

> Este flujo tuvo un bug de contexto de tenant que ya fue corregido, con un guardrail de prueba que
> replica producción. Vale la pena ejercitarlo de punta a punta: invitar desde
> `POST /api/v1/company/users`, recibir el correo, aceptar, y comprobar que el token resultante trae
> el `tid` **de la empresa que invitó**.

Reenvío de invitación: `POST /api/v1/company/users/{publicId}/reset-invitation`.

---

### 1.8 · Logout — `POST /api/v1/auth/logout`

```bash
curl -s -X POST "$API/api/v1/auth/logout" \
  -H "Authorization: Bearer $TOKEN" \
  -o /dev/null -w "status=%{http_code}\n"
```

`204` sesión cerrada · `401` sin token o token inválido.

**Comprobación clave — qué invalida realmente el logout:**

```bash
# ¿el access token sigue sirviendo?
curl -s "$API/api/v1/account/companies" -H "Authorization: Bearer $TOKEN" \
  -o /dev/null -w "access tras logout=%{http_code}\n"

# ¿el refresh token sigue sirviendo?
curl -s -X POST "$API/api/v1/auth/refresh" -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH\"}" -o /dev/null -w "refresh tras logout=%{http_code}\n"
```

Lo esperable es que el **refresh** quede invalidado (`401`) y que el **access token siga siendo
válido hasta que expire** — es la naturaleza de un JWT sin lista de revocación. Anotar el resultado:
si el access token sigue funcionando, la ventana real de cierre de sesión son esos 15 minutos, y eso
hay que tenerlo claro antes de prometer "cierre de sesión inmediato".

---

## Registro de la corrida — sección 1

| Paso | Endpoint | Esperado | Obtenido | Fecha | Notas |
|---|---|---|---|---|---|
| 1.1 | `auth/register` | `202` | *no ejecutado* | | ya validado antes |
| 1.2 | `auth/login` | `200` | | | |
| 1.2n | `auth/login` (mala contraseña) | `401` | | | |
| 1.3 | `auth/refresh` | `200` | | | ¿rota el refresh? |
| 1.3n | `auth/refresh` (reuso) | `401` | | | |
| 1.4a | `password-reset/request` | `202` | | | |
| 1.4a' | `password-reset/request` (repetido) | `429` | | | enfriamiento 2 min |
| 1.4b | enlace del correo | sin caracteres invisibles | | | |
| 1.4c | `password-reset/validate` | `200` | | | |
| 1.4d | `password-reset/redeem` | `204` | | | |
| 1.4d' | `redeem` (token reusado) | `401` | | | |
| 1.5a | `email-verification/resend` | `202` | | | |
| 1.5b | `email-verification/confirm` | `200` | | | |
| 1.6 | `auth/external` | `200`/`201` | *no ejecutado* | | proveedor con problema abierto |
| 1.7 | `company-user-invitations/accept` | `200` | | | verificar `tid` |
| 1.8 | `auth/logout` | `204` | | | ¿access sigue vivo? |

---

## 2. Empresa y perfil legal

Arranca con sesión iniciada (paso 1.2). De acá sale el `companyPublicId` que usan todas las secciones
siguientes.

> **Bug conocido en este tramo.** El arreglo del `POST /legal-profile` —que devuelve `500` aunque el
> registro **sí se crea**— está en el árbol local pero **no commiteado ni desplegado**. Afecta al paso
> 2.10. Verificado con `git show HEAD` el 2026-08-04.

### 2.1 · Catálogos previos a crear la empresa

Cuatro lecturas que alimentan el formulario de creación. Todas requieren sesión pero **no** contexto de
empresa.

```bash
for c in countries company-types legal-representative-position-titles legal-representative-representation-types; do
  printf "%-45s " "$c"
  curl -s "$API/api/v1/account/companies/$c" -H "Authorization: Bearer $TOKEN" \
    -o "/tmp/cat-$c.json" -w "%{http_code}"
  python3 -c "
import json;d=json.load(open('/tmp/cat-$c.json'))
i=d.get('items',d) if isinstance(d,dict) else d
print(f'  ({len(i)} items)')" 2>/dev/null || echo
done
```

Los cuatro deben dar `200` con listas no vacías. **Si `countries` viene vacío no se puede crear ninguna
empresa** — es catálogo de sistema sembrado por migración.

Anotar el `code` del país (`SV`) y el `publicId` del tipo de empresa que corresponda; van en el paso
siguiente.

### 2.2 · Crear la empresa — `POST /api/v1/account/companies`

```jsonc
{
  "name": "Avianca El Salvador",
  "countryCode": "SV",
  "companyTypePublicId": "<de 2.1>",          // opcional
  "initialLegalRepresentative": {
    "firstName": "…",
    "lastName": "…",
    "documentType": "DUI",
    "documentNumber": "01234567-8",
    "positionTitle": "<de 2.1>",
    "representationType": "PrimaryLegalRepresentative",
    "effectiveFromUtc": "2026-01-01T00:00:00Z",
    "isPrimary": true,
    "authorityDescription": null,
    "appointmentInstrument": null,
    "appointmentDateUtc": null,
    "effectiveToUtc": null,
    "email": null,
    "phone": null
  }
}
```

`201` creada · `400` validación · `409` límite de empresas activas alcanzado · `422` país o tipo
inválido.

> **El representante legal inicial es obligatorio y se crea junto con la empresa.** Si se manda con
> datos de relleno queda registrado así: en el ambiente actual apareció uno llamado *"Tempora ral de
> empr"*, generado con los datos del usuario creador. Poner datos reales acá evita tener que
> corregirlo después.

**Guardar el `publicId` devuelto:**

```bash
export COMPANY=$(python3 -c "import json;print(json.load(open('/tmp/company.json'))['publicId'])")
```

Anotar también el `slug`: se deriva del nombre **al crear** y **no se renombra después**. Si la empresa
nació como "Empresa 1", el slug queda `empresa-1` para siempre aunque luego se renombre.

### 2.3 · Listar y ver el detalle

```bash
curl -s "$API/api/v1/account/companies" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
curl -s "$API/api/v1/account/companies/$COMPANY" -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

El detalle (`AccountCompanyDetailResponse`) trae `publicId`, `name`, `slug`, `countryCode`, `status`,
`planCode`, `isActiveContext`, `isOwnedByCurrentUser`, `concurrencyToken`, `activeLegalRepresentatives`
y `companyType`.

Comprobar `isOwnedByCurrentUser: true` y guardar el `concurrencyToken` — hace falta para editar la
empresa con `PUT`/`PATCH`.

### 2.4 · Cambiar el contexto de empresa — `POST .../{companyPublicId}/switch`

**Este paso es el que más se olvida y el que más confunde después.** El token del login trae un `tid`
fijo; para operar sobre otra empresa hay que pedir un token nuevo.

```bash
curl -s -X POST "$API/api/v1/account/companies/$COMPANY/switch" \
  -H "Authorization: Bearer $TOKEN" -o /tmp/switch.json -w "status=%{http_code}\n"
export TOKEN=$(python3 -c "import json;print(json.load(open('/tmp/switch.json'))['accessToken'])")
```

Sin cuerpo de request. Devuelve `SwitchActiveCompanyResponse`: `accessToken`, `refreshToken`,
`expiresIn`, `activeCompany` y `accessContext`.

`200` cambiado · `401` sin sesión · `403` la empresa no es del usuario · `404` no existe · `409`
conflicto de estado (p. ej. empresa archivada).

**Comprobar que el `tid` cambió:**

```bash
python3 -c "
import json,base64
t='$TOKEN'.split('.')[1]; t+='='*(-len(t)%4)
print('tid =', json.loads(base64.urlsafe_b64decode(t)).get('tid'))
print('coincide con COMPANY:', json.loads(base64.urlsafe_b64decode(t)).get('tid')=='$COMPANY')
"
```

> Si el `tid` no coincide con el `companyPublicId` de la URL, **todos** los endpoints de empresa
> responden `403` de desajuste de tenant. No `404`, que es lo que uno esperaría. Es el diagnóstico
> número uno cuando "el endpoint no funciona".

### 2.5 · Contexto de acceso — `GET .../{companyPublicId}/access-context`

Devuelve el plan y complementos activos, las capacidades y módulos efectivos, y los roles, permisos y
alcances del usuario dentro de esa empresa.

```bash
curl -s "$API/api/v1/account/companies/$COMPANY/access-context" \
  -H "Authorization: Bearer $TOKEN" | python3 -m json.tool
```

Es la referencia para diagnosticar cualquier `403` posterior: acá se ve si el permiso que exige el
endpoint está o no en la lista. Requiere ser dueño de la empresa; una ajena da `403`/`404`.

**Comprobar que aparezcan** `CompanyPreferences.Admin` o `iam.administration.manage` — son los que
gobiernan preferencias y perfil legal (paso 2.10).

### 2.6 · Qué sembró el aprovisionamiento por sí solo

Al crear la empresa se ejecutan cuatro plantillas automáticamente. **No es un paso a ejecutar, es un
paso a verificar.** La regla que las gobierna: se siembra **solo lo que fija la ley o la geografía**,
nunca una suposición sobre cómo está organizada esta empresa en particular. Ninguno de estos catálogos
tiene `DELETE` —solo `activate`/`inactivate`—, así que un genérico sembrado quedaría como ruido
permanente en todo tenant que no calce con él.

| Qué se sembró | Verificar con |
|---|---|
| Jerarquía de ubicaciones del país | `GET /api/v1/companies/{c}/location-levels` |
| Tipos y riesgos de incapacidad (ISSS) | `GET /api/v1/companies/{c}/incapacity-risks` |
| Asuetos nacionales **del año en curso únicamente** | `GET /api/v1/companies/{c}/company-holidays` |
| Tipos de horas extra `HED`/`HEN`/`HEDF`/`HENF` | `GET /api/v1/companies/{c}/overtime-types` |
| Una jornada: `JORNADA_ORDINARIA` (44 h) | `GET /api/v1/companies/{c}/work-schedules` |

> Los multiplicadores de horas extra (`HED` ×2.00, `HEN` ×2.50, `HEDF` ×4.00, `HENF` ×5.00) los fija el
> Código de Trabajo. Aparecen editables, pero cambiar los números deja la planilla calculando mal sin
> que nada proteste. Verificar que lleguen con esos valores.

**Y lo que NO llega sembrado** — todos estos `GET` deben devolver `totalCount: 0` en una empresa recién
creada. No es un bug: es la contraparte de la regla de arriba.

| Qué llega vacío | Dónde se carga |
|---|---|
| Tipos de unidad y áreas funcionales | **3.4** — sin esto no existe organigrama, ver el aviso de esa sección |
| Catálogos de descripción de puestos: funciones, tipos de contrato, objetivos estratégicos, equipo, responsabilidades, clasificaciones y categorías | sección 4 |
| Marco de competencias: tipos de competencia y **escala de calificación** | sección 4 — `GET .../competency-rating-scale` responde `isConfigured: false` hasta que crees una |
| Reconocimientos, amonestaciones y sus causas | sección 5 |
| Justificaciones de horas extra | sección 5 |
| Tramos de Renta (ISR) | **5.5** — sin ellos el ISR sale en `$0.00` sin avisar |

> **Ojo con el asistente de `/setup`.** Marcar un paso como "completado" porque el catálogo existe no
> sirve de nada si el catálogo está vacío: hay que verificar contenido, no existencia.

### 2.7 · Preferencias — `GET` / `PUT` `/api/v1/companies/{companyPublicId}/preferences`

```bash
curl -s "$API/api/v1/companies/$COMPANY/preferences" -H "Authorization: Bearer $TOKEN" \
  -o /tmp/prefs.json -w "status=%{http_code}\n"
export PREF_CT=$(python3 -c "import json;print(json.load(open('/tmp/prefs.json'))['concurrencyToken'])")
```

El `PUT` es **reemplazo total** y exige `If-Match`. Omitir un campo opcional lo devuelve a su valor
legal por defecto — no lo deja como estaba.

```bash
curl -s -X PUT "$API/api/v1/companies/$COMPANY/preferences" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -H "If-Match: $PREF_CT" \
  -d '{"currencyCode":"USD","timeZone":"America/El_Salvador","companyRestDayOfWeek":0,
       "annualVacationDaysDefault":15,"employerCoveredIncapacityDaysPerYear":9}' \
  -o /tmp/prefs2.json -w "status=%{http_code}\n"
```

`200` · `400` sin `If-Match` · `409` token desactualizado · `422` valor inválido (zona horaria o moneda
inexistente).

| Preferencia | Valor para El Salvador |
|---|---|
| `currencyCode` | `USD` |
| `timeZone` | `America/El_Salvador` — **el valor de fábrica es `UTC`** y afecta el corte de los periodos de planilla |
| `companyRestDayOfWeek` | `0` = domingo |
| `annualVacationDaysDefault` | `15` (Art. 177 CT) |
| `employerCoveredIncapacityDaysPerYear` | `9` |

**Casos negativos:** `PUT` sin `If-Match` → `400`; con un token viejo → `409`. Comprobar además que el
`concurrencyToken` rota tras cada guardado.

> `payrollComplianceGatesEnabled` **no está en el request público** — es deliberado. No hay forma de
> encenderlo por API.

Existe también `PATCH` con `application/json-patch+json` (RFC 6902) para cambios parciales, con el
mismo `If-Match`.

### 2.8 · Representantes legales

```
GET    /api/v1/companies/{companyPublicId}/legal-representatives
POST   /api/v1/companies/{companyPublicId}/legal-representatives
GET    /api/v1/legal-representatives/{publicId}
PUT    /api/v1/legal-representatives/{publicId}
PATCH  /api/v1/legal-representatives/{publicId}
PATCH  /api/v1/legal-representatives/{publicId}/activate
PATCH  /api/v1/legal-representatives/{publicId}/inactivate
PATCH  /api/v1/legal-representatives/{publicId}/set-primary
GET    /api/v1/legal-representatives/{publicId}/usage
GET    /api/v1/companies/{companyPublicId}/legal-representatives/export
```

Empezar listando: debe aparecer el creado en 2.2.

**Reglas que conviene ejercitar, porque son invariantes de base de datos:**

| Prueba | Esperado |
|---|---|
| Crear un segundo representante | `201` — una empresa puede tener varios |
| Marcar un segundo como principal con `set-primary` | El anterior deja de serlo. **Solo puede haber un principal activo por empresa** (índice único parcial) |
| `PUT` sobre un representante con `isPrimary: true` | Igual que `set-primary`: promueve y degrada al anterior |
| Crear otro con el mismo `documentType` + `documentNumber` | Conflicto — la unicidad de documento es **por empresa** |
| `inactivate` sobre el único activo | Verificar si se permite; una empresa sin representante activo no puede firmar reportes |
| `usage` antes de inactivar | Muestra dónde está referenciado |

> **La misma persona puede representar a varias empresas**, pero como filas distintas, una por empresa,
> cada una con su propio `publicId`. No hay una entidad "persona" compartida.

> **Bug encontrado el 2026-08-04 al ejecutar este paso — arreglado, pendiente de desplegar.**
> `PATCH .../set-primary` y `PUT` con `isPrimary: true` devolvían `500` cuando ya había otro principal
> activo. En producción el detalle sale enmascarado como *"An unexpected error occurred"*; contra un
> ambiente local el mensaje real es `Unique constraint 'ux_legal_representatives__tenant_primary_active'
> was violated`.
>
> Los tres handlers que mueven la bandera degradaban al principal vigente y promovían al nuevo en un
> **único `SaveChanges`**. El índice es parcial, se evalúa por sentencia y no es diferible, así que si EF
> ordena la promoción primero quedan dos filas `(principal, activo)` por un instante y Postgres rechaza
> el lote. La creación con `isPrimary: true` sobrevivía sólo por el orden que EF elegía — no por diseño.
>
> El arreglo vacía la degradación antes de la promoción, dentro de la misma transacción, en las tres
> rutas. Cubierto por `ApiIntegrationTests.LegalRepresentativePrimary.cs`, que falla sin el arreglo.
> **Este tipo de fallo solo se detecta contra una base real** — un test unitario con repositorio falso
> nunca emite el SQL.

### 2.9 · Perfil legal patronal — el tramo con más aristas

```
GET   /api/v1/companies/{companyPublicId}/legal-profile
POST  /api/v1/companies/{companyPublicId}/legal-profile
PUT   /api/v1/companies/{companyPublicId}/legal-profile
```

**a) Estado inicial** — debe dar `404`:

```bash
curl -s "$API/api/v1/companies/$COMPANY/legal-profile" -H "Authorization: Bearer $TOKEN" \
  -o /tmp/lp.json -w "status=%{http_code}\n"
```

`404` con `code: COMPANY_LEGAL_PROFILE_NOT_FOUND` **no es un error**: es el estado de toda empresa
nueva. El perfil no se crea al aprovisionar.

**b) Crear** — `POST`:

```bash
curl -s -X POST "$API/api/v1/companies/$COMPANY/legal-profile" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"legalName":"Avianca El Salvador, S.A. de C.V.",
       "employerNitNumber":"0614-010180-101-2",
       "isssEmployerRegistrationNumber":"123456-7",
       "fiscalAddress":"Km 42 Carretera al Aeropuerto, San Luis Talpa, La Paz",
       "economicActivityDescription":"Transporte aéreo de pasajeros y carga",
       "legalRepresentativePublicId":"<el de 2.8>"}' \
  -D /tmp/lp-headers.txt -o /tmp/lp2.json -w "status=%{http_code}\n"
```

**Esperado hoy en producción: `500`** con `detail: "No route matches the supplied values."` — **y el
registro sí queda creado**. Confirmarlo con un `GET` inmediato. Cuando se despliegue el arreglo debe
dar `201` con cabeceras `Location` y `ETag`.

**c) Validaciones nuevas** (estas sí están desplegadas):

| Payload | Esperado |
|---|---|
| `employerNitNumber: "06140101801012"` (sin guiones) | `400`, mensaje sobre el formato `####-######-###-#` |
| `isssEmployerRegistrationNumber: "ABC XYZ"` | `400`, mensaje sobre dígitos y guiones |
| `legalRepresentativePublicId` inventado | `422 COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_NOT_FOUND` |
| Representante inactivo | `422 COMPANY_LEGAL_PROFILE_LEGAL_REPRESENTATIVE_INACTIVE` |
| Representante de otra empresa | `403` desajuste de tenant |
| `POST` cuando ya existe | `409 COMPANY_LEGAL_PROFILE_ALREADY_EXISTS` |

> Los mensajes por campo llegan bajo la clave **vacía** de `errors`, no bajo el nombre del campo:
> `"errors": { "": ["El NIT patronal debe seguir el formato ####-######-###-#."] }`. El frontend no
> puede mapearlos al input automáticamente.

**d) Actualizar** — `PUT` con `If-Match`:

`400` sin la cabecera · `409 COMPANY_LEGAL_PROFILE_CONCURRENCY_CONFLICT` con un token viejo · `200` con
el token vigente y el `concurrencyToken` rotado en la respuesta.

**e) Nombres del contrato** — dos renombres que el código fuente no revela:

- El parámetro de ruta es **`companyPublicId`**, aunque el controller lo declare `companyId`.
- La respuesta expone **`publicId`**, aunque el record de C# lo declare `Id`.

Los reescribe `PublicContractRouteConvention`. Guiarse por el `openapi.yaml`, no por el código.

### 2.10 · Verificación en base de datos

Cerrar la sección confirmando que los datos quedaron bien grabados:

```bash
psql "<connection-string>" -f docs/technical/operations/scripts/verificar-perfil-legal.sql
```

Solo lectura. La sección 4 del script (integridad) debe devolver **cero filas**.

---

## Registro de la corrida — sección 2

| Paso | Endpoint | Esperado | Obtenido | Fecha | Notas |
|---|---|---|---|---|---|
| 2.1 | catálogos previos | `200` ×4, no vacíos | | | |
| 2.2 | `POST account/companies` | `201` | | | anotar `publicId` y `slug` |
| 2.3 | `GET account/companies/{c}` | `200` | | | `isOwnedByCurrentUser: true` |
| 2.4 | `POST .../switch` | `200` | | | **verificar que el `tid` cambió** |
| 2.5 | `GET .../access-context` | `200` | | | ¿está `CompanyPreferences.Admin`? |
| 2.6 | plantillas sembradas | 7 presentes | | | multiplicadores HE intactos |
| 2.7 | `GET/PUT preferences` | `200` | | | zona horaria pasa de `UTC` a SV |
| 2.7n | `PUT` sin `If-Match` | `400` | | | |
| 2.7n | `PUT` con token viejo | `409` | | | |
| 2.8 | representantes legales | `200`/`201` | | | un solo principal activo |
| 2.9a | `GET legal-profile` (inicial) | `404` | | | no es error |
| 2.9b | `POST legal-profile` | `500` hoy / `201` tras desplegar | | | **el registro se crea igual** |
| 2.9c | NIT mal formado | `400` | | | |
| 2.9c | ISSS mal formado | `400` | | | |
| 2.9c | representante inventado | `422` | | | |
| 2.9d | `PUT` sin `If-Match` | `400` | | | |
| 2.10 | SQL de verificación | 0 anomalías | | | |

---

## 3. Estructura organizativa

Cuatro bloques con dependencias estrictas entre sí. **Ejecutarlos en orden** — cada uno necesita
identificadores del anterior:

```
geografía ──► centros de trabajo
catálogos de estructura ──► unidades organizativas
tipos de centro de costo ──► centros de costo
```

Requiere el token con el `tid` de la empresa (paso 2.4).

> **`normalizedCode` aparece en los esquemas de request pero está marcado `readOnly`.** Lo inyecta
> `PublicContractSchemaFilter` para documentar que existe en la respuesta. **No hay que enviarlo** — el
> servidor lo deriva del `code`. Si el cliente del frontend se generó sin respetar `readOnly`, va a
> mandarlo de más.

> Todos los códigos se normalizan y son **únicos por empresa**. Crear dos con el mismo `code` da
> conflicto, y ahí es donde conviene probar los casos negativos.

### 3.1 · Geografía — ya viene sembrada, se verifica

```
GET  /api/v1/companies/{c}/location-levels
GET  /api/v1/companies/{c}/location-hierarchy      PUT para reconfigurar
GET  /api/v1/companies/{c}/location-groups         POST para agregar
GET  /api/v1/companies/{c}/location-groups/tree
GET  /api/v1/location-groups/{id}/children
GET  /api/v1/location-groups/{id}/path
GET  /api/v1/location-groups/{id}/usage
PATCH /api/v1/location-groups/{id}/move
PATCH /api/v1/location-groups/{id}/activate | /inactivate
```

El aprovisionamiento siembra la jerarquía del país (para El Salvador: País → Departamento →
Municipio) con sus 14 departamentos y municipios.

**Verificar antes de tocar nada:**

```bash
curl -s "$API/api/v1/companies/$COMPANY/location-levels" -H "Authorization: Bearer $TOKEN" \
  | python3 -m json.tool | head -30
curl -s "$API/api/v1/companies/$COMPANY/location-groups/tree" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "import json,sys; d=json.load(sys.stdin); print('nodos raíz:', len(d if isinstance(d,list) else d.get('items',[])))"
```

| Verificación | Esperado |
|---|---|
| `location-levels` | 3 niveles activos, ordenados |
| `location-groups/tree` | Un solo nodo raíz (el país) |
| `path` sobre un municipio | La cadena completa hasta el país |

**No modificar la jerarquía si ya hay centros de trabajo colgando.** El `PUT` de
`location-hierarchy` reconfigura los niveles; probar `move` e `inactivate` **antes** de crear centros
de trabajo, o con grupos que no estén en uso. `usage` dice si un grupo está referenciado.

### 3.2 · Tipos de centro de trabajo

```
GET,POST /api/v1/companies/{c}/work-center-types
GET,PUT,PATCH /api/v1/work-center-types/{id}
PATCH /api/v1/work-center-types/{id}/activate | /inactivate
```

```jsonc
{
  "code": "ESTACION_AEROPUERTO",
  "name": "Estación aeroportuaria",
  "description": null,
  "requiresAddress": true,
  "requiresGeo": false,
  "allowsBiometric": true
}
```

Los tres booleanos son reglas que se aplican al crear centros de trabajo de ese tipo. **Vale la pena
probar que se respetan**: crear un tipo con `requiresAddress: true` y luego un centro de trabajo sin
`address` debe ser rechazado.

**Conjunto de datos** — cinco tipos, con combinaciones distintas de banderas a propósito:

| `code` | `name` | `requiresAddress` | `requiresGeo` | `allowsBiometric` |
|---|---|---|---|---|
| `ESTACION_AEROPUERTO` | Estación aeroportuaria | ✅ | ✅ | ✅ |
| `HANGAR` | Hangar de mantenimiento | ✅ | ✅ | ✅ |
| `OFICINA` | Oficina corporativa | ✅ | ❌ | ✅ |
| `TERMINAL_CARGA` | Terminal de carga | ✅ | ❌ | ❌ |
| `CENTRO_ENTRENAMIENTO` | Centro de entrenamiento | ❌ | ❌ | ❌ |

Las tres últimas filas son las que permiten probar que cada bandera se evalúa por separado: un
`CENTRO_ENTRENAMIENTO` debe aceptarse **sin** dirección, mientras que un `TERMINAL_CARGA` sin dirección
debe rechazarse.

### 3.3 · Centros de trabajo

```
GET,POST /api/v1/companies/{c}/work-centers
GET,PUT,PATCH /api/v1/work-centers/{id}
PATCH /api/v1/work-centers/{id}/activate | /inactivate | /reassign-group
```

```jsonc
{
  "code": "SAL-EST",
  "name": "Estación SAL — Aeropuerto Int. Mons. Óscar A. Romero",
  "workCenterTypePublicId": "<de 3.2>",
  "locationGroupPublicId": "<el municipio, de 3.1>",   // obligatorio
  "address": "Km 42 Carretera al Aeropuerto",
  "geoLat": null, "geoLong": null,
  "phone": null, "email": null, "notes": null
}
```

> **`locationGroupPublicId` es obligatorio**: un centro de trabajo siempre cuelga de un nodo
> geográfico. Por eso 3.1 va antes.

**Conjunto de datos** — cinco sedes en tres municipios distintos:

| `code` | `name` | Tipo | Municipio | Dirección |
|---|---|---|---|---|
| `SAL-EST` | Estación SAL — Aeropuerto Int. Mons. Óscar A. Romero | `ESTACION_AEROPUERTO` | San Luis Talpa, La Paz | Km 42 Carretera al Aeropuerto |
| `SAL-HGR` | Hangar de Mantenimiento SAL | `HANGAR` | San Luis Talpa, La Paz | Km 42, zona técnica |
| `SAL-CRG` | Terminal de Carga SAL | `TERMINAL_CARGA` | San Luis Talpa, La Paz | Km 42, terminal de carga |
| `SS-CORP` | Oficina Corporativa San Salvador | `OFICINA` | San Salvador, San Salvador | Col. Escalón, Av. Norte |
| `SS-CAP` | Centro de Entrenamiento | `CENTRO_ENTRENAMIENTO` | Antiguo Cuscatlán, La Libertad | *(sin dirección — el tipo no la exige)* |

Tres sedes en el mismo municipio y dos en municipios distintos: permite probar que `reassign-group` mueve
una sede entre nodos geográficos y que el agrupamiento por ubicación en los reportes distingue bien.

| Prueba | Esperado |
|---|---|
| Crear sin `locationGroupPublicId` | Rechazo de validación |
| Crear con un tipo que exige dirección, sin `address` | Rechazo |
| `reassign-group` a otro municipio | `200`, y el centro cambia de nodo |
| `reassign-group` a un grupo inactivo o inexistente | Rechazo |
| Código repetido dentro de la empresa | Conflicto |

### 3.4 · Catálogos de estructura — construirlos desde cero

```
GET,POST /api/v1/companies/{c}/organization-structure-catalogs/unit-types
GET,POST /api/v1/companies/{c}/organization-structure-catalogs/functional-areas
GET,PUT  /api/v1/organization-structure-catalogs/unit-types/{id}
PATCH    /api/v1/organization-structure-catalogs/unit-types/{id}/activate | /inactivate
```

> **Los dos catálogos llegan VACÍOS.** Una empresa nueva no trae ni un tipo de unidad ni un área
> funcional. Cómo se divide una empresa no es algo que el sistema pueda adivinar, y ninguno de estos
> catálogos tiene `DELETE` —solo `activate`/`inactivate`—, así que un genérico sembrado sería ruido
> permanente en todos los tenants que no calcen con él. Confirmalo antes de empezar: los dos `GET`
> deben devolver `totalCount: 0`.

Cuerpo (`UpsertCatalogItemRequest`): `code`, `name`, `description?`, `sortOrder`.

> **Este paso es un bloqueo duro, no una comodidad.** `orgUnitTypePublicId` es obligatorio al crear una
> unidad organizativa (`Guid` no anulable en `CreateOrgUnitRequest`). Mientras esta sección esté vacía
> **no se puede crear ni una sola unidad organizativa**, y con ella se cae todo lo que cuelga del
> organigrama: puestos, plazas y asignaciones. En el frontend el paso "Tipos de unidad" del asistente de
> `/setup` tiene que ser obligatorio y bloquear los siguientes, no marcarse como completado por defecto.

#### 3.4.0 · Conjunto de datos — tipos de unidad (los escalones)

Ocho escalones, de arriba hacia abajo. El `sortOrder` es lo que define la jerarquía visual: **cargarlos con
estos valores es lo que permite verificar que el árbol respeta los niveles.**

| Escalón | `code` | `name` | `sortOrder` |
|---|---|---|---|
| 1 | `DIRECCION_GENERAL` | Dirección General | 10 |
| 2 | `VICEPRESIDENCIA` | Vicepresidencia | 20 |
| 3 | `DIRECCION` | Dirección | 30 |
| 4 | `GERENCIA` | Gerencia | 40 |
| 5 | `JEFATURA` | Jefatura | 50 |
| 6 | `DEPARTAMENTO` | Departamento | 60 |
| 7 | `AREA` | Área | 70 |
| — | `BASE` | Base / Estación | 80 |

`BASE` queda fuera de la escala a propósito: es una unidad **transversal** (una estación aeroportuaria no
está por encima ni por debajo de una gerencia). Sirve para probar que el árbol admite un tipo que no encaja
en la cadena de mando.

**Áreas funcionales** — once, sin jerarquía entre sí:

| `code` | `name` | `sortOrder` |
|---|---|---|
| `OPS_VUELO` | Operaciones de Vuelo | 10 |
| `SERV_ABORDO` | Servicio a Bordo | 20 |
| `MANTENIMIENTO` | Mantenimiento e Ingeniería | 30 |
| `AEROPUERTOS` | Aeropuertos y Servicio en Tierra | 40 |
| `CARGA` | Carga | 50 |
| `SEG_OPERACIONAL` | Seguridad Operacional y Calidad | 60 |
| `COMERCIAL` | Comercial y Ventas | 70 |
| `FINANZAS` | Finanzas y Administración | 80 |
| `GENTE` | Gente y Cultura | 90 |
| `TECNOLOGIA` | Tecnología | 100 |
| `LEGAL` | Legal y Cumplimiento | 110 |

> `GENTE` es la que va en `hrFunctionalAreaCode` de las preferencias (paso 2.7); alimenta el indicador de
> ratio de RRHH del tablero.

Carga en lote:

```bash
for row in "DIRECCION_GENERAL|Dirección General|10" "VICEPRESIDENCIA|Vicepresidencia|20" \
           "DIRECCION|Dirección|30" "GERENCIA|Gerencia|40" "JEFATURA|Jefatura|50" \
           "DEPARTAMENTO|Departamento|60" "AREA|Área|70" "BASE|Base / Estación|80"; do
  IFS='|' read -r code name order <<< "$row"
  printf "%-20s " "$code"
  curl -s -X POST "$API/api/v1/companies/$COMPANY/organization-structure-catalogs/unit-types" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d "{\"code\":\"$code\",\"name\":\"$name\",\"description\":null,\"sortOrder\":$order}" \
    -o /dev/null -w "%{http_code}\n"
done
```

Los ocho tienen que dar **`201`**. Un `409 ORG_STRUCTURE_CATALOG_CODE_CONFLICT` significa que ese código
ya existe en el tenant —el chequeo de duplicados ignora si está activo o inactivo—: revisá que estés
apuntando a una empresa recién creada.

#### 3.4.1 · Reasignar el tipo de una unidad y retirar el viejo — receta completa

Un catálogo mal cargado no se deshace borrando: **no existe `DELETE` en ninguno de estos catálogos.**
Solo hay `activate` / `inactivate`. Un tipo inactivado sigue en la base de datos y sigue siendo visible
para las filas históricas que lo referencian; lo único que cambia es que deja de poder elegirse. Por eso
conviene ensayar esta receta: es la única salida cuando te equivocaste de escalón.

Para probarla creá un tipo desechable (por ejemplo `PROVISIONAL`), asignáselo a una unidad, y después
migrá la unidad a su tipo definitivo. Son tres movimientos, y el del medio tiene una trampa:

**Paso 1 — Crear el catálogo nuevo**

```bash
curl -s -X POST "$API/api/v1/companies/$COMPANY/organization-structure-catalogs/unit-types" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"code":"VICEPRESIDENCIA","name":"Vicepresidencia","description":null,"sortOrder":20}' \
  -w "\nstatus=%{http_code}\n"
```

**Paso 2 — Reasignar lo que apunta al viejo**

> **Trampa: el tipo de una unidad organizativa NO es parcheable.** El `PATCH` de
> `/organization-units/{id}` solo admite `/name`, `/sortOrder` y `/description`. Para cambiar
> `orgUnitTypePublicId` o `functionalAreaPublicId` hay que usar el **`PUT` completo**, que es
> reemplazo total: omitir un campo opcional lo borra.

```bash
# a) traer el estado actual, incluido el concurrencyToken
curl -s "$API/api/v1/organization-units/$UNIT" -H "Authorization: Bearer $TOKEN" -o /tmp/u.json
CT=$(python3 -c "import json;print(json.load(open('/tmp/u.json'))['concurrencyToken'])")

# b) reenviar TODO, cambiando solo el tipo
python3 - <<'PY' > /tmp/u-put.json
import json
d = json.load(open('/tmp/u.json'))
print(json.dumps({
    "code":                    d["code"],
    "name":                    d["name"],
    "orgUnitTypePublicId":     "<publicId del tipo NUEVO>",
    "functionalAreaPublicId":  d.get("functionalAreaPublicId"),
    "sortOrder":               d.get("sortOrder"),
    "description":             d.get("description"),
    "costCenterCode":          d.get("costCenterCode"),
    "managerEmployeePublicId": d.get("managerEmployeePublicId"),
}))
PY

curl -s -X PUT "$API/api/v1/organization-units/$UNIT" \
  -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -H "If-Match: $CT" --data @/tmp/u-put.json -w "\nstatus=%{http_code}\n"
```

> **`parentPublicId` no viaja en el `PUT`.** El padre se cambia únicamente con
> `PATCH /organization-units/{id}/move`, cuerpo `{ "newParentPublicId": "...", "sortOrder": 10 }`.
> Reasignar el tipo y mover en el árbol son dos operaciones distintas.

**Paso 3 — Inactivar el tipo viejo**

```bash
curl -s -X PATCH "$API/api/v1/organization-structure-catalogs/unit-types/$VIEJO/inactivate" \
  -H "Authorization: Bearer $TOKEN" -H "If-Match: $CT_VIEJO" -w "\nstatus=%{http_code}\n"
```

Si queda algo apuntando, responde **`ORG_STRUCTURE_CATALOG_IN_USE`**. Verificado en el código: el
handler bloquea si `HasOrgUnitsUsingOrgUnitTypeAsync` **o**
`HasPositionCategoryClassificationsUsingOrgUnitTypeAsync` devuelven verdadero.

> **Dos cosas distintas pueden bloquearlo.** Aunque reasignes todas las unidades organizativas, una
> **clasificación de categoría de puesto** (sección 4) puede seguir reteniendo el tipo. Si el paso 3
> falla y las unidades ya están migradas, buscá por ahí.

#### 3.4.2 · Las protecciones no son iguales en los tres módulos

Verificado leyendo cada handler de inactivación:

| Recurso | Cambiar el tipo con `PATCH` | Bloqueo al inactivar el tipo si está en uso |
|---|---|---|
| Unidad organizativa | ❌ solo `PUT` completo | ✅ `ORG_STRUCTURE_CATALOG_IN_USE` (unidades + clasificaciones de categoría) |
| Área funcional | ❌ solo `PUT` completo | ✅ `ORG_STRUCTURE_CATALOG_IN_USE` (unidades) |
| Centro de costo | ✅ `/costCenterTypeId` es parcheable | ✅ `COST_CENTER_TYPE_IN_USE` |
| Centro de trabajo | ❌ el tipo no está entre las rutas parcheables | ⚠️ **sin bloqueo** — `InactivateWorkCenterTypeCommandHandler` va de la verificación de concurrencia directo a inactivar |

> **La última fila es una asimetría, no necesariamente un error.** Puede ser deliberado —el centro de
> trabajo conserva su referencia y el tipo solo deja de ofrecerse para nuevos— pero como sus dos
> hermanos sí bloquean, conviene confirmarlo con el equipo. **Prueba concreta:** inactivar un
> `work-center-type` que tenga centros activos usándolo y anotar qué pasa; después consultar esos
> centros y ver si siguen operativos.

### 3.5 · Centros de costo

Son **dos pasos encadenados**: primero los tipos, después los centros. Un centro exige
`costCenterTypePublicId`, así que sin tipos no se puede crear ninguno.

```
3.5.a  tipos    →  3.5.b  centros    →  3.6  unidades organizativas
```

#### 3.5.a · Tipos de centro de costo

```
GET,POST      /api/v1/companies/{companyPublicId}/cost-center-types
GET,PUT,PATCH /api/v1/cost-center-types/{publicId}
PATCH         /api/v1/cost-center-types/{publicId}/activate | /inactivate
```

```jsonc
{
  "code": "OPERATIVO",
  "name": "Operativo",
  "description": null
}
```

> `normalizedCode` aparece en el esquema marcado `readOnly` — **no enviarlo**, lo deriva el servidor.

**Conjunto de datos** — cuatro tipos:

| `code` | `name` |
|---|---|
| `OPERATIVO` | Operativo |
| `TECNICO` | Técnico |
| `COMERCIAL` | Comercial |
| `ADMINISTRATIVO` | Administrativo |

```bash
for row in "OPERATIVO|Operativo" "TECNICO|Técnico" "COMERCIAL|Comercial" "ADMINISTRATIVO|Administrativo"; do
  IFS='|' read -r code name <<< "$row"
  printf "%-16s " "$code"
  curl -s -X POST "$API/api/v1/companies/$COMPANY/cost-center-types" \
    -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
    -d "{\"code\":\"$code\",\"name\":\"$name\",\"description\":null}" \
    -o /dev/null -w "%{http_code}\n"
done
```

Guardar los `publicId` devueltos: son el `costCenterTypePublicId` del paso siguiente.

| Prueba | Esperado |
|---|---|
| Código repetido en la misma empresa | Conflicto |
| `inactivate` de un tipo **sin** centros | `200` |
| `inactivate` de un tipo **con** centros | `COST_CENTER_TYPE_IN_USE` — probarlo después de 3.5.b |

#### 3.5.b · Centros de costo

```
GET,POST      /api/v1/companies/{companyPublicId}/cost-centers
GET           /api/v1/companies/{companyPublicId}/cost-centers/export
GET,PUT,PATCH /api/v1/cost-centers/{publicId}
PATCH         /api/v1/cost-centers/{publicId}/activate | /inactivate
GET           /api/v1/cost-centers/{publicId}/usage
```

```jsonc
{
  "code": "CC-1000",
  "name": "Operaciones de Vuelo",
  "costCenterTypePublicId": "<publicId del tipo, de 3.5.a>",
  "payrollExpenseAccountCode": null,
  "employerContributionAccountCode": null,
  "provisionAccountCode": null,
  "description": null
}
```

Los tres códigos contables son opcionales acá pero **alimentan la contabilización de la planilla**
(sección 8). Si el equipo contable ya los tiene definidos, cargarlos ahora ahorra una segunda pasada.

> **A diferencia del tipo en las unidades organizativas, acá sí se puede corregir después:**
> `/costCenterTypeId` está entre las rutas parcheables de `PATCH /cost-centers/{publicId}`. Es la
> excepción — en unidades hay que hacer el `PUT` completo.

**Conjunto de datos** — catorce centros:

| `code` | `name` | Tipo |
|---|---|---|
| `CC-1000` | Operaciones de Vuelo | `OPERATIVO` |
| `CC-1100` | Servicio a Bordo | `OPERATIVO` |
| `CC-1200` | Aeropuertos SAL | `OPERATIVO` |
| `CC-1300` | Seguridad Operacional | `OPERATIVO` |
| `CC-2000` | Mantenimiento en Línea | `TECNICO` |
| `CC-2100` | Ingeniería y Planeación | `TECNICO` |
| `CC-2200` | Almacén Técnico | `TECNICO` |
| `CC-3000` | Ventas | `COMERCIAL` |
| `CC-3100` | Carga | `COMERCIAL` |
| `CC-4000` | Finanzas | `ADMINISTRATIVO` |
| `CC-4100` | Gente y Cultura | `ADMINISTRATIVO` |
| `CC-4200` | Tecnología | `ADMINISTRATIVO` |
| `CC-4300` | Legal | `ADMINISTRATIVO` |
| `CC-9000` | Dirección General | `ADMINISTRATIVO` |

| Prueba | Esperado |
|---|---|
| `usage` sobre un centro sin uso | Indica que se puede inactivar |
| `inactivate` de uno en uso | Rechazo — **probarlo después de la sección 5**, cuando haya asignaciones |
| `export` | Listado completo |

> El centro de costo se asigna en la **asignación del empleado**, no en la plaza. Su uso real recién
> se ve en la sección 5.

### 3.6 · Unidades organizativas — el árbol

```
GET,POST /api/v1/companies/{c}/organization-units
GET      /api/v1/companies/{c}/organization-units/tree
GET      /api/v1/companies/{c}/organization-units/graph
GET      /api/v1/companies/{c}/organization-units/export
GET      /api/v1/companies/{c}/organization-units/diagram-export
GET,PUT,PATCH /api/v1/organization-units/{id}
PATCH    /api/v1/organization-units/{id}/activate | /inactivate | /move
```

```jsonc
{
  "code": "VP-OPS",
  "name": "Vicepresidencia de Operaciones",
  "orgUnitTypePublicId": "<de 3.4>",
  "functionalAreaPublicId": "<de 3.4>",
  "parentPublicId": "<la unidad padre, null para la raíz>",
  "sortOrder": 20,
  "description": null,
  "costCenterCode": null,
  "managerEmployeePublicId": null              // se llena en la sección 5
}
```

**Crear de arriba hacia abajo**: cada unidad necesita que su padre exista.

#### 3.6.1 · Conjunto de datos — el organigrama completo

27 unidades, profundidad máxima **4 escalones**. La columna `Niv.` es la profundidad esperada en el árbol:
es contra eso que se verifica el resultado de `GET .../organization-units/tree`.

> **Por esto los centros de costo van antes (3.5).** `costCenterCode` es opcional en el contrato, pero el
> handler exige que —si viene con valor— corresponda a un centro de costo **activo**, o devuelve
> `ORG_UNIT_COST_CENTER_INVALID`. Y **no es parcheable**: el `PATCH` de unidades solo admite `/name`,
> `/sortOrder` y `/description`, así que completarlo después obliga a un `PUT` completo por unidad —
> `GET` para el token, reenviar todos los campos, `If-Match`— con riesgo de borrar un campo opcional por
> omisión. Cargando los centros primero, el organigrama entra de una sola pasada.

| Niv. | `code` | `name` | Tipo | Padre | Área funcional | Centro de costo |
|---|---|---|---|---|---|---|
| 1 | `DG` | Dirección General | `DIRECCION_GENERAL` | — | `FINANZAS` | `CC-9000` |
| 2 | `VP-OPS` | Vicepresidencia de Operaciones | `VICEPRESIDENCIA` | `DG` | `OPS_VUELO` | `CC-1000` |
| 3 | `GER-VUELO` | Gerencia de Operaciones de Vuelo | `GERENCIA` | `VP-OPS` | `OPS_VUELO` | `CC-1000` |
| **4** | `JEF-PILOTOS` | Jefatura de Pilotos | `JEFATURA` | `GER-VUELO` | `OPS_VUELO` | `CC-1000` |
| **4** | `DEP-DESPACHO` | Despacho y Control de Vuelo | `DEPARTAMENTO` | `GER-VUELO` | `OPS_VUELO` | `CC-1000` |
| 3 | `GER-ABORDO` | Gerencia de Servicio a Bordo | `GERENCIA` | `VP-OPS` | `SERV_ABORDO` | `CC-1100` |
| 3 | `GER-AEROP` | Gerencia de Aeropuertos | `GERENCIA` | `VP-OPS` | `AEROPUERTOS` | `CC-1200` |
| **4** | `DEP-RAMPA` | Rampa y Equipaje | `DEPARTAMENTO` | `GER-AEROP` | `AEROPUERTOS` | `CC-1200` |
| **4** | `DEP-COUNTER` | Counter y Sala de Abordaje | `DEPARTAMENTO` | `GER-AEROP` | `AEROPUERTOS` | `CC-1200` |
| **4** | `BASE-SAL` | Base San Salvador | `BASE` | `GER-AEROP` | `AEROPUERTOS` | `CC-1200` |
| 2 | `VP-TEC` | Vicepresidencia Técnica | `VICEPRESIDENCIA` | `DG` | `MANTENIMIENTO` | `CC-2000` |
| 3 | `GER-MTTO` | Gerencia de Mantenimiento en Línea | `GERENCIA` | `VP-TEC` | `MANTENIMIENTO` | `CC-2000` |
| 3 | `GER-ING` | Gerencia de Ingeniería y Planeación | `GERENCIA` | `VP-TEC` | `MANTENIMIENTO` | `CC-2100` |
| 3 | `DEP-ALMACEN` | Almacén Técnico | `DEPARTAMENTO` | `VP-TEC` | `MANTENIMIENTO` | `CC-2200` |
| 2 | `DIR-SEG` | Dirección de Seguridad Operacional | `DIRECCION` | `DG` | `SEG_OPERACIONAL` | `CC-1300` |
| 2 | `VP-COM` | Vicepresidencia Comercial | `VICEPRESIDENCIA` | `DG` | `COMERCIAL` | `CC-3000` |
| 3 | `GER-VENTAS` | Gerencia de Ventas | `GERENCIA` | `VP-COM` | `COMERCIAL` | `CC-3000` |
| 3 | `GER-CARGA` | Gerencia de Carga | `GERENCIA` | `VP-COM` | `CARGA` | `CC-3100` |
| 2 | `VP-FIN` | Vicepresidencia de Finanzas y Administración | `VICEPRESIDENCIA` | `DG` | `FINANZAS` | `CC-4000` |
| 3 | `DEP-CONTA` | Contabilidad | `DEPARTAMENTO` | `VP-FIN` | `FINANZAS` | `CC-4000` |
| 3 | `DEP-TESO` | Tesorería | `DEPARTAMENTO` | `VP-FIN` | `FINANZAS` | `CC-4000` |
| 3 | `DEP-COMPRAS` | Compras | `DEPARTAMENTO` | `VP-FIN` | `FINANZAS` | `CC-4000` |
| 2 | `DIR-GENTE` | Dirección de Gente y Cultura | `DIRECCION` | `DG` | `GENTE` | `CC-4100` |
| 3 | `DEP-ADMPER` | Administración de Personal | `DEPARTAMENTO` | `DIR-GENTE` | `GENTE` | `CC-4100` |
| 3 | `DEP-TALENTO` | Atracción de Talento | `DEPARTAMENTO` | `DIR-GENTE` | `GENTE` | `CC-4100` |
| 2 | `GER-TI` | Gerencia de Tecnología | `GERENCIA` | `DG` | `TECNOLOGIA` | `CC-4200` |
| 2 | `DIR-LEGAL` | Dirección Legal | `DIRECCION` | `DG` | `LEGAL` | `CC-4300` |

**Lo que este conjunto está diseñado para probar:**

| Caso | Dónde se ve |
|---|---|
| Profundidad de 4 escalones | La rama `DG → VP-OPS → GER-VUELO → JEF-PILOTOS` |
| Dos tipos distintos en el mismo nivel | `JEF-PILOTOS` (Jefatura) y `DEP-DESPACHO` (Departamento), ambos nivel 4 |
| Un escalón salteado | `GER-TI` cuelga directo de `DG` — de nivel 1 a Gerencia, sin Vicepresidencia |
| Tipo transversal | `BASE-SAL` con tipo `BASE` conviviendo con departamentos |
| Un departamento colgando de una Vicepresidencia | `DEP-ALMACEN` bajo `VP-TEC`, sin gerencia intermedia |
| Varias unidades con la misma área funcional | Las cuatro de `FINANZAS` |
| Varias unidades compartiendo centro de costo | `CC-1000` en cinco unidades de Operaciones de Vuelo |
| Una unidad con centro de costo propio | `DEP-ALMACEN` → `CC-2200` |

> **El salto de escalón y el departamento sin gerencia intermedia son deliberados.** Si el sistema exige
> que los tipos se respeten en orden estricto, estos dos casos deben fallar y hay que anotarlo: sería una
> regla no documentada. Si pasan, el árbol es libre y el `sortOrder` del tipo es solo presentación.

**Casos negativos del centro de costo:**

| Prueba | Esperado |
|---|---|
| `costCenterCode` que no existe (p. ej. `CC-9999`) | `ORG_UNIT_COST_CENTER_INVALID` |
| `costCenterCode` de un centro **inactivo** | `ORG_UNIT_COST_CENTER_INVALID` — el handler exige que esté activo |
| `costCenterCode` omitido o `null` | `201` — es opcional |
| `costCenterCode` en minúsculas (`cc-1000`) | Anotar: el handler normaliza a mayúsculas antes de buscar |

**Verificación del árbol tras la carga:**

```bash
curl -s "$API/api/v1/companies/$COMPANY/organization-units/tree" -H "Authorization: Bearer $TOKEN" \
  -o /tmp/tree.json
python3 - <<'PY'
import json
d = json.load(open('/tmp/tree.json'))
roots = d if isinstance(d, list) else d.get('items', [])

def walk(nodes, depth=1, acc=None):
    acc = acc if acc is not None else {}
    for n in nodes:
        acc[n.get('code')] = depth
        walk(n.get('children') or [], depth + 1, acc)
    return acc

levels = walk(roots)
print('raíces:', len(roots), '→', 'OK' if len(roots) == 1 else 'REVISAR')
print('unidades:', len(levels), '→', 'OK' if len(levels) == 27 else 'REVISAR')
print('profundidad máxima:', max(levels.values()) if levels else 0, '→ esperado 4')
for code, expected in (('DG',1), ('VP-OPS',2), ('GER-VUELO',3), ('JEF-PILOTOS',4), ('GER-TI',2)):
    got = levels.get(code)
    print(f'  {code:14} nivel {got}  {"OK" if got==expected else f"esperado {expected}"}')
PY
```

| Prueba | Esperado |
|---|---|
| Crear la raíz con `parentPublicId: null` | `201` |
| `tree` tras cargar el organigrama | **Una sola raíz**. Si hay varias, alguna quedó sin padre |
| `move` de una rama a otro padre | `200`, el subárbol viaja completo |
| `move` de una unidad **dentro de su propio subárbol** | `422 ORG_UNIT_CYCLE_DETECTED` |
| `inactivate` de una unidad con hijas activas | Verificar el comportamiento y anotarlo |
| `export` y `diagram-export` | Descarga con el árbol completo |

> El caso del ciclo es el más valioso de esta sección: mover una unidad debajo de su propia
> descendiente rompería el organigrama de forma irrecuperable. Existe `WouldCreateCycle` para
> evitarlo y devuelve `ORG_UNIT_CYCLE_DETECTED`. **Probarlo explícitamente.**

`graph` y `diagram-export` alimentan la visualización del organigrama en el frontend; conviene
confirmar que devuelven algo coherente aunque no se dibujen todavía.

### 3.7 · Cierre de la sección

Con todo cargado, comprobar de una sola pasada:

```bash
for r in location-levels work-center-types work-centers cost-center-types cost-centers \
         organization-structure-catalogs/unit-types organization-structure-catalogs/functional-areas \
         organization-units; do
  printf "%-52s " "$r"
  curl -s "$API/api/v1/companies/$COMPANY/$r" -H "Authorization: Bearer $TOKEN" -o /tmp/r.json -w "%{http_code}"
  python3 -c "
import json;d=json.load(open('/tmp/r.json'))
i=d.get('items',d) if isinstance(d,dict) else d
print(f'  ({len(i) if isinstance(i,list) else \"?\"} items)')" 2>/dev/null || echo
done
```

Y el árbol con una sola raíz:

```bash
curl -s "$API/api/v1/companies/$COMPANY/organization-units/tree" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "import json,sys; d=json.load(sys.stdin); r=d if isinstance(d,list) else d.get('items',[]); print('raíces:', len(r), '→', 'OK' if len(r)==1 else 'REVISAR')"
```

---

## Registro de la corrida — sección 3

| Paso | Endpoint | Esperado | Obtenido | Fecha | Notas |
|---|---|---|---|---|---|
| 3.1 | `location-levels` / `location-groups/tree` | `200`, 3 niveles, 1 raíz | | | sembrado por provisioning |
| 3.1n | `location-groups/{id}/usage` | indica si está en uso | | | |
| 3.2 | `POST work-center-types` | `201` | | | |
| 3.2n | código repetido | conflicto | | | |
| 3.3 | `POST work-centers` | `201` | | | |
| 3.3n | sin `locationGroupPublicId` | rechazo | | | |
| 3.3n | tipo exige dirección, sin `address` | rechazo | | | |
| 3.3 | `reassign-group` | `200` | | | |
| 3.4 | los dos `GET` en empresa nueva | `totalCount: 0` | | | llegan **vacíos** a propósito |
| 3.4 | `POST unit-types` (8) / `functional-areas` (11) | `201` | | | **bloquea todo el organigrama** |
| 3.4n | `POST unit-types` con código repetido | `409 ORG_STRUCTURE_CATALOG_CODE_CONFLICT` | | | el chequeo ignora activo/inactivo |
| 3.4n | `POST organization-units` sin tipos cargados | rechazo | | | `orgUnitTypePublicId` es obligatorio |
| 3.4.1 | `PUT organization-units/{id}` reasignando el tipo | `200` | | | `PATCH` no sirve para esto |
| 3.4.1n | inactivar un tipo aún en uso | `ORG_STRUCTURE_CATALOG_IN_USE` | | | |
| 3.4.1 | inactivar el tipo viejo ya migrado | `200` | | | queda inactivo, **no se borra** |
| 3.4.2n | inactivar `work-center-type` en uso | ⚠️ sin bloqueo — anotar qué pasa | | | asimetría a confirmar |
| 3.5.a | `POST cost-center-types` (4) | `201` | | | **primero los tipos** |
| 3.5.a n | código de tipo repetido | conflicto | | | |
| 3.5.b | `POST cost-centers` (14) | `201` | | | **van ANTES de las unidades** |
| 3.5.b | `cost-centers/{id}/usage` | responde | | | |
| 3.5.b n | `inactivate` de un tipo con centros | `COST_CENTER_TYPE_IN_USE` | | | correr tras crear centros |
| 3.6 | `POST organization-units` (raíz) | `201` | | | |
| 3.6 | 27 unidades con su `costCenterCode` | `201` | | | de una sola pasada |
| 3.6 | `organization-units/tree` | **1 sola raíz**, 27 unidades, prof. 4 | | | script de verificación |
| 3.6n | `costCenterCode` inexistente | `ORG_UNIT_COST_CENTER_INVALID` | | | |
| 3.6n | `costCenterCode` de un centro inactivo | `ORG_UNIT_COST_CENTER_INVALID` | | | |
| 3.6n | salto de escalón (`GER-TI` bajo `DG`) | anotar: ¿pasa o falla? | | | ¿regla no documentada? |
| 3.6 | `move` a otro padre | `200` | | | |
| 3.6n | `move` dentro de su subárbol | `422 ORG_UNIT_CYCLE_DETECTED` | | | prueba clave |
| 3.6 | `export` / `diagram-export` | descarga | | | |
| 3.7 | conteo global + árbol | todo `200`, 1 raíz | | | |

---

## 4. Puestos, plazas y tabulador salarial

Cadena estricta: **catálogos de puesto → perfiles de puesto → tabulador → plazas**. La plaza es lo que un
empleado ocupa, así que sin esta sección no hay expedientes.

### 4.1 · Catálogos de clasificación

```
GET,POST /api/v1/companies/{c}/occupational-pyramid-levels
GET,POST /api/v1/companies/{c}/position-categories
GET,POST /api/v1/companies/{c}/position-category-classifications
GET,POST /api/v1/companies/{c}/job-catalogs/{category}
GET,POST /api/v1/companies/{c}/position-description-catalogs/{catalogType}/items
GET      /api/v1/job-profiles/catalog-manifest
GET      /api/v1/job-profiles/internal-catalogs
```

Empezar por `catalog-manifest`: enumera qué catálogos alimentan el perfil de puesto y con qué claves. Es el
mapa de esta sección.

| Prueba | Esperado |
|---|---|
| `GET occupational-pyramid-levels` en empresa nueva | Lista — anotar si viene vacía |
| `POST` un nivel, luego `inactivate` | `201` / `200` |
| `position-category-classifications` | **Referencia tipos de unidad** (sección 3.4). Con catálogo vacío no se puede clasificar |

> **Enlace con la sección 3:** una clasificación de categoría de puesto retiene el tipo de unidad. Es la
> segunda cosa que puede bloquear `ORG_STRUCTURE_CATALOG_IN_USE` al inactivar un tipo (el bloqueo solo
> cuenta clasificaciones **activas**: si el paso 3 de 3.4.1 falla y las unidades ya están migradas, la
> salida es inactivar primero la clasificación).

**Conjunto de datos — niveles de la pirámide ocupacional** (7 escalones de responsabilidad, paralelos pero
independientes de los escalones de unidad de 3.4.0):

| Escalón | `code` | `name` | `sortOrder` |
|---|---|---|---|
| 1 | `DIRECTIVO` | Directivo | 10 |
| 2 | `GERENCIAL` | Gerencial | 20 |
| 3 | `JEFATURA` | Jefatura / Supervisión | 30 |
| 4 | `PROFESIONAL` | Profesional | 40 |
| 5 | `TECNICO` | Técnico | 50 |
| 6 | `OPERATIVO` | Operativo | 60 |
| 7 | `APOYO` | Apoyo | 70 |

**Categorías de puesto** (5, sin jerarquía):

| `code` | `name` |
|---|---|
| `OPERATIVO_AEREO` | Operativo aéreo (tripulaciones) |
| `TECNICO_AERONAUTICO` | Técnico aeronáutico |
| `OPERATIVO_TIERRA` | Operativo en tierra |
| `COMERCIAL` | Comercial |
| `ADMINISTRATIVO` | Administrativo |

> **Dos jerarquías que no se corresponden**, y es intencional: un `Piloto Comandante` es nivel
> `PROFESIONAL` pero cobra más que un `GERENCIAL`, y cuelga de una `JEFATURA` en el organigrama. Sirve para
> comprobar que el sistema no asume que nivel ocupacional, escalón de unidad y salario van alineados.

### 4.2 · Perfiles de puesto

```
GET,POST      /api/v1/companies/{c}/job-profiles
GET,PUT,PATCH /api/v1/job-profiles/{publicId}
```

Y ocho colecciones hijas, todas con el mismo patrón `GET,POST` en la colección y
`GET,PUT,PATCH,DELETE` en el elemento:

```
/api/v1/job-profiles/{id}/functions              · funciones
/api/v1/job-profiles/{id}/requirements           · requisitos
/api/v1/job-profiles/{id}/competencies           · competencias
/api/v1/job-profiles/{id}/working-conditions     · condiciones de trabajo
/api/v1/job-profiles/{id}/relations              · relaciones
/api/v1/job-profiles/{id}/dependent-positions    · puestos dependientes
/api/v1/job-profiles/{id}/benefits               · prestaciones
/api/v1/job-profiles/{id}/trainings              · formación
/api/v1/job-profiles/{id}/compensations          · compensación
```

**Estas colecciones sí tienen `DELETE`**, a diferencia de los catálogos de la sección 3. Vale la pena
comprobarlo: agregar una función y borrarla debe dejar el perfil limpio.

Matriz de competencias:

```
GET   /api/v1/job-profiles/{id}/competency-matrix
POST  /api/v1/job-profiles/{id}/competency-matrix/items
GET   /api/v1/job-profiles/{id}/competency-matrix/export
```

Requiere la **escala de calificación** y las **conductas de competencia**
(`/companies/{c}/competency-conducts`). **Ninguna de las dos viene sembrada:** `GET
.../competency-rating-scale` responde `isConfigured: false` en una empresa nueva. Crear primero la escala
—una empresa puede querer 1–5, 1–4 o A–E, por eso no hay una por defecto— y después las conductas.

> Probar al menos **un perfil completo** con sus nueve colecciones. Es lo que alimenta el PDF de descriptor
> de puesto y el reporte de competencias; un perfil a medias no revela los errores de armado.

#### 4.2.1 · Conjunto de datos — 33 perfiles de puesto

La columna **Banda** es el rango mensual en USD que se carga en el tabulador (4.3); **Base** es el salario
que se configura en la plaza (4.4).

| `code` | Puesto | Nivel | Categoría | Unidad | Banda mín–máx | Base |
|---|---|---|---|---|---|---|
| `P-DG` | Director General | `DIRECTIVO` | `ADMINISTRATIVO` | `DG` | 8,000–12,000 | 10,000 |
| `P-VPOPS` | Vicepresidente de Operaciones | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-OPS` | 6,000–9,000 | 7,500 |
| `P-VPTEC` | Vicepresidente Técnico | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-TEC` | 6,000–9,000 | 7,500 |
| `P-VPCOM` | Vicepresidente Comercial | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-COM` | 6,000–9,000 | 7,500 |
| `P-VPFIN` | Vicepresidente de Finanzas | `DIRECTIVO` | `ADMINISTRATIVO` | `VP-FIN` | 6,000–9,000 | 7,500 |
| `P-DIRSEG` | Director de Seguridad Operacional | `DIRECTIVO` | `ADMINISTRATIVO` | `DIR-SEG` | 5,000–7,500 | 6,250 |
| `P-DIRGENTE` | Director de Gente y Cultura | `DIRECTIVO` | `ADMINISTRATIVO` | `DIR-GENTE` | 4,500–7,000 | 5,750 |
| `P-JEFPIL` | Jefe de Pilotos | `JEFATURA` | `OPERATIVO_AEREO` | `JEF-PILOTOS` | 8,000–11,000 | 9,500 |
| `P-CMDTE` | Piloto Comandante | `PROFESIONAL` | `OPERATIVO_AEREO` | `JEF-PILOTOS` | 7,000–11,000 | 9,000 |
| `P-PRIOF` | Primer Oficial | `PROFESIONAL` | `OPERATIVO_AEREO` | `JEF-PILOTOS` | 3,500–5,500 | 4,500 |
| `P-DESPACH` | Despachador de Vuelo | `TECNICO` | `OPERATIVO_AEREO` | `DEP-DESPACHO` | 900–1,500 | 1,200 |
| `P-SOBJEFE` | Sobrecargo Jefe | `TECNICO` | `OPERATIVO_AEREO` | `GER-ABORDO` | 1,400–2,200 | 1,800 |
| `P-TCP` | Tripulante de Cabina | `TECNICO` | `OPERATIVO_AEREO` | `GER-ABORDO` | 800–1,400 | 1,100 |
| `P-GERAER` | Gerente de Aeropuertos | `GERENCIAL` | `OPERATIVO_TIERRA` | `GER-AEROP` | 3,000–4,500 | 3,750 |
| `P-SUPRAMPA` | Supervisor de Rampa | `JEFATURA` | `OPERATIVO_TIERRA` | `DEP-RAMPA` | 900–1,400 | 1,150 |
| `P-AGRAMPA` | Agente de Rampa | `OPERATIVO` | `OPERATIVO_TIERRA` | `DEP-RAMPA` | 420–650 | 535 |
| `P-AGPAX` | Agente de Servicio al Pasajero | `OPERATIVO` | `OPERATIVO_TIERRA` | `DEP-COUNTER` | 450–700 | 575 |
| `P-JEFBASE` | Jefe de Base | `JEFATURA` | `OPERATIVO_TIERRA` | `BASE-SAL` | 1,500–2,400 | 1,900 |
| `P-GERMTTO` | Gerente de Mantenimiento en Línea | `GERENCIAL` | `TECNICO_AERONAUTICO` | `GER-MTTO` | 3,500–5,000 | 4,250 |
| `P-TECAP` | Técnico Aeronáutico A&P | `TECNICO` | `TECNICO_AERONAUTICO` | `GER-MTTO` | 1,200–2,000 | 1,600 |
| `P-INGMTTO` | Ingeniero de Mantenimiento | `PROFESIONAL` | `TECNICO_AERONAUTICO` | `GER-ING` | 2,000–3,200 | 2,600 |
| `P-INSPCAL` | Inspector de Calidad | `PROFESIONAL` | `TECNICO_AERONAUTICO` | `DIR-SEG` | 1,800–2,800 | 2,300 |
| `P-ALMTEC` | Almacenista Técnico | `OPERATIVO` | `TECNICO_AERONAUTICO` | `DEP-ALMACEN` | 500–750 | 625 |
| `P-GERVTA` | Gerente de Ventas | `GERENCIAL` | `COMERCIAL` | `GER-VENTAS` | 2,800–4,200 | 3,500 |
| `P-EJEVTA` | Ejecutivo de Ventas | `PROFESIONAL` | `COMERCIAL` | `GER-VENTAS` | 800–1,400 | 1,100 |
| `P-AGCARGA` | Agente de Carga | `OPERATIVO` | `COMERCIAL` | `GER-CARGA` | 480–700 | 590 |
| `P-CONTGRAL` | Contador General | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-CONTA` | 1,800–2,800 | 2,300 |
| `P-ANACONT` | Analista Contable | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-CONTA` | 700–1,100 | 900 |
| `P-ANANOM` | Analista de Nómina | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-ADMPER` | 800–1,300 | 1,050 |
| `P-GENRRHH` | Generalista de Gente y Cultura | `PROFESIONAL` | `ADMINISTRATIVO` | `DEP-TALENTO` | 900–1,400 | 1,150 |
| `P-ANASIS` | Analista de Sistemas | `PROFESIONAL` | `ADMINISTRATIVO` | `GER-TI` | 1,200–2,000 | 1,600 |
| `P-ABOG` | Abogado Corporativo | `PROFESIONAL` | `ADMINISTRATIVO` | `DIR-LEGAL` | 2,000–3,000 | 2,500 |
| `P-RECEP` | Recepcionista | `APOYO` | `ADMINISTRATIVO` | `DG` | 408.80–550 | 480 |

**Casos que este conjunto ejercita:**

| Caso | Dónde |
|---|---|
| Salario mínimo exacto | `P-RECEP` arranca en **408.80** — el mínimo legal |
| Un `PROFESIONAL` que cobra más que un `GERENCIAL` | `P-CMDTE` (9,000) vs `P-GERAER` (3,750) |
| Tres perfiles en la misma unidad | `P-JEFPIL`, `P-CMDTE`, `P-PRIOF` en `JEF-PILOTOS` |
| Perfil en una unidad transversal | `P-JEFBASE` en `BASE-SAL` |
| Perfil en la unidad raíz | `P-RECEP` cuelga de `DG` |
| Rango muy amplio | `P-CMDTE`: 7,000–11,000, para probar el impacto del tabulador |

### 4.3 · Tabulador salarial

```
GET  /api/v1/companies/{c}/salary-tabulator/lines
GET  /api/v1/salary-tabulator/lines/{id}
GET  /api/v1/companies/{c}/salary-tabulator/export
GET,POST /api/v1/companies/{c}/salary-tabulator/change-requests
GET,PUT  /api/v1/salary-tabulator/change-requests/{id}
GET      /api/v1/salary-tabulator/change-requests/{id}/impact
```

> **El tabulador no se edita directamente.** No hay `POST` de líneas: se crea una **solicitud de cambio**,
> se consulta su `impact` y se aplica. Es el único maestro del sistema con ese flujo, y es fácil buscar un
> `POST /lines` que no existe.

| Prueba | Esperado |
|---|---|
| `POST change-requests` con bandas nuevas | `201` |
| `GET .../impact` | Qué plazas y empleados quedarían fuera de banda |
| Aplicar la solicitud | Las líneas aparecen en `GET lines` |
| Banda con mínimo bajo `$408.80` | Anotar si se rechaza o solo advierte |

### 4.4 · Plazas

```
GET,POST      /api/v1/companies/{c}/position-slots
GET,PUT,PATCH /api/v1/position-slots/{publicId}
GET           /api/v1/companies/{c}/position-slots/graph
GET           /api/v1/companies/{c}/position-slots/export
GET           /api/v1/companies/{c}/position-slots/diagram-export
```

Campos clave: `jobProfilePublicId`, `workCenterPublicId`, `directDependencyPositionSlotPublicId`,
`maxEmployees`, `configuredBaseSalary`, `effectiveFromUtc`.

> **El salario vive en la plaza, no en el perfil de puesto.** El perfil describe el trabajo; la plaza
> define cuánto se paga por ella. Es el error de configuración más frecuente.

#### 4.4.1 · Conjunto de datos — 33 plazas, 60 posiciones ocupables

`Máx.` es `maxEmployees`: cuántas personas caben en esa plaza. `Depende de` arma el organigrama **de
plazas**, que es distinto del de unidades — se prueba con `GET .../position-slots/graph`.

| `code` | Perfil | Sede | Máx. | Base | Depende de |
|---|---|---|---|---|---|
| `PL-DG-001` | `P-DG` | `SS-CORP` | 1 | 10,000 | — |
| `PL-VPOPS-001` | `P-VPOPS` | `SS-CORP` | 1 | 7,500 | `PL-DG-001` |
| `PL-VPTEC-001` | `P-VPTEC` | `SAL-HGR` | 1 | 7,500 | `PL-DG-001` |
| `PL-VPCOM-001` | `P-VPCOM` | `SS-CORP` | 1 | 7,500 | `PL-DG-001` |
| `PL-VPFIN-001` | `P-VPFIN` | `SS-CORP` | 1 | 7,500 | `PL-DG-001` |
| `PL-DIRSEG-001` | `P-DIRSEG` | `SS-CORP` | 1 | 6,250 | `PL-DG-001` |
| `PL-DIRGENTE-001` | `P-DIRGENTE` | `SS-CORP` | 1 | 5,750 | `PL-DG-001` |
| `PL-JEFPIL-001` | `P-JEFPIL` | `SAL-EST` | 1 | 9,500 | `PL-VPOPS-001` |
| `PL-CMDTE-001` | `P-CMDTE` | `SAL-EST` | **5** | 9,000 | `PL-JEFPIL-001` |
| `PL-PRIOF-001` | `P-PRIOF` | `SAL-EST` | **5** | 4,500 | `PL-JEFPIL-001` |
| `PL-DESPACH-001` | `P-DESPACH` | `SAL-EST` | **2** | 1,200 | `PL-JEFPIL-001` |
| `PL-SOBJEFE-001` | `P-SOBJEFE` | `SAL-EST` | 1 | 1,800 | `PL-VPOPS-001` |
| `PL-TCP-001` | `P-TCP` | `SAL-EST` | **9** | 1,100 | `PL-SOBJEFE-001` |
| `PL-GERAER-001` | `P-GERAER` | `SAL-EST` | 1 | 3,750 | `PL-VPOPS-001` |
| `PL-JEFBASE-001` | `P-JEFBASE` | `SAL-EST` | 1 | 1,900 | `PL-GERAER-001` |
| `PL-SUPRAMPA-001` | `P-SUPRAMPA` | `SAL-EST` | **2** | 1,150 | `PL-GERAER-001` |
| `PL-AGRAMPA-001` | `P-AGRAMPA` | `SAL-EST` | **5** | 535 | `PL-SUPRAMPA-001` |
| `PL-AGPAX-001` | `P-AGPAX` | `SAL-EST` | **3** | 575 | `PL-GERAER-001` |
| `PL-GERMTTO-001` | `P-GERMTTO` | `SAL-HGR` | 1 | 4,250 | `PL-VPTEC-001` |
| `PL-TECAP-001` | `P-TECAP` | `SAL-HGR` | **3** | 1,600 | `PL-GERMTTO-001` |
| `PL-INGMTTO-001` | `P-INGMTTO` | `SAL-HGR` | 1 | 2,600 | `PL-VPTEC-001` |
| `PL-INSPCAL-001` | `P-INSPCAL` | `SAL-HGR` | 1 | 2,300 | `PL-DIRSEG-001` |
| `PL-ALMTEC-001` | `P-ALMTEC` | `SAL-HGR` | 1 | 625 | `PL-VPTEC-001` |
| `PL-GERVTA-001` | `P-GERVTA` | `SS-CORP` | 1 | 3,500 | `PL-VPCOM-001` |
| `PL-EJEVTA-001` | `P-EJEVTA` | `SS-CORP` | **2** | 1,100 | `PL-GERVTA-001` |
| `PL-AGCARGA-001` | `P-AGCARGA` | `SAL-CRG` | 1 | 590 | `PL-VPCOM-001` |
| `PL-CONTGRAL-001` | `P-CONTGRAL` | `SS-CORP` | 1 | 2,300 | `PL-VPFIN-001` |
| `PL-ANACONT-001` | `P-ANACONT` | `SS-CORP` | 1 | 900 | `PL-CONTGRAL-001` |
| `PL-ANANOM-001` | `P-ANANOM` | `SS-CORP` | 1 | 1,050 | `PL-DIRGENTE-001` |
| `PL-GENRRHH-001` | `P-GENRRHH` | `SS-CORP` | 1 | 1,150 | `PL-DIRGENTE-001` |
| `PL-ANASIS-001` | `P-ANASIS` | `SS-CORP` | 1 | 1,600 | `PL-DG-001` |
| `PL-ABOG-001` | `P-ABOG` | `SS-CORP` | 1 | 2,500 | `PL-DG-001` |
| `PL-RECEP-001` | `P-RECEP` | `SS-CORP` | 1 | 480 | `PL-DG-001` |

**Total: 33 plazas, 60 posiciones ocupables** (sumando `maxEmployees`).

**Casos que este conjunto ejercita:**

| Caso | Dónde |
|---|---|
| Plaza raíz sin dependencia | `PL-DG-001` |
| Cadena de 5 niveles de plazas | `PL-DG-001 → PL-VPOPS-001 → PL-GERAER-001 → PL-SUPRAMPA-001 → PL-AGRAMPA-001` |
| Plazas tipo *pool* con varios ocupantes | `PL-TCP-001` (9), `PL-CMDTE-001` (5), `PL-AGRAMPA-001` (5) |
| Plazas de la misma sede en ramas distintas | Las 11 de `SAL-EST` |
| Sede con una sola plaza | `SAL-CRG` con `PL-AGCARGA-001` |
| Sede **sin** plazas | `SS-CAP` — el centro de entrenamiento queda vacío a propósito |
| Dependencia que cruza sedes | `PL-INSPCAL-001` (`SAL-HGR`) depende de `PL-DIRSEG-001` (`SS-CORP`) |

> **`SS-CAP` sin plazas es deliberado.** Sirve para comprobar que un centro de trabajo sin ocupantes se
> lista igual, no rompe los reportes, y que `inactivate` sobre él **no** debería estar bloqueado por uso.

**Verificación del organigrama de plazas:**

```bash
curl -s "$API/api/v1/companies/$COMPANY/position-slots" -H "Authorization: Bearer $TOKEN" \
  -o /tmp/slots.json
python3 - <<'PY'
import json
d = json.load(open('/tmp/slots.json'))
items = d.get('items', d) if isinstance(d, dict) else d
total = len(items)
cupos = sum((s.get('maxEmployees') or 0) for s in items)
print(f'plazas: {total}  → esperado 33  {"OK" if total==33 else "REVISAR"}')
print(f'posiciones ocupables: {cupos}  → esperado 60  {"OK" if cupos==60 else "REVISAR"}')
raices = [s for s in items if not s.get('directDependencyPositionSlotPublicId')]
print(f'plazas raíz: {len(raices)}  → esperado 1  {"OK" if len(raices)==1 else "REVISAR"}')
PY
```

| Prueba | Esperado |
|---|---|
| Crear una plaza raíz sin dependencia | `201` |
| Crear plaza con `directDependency` a otra | `201`, y `graph` la muestra colgando |
| `maxEmployees: 5` en una plaza tipo pool | `201` — permite varios ocupantes |
| Salario fuera de la banda del tabulador | Anotar: ¿rechaza, advierte o acepta? |
| `graph` / `diagram-export` | Estructura coherente |

---

## 5. Nómina: definición, calendario y jornadas

Sin esto, un empleado no puede asignarse ni cobrar. Son los cuatro insumos que consume la corrida.

### 5.1 · Definición de nómina

```
GET,POST /api/v1/companies/{c}/payroll-definitions
GET,PUT  /api/v1/payroll-definitions/{publicId}
PATCH    /api/v1/payroll-definitions/{publicId}/activation | /inactivation
```

Campos: `code`, `name`, `payrollTypeCode`, `payPeriodCode`, `totalPeriods`, `guaranteesMinimumIncome`,
`currencyCode`, ventanas de captura de horas extra y asistencia con su desfase en días.

| Prueba | Esperado |
|---|---|
| Crear `QUINCENAL` con `totalPeriods: 24` | `201` |
| `totalPeriods` incoherente con la frecuencia (p. ej. 13 en quincenal) | Validación **suave** — se permite a propósito (aguinaldo) |
| Código repetido | Conflicto |
| `inactivation` de una nómina con periodos | Anotar el comportamiento |

### 5.2 · Calendario de periodos

```
POST /api/v1/companies/{c}/payroll-definitions/{payrollDefinitionPublicId}/periods/generate
GET,POST /api/v1/companies/{c}/payroll-periods
GET,PUT  /api/v1/payroll-periods/{publicId}
PATCH    /api/v1/payroll-periods/{publicId}/activate | /inactivate
```

Generar el año completo y verificar:

| Verificación | Esperado |
|---|---|
| Cantidad de periodos | 24 en quincenal, 12 en mensual |
| Rangos | Del 1 al 15 y del 16 al fin de mes |
| Fechas de corte de captura | Derivadas del desfase de 5.1, **editables** periodo a periodo |
| Regenerar el mismo año | Anotar: ¿duplica, omite o falla? |

> **La quincena son siempre 15 días comerciales**, tenga el mes 28, 30 o 31. No es un error de redondeo:
> el salario diario es `mensual / 30` y la hora es `diaria / 8`.

### 5.3 · Jornadas

```
GET,POST /api/v1/companies/{c}/work-schedules
GET,PUT  /api/v1/work-schedules/{publicId}
PATCH    /api/v1/work-schedules/{publicId}/activation | /inactivation
```

Viene sembrada `JORNADA_ORDINARIA` de 44 h. Crear las que falten con sus días
(`dayOfWeek`, `startTime`, `endTime`, `mealStart`, `mealEnd`).

| Prueba | Esperado |
|---|---|
| Jornada que suma 44 h | `201` |
| Jornada de 0 h o más de 168 h semanales | Rechazo |
| `scheduleClass` fuera de `ORDINARIA`/`EXTRAORDINARIA` | Rechazo |
| `attendanceDateAnchor` fuera de `ENTRADA`/`SALIDA` | Rechazo |

> El **código** de la jornada es lo que la asignación del empleado referencia como `workdayCode`. Si no
> coincide exactamente (comparación en mayúsculas), la asignación falla en la sección 7.

### 5.4 · Asuetos

```
GET,POST /api/v1/companies/{c}/company-holidays
GET,PUT  /api/v1/company-holidays/{publicId}
PATCH    /api/v1/company-holidays/{publicId}/activate | /inactivate
```

Vienen los **11 del año en curso**. Verificar que estén los nueve nacionales y **cargar los del año
siguiente antes de generar su calendario**.

Alimentan el cálculo de horas extra en asueto (`HEDF` ×4, `HENF` ×5), así que un asueto faltante se paga
como día normal.

### 5.5 · Tablas de Renta (ISR) — paso que no se puede saltar

```
GET,PUT /api/v1/income-tax-brackets
```

**Una empresa nueva no tiene tramos.** Sin ellos la planilla corre y retiene **$0.00 de ISR en todos los
empleados**, sin error ni advertencia. Es el fallo más silencioso de toda la configuración.

Cargar las tres tablas oficiales (DL 95/2015) vigentes desde `2024-01-01`. Con nómina quincenal se usa la
segunda, pero cargar las tres.

**QUINCENAL**

| # | Desde | Hasta | Cuota fija | % | Sobre exceso de |
|---|---|---|---|---|---|
| 1 | 0.01 | 236.00 | 0.00 | 0 % | 0.00 |
| 2 | 236.01 | 447.62 | 8.83 | 10 % | 236.00 |
| 3 | 447.63 | 1,019.05 | 30.00 | 20 % | 447.62 |
| 4 | 1,019.06 | — | 144.28 | 30 % | 1,019.05 |

**MENSUAL**: `0.01–472.00` exento · `472.01–895.24` → 17.67 + 10 % · `895.25–2,038.10` → 60.00 + 20 % ·
`2,038.11+` → 288.57 + 30 %

**SEMANAL**: `0.01–118.00` exento · `118.01–223.81` → 4.42 + 10 % · `223.82–509.52` → 15.00 + 20 % ·
`509.53+` → 72.14 + 30 %

> **No derivar una tabla de otra.** La quincenal no es la mensual dividida entre dos. Copiar las cifras
> del decreto tal cual.

**Verificación:** tras el `PUT`, un `GET` debe devolver 12 tramos (4 × 3 periodicidades).

### 5.6 · Conceptos de compensación y liquidación

```
GET /api/v1/compensation-concept-types
GET /api/v1/settlement-concepts
```

Solo lectura — son catálogos de sistema. Comprobar que respondan con listas no vacías: si están vacíos, el
motor de planilla y el de finiquitos no tienen con qué calcular.

---

## 6. Políticas operativas y usuarios

Lo último antes de personas. Varias de estas son las que dejaron de sembrarse solas.

### 6.1 · Catálogos que ahora arrancan vacíos

Consecuencia del cambio de sembrado del 2026-08-04. **Sin ellos, el módulo transaccional correspondiente no
se puede usar.**

| Catálogo | Endpoint | Necesario para |
|---|---|---|
| Justificaciones de horas extra | `GET,POST /companies/{c}/overtime-justification-types` | Registrar horas extra |
| Tipos de reconocimiento | `GET,POST /companies/{c}/recognition-types` | Registrar reconocimientos |
| Tipos de amonestación | `GET,POST /companies/{c}/disciplinary-action-types` | Registrar amonestaciones |
| Causas de amonestación | `GET,POST /companies/{c}/disciplinary-action-causes` | Registrar amonestaciones |

**Prueba clave:** intentar registrar una hora extra **antes** de crear justificaciones, y anotar qué error
da. Es exactamente lo que le va a pasar a un cliente nuevo.

### 6.2 · Tipos de horas extra

```
GET,POST /api/v1/companies/{c}/overtime-types
```

Vienen los 4 legales. **Verificar los factores: `HED` 2.00, `HEN` 2.50, `HEDF` 4.00, `HENF` 5.00.** Son
editables pero los fija el Código de Trabajo; si alguno llega distinto, la planilla calcula mal en silencio.

### 6.3 · Incapacidades y clínicas

```
GET,POST /api/v1/companies/{c}/incapacity-types
GET,POST /api/v1/companies/{c}/incapacity-risks
GET,POST /api/v1/companies/{c}/medical-clinics
POST     /api/v1/companies/{c}/leave-configuration/load-template
```

Tipos y riesgos vienen sembrados (6 tipos). **Verificar el marcador de riesgo profesional** en
`ACCIDENTE_TRABAJO` y `ENFERMEDAD_PROFESIONAL`: cambia el cálculo del subsidio.

Las clínicas médicas **no** vienen sembradas.

### 6.4 · Tiempos no trabajados y tiempo compensatorio

```
GET,POST /api/v1/companies/{c}/not-worked-time-types
POST     /api/v1/companies/{c}/not-worked-time-configuration/load-template
GET,POST /api/v1/companies/{c}/compensatory-time-types
```

Los tiempos no trabajados **nunca** se sembraron solos: o se cargan con su plantilla o se crean a mano. Es
el único `load-template` que sigue siendo la vía normal.

> Ojo con el tipo de tiempo compensatorio marcado como calculado por el sistema: el corte de planilla
> chequea ese marcador del catálogo, no el del movimiento.

### 6.5 · Planes de vacaciones

```
GET,POST /api/v1/companies/{c}/vacation-plans
GET,PUT  /api/v1/companies/{c}/vacation-plans/{vacationPlanPublicId}
PATCH    /api/v1/companies/{c}/vacation-plans/{vacationPlanPublicId}/annulment
```

Las reglas base (15 días, arranque en asueto, día de descanso) ya se fijaron en las preferencias de empresa
(paso 2.7). Acá se prueban los planes concretos.

### 6.6 · Constancias y ayuda económica

```
GET,PUT /api/v1/companies/{c}/certificate-settings
GET,POST /api/v1/companies/{c}/economic-aid-requests
```

`certificate-settings` define el encabezado y la firma de las constancias laborales. Sin configurarlo, la
emisión falla o sale sin membrete.

### 6.7 · Roles, permisos y usuarios

```
GET,POST      /api/v1/account/companies/{c}/authorization/roles
GET,PUT       /api/v1/account/companies/{c}/authorization/roles/{rolePublicId}/grants
PUT           /api/v1/account/companies/{c}/authorization/users/{userPublicId}/roles
GET           /api/v1/account/companies/{c}/authorization/role-builder-catalog
GET           /api/v1/account/companies/{c}/authorization/resource-policies/{resourceKey}
GET,POST      /api/v1/company/users
PATCH         /api/v1/company/users/{publicId}/deactivate | /reactivate
POST          /api/v1/company/users/{publicId}/reset-invitation
```

`role-builder-catalog` lista todos los permisos asignables — es la referencia para armar roles.

**Separación obligatoria de funciones.** Varios flujos tienen control anti-autoservicio: quien registra no
puede autorizar. Crear al menos:

| Rol | Para qué | No debe poder |
|---|---|---|
| Nómina | Captura y genera planillas | Autorizarlas |
| RRHH | Expedientes y acciones de personal | Autorizar sus propios registros |
| Autorizador | Autoriza y cierra planillas | — |

**Prueba clave:** con el rol Nómina, generar una planilla y luego intentar autorizarla. Debe fallar. Si
pasa, la separación no está funcionando y se descubre en la sección 8, mucho más caro.

Verificar también `resource-policies/{resourceKey}` para `PERSONNEL_FILES`: la sección 7 no arranca sin
política de autorización configurada.

---

## Lista de verificación — ¿la empresa está lista para empleados?

Correr esto antes de la sección 7. Todo debe dar `200` y las cantidades marcadas deben ser mayores a cero.

```bash
check() { printf "  %-46s " "$2"
  curl -s "$API/api/v1/companies/$COMPANY/$1" -H "Authorization: Bearer $TOKEN" -o /tmp/c.json -w "%{http_code}"
  python3 -c "
import json;d=json.load(open('/tmp/c.json'))
i=d.get('items',d) if isinstance(d,dict) else d
n=len(i) if isinstance(i,list) else '?'
print(f'  {n} items' + ('   ← VACÍO' if n==0 else ''))" 2>/dev/null || echo; }

echo "ESTRUCTURA";        check organization-structure-catalogs/unit-types "tipos de unidad"
                          check organization-units "unidades organizativas"
                          check work-centers "centros de trabajo"
                          check cost-centers "centros de costo"
echo "PUESTOS";           check occupational-pyramid-levels "niveles ocupacionales"
                          check job-profiles "perfiles de puesto"
                          check position-slots "plazas"
                          check salary-tabulator/lines "líneas del tabulador"
echo "NÓMINA";            check payroll-definitions "nóminas"
                          check payroll-periods "periodos"
                          check work-schedules "jornadas"
                          check company-holidays "asuetos"
echo "POLÍTICAS";         check overtime-types "tipos de horas extra"
                          check overtime-justification-types "justificaciones horas extra"
                          check incapacity-types "tipos de incapacidad"
                          check not-worked-time-types "tiempos no trabajados"

printf "  %-46s " "tramos de Renta (deben ser 12)"
curl -s "$API/api/v1/income-tax-brackets" -H "Authorization: Bearer $TOKEN" \
  | python3 -c "import json,sys;d=json.load(sys.stdin);i=d.get('items',d);print(f'{len(i)} tramos' + ('   ← FALTAN' if len(i)<12 else '   OK'))"
```

**Los cuatro que más se olvidan y bloquean después:**

1. **Tramos de Renta** — sin ellos el ISR sale en cero, sin aviso
2. **Justificaciones de horas extra** — ya no se siembran; sin ellas no se registran horas extra
3. **Jornadas** — el `workdayCode` de la asignación tiene que existir, comparación exacta en mayúsculas
4. **Roles separados** — quien genera no puede autorizar; si no están, se descubre al cerrar la planilla

---

## Registro de la corrida — secciones 4 a 6

| Paso | Endpoint | Esperado | Obtenido | Fecha | Notas |
|---|---|---|---|---|---|
| 4.1 | `job-profiles/catalog-manifest` | `200` | | | mapa de la sección |
| 4.1 | `POST occupational-pyramid-levels` / `position-categories` | `201` | | | |
| 4.2 | `POST job-profiles` + 9 colecciones hijas | `201` | | | probar UN perfil completo |
| 4.2 | `DELETE` de una colección hija | `204` | | | acá sí hay DELETE |
| 4.2 | `competency-rating-scale` en empresa nueva | `isConfigured: false` | | | crear la escala ANTES de la matriz |
| 4.2 | `competency-matrix` | `200` | | | requiere escala + conductas, ninguna sembrada |
| 4.3 | `POST salary-tabulator/change-requests` | `201` | | | no hay POST de líneas |
| 4.3 | `.../impact` | `200` | | | |
| 4.3n | banda bajo `$408.80` | anotar | | | ¿rechaza o advierte? |
| 4.4 | `POST position-slots` | `201` | | | salario en la PLAZA |
| 4.4n | salario fuera de banda | anotar | | | |
| 5.1 | `POST payroll-definitions` QUINCENAL | `201` | | | |
| 5.2 | `periods/generate` | `200`, 24 periodos | | | quincena = 15 días |
| 5.2n | regenerar el mismo año | anotar | | | ¿duplica? |
| 5.3 | `POST work-schedules` | `201` | | | |
| 5.3n | jornada de 0 h / >168 h | rechazo | | | |
| 5.4 | `company-holidays` | 11 del año | | | cargar el año siguiente |
| 5.5 | `PUT income-tax-brackets` ×3 | `200`, 12 tramos | | | **el más olvidado** |
| 5.6 | `compensation-concept-types` / `settlement-concepts` | listas no vacías | | | |
| 6.1 | crear justificaciones, reconocimientos, amonestaciones | `201` | | | ya no se siembran |
| 6.1n | registrar hora extra sin justificaciones | anotar el error | | | caso del cliente nuevo |
| 6.2 | factores HE 2.00/2.50/4.00/5.00 | exactos | | | |
| 6.3 | tipos de incapacidad + riesgo profesional | 6, marcador correcto | | | |
| 6.4 | `not-worked-time-types` | cargados | | | vía plantilla o a mano |
| 6.6 | `certificate-settings` | `200` | | | |
| 6.7 | 3 roles con permisos separados | `201` | | | |
| 6.7n | rol Nómina intenta autorizar | rechazo | | | **prueba clave** |
| 6.7 | `resource-policies/PERSONNEL_FILES` | configurada | | | requisito de la sección 7 |
| — | lista de verificación final | todo > 0 | | | |
