# Postman Collection

Contenido:

- `CLARIHR Local.postman_collection.json`
- `CLARIHR Local.postman_environment.json`

## Uso

1. Importa ambos archivos en Postman.
2. Selecciona el entorno `CLARIHR Local`.
3. Ejecuta `Auth/Register Local User` para obtener `accessToken` y `refreshToken`.
4. Usa los requests protegidos con el token almacenado en variables de la coleccion.

## Variables relevantes

- `baseUrl`
- `accessToken`
- `refreshToken`
- `roleId`
- `permissionId`
- `iamUserId`
- `companyUserId`
- `auditLogId`
