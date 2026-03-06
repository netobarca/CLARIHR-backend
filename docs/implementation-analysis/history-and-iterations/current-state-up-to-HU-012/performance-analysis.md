# Performance Analysis

## Summary

HU-011 y HU-012 estan en buen estado para volumen local/QA y crecimiento moderado.

## Positive Signals

- tablas nuevas con indices tenant-scoped por codigo, estado, jerarquia y categoria
- listados paginados obligatorios en OrgUnits, JobProfiles y JobCatalogs
- uso extendido de `AsNoTracking()` en lecturas
- `tree` y `graph` de OrgUnits se construyen desde una carga unica en memoria (sin N+1)
- cache en memoria por tenant/categoria para busqueda de catalogos, con invalidacion en create/update/activate/inactivate

## Watch Items

- `GetResponseById` de JobProfiles usa varias colecciones relacionadas; el costo de hidratacion puede crecer con perfiles muy densos.
- filtros de busqueda por `Contains` sobre texto normalizado son suficientes hoy, pero requeriran hardening cuando el volumen suba materialmente.
- validaciones de ciclos en JobProfiles dependen de lectura de grafo por tenant; correcto para v1, a monitorear si el numero de perfiles crece fuerte.
