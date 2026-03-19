# Analisis actual de performance

## 1. Resumen ejecutivo

El backend esta construido con una base razonable de rendimiento para un SaaS transaccional:

- PostgreSQL + EF Core
- proyecciones a DTO
- `AsNoTracking()` en muchas lecturas
- paginacion validada por modulo
- cache en memoria para algunos componentes de permisos y catalogos

La mayor tension actual esta en exportes, diagramas y reportes ricos que aun se procesan sin background jobs.

## 2. Evidencia visible en codigo

### 2.1 Lecturas optimizadas

Hay uso extendido de `AsNoTracking()` en repositorios de lectura, por ejemplo en:

- `Companies`
- `Locations`
- `OrgUnits`
- `JobProfiles`
- `CompetencyFramework`
- `PersonnelFiles`
- `Audit`

### 2.2 Paginacion

Los modulos principales validan `PageSize` y definen `DefaultPageSize` y `MaxPageSize`. En varios dominios el maximo visible es `100`, incluyendo `PersonnelFiles`, `SalaryTabulator`, `LegalRepresentatives` y otros.

### 2.3 Cache

Actualmente existe al menos:

- `AddMemoryCache()`
- cache para overrides de permisos por campo
- cache para algunos catalogos

Esto reduce costo repetido de resolucion de permisos y metadata usada frecuentemente.

## 3. Patrones saludables actuales

- listados principales con paginacion
- validacion de filtros y sorting soportado
- joins y proyecciones en repositorios en lugar de exponer entidades completas
- segregacion CQRS que facilita optimizar lecturas y escrituras por separado
- `AllowedActions` calculado opcionalmente, no obligatorio en todas las lecturas

## 4. Zonas potencialmente costosas

### 4.1 Exportes

Hay varios modulos con exportes:

- org units
- position slots
- legal representatives
- job profiles
- competency matrix
- salary tabulator
- personnel files
- personnel actions
- payroll transactions

Muchos de estos exportes se generan en el request path y en memoria.

### 4.2 Diagramas y grafos

Los modulos organizacionales exponen:

- `graph`
- `diagram-export`

Estos endpoints pueden crecer en costo conforme aumente la complejidad jerarquica del tenant.

### 4.3 Personnel file reporting

El modulo `PersonnelFiles` soporta:

- detalle completo
- `print`
- `dynamic-query`
- `analytics summary`
- exportes especializados

Es una zona de crecimiento natural en volumen y carga.

### 4.4 Auditoria

La auditoria agrega valor, pero tambien costo adicional por persistencia y por resolucion de correo de actor al registrar eventos.

## 5. Impacto de diseno actual

### 5.1 Bueno para volumen medio

La arquitectura actual es adecuada para cargas operativas normales de un sistema administrativo multi-tenant.

### 5.2 Riesgo en operaciones pesadas

Cuando aumente el volumen por tenant, los primeros candidatos a tension seran:

- exportes grandes
- impresion rica de recursos complejos
- consultas dinamicas
- arboles y grafos organizacionales
- tabulador salarial con historial y analisis de impacto

## 6. Riesgos actuales

- exportes y reportes sin procesamiento asincrono
- posible uso intensivo de memoria en respuestas grandes
- alto numero de endpoints administrativos sin observabilidad detallada de latencia por modulo
- OpenAPI no versionado, lo que dificulta visibilidad automatizada del costo observable de la API

## 7. Conclusiones

El estado actual no muestra un problema estructural grave de rendimiento. La base esta bien orientada, pero la siguiente madurez deberia enfocarse en:

1. observabilidad por modulo y endpoint
2. criterios para sacar exportes grandes del request path
3. revisiones periodicas de consultas dinamicas y reportes
4. definicion de umbrales operativos para respuestas pesadas
