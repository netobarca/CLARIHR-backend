# Remediation Plan

## Status

No se detecto un blocker critico nuevo de arquitectura, seguridad o testing introducido por HU-015.

## Follow-up Candidates (Non-Infrastructure)

1. Extraer `CostCenterCode` a FK fisica opcionalmente dual-write en una fase de migracion controlada.
2. Incorporar cache de lectura de catalogo de centros de costo por tenant para reducir lecturas repetidas.
3. Separar exportadores `csv/xlsx` de controller a servicio dedicado reutilizable.
4. Agregar validaciones contables configurables por tenant para formato de cuentas.
5. Extender analitica de uso historico (series temporales) sin impactar API v1.
