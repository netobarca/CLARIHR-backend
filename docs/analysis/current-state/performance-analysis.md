# Analisis actual de performance

## 1. Resumen ejecutivo

La base de rendimiento del backend sigue siendo razonable para un SaaS administrativo:

- PostgreSQL + EF Core
- proyecciones a DTO
- `AsNoTracking()` visible en muchos repositorios de lectura
- paginacion validada en modulos principales
- cache en componentes de permisos y catalogos
- uploads de documentos de expediente con limite duro de `10 MiB` y validacion de extension/MIME/firma antes de leer el archivo completo en memoria
- exportes de reporte con limite sincrono de `5,000` filas, pipeline asincrono DB-backed para exportes grandes, worker `BackgroundService` y artefactos en Azure Blob Storage
- el ciclo de vida de suscripciones empresariales se resuelve con lecturas paginadas, proyecciones y procesamiento batch fuera del request path para promociones `Scheduled` y expiraciones por fecha

La remediacion del **19 de abril de 2026** corrige el mayor punto caliente confirmado: los exportes grandes ya no deben ejecutarse dentro del request path. Los endpoints sincronos quedan para datasets pequenos y los datasets grandes se derivan al flujo asincrono persistido.

## 2. Evidencia principal observada

### 2.1 Exportes sincronos acotados

Los exportes sincronos de `PERSONNEL_FILES`, acciones de personal, transacciones de planilla, unidades organizativas, plazas, tabulador salarial, centros de costo, representantes legales y matriz de competencias ahora leen como maximo `MaxSynchronousExportRows + 1`.

Si hay overflow, responden `413 REPORT_EXPORT_TOO_LARGE` y no construyen archivo.

### 2.2 Pipeline asincrono de exportes

`ReportExportJob` persiste estado, tenant, recurso, formato, intentos, lease de worker, metadata de artefacto y expiracion. El worker reclama jobs por lotes acotados, genera archivos mediante writer comun y guarda artefactos en Blob Storage con retencion por defecto de `24` horas.

### 2.3 Analytics y lecturas pesadas

`GetAnalyticsSummaryAsync` de expedientes ya no reutiliza el dataset de export. Calcula conteos y agrupaciones en base de datos y luego mapea labels en memoria.

Las lecturas de detalle pesado de expediente con muchos `Include()` usan `AsSplitQuery()` para reducir riesgo de cartesian explosion.

## 3. Patrones saludables que si se mantienen

- listados principales con paginacion
- consultas con `AsNoTracking()` en varias lecturas
- uso amplio de proyecciones y DTOs en repositorios
- writer comun `ReportExportFileWriter` para CSV/XLSX/JSON, reutilizado por controllers y worker de exportes
- controllers de export sincrono sin builders CSV/XLSX duplicados para los recursos cubiertos
- los catalogos globales de plataforma para planes y add-ons mantienen lectura paginada, `AsNoTracking()` e indices por codigo, estado y filtros comerciales; `CommercialAddon` ahora filtra tambien por `type` y `billingModel` con indice dedicado sobre `billing_model`
- los catalogos internos globales de requisitos usan `normalized_value`, indice unico por `catalog_key + normalized_value`, indice `GIN` con `pg_trgm` para similitud y ranking server-side por exactitud, prefijo, score y uso
- el backoffice global de suscripciones mantiene overview, historial por empresa, historial de estados y listado global con paginacion, `AsNoTracking()` y ordenamiento server-side sobre columnas persistidas
- las reactivaciones programadas usan una tabla dedicada con indice por `status + effective_date_utc` y unicidad parcial por suscripcion para evitar duplicados en el request path
- las transiciones automaticas de suscripcion por fecha se ejecutan en `CompanySubscriptionLifecycleBackgroundService`, con lotes acotados para promociones, reactivaciones programadas y vencimientos, evitando mover ese costo al request path y aplicando primero reactivaciones antes de cambios de plan o add-ons del mismo dia
- segregacion CQRS que facilita aislar optimizaciones
- build limpio y test suite estable, lo que ayuda a refactorizar sin romper flujo funcional

## 4. Riesgos confirmados

### 4.1 Exportes grandes dependen de storage configurado

El flujo asincrono es el camino canonico para datasets grandes. Si Azure Blob Storage no esta configurado, el endpoint de jobs responde `503 REPORT_EXPORT_STORAGE_NOT_CONFIGURED`; eso evita trabajo pesado sin destino de artefacto, pero exige configuracion operativa antes de habilitar exportes grandes en ambientes reales.

### 4.2 Limite asincrono y vencimiento de artefactos

Los jobs asincronos tienen limite de `100,000` filas y artefactos con expiracion de `24` horas. Esto protege memoria y almacenamiento, pero implica que clientes deben reencolar si intentan descargar despues de vencido el artefacto.

### 4.3 Diagramas sincronos acotados

Los diagram exports de `ORG_UNITS` y `POSITION_SLOTS` siguen siendo sincronos por ser formatos de grafo, pero ahora aplican limite de `5,000` nodos y responden `413 REPORT_EXPORT_LIMIT_EXCEEDED` si el grafo supera esa frontera.

### 4.4 Uploads de documentos con guardrail de tamano

El upload de documentos de expediente ya no lee archivos arbitrariamente grandes antes de validar. La API rechaza `IFormFile.Length > 10 MiB` con `413` y formatos no permitidos con `400` antes de copiar el stream completo a memoria.

Este cambio reduce riesgo de disponibilidad por payloads grandes en el endpoint de documentos, aunque no reemplaza una estrategia futura de streaming/antivirus/storage externo si el volumen documental crece.

## 5. Conclusiones

No hay evidencia de un colapso estructural general de performance. La base sigue siendo buena para volumen medio y el mayor riesgo de request path pesado ya quedo acotado:

- los exportes pequenos siguen siendo sincronos, pero con limite duro
- los exportes grandes salen del request path mediante jobs persistidos y Blob Storage
- analytics de `PersonnelFiles` ya no materializa filas de export
- el riesgo de memoria por upload de documentos queda acotado por limite duro de tamano y validacion temprana
- el margen de crecimiento por tenant ahora depende principalmente de tuning de indices, batch size, storage y observabilidad del worker

## 6. Recomendaciones inmediatas

1. Configurar `BlobStorage:ReportExportsContainer` y credenciales por ambiente antes de habilitar jobs asincronos en produccion.
2. Instrumentar latencia, filas, tamano de artefacto, intentos y errores del worker de exportes.
3. Revisar planes de retencion y limpieza fisica de blobs si el volumen de jobs crece.
4. Validar con carga real los indices agregados para `personnel_files` y ajustar patrones de sort/filtro segun telemetry.
