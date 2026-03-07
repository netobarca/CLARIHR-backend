# Performance Analysis

## Scope

Analisis de rendimiento para HU-015 (`CostCenters`) sobre baseline HU-014.

## Findings

1. Query paths de lectura usan `AsNoTracking` y proyecciones.
2. Listado paginado obligatorio para busquedas (`page`, `pageSize`).
3. Filtros indexables:
   - `tenant_id + type + is_active`
   - `tenant_id + normalized_name`
4. `usage` se resuelve con consultas agregadas por tenant/codigo sin N+1.
5. Export `csv/xlsx` reutiliza dataset proyectado unico.

## Risks

- Busqueda por texto libre con `Contains` sobre columnas normalizadas puede degradar en volumen extremo sin estrategia adicional (trigram/full-text).

## Conclusion

HU-015 cumple objetivos de performance para v1 y escalamiento medio.
