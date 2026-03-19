CLARIHR backend

## Docker local

Levanta la API y PostgreSQL con:

```bash
docker compose up --build
```

La API queda disponible en `http://localhost:5000` y Swagger en `http://localhost:5000/swagger`.

Variables opcionales:

- `CLARIHR_API_PORT` para cambiar el puerto expuesto de la API.
- `CLARIHR_DB_PORT` para cambiar el puerto expuesto de PostgreSQL.

Verificación rápida:

```bash
curl http://localhost:5000/api/system/status
```
