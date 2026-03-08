# Auth Logout API

## Endpoint

- `POST /api/auth/logout`

## Auth

- Requiere `Authorization: Bearer <access-token>`

## Request

- Sin body

## Success response

- `204 No Content`

## Error behavior

- `401 Unauthorized` si no hay sesion valida o el contexto del usuario autenticado no es resoluble
- `500 Internal Server Error` para fallas no controladas

## Notes

- Revoca todos los refresh tokens activos del usuario autenticado
- Los access tokens ya emitidos siguen vigentes hasta su expiracion natural
- Despues de `logout`, el endpoint `POST /api/auth/refresh` respondera `401` para refresh tokens revocados
