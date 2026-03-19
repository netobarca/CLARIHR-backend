# Analisis actual de testing

## 1. Resumen ejecutivo

La estrategia de pruebas actual esta centrada en dos capas:

- unit tests sobre Domain, Application, validadores, autorizacion y servicios puros
- integration tests HTTP sobre la API completa con base PostgreSQL efimera

No hay evidencia en el repositorio de pruebas de carga, pruebas E2E de interfaz ni snapshots automatizados del contrato OpenAPI.

## 2. Estado validado

Snapshot validado el **18 de marzo de 2026**:

- `168` unit tests aprobados
- `123` integration tests aprobados
- `291` tests en total detectados en el repositorio

## 3. Estructura de pruebas actual

### 3.1 Unit tests

Proyecto: `tests/CLARIHR.Application.UnitTests`

Cobertura visible sobre:

- auth y provisionamiento
- dispatchers y dependency injection
- tenancy
- autorizacion RBAC
- permisos por campo
- modulos de locations
- org units
- job profiles
- position slots
- personnel files
- salary tabulator
- competency framework
- cost centers

### 3.2 Integration tests

Proyecto: `tests/CLARIHR.Api.IntegrationTests`

El harness usa:

- `WebApplicationFactory<Program>`
- autenticacion de prueba via `TestAuthenticationHandler`
- base PostgreSQL efimera
- `EnsureDeletedAsync()` y `EnsureCreatedAsync()` por escenario
- seeding controlado por `IntegrationTestSeeder`

## 4. Fortalezas actuales

- existen pruebas reales de API, no solo unit tests
- los tests integrados ejercitan wiring de ASP.NET Core, EF Core y seguridad
- la estrategia prueba escenarios funcionales de onboarding, auth y modulos administrativos
- hay buena cobertura de reglas de dominio y validadores
- tenant context y autorizacion tienen pruebas dedicadas

## 5. Huecos actuales

### 5.1 Sin pruebas de contrato versionado

La API tiene Swagger runtime, pero no hay snapshot versionado o validacion automatica de `openapi.yaml`.

### 5.2 Sin pruebas de performance

No hay pruebas de carga, smoke de rendimiento ni validacion automatica de rutas pesadas.

### 5.3 Sin UI E2E

El repositorio backend no contiene pruebas de interfaz o flujos de punta a punta con frontend.

### 5.4 Integracion sin migraciones

Los integration tests recrean base con `EnsureCreatedAsync()`, lo cual sirve para validacion funcional rapida, pero no prueba el pipeline real de migraciones.

## 6. Criterio recomendado para nuevas historias

Toda HU o requerimiento backend relevante deberia seguir esta expectativa minima:

- unit tests del handler o regla critica
- pruebas de validacion
- pruebas de permisos
- pruebas de tenant isolation
- integration test cuando el comportamiento HTTP o el wiring sean importantes

## 7. Conclusiones

La base de testing actual es buena para evolucion diaria del backend y ya supera un nivel minimo saludable. Las siguientes mejoras naturales son:

1. automatizar contrato API
2. agregar pruebas dirigidas a endpoints de alto riesgo
3. definir cuando un cambio requiere integration test obligatorio
4. incorporar validaciones de migraciones y, mas adelante, pruebas de carga
