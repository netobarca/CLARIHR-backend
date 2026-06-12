# System — Guía de consumo (frontend) · Fase 18

> Endpoint de **meta/salud** del API. No es un recurso de negocio. Convenciones globales en el
> [índice maestro](../README.md).

---

## Overview

Un controlador, **1 endpoint** anónimo de liveness/status:

| Método | Ruta | Para qué |
|--------|------|----------|
| `GET` | `/api/v1/system/status` | probe de estado del API (+ contexto de sesión si mandás token) |

Sirve dos propósitos:

1. **Liveness/conectividad** (sin token): verificar que el API responde — para health checks, pantalla
   de carga, o detectar caída del backend.
2. **Bootstrap de sesión** (con token): aunque es `[AllowAnonymous]`, si enviás
   `Authorization: Bearer <accessToken>` la respuesta refleja tu contexto (`isAuthenticated`,
   `tenantPublicId`, `userPublicId`) — un chequeo barato para validar que la sesión sigue viva y a qué
   tenant apunta, sin gastar una llamada de negocio.

---

## `GET /api/v1/system/status`

### Authentication
**Ninguna requerida** (anónimo). Opcionalmente aceptá `Authorization: Bearer <accessToken>` para
obtener el contexto de sesión.

### Request Headers
| Header | Req. | Valor |
|--------|------|-------|
| `Authorization` | No | `Bearer <accessToken>` (opcional; enriquece la respuesta) |

### Path / Query Parameters / Request Body
Ninguno.

### Responses

`200 OK` — `ApiStatusResponse`:

```json
{
  "applicationName": "CLARIHR.Api",
  "utcNow": "2026-06-10T16:45:00Z",
  "isAuthenticated": true,
  "tenantPublicId": "8f3a1c2e-…",
  "userPublicId": "…"
}
```

| Campo | Tipo | Notas |
|-------|------|-------|
| `applicationName` | string | nombre del servicio |
| `utcNow` | date-time | hora UTC del servidor (útil para detectar desfase de reloj del cliente) |
| `isAuthenticated` | boolean | `true` si llegó un token válido; `false` si fue anónimo o el token no sirve |
| `tenantPublicId` | uuid \| null | la compañía activa del token (null si anónimo) |
| `userPublicId` | string \| null | el usuario del token (null si anónimo) |

`400` / `500` — ProblemDetails (errores inesperados).

### Business Rules / Security Considerations
- Anónimo por diseño: no expone datos sensibles. Con token, solo devuelve **tu propio**
  contexto (tenant/user del JWT), nunca el de otros.
- Un token expirado/inválido no da `401` acá (es anónimo): devuelve `isAuthenticated: false`. Usalo
  justamente para distinguir "backend caído" (sin respuesta / `5xx`) de "sesión vencida"
  (`200` con `isAuthenticated: false`).

## Guía de implementación del cliente

1. **Health check / arranque**: pegá a `/system/status` al iniciar la app (con el token guardado, si
   hay) para: (a) confirmar que el backend responde, (b) saber si la sesión sigue válida
   (`isAuthenticated`), (c) confirmar el `tenantPublicId` activo.
2. **Skew de reloj**: compará `utcNow` con el reloj local para ajustar timers (ej. la expiración del
   access token) si el dispositivo está desfasado.
3. **Diagnóstico de fallos**: ante errores en otras llamadas, un `/system/status` que responde
   `200` confirma que el backend está vivo y el problema es de la request puntual (auth/permiso/datos),
   no de conectividad.

## Estado de la documentación

System es un módulo meta transversal. Ver el [índice maestro](../README.md) para todas las áreas.
