# Worker — load testing del render de PDF (§8.2)

> **Para qué sirve**: medir cuántos PDFs/segundo puede renderizar el worker
> (`ReportExportJobProcessor`) y cómo escala con la concurrencia y con el tamaño
> del perfil. Responde la pregunta abierta de §8.2: *"se desconoce cuántos
> PDFs/segundo soporta un worker ni cómo escala con perfiles ricos vs. vacíos"*.

## Qué mide

`tests/CLARIHR.Worker.LoadTests` es un **harness propio sin dependencias** (no usa
NBomber ni otra librería con licencia comercial). Ejercita **directamente** el
render in-process (`JobProfilePdfRenderer` → QuestPDF) —sin HTTP/DB/cola— con N
tareas concurrentes durante D segundos, y reporta throughput (PDFs/s) y latencia
(p50/p95/p99) para un perfil **vacío** y uno **rico**.

> El render in-process (QuestPDF) es la parte CPU-bound del worker. La ruta de
> producción (Gotenberg, §4.2) es HTTP-bound y se mediría aparte contra el servicio.

## Cómo correrlo

```bash
dotnet run --project tests/CLARIHR.Worker.LoadTests -- [concurrency] [durationSeconds]
# ej.: concurrency=2 (= WorkerBatchSize default), 30s por escenario
dotnet run --project tests/CLARIHR.Worker.LoadTests -- 2 30
```

`concurrency` simula los slots de render paralelos del worker
(`Reporting:Performance:WorkerBatchSize`, default 2). No es un test de `dotnet test`.

## Baseline (MacBook, 11 cores, .NET 10, QuestPDF 2024.12.3)

| Concurrencia | Escenario | Throughput | p50 | p95 | p99 | fails |
|---|---|---|---|---|---|---|
| 2 | vacío | **457 PDFs/s** | 4.1 ms | 5.5 ms | 6.4 ms | 0 |
| 2 | rico  | **274 PDFs/s** | 6.9 ms | 9.2 ms | 10.0 ms | 0 |
| 4 | vacío | 452 PDFs/s | 8.3 ms | 10.8 ms | 14.4 ms | 0 |
| 4 | rico  | 272 PDFs/s | 14.5 ms | 17.7 ms | 23.0 ms | 0 |

> Los números son relativos a la máquina; lo importante son las **tendencias**.

## Conclusiones

1. **Capacidad**: un worker rinde ~**450 PDFs/s** (perfil vacío) / ~**270 PDFs/s**
   (perfil rico). Un pico de month-end de 1000+ exports es **render puro de ~2–4 s**;
   el cuello de botella real será BD/storage/HTTP, no el render.
2. **Rico vs. vacío**: el perfil rico cuesta ~**40 % más** (más secciones/tablas).
3. **No escala con la concurrencia**: subir de 2 a 4 concurrentes **no aumenta el
   throughput** (452 vs 457) y **duplica la latencia** → el render de QuestPDF se
   **serializa internamente** (~2 renders útiles en paralelo). **Subir
   `WorkerBatchSize` por encima de ~2 no ayuda**; para más capacidad, **escalar
   horizontalmente** (más instancias de worker) en lugar de agrandar el batch.

## Cuándo re-medir

- Antes del primer pico esperado (month-end con 1000+ exports concurrentes).
- Tras cambiar el motor de PDF (p. ej. validar throughput de **Gotenberg** vía HTTP).
- Tras cambios en el mapper/AST o en perfiles "patológicos" (muchas competencias/funciones).
