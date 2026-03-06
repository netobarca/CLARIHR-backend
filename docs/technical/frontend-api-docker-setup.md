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
    "companyName": "Frontend QA Company",
    "country": "SV",
    "source": "frontend-local"
  }'
```

La respuesta devuelve `accessToken` y `refreshToken` para pruebas de endpoints protegidos.

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
