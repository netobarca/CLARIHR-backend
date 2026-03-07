# Performance Analysis

## Summary

HU-013 queda en estado adecuado para volumen local/QA y crecimiento moderado.

## Positive Signals

- tabla `position_slots` con indices tenant-scoped por codigo, estado y dependencias
- listados paginados obligatorios con filtros indexables
- lecturas con `AsNoTracking()` en consultas de listado, detalle, grafo y export
- grafo construido en memoria desde una sola carga del tenant, evitando N+1
- dataset unico reutilizado para export `csv/xlsx`

## Watch Items

- filtros `Contains` sobre textos normalizados funcionan en v1, pero requeriran hardening cuando crezca volumen.
- exportes grandes (`xlsx/graphml`) hoy se generan en memoria; para datasets masivos convendra streaming.
- `GetGraphNodesAsync` carga el subconjunto del tenant; en estructuras muy grandes puede requerir paginacion/particionado por raiz.
