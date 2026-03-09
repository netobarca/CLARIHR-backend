# API E2E Test Flow (Seed) - Orden Ideal

## 1. Objetivo

Documento de pruebas manuales end-to-end usando el seed:

- `docs/technical/sql/seed_api_test_data.sql`

El flujo esta ordenado para validar:

1. Sesion/autenticacion.
2. Contexto de empresa.
3. Modulos funcionales core.
4. Reglas de negocio criticas.
5. Seguridad multi-tenant.
6. Exportes/reportes.

## 2. Precondiciones

1. Levantar ambiente limpio para que corra schema + seed:

```bash
docker compose down -v
docker compose up --build -d
```

2. Base URL:

- `http://localhost:5000` (o tu puerto configurado en `CLARIHR_API_PORT`)

3. Herramientas:

- `curl`
- `jq` (recomendado para extraer ids/tokens)

## 3. Variables de trabajo

```bash
export BASE_URL="http://localhost:5000"

export COMPANY_A="aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"
export COMPANY_B="bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"

export REFRESH_TOKEN_A="seed-main-refresh-token-2026"
export REFRESH_TOKEN_B="seed-secondary-refresh-token-2026"

# IDs seed utiles (Tenant A)
export ORG_UNIT_DIR_ID="aaaaaaaa-3000-0000-0000-000000000001"
export ORG_UNIT_HR_ID="aaaaaaaa-3000-0000-0000-000000000002"
export WORK_CENTER_HQ_ID="aaaaaaaa-1000-0000-0000-000000000040"
export LEGAL_REP_PRIMARY_A_ID="aaaaaaaa-8000-0000-0000-000000000001"

# ID seed util (Tenant B)
export LEGAL_REP_PRIMARY_B_ID="bbbbbbbb-8000-0000-0000-000000000001"

# Sufijo unico para evitar colisiones si repites pruebas
export RUN_ID="$(date +%s)"
export CC_CODE="CC-QA-${RUN_ID}"
export OU_CODE="OU-QA-${RUN_ID}"
export JP_CODE="JP-QA-${RUN_ID}"
export PS_CODE="PS-QA-${RUN_ID}"

# Credenciales para primera vez (registro local)
export FIRST_TIME_EMAIL="first.time.${RUN_ID}@clarihr.test"
export FIRST_TIME_PASSWORD="StrongPass123!"
```

### 3.1 Usuarios seed locales (BD local)

Usuarios cargados por seed:

- `seed.admin@clarihr.test` (Tenant A)
- `seed.hr@clarihr.test` (Tenant A)
- `seed.audit@clarihr.test` (Tenant B)

Nota importante:

- El endpoint local de login existe: `POST /api/auth/login`.
- El primer acceso local se hace con `POST /api/auth/register` (crea usuario + inicia sesion).
- Para usuarios seed de este script, el acceso de prueba recomendado es via `POST /api/auth/refresh` con:
  - `seed-main-refresh-token-2026`
  - `seed-secondary-refresh-token-2026`

## 4. Flujo ideal de pruebas

### Escenario 01 - Health check

Request:

```bash
curl -sS "$BASE_URL/api/system/status" | jq
```

Response esperada (`200`):

```json
{
  "applicationName": "CLARIHR.Api",
  "utcNow": "2026-03-07T...",
  "tenantId": null,
  "userId": null,
  "isAuthenticated": false
}
```

### Escenario 02A (primera vez) - Registro/Login inicial de usuario local

Request:

```bash
AUTH_FIRST=$(curl -sS -X POST "$BASE_URL/api/auth/register" \
  -H "Content-Type: application/json" \
  -d "{
    \"firstName\": \"First\",
    \"lastName\": \"Access\",
    \"email\": \"$FIRST_TIME_EMAIL\",
    \"password\": \"$FIRST_TIME_PASSWORD\",
    \"companyName\": \"First Access Company $RUN_ID\",
    \"initialLegalRepresentative\": {
      \"firstName\": \"First\",
      \"lastName\": \"Representative\",
      \"documentType\": \"TaxId\",
      \"documentNumber\": \"FT-$RUN_ID\",
      \"positionTitle\": \"Representante Legal\",
      \"representationType\": \"PrimaryLegalRepresentative\",
      \"authorityDescription\": \"Representacion general\",
      \"appointmentInstrument\": \"Acta de nombramiento\",
      \"appointmentDateUtc\": \"2026-01-01T00:00:00Z\",
      \"effectiveFromUtc\": \"2026-01-01T00:00:00Z\",
      \"effectiveToUtc\": null,
      \"email\": \"rep.first.$RUN_ID@clarihr.test\",
      \"phone\": \"+50370000099\",
      \"isPrimary\": true
    },
    \"country\": \"SV\",
    \"source\": \"manual-e2e\"
  }")
echo "$AUTH_FIRST" | jq
export ACCESS_TOKEN_FIRST="$(echo "$AUTH_FIRST" | jq -r '.accessToken')"
export REFRESH_TOKEN_FIRST="$(echo "$AUTH_FIRST" | jq -r '.refreshToken')"
```

Response esperada (`201`):

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "expiresIn": 900,
  "user": {
    "id": "guid",
    "email": "first.time.123456@clarihr.test",
    "authProvider": "Local"
  }
}
```

Uso recomendado:

- Si quieres validar onboarding completo, inicia con este escenario.
- Si quieres validar reautenticacion local (refresh expirado/revocado), usa el escenario `02C`.
- Si quieres validar sobre datos seed predecibles, usa el escenario `02B`.

---

### Escenario 02C - Login local (cuando el refresh token ya no es usable)

Request:

```bash
AUTH_LOGIN=$(curl -sS -X POST "$BASE_URL/api/auth/login" \
  -H "Content-Type: application/json" \
  -d "{
    \"email\": \"$FIRST_TIME_EMAIL\",
    \"password\": \"$FIRST_TIME_PASSWORD\"
  }")
echo "$AUTH_LOGIN" | jq
export ACCESS_TOKEN_LOGIN="$(echo "$AUTH_LOGIN" | jq -r '.accessToken')"
export REFRESH_TOKEN_LOGIN="$(echo "$AUTH_LOGIN" | jq -r '.refreshToken')"
```

Response esperada (`200`):

```json
{
  "accessToken": "jwt",
  "refreshToken": "refresh-token",
  "expiresIn": 900,
  "user": {
    "id": "guid",
    "email": "first.time.123456@clarihr.test",
    "authProvider": "Local"
  }
}
```

---

### Escenario 02D - Logout y validacion de revocacion

Request 1 (logout):

```bash
curl -sS -D - -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer $ACCESS_TOKEN_LOGIN"
```

Response esperada (`204`):

- Sin body (`No Content`)

Request 2 (intentar refresh token revocado):

```bash
curl -sS -X POST "$BASE_URL/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{
    \"refreshToken\": \"$REFRESH_TOKEN_LOGIN\"
  }" | jq
```

Response esperada (`401`):

```json
{
  "title": "The refresh token is invalid or expired.",
  "status": 401,
  "code": "auth.refresh.invalid_token"
}
```

---

### Escenario 02B - Obtener sesion Tenant A por refresh token seed

Request:

```bash
AUTH_A=$(curl -sS -X POST "$BASE_URL/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN_A\"}")
echo "$AUTH_A" | jq
export ACCESS_TOKEN_A="$(echo "$AUTH_A" | jq -r '.accessToken')"
```

Response esperada (`200`):

```json
{
  "accessToken": "jwt",
  "refreshToken": "rotated-refresh-token",
  "expiresIn": 900,
  "user": {
    "id": "11111111-1111-1111-1111-111111111111",
    "email": "seed.admin@clarihr.test",
    "authProvider": "Local"
  }
}
```

---

### Escenario 03 - Listar empresas de la cuenta (Account level)

Request:

```bash
curl -sS "$BASE_URL/api/account/companies?page=1&pageSize=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`200`):

```json
{
  "items": [
    {
      "companyId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
      "name": "Seed Acme A",
      "slug": "seed-acme-a",
      "status": "Active",
      "isActiveContext": true
    }
  ],
  "pageNumber": 1,
  "pageSize": 20
}
```

---

### Escenario 04 - Detalle de empresa con representantes activos

Request:

```bash
curl -sS "$BASE_URL/api/account/companies/$COMPANY_A" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`200`):

```json
{
  "companyId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "name": "Seed Acme A",
  "activeLegalRepresentatives": [
    {
      "id": "aaaaaaaa-8000-0000-0000-000000000001",
      "fullName": "Ana Mendoza",
      "isPrimary": true
    }
  ]
}
```

---

### Escenario 05 - Capacidades de reportes por recurso

Request:

```bash
curl -sS "$BASE_URL/api/v1/companies/$COMPANY_A/reports/capabilities?resource=ORG_UNITS" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`200`):

```json
{
  "resourceKey": "ORG_UNITS",
  "supportsPrint": false,
  "supportsExport": true,
  "supportedTableFormats": ["csv", "xlsx"],
  "supportedGraphFormats": ["graphml", "json", "dot"]
}
```

---

### Escenario 06 - Validar base de locations

Request 1:

```bash
curl -sS "$BASE_URL/api/v1/companies/$COMPANY_A/location-hierarchy" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`200`):

```json
{
  "isMultiLevel": false,
  "defaultGroupCode": "GENERAL",
  "defaultGroupName": "General",
  "concurrencyToken": "guid"
}
```

Request 2:

```bash
curl -sS "$BASE_URL/api/v1/companies/$COMPANY_A/location-groups?page=1&pageSize=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`200`): incluye grupos seed (`GENERAL`, `HQ`, `PLANT`).

---

### Escenario 07 - Crear centro de costo

Request:

```bash
CC_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/cost-centers" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"code\": \"$CC_CODE\",
    \"name\": \"QA Cost Center $RUN_ID\",
    \"type\": \"Mixed\",
    \"payrollExpenseAccountCode\": \"5100-QA\",
    \"employerContributionAccountCode\": \"5200-QA\",
    \"provisionAccountCode\": \"5300-QA\",
    \"description\": \"Cost center for API QA flow\"
  }")
echo "$CC_CREATE" | jq
export CC_ID="$(echo "$CC_CREATE" | jq -r '.id')"
export CC_TOKEN="$(echo "$CC_CREATE" | jq -r '.concurrencyToken')"
```

Response esperada (`201`):

```json
{
  "id": "guid",
  "companyId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
  "code": "CC-QA-...",
  "isActive": true,
  "concurrencyToken": "guid"
}
```

---

### Escenario 08 - Crear unidad organizativa (Org Unit)

Request:

```bash
OU_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/org-units" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"code\": \"$OU_CODE\",
    \"name\": \"QA Unit $RUN_ID\",
    \"unitType\": \"Unidad\",
    \"parentId\": \"$ORG_UNIT_DIR_ID\",
    \"sortOrder\": 90,
    \"description\": \"Org unit for API QA flow\",
    \"costCenterCode\": \"$CC_CODE\",
    \"managerEmployeeId\": null
  }")
echo "$OU_CREATE" | jq
export OU_ID="$(echo "$OU_CREATE" | jq -r '.id')"
export OU_TOKEN="$(echo "$OU_CREATE" | jq -r '.concurrencyToken')"
```

Response esperada (`201`):

```json
{
  "id": "guid",
  "code": "OU-QA-...",
  "unitType": "Unidad",
  "isActive": true,
  "concurrencyToken": "guid"
}
```

---

### Escenario 09 - Crear perfil de puesto (Draft)

Request:

```bash
JP_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/job-profiles" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"code\": \"$JP_CODE\",
    \"title\": \"QA Analyst $RUN_ID\",
    \"objective\": \"Ensure API quality end-to-end\",
    \"orgUnitId\": \"$OU_ID\",
    \"reportsToJobProfileId\": null,
    \"decisionScope\": \"Operational decisions for QA execution\",
    \"assignedResources\": \"Postman, API docs\",
    \"responsibilities\": \"Execute test suites and report issues\",
    \"benefitsSummary\": \"Standard benefits\",
    \"workingConditionSummary\": \"Hybrid\",
    \"marketSalaryReference\": \"Internal benchmark\",
    \"valuationNotes\": \"QA role\",
    \"effectiveFromUtc\": \"2026-01-01T00:00:00Z\",
    \"effectiveToUtc\": null,
    \"allowInlineCatalogCreate\": false,
    \"requirements\": [
      {
        \"requirementType\": \"Experience\",
        \"catalogItemId\": null,
        \"catalogCode\": null,
        \"catalogName\": null,
        \"description\": \"At least 2 years in API QA\",
        \"sortOrder\": 1
      }
    ],
    \"functions\": [
      {
        \"functionType\": \"General\",
        \"description\": \"Run API functional regression\",
        \"sortOrder\": 1
      }
    ],
    \"relations\": [],
    \"competencies\": [],
    \"trainings\": [],
    \"compensations\": [],
    \"benefits\": [],
    \"workingConditions\": [],
    \"dependentPositions\": []
  }")
echo "$JP_CREATE" | jq
export JP_ID="$(echo "$JP_CREATE" | jq -r '.id')"
export JP_TOKEN="$(echo "$JP_CREATE" | jq -r '.concurrencyToken')"
```

Response esperada (`201`):

```json
{
  "id": "guid",
  "code": "JP-QA-...",
  "status": "Draft",
  "concurrencyToken": "guid"
}
```

---

### Escenario 10 - Publicar perfil de puesto

Request:

```bash
JP_PUBLISH=$(curl -sS -X PATCH "$BASE_URL/api/v1/job-profiles/$JP_ID/publish" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"concurrencyToken\":\"$JP_TOKEN\"}")
echo "$JP_PUBLISH" | jq
export JP_TOKEN="$(echo "$JP_PUBLISH" | jq -r '.concurrencyToken')"
```

Response esperada (`200`):

```json
{
  "id": "guid",
  "status": "Published",
  "concurrencyToken": "guid"
}
```

---

### Escenario 11 - Crear plaza (Position Slot)

Request:

```bash
PS_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/position-slots" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"code\": \"$PS_CODE\",
    \"title\": \"QA Position $RUN_ID\",
    \"jobProfileId\": \"$JP_ID\",
    \"orgUnitId\": \"$OU_ID\",
    \"workCenterId\": \"$WORK_CENTER_HQ_ID\",
    \"costCenterCode\": \"$CC_CODE\",
    \"directDependencyPositionSlotId\": null,
    \"functionalDependencyPositionSlotId\": null,
    \"status\": \"Vacant\",
    \"maxEmployees\": 1,
    \"occupiedEmployees\": 0,
    \"isFixedTerm\": false,
    \"effectiveFromUtc\": \"2026-01-01T00:00:00Z\",
    \"effectiveToUtc\": null,
    \"notes\": \"Position for QA API flow\"
  }")
echo "$PS_CREATE" | jq
export PS_ID="$(echo "$PS_CREATE" | jq -r '.id')"
export PS_TOKEN="$(echo "$PS_CREATE" | jq -r '.concurrencyToken')"
```

Response esperada (`201`):

```json
{
  "id": "guid",
  "code": "PS-QA-...",
  "status": "Vacant",
  "occupiedEmployees": 0,
  "concurrencyToken": "guid"
}
```

---

### Escenario 12 - Actualizar ocupacion y estado de plaza

Request 1 (occupancy):

```bash
PS_OCC=$(curl -sS -X PATCH "$BASE_URL/api/v1/position-slots/$PS_ID/occupancy" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"occupiedEmployees\":1,\"concurrencyToken\":\"$PS_TOKEN\"}")
echo "$PS_OCC" | jq
export PS_TOKEN="$(echo "$PS_OCC" | jq -r '.concurrencyToken')"
```

Request 2 (status):

```bash
PS_STATUS=$(curl -sS -X PATCH "$BASE_URL/api/v1/position-slots/$PS_ID/status" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"status\":\"Occupied\",\"concurrencyToken\":\"$PS_TOKEN\"}")
echo "$PS_STATUS" | jq
export PS_TOKEN="$(echo "$PS_STATUS" | jq -r '.concurrencyToken')"
```

Response esperada (`200` en ambos): estado final `Occupied`, `occupiedEmployees = 1`.

---

### Escenario 13 - Crear solicitud de cambio de tabulador

Request:

```bash
ST_REQ_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/salary-tabulator/change-requests" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"reason\": \"QA salary update $RUN_ID\",
    \"effectiveFromUtc\": \"2026-12-01T00:00:00Z\",
    \"items\": [
      {
        \"salaryClassCode\": \"SAL-A1\",
        \"salaryScaleCode\": \"SCALE-01\",
        \"currencyCode\": \"USD\",
        \"changeType\": \"Update\",
        \"proposedBaseAmount\": 1325.00,
        \"proposedMinAmount\": 1180.00,
        \"proposedMaxAmount\": 1600.00,
        \"notes\": \"QA test change\"
      }
    ]
  }")
echo "$ST_REQ_CREATE" | jq
export ST_REQ_ID="$(echo "$ST_REQ_CREATE" | jq -r '.id')"
export ST_REQ_TOKEN="$(echo "$ST_REQ_CREATE" | jq -r '.concurrencyToken')"
```

Response esperada (`201`):

```json
{
  "id": "guid",
  "requestNumber": "ST-REQ-....",
  "status": "Draft",
  "concurrencyToken": "guid"
}
```

---

### Escenario 14 - Submit de solicitud de tabulador

Request:

```bash
ST_REQ_SUBMIT=$(curl -sS -X PATCH "$BASE_URL/api/v1/salary-tabulator/change-requests/$ST_REQ_ID/submit" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"concurrencyToken\":\"$ST_REQ_TOKEN\"}")
echo "$ST_REQ_SUBMIT" | jq
export ST_REQ_TOKEN="$(echo "$ST_REQ_SUBMIT" | jq -r '.concurrencyToken')"
```

Response esperada (`200`): `status = "Submitted"`.

---

### Escenario 15 - Aprobar solicitud de tabulador

Request:

```bash
ST_REQ_APPROVE=$(curl -sS -X PATCH "$BASE_URL/api/v1/salary-tabulator/change-requests/$ST_REQ_ID/approve" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"decisionComment\":\"Approved by QA flow\",\"concurrencyToken\":\"$ST_REQ_TOKEN\"}")
echo "$ST_REQ_APPROVE" | jq
export ST_REQ_TOKEN="$(echo "$ST_REQ_APPROVE" | jq -r '.concurrencyToken')"
```

Response esperada (`200`): `status = "Approved"`.

---

### Escenario 16 - Listado de representantes legales con acciones permitidas

Request:

```bash
curl -sS "$BASE_URL/api/v1/companies/$COMPANY_A/legal-representatives?includeAllowedActions=true&page=1&pageSize=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`200`): cada item incluye `allowedActions`.

```json
{
  "items": [
    {
      "id": "aaaaaaaa-8000-0000-0000-000000000001",
      "isPrimary": true,
      "isActive": true,
      "allowedActions": {
        "canEdit": true,
        "canDelete": false,
        "canArchive": false,
        "canActivate": false,
        "canInactivate": true,
        "reasons": []
      }
    }
  ]
}
```

---

### Escenario 17 - Crear representante legal alterno

Request:

```bash
LR_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/legal-representatives" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"firstName\": \"QA\",
    \"lastName\": \"Representative $RUN_ID\",
    \"documentType\": \"Passport\",
    \"documentNumber\": \"QA-PASS-$RUN_ID\",
    \"positionTitle\": \"Representante Legal QA\",
    \"representationType\": \"AlternateLegalRepresentative\",
    \"authorityDescription\": \"Alternate legal representation\",
    \"appointmentInstrument\": \"QA appointment letter\",
    \"appointmentDateUtc\": \"2026-01-15T00:00:00Z\",
    \"effectiveFromUtc\": \"2026-01-15T00:00:00Z\",
    \"effectiveToUtc\": null,
    \"email\": \"qa.legal.$RUN_ID@clarihr.test\",
    \"phone\": \"+50379990000\",
    \"isPrimary\": false
  }")
echo "$LR_CREATE" | jq
export LR_ID="$(echo "$LR_CREATE" | jq -r '.id')"
export LR_TOKEN="$(echo "$LR_CREATE" | jq -r '.concurrencyToken')"
```

Response esperada (`201`): `isPrimary = false`, `isActive = true`.

---

### Escenario 18 - Cambiar representante primario (set-primary)

Request:

```bash
LR_PRIMARY=$(curl -sS -X PATCH "$BASE_URL/api/v1/legal-representatives/$LR_ID/set-primary" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"concurrencyToken\":\"$LR_TOKEN\"}")
echo "$LR_PRIMARY" | jq
export LR_TOKEN="$(echo "$LR_PRIMARY" | jq -r '.concurrencyToken')"
```

Response esperada (`200`): `isPrimary = true`.

---

### Escenario 19 - Inactivar anterior primario (A) manteniendo minimo activo

1. Obtener token de concurrencia del primario anterior:

```bash
LR_OLD=$(curl -sS "$BASE_URL/api/v1/legal-representatives/$LEGAL_REP_PRIMARY_A_ID" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A")
echo "$LR_OLD" | jq
OLD_TOKEN="$(echo "$LR_OLD" | jq -r '.concurrencyToken')"
```

2. Inactivar:

```bash
curl -sS -X PATCH "$BASE_URL/api/v1/legal-representatives/$LEGAL_REP_PRIMARY_A_ID/inactivate" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{\"concurrencyToken\":\"$OLD_TOKEN\"}" | jq
```

Response esperada (`200`): `isActive = false`.

---

### Escenario 20 - Regla critica: no inactivar ultimo activo (Tenant B)

1. Obtener sesion Tenant B:

```bash
AUTH_B=$(curl -sS -X POST "$BASE_URL/api/auth/refresh" \
  -H "Content-Type: application/json" \
  -d "{\"refreshToken\":\"$REFRESH_TOKEN_B\"}")
echo "$AUTH_B" | jq
export ACCESS_TOKEN_B="$(echo "$AUTH_B" | jq -r '.accessToken')"
```

2. Obtener token de concurrencia del unico representante activo:

```bash
LR_B=$(curl -sS "$BASE_URL/api/v1/legal-representatives/$LEGAL_REP_PRIMARY_B_ID" \
  -H "Authorization: Bearer $ACCESS_TOKEN_B")
echo "$LR_B" | jq
LR_B_TOKEN="$(echo "$LR_B" | jq -r '.concurrencyToken')"
```

3. Intentar inactivar:

```bash
curl -sS -X PATCH "$BASE_URL/api/v1/legal-representatives/$LEGAL_REP_PRIMARY_B_ID/inactivate" \
  -H "Authorization: Bearer $ACCESS_TOKEN_B" \
  -H "Content-Type: application/json" \
  -d "{\"concurrencyToken\":\"$LR_B_TOKEN\"}" | jq
```

Response esperada (`409`):

```json
{
  "title": "Conflict",
  "errorCode": "LEGAL_REPRESENTATIVE_ACTIVE_MIN_REQUIRED"
}
```

---

### Escenario 21 - Seguridad: tenant mismatch

Request (token de Tenant B contra `companyId` de Tenant A):

```bash
curl -sS "$BASE_URL/api/v1/companies/$COMPANY_A/cost-centers?page=1&pageSize=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN_B" | jq
```

Response esperada (`403`): error de tenant mismatch/forbidden.

---

### Escenario 22 - Validar exportes y formato invalido

Request 1 (export valido):

```bash
curl -sS -D - "$BASE_URL/api/v1/companies/$COMPANY_A/legal-representatives/export?format=csv" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" -o /tmp/legal-representatives.csv
```

Response esperada (`200`):

- `Content-Type: text/csv`
- archivo descargado en `/tmp/legal-representatives.csv`

Request 2 (formato invalido):

```bash
curl -sS "$BASE_URL/api/v1/companies/$COMPANY_A/org-units/export?format=pdf" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

Response esperada (`400`):

```json
{
  "title": "Bad Request",
  "errorCode": "REPORT_FORMAT_NOT_SUPPORTED"
}
```

## 5. Checklist de salida

Al finalizar, deberias haber validado:

1. Autenticacion por register/login/refresh segun el caso.
2. Contexto de empresa y detalle account-level.
3. CRUD base de `CostCenters`, `OrgUnits`, `JobProfiles`, `PositionSlots`.
4. Flujo de `SalaryTabulator` (create -> submit -> approve).
5. Flujo de `LegalRepresentatives` incluyendo regla de minimo activo.
6. Seguridad tenant-scoped (`403`).
7. Reportes/exportes y validacion de formato (`400`).
8. Cierre de sesion con revocacion de refresh tokens (`POST /api/auth/logout`).

## 6. Requests en texto claro para Swagger (copy/paste)

Nota:

- Estos JSON son para pegar directo en `Request body` de Swagger.
- Reemplaza IDs/tokens cuando veas valores tipo `GUID_AQUI` o `TOKEN_AQUI`.

### 6.1 `POST /api/auth/register` (primera vez)

```json
{
  "firstName": "First",
  "lastName": "Access",
  "email": "first.time.demo@clarihr.test",
  "password": "StrongPass123!",
  "companyName": "First Access Company Demo",
  "initialLegalRepresentative": {
    "firstName": "First",
    "lastName": "Representative",
    "documentType": "TaxId",
    "documentNumber": "FT-DEMO-001",
    "positionTitle": "Representante Legal",
    "representationType": "PrimaryLegalRepresentative",
    "authorityDescription": "Representacion general",
    "appointmentInstrument": "Acta de nombramiento",
    "appointmentDateUtc": "2026-01-01T00:00:00Z",
    "effectiveFromUtc": "2026-01-01T00:00:00Z",
    "effectiveToUtc": null,
    "email": "rep.first.demo@clarihr.test",
    "phone": "+50370000099",
    "isPrimary": true
  },
  "country": "SV",
  "source": "manual-e2e"
}
```

### 6.2 `POST /api/auth/refresh` (seed Tenant A)

```json
{
  "refreshToken": "seed-main-refresh-token-2026"
}
```

### 6.3 `POST /api/auth/login` (local)

```json
{
  "email": "first.time.demo@clarihr.test",
  "password": "StrongPass123!"
}
```

### 6.4 `POST /api/auth/refresh` (seed Tenant B)

```json
{
  "refreshToken": "seed-secondary-refresh-token-2026"
}
```

### 6.5 `POST /api/v1/companies/{companyId}/cost-centers`

```json
{
  "code": "CC-QA-123456",
  "name": "QA Cost Center Demo",
  "type": "Mixed",
  "payrollExpenseAccountCode": "5100-QA",
  "employerContributionAccountCode": "5200-QA",
  "provisionAccountCode": "5300-QA",
  "description": "Cost center for API QA flow"
}
```

### 6.6 `POST /api/v1/companies/{companyId}/org-units`

```json
{
  "code": "OU-QA-123456",
  "name": "QA Unit Demo",
  "unitType": "Unidad",
  "parentId": "aaaaaaaa-3000-0000-0000-000000000001",
  "sortOrder": 90,
  "description": "Org unit for API QA flow",
  "costCenterCode": "CC-QA-123456",
  "managerEmployeeId": null
}
```

### 6.7 `POST /api/v1/companies/{companyId}/job-profiles`

```json
{
  "code": "JP-QA-123456",
  "title": "QA Analyst Demo",
  "objective": "Ensure API quality end-to-end",
  "orgUnitId": "GUID_ORG_UNIT_CREADA",
  "reportsToJobProfileId": null,
  "decisionScope": "Operational decisions for QA execution",
  "assignedResources": "Postman, API docs",
  "responsibilities": "Execute test suites and report issues",
  "benefitsSummary": "Standard benefits",
  "workingConditionSummary": "Hybrid",
  "marketSalaryReference": "Internal benchmark",
  "valuationNotes": "QA role",
  "effectiveFromUtc": "2026-01-01T00:00:00Z",
  "effectiveToUtc": null,
  "allowInlineCatalogCreate": false,
  "requirements": [
    {
      "requirementType": "Experience",
      "catalogItemId": null,
      "catalogCode": null,
      "catalogName": null,
      "description": "At least 2 years in API QA",
      "sortOrder": 1
    }
  ],
  "functions": [
    {
      "functionType": "General",
      "description": "Run API functional regression",
      "sortOrder": 1
    }
  ],
  "relations": [],
  "competencies": [],
  "trainings": [],
  "compensations": [],
  "benefits": [],
  "workingConditions": [],
  "dependentPositions": []
}
```

### 6.8 `PATCH /api/v1/job-profiles/{id}/publish`

```json
{
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_JOB_PROFILE"
}
```

### 6.9 `POST /api/v1/companies/{companyId}/position-slots`

```json
{
  "code": "PS-QA-123456",
  "title": "QA Position Demo",
  "jobProfileId": "GUID_JOB_PROFILE_CREADO",
  "orgUnitId": "GUID_ORG_UNIT_CREADA",
  "workCenterId": "aaaaaaaa-1000-0000-0000-000000000040",
  "costCenterCode": "CC-QA-123456",
  "directDependencyPositionSlotId": null,
  "functionalDependencyPositionSlotId": null,
  "status": "Vacant",
  "maxEmployees": 1,
  "occupiedEmployees": 0,
  "isFixedTerm": false,
  "effectiveFromUtc": "2026-01-01T00:00:00Z",
  "effectiveToUtc": null,
  "notes": "Position for QA API flow"
}
```

### 6.10 `PATCH /api/v1/position-slots/{id}/occupancy`

```json
{
  "occupiedEmployees": 1,
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_POSITION_SLOT"
}
```

### 6.11 `PATCH /api/v1/position-slots/{id}/status`

```json
{
  "status": "Occupied",
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_POSITION_SLOT"
}
```

### 6.12 `POST /api/v1/companies/{companyId}/salary-tabulator/change-requests`

```json
{
  "reason": "QA salary update demo",
  "effectiveFromUtc": "2026-12-01T00:00:00Z",
  "items": [
    {
      "salaryClassCode": "SAL-A1",
      "salaryScaleCode": "SCALE-01",
      "currencyCode": "USD",
      "changeType": "Update",
      "proposedBaseAmount": 1325.0,
      "proposedMinAmount": 1180.0,
      "proposedMaxAmount": 1600.0,
      "notes": "QA test change"
    }
  ]
}
```

### 6.13 `PATCH /api/v1/salary-tabulator/change-requests/{id}/submit`

```json
{
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_REQUEST"
}
```

### 6.14 `PATCH /api/v1/salary-tabulator/change-requests/{id}/approve`

```json
{
  "decisionComment": "Approved by QA flow",
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_REQUEST"
}
```

### 6.15 `POST /api/v1/companies/{companyId}/legal-representatives`

```json
{
  "firstName": "QA",
  "lastName": "Representative Demo",
  "documentType": "Passport",
  "documentNumber": "QA-PASS-DEMO-001",
  "positionTitle": "Representante Legal QA",
  "representationType": "AlternateLegalRepresentative",
  "authorityDescription": "Alternate legal representation",
  "appointmentInstrument": "QA appointment letter",
  "appointmentDateUtc": "2026-01-15T00:00:00Z",
  "effectiveFromUtc": "2026-01-15T00:00:00Z",
  "effectiveToUtc": null,
  "email": "qa.legal.demo@clarihr.test",
  "phone": "+50379990000",
  "isPrimary": false
}
```

### 6.16 `PATCH /api/v1/legal-representatives/{id}/set-primary`

```json
{
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_LEGAL_REP"
}
```

### 6.17 `PATCH /api/v1/legal-representatives/{id}/inactivate`

```json
{
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_LEGAL_REP"
}
```

### 6.18 `POST /api/auth/logout`

`POST /api/auth/logout` no requiere body en Swagger.

Response esperada:

- `204 No Content`

Si pruebas con curl:

```bash
curl -sS -X POST "$BASE_URL/api/auth/logout" \
  -H "Authorization: Bearer TOKEN_AQUI" \
  -D -
```

Validacion recomendada post-logout:

```json
{
  "refreshToken": "REFRESH_TOKEN_AQUI"
}
```

Ejecuta ese body en `POST /api/auth/refresh` y espera `401` con `code = auth.refresh.invalid_token`.

### 6.19 `POST /api/v1/companies/{companyId}/job-catalogs/{category}` (catalogos base framework)

Crear 4 catalogos (guardar ids retornados):

- `category=Competency`
- `category=CompetencyType`
- `category=BehaviorLevel`
- `category=Behavior`

Body ejemplo:

```json
{
  "code": "COMP-QA-123456",
  "name": "Liderazgo QA"
}
```

### 6.20 `POST /api/v1/companies/{companyId}/occupational-pyramid-levels`

```json
{
  "code": "OPL-QA-123456",
  "name": "Nivel Estrategico QA",
  "levelOrder": 1,
  "description": "Nivel para pruebas competency framework"
}
```

### 6.21 `POST /api/v1/companies/{companyId}/competency-conducts`

```json
{
  "competencyId": "GUID_COMPETENCY",
  "competencyTypeId": "GUID_COMPETENCY_TYPE",
  "behaviorLevelId": "GUID_BEHAVIOR_LEVEL",
  "description": "Alinea decisiones con objetivos institucionales",
  "sortOrder": 1
}
```

### 6.22 `PUT /api/v1/competency-conducts/{id}/behaviors`

```json
{
  "behaviors": [
    {
      "behaviorId": "GUID_BEHAVIOR",
      "notes": "Conducta base del escenario E2E",
      "sortOrder": 0
    }
  ],
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_CONDUCT"
}
```

### 6.23 `PUT /api/v1/job-profiles/{id}/competency-matrix`

Usa `id` de un job profile del flujo (`JP-QA-*` o seed `JP-HR-MANAGER`).

```json
{
  "items": [
    {
      "occupationalPyramidLevelId": "GUID_PYRAMID_LEVEL",
      "competencyId": "GUID_COMPETENCY",
      "competencyTypeId": "GUID_COMPETENCY_TYPE",
      "behaviorLevelId": "GUID_BEHAVIOR_LEVEL",
      "conductIds": [
        "GUID_CONDUCT"
      ],
      "expectedEvidence": "Evidencia observada en objetivos institucionales",
      "sortOrder": 1
    }
  ],
  "concurrencyToken": "GUID_CONCURRENCY_TOKEN_JOB_PROFILE"
}
```

### 6.24 `GET /api/v1/job-profiles/{id}/competency-matrix/export?format=json|csv|xlsx`

Validar:

- `format=json` retorna lista estructurada.
- `format=csv` retorna `Content-Type: text/csv`.
- `format=xlsx` retorna `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`.

### 6.25 Expediente Empleado Fase 1 - candidate -> hire -> perfil base

1. Crear candidato:

```bash
PF_CREATE=$(curl -sS -X POST "$BASE_URL/api/v1/companies/$COMPANY_A/personnel-files" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d '{
    "recordType":"Candidate",
    "firstName":"HU028",
    "lastName":"Employee",
    "birthDate":"1995-05-20T00:00:00Z",
    "identifications":[{"identificationType":"DUI","identificationNumber":"HU028-001","isPrimary":true}]
  }')
echo "$PF_CREATE" | jq
export PF_EMPLOYEE_ID="$(echo "$PF_CREATE" | jq -r '.id')"
export PF_EMPLOYEE_TOKEN="$(echo "$PF_CREATE" | jq -r '.concurrencyToken')"
```

2. Ejecutar `hire`:

```bash
HIRE=$(curl -sS -X POST "$BASE_URL/api/v1/personnel-files/$PF_EMPLOYEE_ID/hire" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"employeeCode\":\"EMP-HU028-$RUN_ID\",
    \"employmentStatusCode\":\"ACTIVE\",
    \"isEmploymentActive\":true,
    \"contractTypeCode\":\"UNSPECIFIED\",
    \"hireDate\":\"2026-01-10T00:00:00Z\",
    \"workdayCode\":\"FULL_TIME\",
    \"payrollTypeCode\":\"MONTHLY\",
    \"concurrencyToken\":\"$PF_EMPLOYEE_TOKEN\"
  }")
echo "$HIRE" | jq
```

3. Obtener nuevo `concurrencyToken` desde `GET /api/v1/personnel-files/{id}` y usarlo para:
   - `PUT /employee-profile`
   - `PUT /employment-assignments`
   - `PUT /salary-items`
   - `PUT /additional-benefits`
   - `PUT /payment-methods`
   - `PUT /authorization-substitutions`

### 6.26 Expediente Empleado Fase 2 - historicos + export

1. Crear accion manual:

```bash
curl -sS -X POST "$BASE_URL/api/v1/personnel-files/$PF_EMPLOYEE_ID/personnel-actions" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" \
  -H "Content-Type: application/json" \
  -d "{
    \"actionTypeCode\":\"SALARY_INCREMENT\",
    \"actionStatusCode\":\"APPLIED\",
    \"actionDateUtc\":\"2026-02-01T00:00:00Z\",
    \"description\":\"Incremento anual\",
    \"amount\":125.50,
    \"currencyCode\":\"USD\",
    \"concurrencyToken\":\"$PF_EMPLOYEE_TOKEN\"
  }" | jq
```

2. Consultar historicos con filtros:

```bash
curl -sS "$BASE_URL/api/v1/personnel-files/$PF_EMPLOYEE_ID/personnel-actions?fromUtc=2026-01-01T00:00:00Z&toUtc=2026-12-31T23:59:59Z&type=SALARY_INCREMENT&status=APPLIED&q=Incremento&page=1&pageSize=20" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" | jq
```

3. Exportes:

```bash
curl -sS -D - "$BASE_URL/api/v1/personnel-files/$PF_EMPLOYEE_ID/personnel-actions/export?format=csv" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" -o /tmp/personnel-actions.csv

curl -sS -D - "$BASE_URL/api/v1/personnel-files/$PF_EMPLOYEE_ID/payroll-transactions/export?format=xlsx" \
  -H "Authorization: Bearer $ACCESS_TOKEN_A" -o /tmp/payroll-transactions.xlsx
```

Validar `200` y tipo de contenido correcto.

### 6.27 Expediente Empleado Fase 3 - staging de integraciones

Validar roundtrip `PUT + GET`:

- `PUT /evaluations` + `GET /evaluations`
- `PUT /position-competency-results` + `GET /position-competencies`
- `PUT /selection-contests` + `GET /selection-contests`
- `PUT /curricular-competencies`

Cada payload de staging debe incluir cuando aplique:

- `sourceSystem`
- `sourceReference`
- `sourceSyncedUtc`

### 6.28 Seguridad `401/403`

- `401`: repetir cualquier endpoint del modulo sin header `Authorization`.
- `403`: usar token de tenant B (`ACCESS_TOKEN_B`) contra `PF_EMPLOYEE_ID` creado en tenant A y validar `TENANT_MISMATCH` o forbidden equivalente.

### 6.29 Concurrencia `409`

Usar `concurrencyToken` viejo en un `PUT` de seccion (por ejemplo `PUT /employee-profile`) y validar `CONCURRENCY_CONFLICT`.

### 6.30 Regla de negocio `422` (hire obligatorio)

Intentar convertir candidato a empleado via `PUT /api/v1/personnel-files/{id}/personal-info` con `recordType=Employee` y validar:

- status `422`
- code `PERSONNEL_FILE_HIRE_ENDPOINT_REQUIRED`
