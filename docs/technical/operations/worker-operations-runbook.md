# Worker de export de reportes — runbook de operación (§9.2)

> **Para qué sirve**: operar y diagnosticar el worker que procesa los jobs
> asíncronos de export (`ReportExportJobBackgroundService`): qué métricas mirar,
> qué logs filtrar, cómo escalar y cómo reaccionar ante los modos de falla
> comunes. Se apoya en la telemetría dedicada (§6.1) ya emitida por el worker.

## Qué es y dónde corre

- **Servicio**: `ReportExportJobBackgroundService` (un `BackgroundService` de .NET),
  registrado con `AddHostedService` en `CLARIHR.Infrastructure/DependencyInjection.cs`.
- **Dónde**: corre **in-process dentro de la API** (no hay proceso/host separado).
  Cada instancia de la API ejecuta su propio worker. Escalar el worker = correr
  más instancias de la API (ver *Cómo escalar*).
- **Qué hace cada ciclo** (`ExecuteCycleAsync`): cada `WorkerPollIntervalSeconds`
  (default 15 s) reclama hasta `WorkerBatchSize` (default 2) jobs vencidos, los
  procesa (genera el artefacto → lo sube a Blob Storage → marca el job), y emite
  un evento de cierre de ciclo. Un fallo del ciclo completo se captura y loguea
  sin tumbar el servicio (el siguiente tick reintenta).
- **Concurrencia segura entre instancias**: cada job se reclama con un
  `worker_id` y un **lease** (`ClaimLeaseMinutes`, default 15 min). Dos instancias
  no procesan el mismo job: el segundo intento de claim pierde y emite
  `report_export_job_claim_conflict` (esperado bajo multi-instancia). Si una
  instancia muere a mitad de un job, el lease expira y otra lo re-reclama.

## Ciclo de vida de un job

```
Queued ──claim──▶ Running ──ok──────────────▶ Succeeded ──(+24h)──▶ Expired
                     │                              (artefacto borrado)
                     ├─ error, attempt<max ──▶ Queued (retry)
                     └─ error, attempt=max ──▶ Failed (terminal)
Queued/Running ──cancel──▶ Cancelled
```

- **Retries**: hasta `MaxAttempts` (default 3). Cada reintento emite
  `report_export_job_retry_scheduled`; al agotarlos, `report_export_job_failed_terminal`
  con `error_code`/`error_message`.
- **Expiración**: el artefacto vive `ArtifactRetentionHours` (default 24 h) desde
  `completedUtc`; luego se borra y el job pasa a `Expired`. La descarga de un job
  expirado responde `410 REPORT_EXPORT_JOB_EXPIRED`.

## Configuración (`Reporting:Performance`)

Definida en `CLARIHR.Infrastructure/Configuration/ReportPerformanceOptions.cs`.
Se sobre-escribe por entorno con `Reporting__Performance__<Clave>`.

| Clave | Default | Qué controla |
|---|---|---|
| `WorkerPollIntervalSeconds` | `15` | Cada cuánto el worker busca trabajo. |
| `WorkerBatchSize` | `2` | Jobs reclamados/procesados por ciclo (slots de render en paralelo). |
| `ClaimLeaseMinutes` | `15` | Duración del lease de un job reclamado (re-claim tras crash). |
| `MaxAttempts` | `3` | Intentos antes de marcar el job como `Failed` terminal. |
| `ArtifactRetentionHours` | `24` | Vida del artefacto antes de expirar y borrarse. |
| `ExportBatchSize` | `1000` | Tamaño de lote de lectura de filas (exports tabulares). |
| `MaxAsyncExportRows` | `100000` | Tope de filas por job asíncrono. |
| `MaxDocumentBytes` | `52428800` (50 MB) | Tope de tamaño de documento renderizado (fail-fast antes del cap de storage). |
| `MaxDiagramNodes` | `5000` | Tope de nodos para diagram exports síncronos. |
| `MaxSynchronousExportRows` | `5000` | Tope de filas para exports síncronos. |

## Telemetría — qué mirar

Todos los `EventId` están en `CLARIHR.Infrastructure/Reports/ReportExportTelemetryEvents.cs`
(rango `400xx`). Filtra logs por el nombre del evento o por `EventId`.

**Ciclo del worker**

| EventId | Nombre | Nivel | Para qué |
|---|---|---|---|
| `40001` | `report_export_worker_cycle_completed` | Info | Ciclo con trabajo. Trae `claimed/processed/succeeded/retried/failed/concurrency_skipped/expired_count`, `cycle_duration_ms`, `worker_batch_size`, `poll_interval_seconds`. |
| `40002` | `report_export_worker_cycle_empty` | Debug | Ciclo sin trabajo (latido). |
| `40003` | `report_export_worker_cycle_failed` | **Error** | El ciclo completo lanzó excepción. **Alerta**: si es recurrente, el worker no avanza. |

**Ciclo de vida del job** (dimensiones comunes: `job_id`, `tenant_id`, `resource_key`, `format`, `worker_id`, `attempt`, `max_attempts`, `queue_latency_ms`)

| EventId | Nombre | Nivel | Para qué |
|---|---|---|---|
| `40004` | `report_export_job_started` | Info | Job tomado. `queue_latency_ms` = tiempo en cola (señal de saturación). |
| `40005` | `report_export_job_succeeded` | Info | OK. Trae `processing_duration_ms`, `row_count`, `artifact_size_bytes`. |
| `40006` | `report_export_job_retry_scheduled` | Warning | Falló pero reintentará. Trae `error_code`. |
| `40007` | `report_export_job_failed_terminal` | **Error** | Agotó intentos. Trae `error_code`/`error_message`. **Alerta** principal. |
| `40008` | `report_export_job_claim_conflict` | Info | Claim perdido (normal con multi-instancia; preocupa solo si es masivo). |

**Artefactos**

| EventId | Nombre | Nivel | Para qué |
|---|---|---|---|
| `40009` | `report_export_artifact_expired` | Info | Artefacto expiró y se borró (limpieza normal). |
| `40010` | `report_export_artifact_delete_failed` | Warning | No se pudo borrar del storage → posible fuga de blobs; revisar storage. |

**Subdominio de generación de documento / PDF (§6.1)**

| EventId | Nombre | Nivel | Para qué |
|---|---|---|---|
| `40011` | `report_export_pdf_render_started` | Info | Inicio de render PDF. |
| `40012` | `report_export_pdf_render_succeeded` | Info | Fin de render. Trae **`render_duration_ms`** y **`pdf_size_bytes`** → p95 de latencia de render y distribución de tamaño por recurso. |

**Métricas/alertas sugeridas**

- **Alerta**: tasa de `40003` (cycle_failed) y conteo de `40007` (failed_terminal) > 0 sostenido.
- **Salud**: `queue_latency_ms` (40004) creciente = backlog; `cycle_duration_ms` vs `poll_interval_seconds`.
- **Rendimiento de render**: p95 de `render_duration_ms` y p95/máx de `pdf_size_bytes` (40012); acercarse a `MaxDocumentBytes` (50 MB) anticipa fallos por documento patológico.
- **Capacidad**: `claimed_count` ≈ `WorkerBatchSize` en cada ciclo de forma sostenida = el worker está saturado (ver *Cómo escalar*).

## Cómo filtrar logs

```bash
# Todos los eventos del worker (por prefijo del nombre)
grep "report_export_" <log>

# Trazar un job específico de punta a punta
grep "job_id <GUID>" <log>

# Solo fallos accionables
grep -E "report_export_(worker_cycle_failed|job_failed_terminal|artifact_delete_failed)" <log>
```

En producción (Serilog → Azure), filtrar por la propiedad estructurada `EventId`
o por las dimensiones (`job_id`, `tenant_id`, `resource_key`).

## Cómo escalar

El cuello de botella y la estrategia están medidos en
[`worker-load-testing.md`](./worker-load-testing.md) (§8.2). Resumen operativo:

1. **Subir `WorkerBatchSize` por encima de ~2 no ayuda** al render in-process
   (QuestPDF se serializa internamente; pasar de 2 a 4 no sube throughput y
   duplica la latencia).
2. **Para más capacidad, escalar horizontalmente**: correr más instancias de la
   API. El claim/lease (`worker_id`) garantiza que no se duplique el procesamiento.
3. En **producción el motor es Gotenberg** (HTTP, §4.2): ahí el cuello de botella
   se desplaza al servicio Gotenberg → escalar también sus réplicas y medir su
   throughput aparte.
4. Un pico de month-end (1000+ exports) es del orden de **segundos de render
   puro**; el límite real suele ser BD/storage/HTTP, no el render.

## Modos de falla y diagnóstico

| Síntoma | Causa probable | Acción |
|---|---|---|
| Jobs quedan en `Queued`, nunca `Running` | Worker no corre (API caída) o `40003` recurrente | Verificar que la API esté arriba; revisar `report_export_worker_cycle_failed`. |
| Crear job responde `503 REPORT_EXPORT_STORAGE_NOT_CONFIGURED` | Blob Storage no configurado | Revisar `Storage`/conexión de Azure Blob (ver `production-deployment.md`). |
| Muchos `40007` (failed_terminal) | Error sistemático de un recurso | Inspeccionar `error_code`/`error_message` y `lastErrorCode`/`lastErrorMessage` del job. |
| `queue_latency_ms` crece sostenido | Worker saturado (`claimed_count` ≈ batch) | Escalar horizontalmente (más instancias). |
| `40010` (artifact_delete_failed) recurrente | Permisos/conexión a storage | Revisar credenciales y conectividad del contenedor de blobs. |
| Render lento o cerca de 50 MB (40012) | Perfil patológico (muchas secciones) | Revisar el perfil; `MaxDocumentBytes` corta antes del cap de storage. |
| `40008` (claim_conflict) masivo | Demasiadas instancias para tan poco trabajo | Normal en baja escala; reducir instancias si es puro ruido. |

## Referencias

- Telemetría: `CLARIHR.Infrastructure/Reports/ReportExportTelemetryEvents.cs`
- Worker: `CLARIHR.Infrastructure/Reports/ReportExportJobBackgroundService.cs` · processor: `ReportExportJobProcessor.cs`
- Config: `CLARIHR.Infrastructure/Configuration/ReportPerformanceOptions.cs`
- Load testing y escalado: [`worker-load-testing.md`](./worker-load-testing.md)
- Contrato de los endpoints: [`../api/endpoint-reference.md`](../api/endpoint-reference.md) §2.9 y §4.9
