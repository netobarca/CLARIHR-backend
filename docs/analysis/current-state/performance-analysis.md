# Analisis actual de performance

## 1. Resumen ejecutivo

La base de rendimiento del backend sigue siendo razonable para un SaaS administrativo:

- PostgreSQL + EF Core
- proyecciones a DTO
- `AsNoTracking()` visible en muchos repositorios de lectura
- paginacion validada en modulos principales
- cache en componentes de permisos y catalogos
- el ciclo de vida de suscripciones empresariales se resuelve con lecturas paginadas, proyecciones y procesamiento batch fuera del request path para promociones `Scheduled` y expiraciones por fecha

La reevaluacion profunda del **28 de marzo de 2026** confirma, sin embargo, que el mayor riesgo de rendimiento ya no es teorico: los exportes y algunos reportes pesados se ejecutan hoy en el request path, materializan colecciones completas en memoria y luego construyen archivos en la capa API.

## 2. Evidencia principal observada

### 2.1 Exportes completos en memoria

[PersonnelFileRepository.cs#L509](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileRepository.cs#L509) exporta filas de expedientes con `ToArrayAsync()` sin paginacion ni streaming.

[PersonnelFileEmployeeRepository.cs#L203](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileEmployeeRepository.cs#L203) y [PersonnelFileEmployeeRepository.cs#L277](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileEmployeeRepository.cs#L277) hacen lo mismo para personnel actions y payroll transactions.

### 2.2 Analytics apoyado en dataset de export

[PersonnelFileRepository.cs#L556](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Infrastructure/PersonnelFiles/PersonnelFileRepository.cs#L556) reutiliza `GetExportRowsAsync()` para construir el analytics summary. Eso simplifica implementacion, pero vuelve el resumen dependiente del costo del export completo.

### 2.3 Generacion de archivos en la capa API

[PersonnelFileReportingController.cs#L151](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFileReportingController.cs#L151) audita, persiste y arma CSV/XLSX dentro del request.

[PersonnelFileCompensationController.cs#L226](/Users/christophercanas/Developments/CLARI%20NEW%20VERSION/clarihr-backend/CLARIHR-backend/src/CLARIHR.Api/Controllers/PersonnelFileCompensationController.cs#L226) repite el mismo patron para payroll transactions.

## 3. Patrones saludables que si se mantienen

- listados principales con paginacion
- consultas con `AsNoTracking()` en varias lecturas
- uso amplio de proyecciones y DTOs en repositorios
- los catalogos globales de plataforma para planes y add-ons mantienen lectura paginada, `AsNoTracking()` e indices por codigo, estado y filtros comerciales; `CommercialAddon` ahora filtra tambien por `type` y `billingModel` con indice dedicado sobre `billing_model`
- el backoffice global de suscripciones mantiene overview, historial por empresa, historial de estados y listado global con paginacion, `AsNoTracking()` y ordenamiento server-side sobre columnas persistidas
- las transiciones automaticas de suscripcion por fecha se ejecutan en `CompanySubscriptionLifecycleBackgroundService`, con lotes acotados para promociones y vencimientos, evitando mover ese costo al request path
- segregacion CQRS que facilita aislar optimizaciones
- build limpio y test suite estable, lo que ayuda a refactorizar sin romper flujo funcional

## 4. Riesgos confirmados

### 4.1 Riesgo de memoria y latencia en tenants grandes

Los exportes actuales pueden crecer con el volumen total del tenant porque no hay:

- streaming de respuesta
- limites duros de filas para export
- background jobs
- desacople entre consulta pesada y descarga

### 4.2 Request path con trabajo demasiado pesado

El costo no es solo la query. En la misma request se hacen:

- lectura completa
- auditoria
- `SaveChangesAsync()`
- construccion del archivo
- serializacion y entrega del binario

Eso vuelve mas fragil la latencia, el consumo de memoria y el tiempo de bloqueo del request.

### 4.3 Mezcla de preocupaciones que dificulta optimizar

Al tener parte del trabajo pesado en controllers, cualquier estrategia seria de streaming, jobs asincronos o politicas de observabilidad queda mas costosa de implementar.

## 5. Conclusiones

No hay evidencia de un colapso estructural general de performance. La base sigue siendo buena para volumen medio. El problema confirmado es mas concreto:

- los exportes y analytics de `PersonnelFiles` son hoy el principal punto caliente del backend
- la solucion actual probablemente aguanta cargas moderadas
- el margen de crecimiento por tenant esta comprometido si no se saca este trabajo del request path

## 6. Recomendaciones inmediatas

1. Definir umbral de filas por export y bloquear exportes demasiado grandes en request sin job asincrono.
2. Mover CSV/XLSX pesados a un servicio dedicado o a procesamiento diferido.
3. Reescribir analytics summary para agregar en base de datos en lugar de reaprovechar datasets de export.
4. Instrumentar latencia y tamano de respuesta por endpoint de reportes y exports.
