# CLARIHR Backend - Project Context

## Project Overview
CLARIHR es un sistema de gestión de recursos humanos (HRMS) multi-tenant desarrollado en **.NET 8/9** siguiendo los principios de **Clean Architecture** y **CQRS**. El sistema permite la gestión integral de expedientes de personal, estructura organizacional, nómina, y administración de suscripciones comerciales.

### Core Technologies
- **Language:** C# 12+
- **Framework:** ASP.NET Core 8/9
- **Persistence:** Entity Framework Core con PostgreSQL (Npgsql)
- **CQRS & Messaging:** Implementación propia de `ICommandDispatcher` / `IQueryDispatcher` (basada en el patrón Mediator).
- **Validation:** FluentValidation
- **Logging:** Serilog
- **Testing:** xUnit, FluentAssertions, y pruebas de integración con `WebApplicationFactory`.
- **API Documentation:** Swagger/OpenAPI (Swashbuckle).

## Architecture & Structure
El proyecto sigue una estructura de Clean Architecture dividida en:

- **src/CLARIHR.Domain:** Entidades de dominio, Value Objects, Domain Events y lógica de negocio pura. No tiene dependencias externas.
- **src/CLARIHR.Application:** Casos de uso (Commands/Queries), Handlers, DTOs de respuesta y abstracciones de infraestructura.
- **src/CLARIHR.Infrastructure:** Implementaciones de persistencia (DbContext, Repositorios), servicios externos (Email, Storage), y seguridad (JWT, Auth).
- **src/CLARIHR.Api:** Punto de entrada principal para los tenants/compañías.
- **src/CLARIHR.Backoffice.Api:** API dedicada a la administración global de la plataforma (operadores de CLARI).

### Key Architectural Concepts
- **Multi-tenancy:** Aislamiento de datos mediante `TenantId`. Se utiliza un `ITenantContext` para propagar el contexto del tenant activo basado en el JWT.
- **Country Scoped Catalogs:** Muchos catálogos (como los de educación o ubicaciones) están compartidos a nivel de país (`CountryScopedCatalogItem`) para mantener la integridad de datos entre compañías de la misma región.
- **RBAC:** Sistema de control de acceso basado en roles y permisos específicos por tenant.

## Building and Running

### Prerequisites
- .NET 8.0 SDK o superior.
- PostgreSQL 16+.

### Key Commands
- **Build Solution:** `dotnet build`
- **Run Core API:** `dotnet run --project src/CLARIHR.Api/CLARIHR.Api.csproj`
- **Run Backoffice API:** `dotnet run --project src/CLARIHR.Backoffice.Api/CLARIHR.Backoffice.Api.csproj`
- **Run Tests:** `dotnet test`
- **Database Migrations:**
  - Crear: `dotnet ef migrations add <Name> --project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api`
  - Aplicar: `dotnet ef database update --project src/CLARIHR.Infrastructure --startup-project src/CLARIHR.Api`

## Development Conventions

### Coding Style & Standards
- **Global Usings & Nullable:** Habilitados en `Directory.Build.props`.
- **Treat Warnings as Errors:** Configurado para asegurar la calidad del código.
- **CQRS Handlers:** Los comandos deben ser inmutables (records) y los handlers deben retornar un objeto `Result<T>`.
- **Surgical Edits:** Preferir el uso de la herramienta `replace` para modificaciones precisas en archivos grandes.

### Specialized Agent Skills
El proyecto cuenta con "skills" especializadas para el desarrollo asistido por IA localizadas en `.agents/skills/`. Estas skills deben activarse mediante `activate_skill` para tareas específicas:
- `implement-dotnet-cqrs-user-story`: Para implementar HUs completas.
- `unit-test-dotnet-cqrs-user-story`: Para generar pruebas unitarias.
- `review-dotnet-cqrs-user-story`: Para revisiones de código y calidad.
- `close-user-story-docs`: Para el cierre documental de tareas.

### Catalog Management
Los catálogos que heredan de `CountryScopedCatalogItem` (como los de educación analizados) se consideran **Catálogos de Sistema**. Su administración CRUD debe realizarse idealmente desde el Backoffice para evitar redundancias y riesgos de integridad cross-tenant, exponiéndolos en la API pública solo para lectura.

## Technical Documentation
- Ubicación: `docs/`
- Flujos de Negocio: `docs/business/current-system-business-flows.md`
- Análisis de controladores: `docs/analysis/`
