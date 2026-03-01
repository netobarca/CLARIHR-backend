# CLARIHR Backend Bootstrap

## Objetivo

Bootstrap inicial del backend `CLARIHR` con:

- .NET 10
- Clean Architecture
- CQRS
- `Result` / `ProblemDetails`
- contexto multi-tenant por claim `tid`
- wiring base para EF Core

## Estructura

- `src/CLARIHR.Domain`: entidades base, tenant scope, auditoria, domain events
- `src/CLARIHR.Application`: CQRS, errores, contratos, caso de uso inicial `GetApiStatus`
- `src/CLARIHR.Infrastructure`: `DbContext`, tenant context, current user, reloj del sistema
- `src/CLARIHR.Api`: controllers, middleware, auth wiring y ProblemDetails mapping
- `tests/CLARIHR.Application.UnitTests`: pruebas del dispatcher CQRS y validacion

## Modulos actuales

- `Auth`: registro local con email/password, hashing y JWT access token
- `Provisioning`: empresa inicial + plan free + membership primaria + seeds IAM
- `IdentityAccess`: administracion de usuarios, roles y permisos RBAC nivel 3

## Notas de infraestructura

- El `DbContext` queda preparado para PostgreSQL.
- En este entorno no estaba disponible localmente el provider `Npgsql.EntityFrameworkCore.PostgreSQL`, por eso la configuracion usa un bootstrap dinamico y falla de forma explicita si intentas usar una cadena de conexion sin agregar ese paquete.
- Cuando tengas acceso al paquete, agregalo al proyecto `src/CLARIHR.Infrastructure`.

## Arranque local

1. Configura `Authentication:Jwt` y `Database:ConnectionString` con `dotnet user-secrets` o variables de entorno.
2. Agrega el provider PostgreSQL en infraestructura si vas a usar base de datos real.
3. Ejecuta `dotnet restore`.
4. Ejecuta `dotnet build`.
5. Ejecuta `dotnet test`.
6. Ejecuta `dotnet run --project src/CLARIHR.Api`.
