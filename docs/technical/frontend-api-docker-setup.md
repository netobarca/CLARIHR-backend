# Frontend Setup: API en Docker

Guia para levantar la API de CLARIHR en contenedores y ejecutar pruebas funcionales desde frontend o Postman.

## 1. Prerequisitos

- Docker Desktop activo (incluye Docker Compose v2)
- Puertos libres (por defecto):
  - `5000` (API)
  - `5432` (PostgreSQL)
- Estar en la raiz del repositorio (`CLARIHR-backend`)

## 2. Levantar la API + base de datos

Ejecuta:

```bash
docker compose up --build -d
```

Si `5000` o `5432` ya estan ocupados, puedes sobrescribir puertos sin editar archivos:

```bash
CLARIHR_API_PORT=5001 CLARIHR_DB_PORT=5433 docker compose up --build -d
```

Que levanta:

- `clarihr-postgres` en `localhost:${CLARIHR_DB_PORT:-5432}`
- `clarihr-api` en `http://localhost:${CLARIHR_API_PORT:-5000}`

Notas:

- En el primer arranque, PostgreSQL aplica automaticamente los scripts SQL de `docs/technical/sql`.
- El arranque inicial puede tardar un poco mas por la construccion de la imagen y el bootstrap de esquema.

## 3. Verificar que todo esta arriba

```bash
docker compose ps
```

Health check rapido:

```bash
curl http://localhost:${CLARIHR_API_PORT:-5000}/api/system/status
```

Swagger local:

- `http://localhost:${CLARIHR_API_PORT:-5000}/swagger`

## 4. Crear usuario de prueba y obtener token

```bash
curl -X POST http://localhost:${CLARIHR_API_PORT:-5000}/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Frontend",
    "lastName": "Tester",
    "email": "frontend.tester@clarihr.test",
    "password": "StrongPass123!",
    "country": "SV",
    "source": "frontend-local"
  }'
```

La respuesta devuelve `accessToken` y `refreshToken`.

Luego crea la primera empresa (o una adicional) con:

```bash
curl -X POST http://localhost:${CLARIHR_API_PORT:-5000}/api/account/companies \
  -H "Authorization: Bearer ACCESS_TOKEN_AQUI" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Frontend QA Company",
    "initialLegalRepresentative": {
      "firstName": "Frontend",
      "lastName": "Representative",
      "documentType": "TaxId",
      "documentNumber": "0614-290190-102-3",
      "positionTitle": "Representante Legal",
      "representationType": "PrimaryLegalRepresentative",
      "authorityDescription": "Representacion general",
      "appointmentInstrument": "Acta de nombramiento",
      "appointmentDateUtc": "2026-01-01T00:00:00Z",
      "effectiveFromUtc": "2026-01-01T00:00:00Z",
      "effectiveToUtc": null,
      "email": "frontend.representative@clarihr.test",
      "phone": "+50370000000",
      "isPrimary": true
    }
  }'
```

Para activar contexto tenant, ejecuta:

```bash
curl -X POST http://localhost:${CLARIHR_API_PORT:-5000}/api/account/companies/COMPANY_ID_AQUI/switch \
  -H "Authorization: Bearer ACCESS_TOKEN_AQUI"
```

Re-login local (si tu refresh token ya no es usable):

```bash
curl -X POST http://localhost:${CLARIHR_API_PORT:-5000}/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "frontend.tester@clarihr.test",
    "password": "StrongPass123!"
  }'
```

Logout (revoca refresh tokens activos del usuario autenticado):

```bash
curl -X POST http://localhost:${CLARIHR_API_PORT:-5000}/api/auth/logout \
  -H "Authorization: Bearer ACCESS_TOKEN_AQUI"
```

## 4.1 Usar seed pre-cargado (sin register)

En arranque limpio (`docker compose down -v` + `up`), PostgreSQL aplica tambien:

- `docs/technical/sql/seed_api_test_data.sql`

Datos seed relevantes:

- Tenant A (`companyId`): `aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa`
- Tenant B (`companyId`): `bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb`
- Usuario admin A (`auth_users.public_id`): `11111111-1111-1111-1111-111111111111`
- Usuario admin B (`auth_users.public_id`): `33333333-3333-3333-3333-333333333333`

Refresh token seed (valor plano) para obtener `accessToken`:

- `seed-main-refresh-token-2026` (Tenant A)
- `seed-secondary-refresh-token-2026` (Tenant B)

Ejemplo:

```bash
curl -X POST http://localhost:${CLARIHR_API_PORT:-5000}/api/auth/refresh \
  -H "Content-Type: application/json" \
  -d '{
    "refreshToken": "seed-main-refresh-token-2026"
  }'
```

## 5. Configurar el frontend para pruebas

Usa `http://localhost:${CLARIHR_API_PORT:-5000}` como base URL de la API en tu app frontend.

Si tu frontend corre en otro puerto (ejemplo `5173`), recuerda que este backend no tiene CORS habilitado por defecto. Para pruebas en navegador:

- usa proxy en el dev server del frontend, o
- consume la API con Postman/curl.

## 6. Logs utiles

```bash
docker compose logs -f api
docker compose logs -f postgres
```

## 7. Reinicio limpio de datos (opcional)

Si quieres resetear base de datos y volver a ejecutar el bootstrap SQL:

```bash
docker compose down -v
docker compose up --build -d
```

## 8. Apagar ambiente

```bash
docker compose down
```
