# Performance Analysis

## Summary

HU-014 queda en estado adecuado para volumen local/QA y crecimiento moderado.

## Positive Signals

- tabla de lineas y solicitudes con indices tenant-scoped por clase/escala, estado y request number
- listados paginados obligatorios en lineas y solicitudes
- uso de `AsNoTracking()` en consultas de lectura
- export `csv/xlsx` basado en proyeccion unica por query
- calculo de impacto de solicitud sobre datos proyectados

## Watch Items

- validacion de traslapes de vigencia se apoya en reglas de aplicacion; para escala alta podria requerir hardening adicional en DB.
- exportes grandes se generan en memoria; conviene streaming para datasets muy altos.
- busquedas por `Contains` son suficientes en v1, pero deben revisarse al aumentar cardinalidad.
