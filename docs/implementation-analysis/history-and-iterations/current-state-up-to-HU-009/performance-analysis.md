# Performance Analysis

## Summary

El impacto de HU-009 es bajo y controlado.

## Positive points

1. `GET /api/account/companies` es paginado.
2. Las lecturas de ownership usan `AsNoTracking`.
3. El switch de empresa y las mutaciones operan sobre un conjunto pequeño de memberships.
4. Se dejo script de indices para ownership/listado:
   - `docs/technical/sql/hu009_account_companies.sql`

## Tradeoff accepted

El conteo de empresas activas por owner se hace cargando los estados del conjunto del usuario y contando en memoria.

Justificacion:

- el limite actual de producto es pequeño
- el conteo es por cuenta, no global
- evita fragilidad innecesaria de traduccion LINQ para una regla de negocio hoy acotada

## Residual risks

- Si en el futuro el numero de empresas por cuenta crece mucho, ese conteo debera volver a query server-side.
- No existen pruebas de carga.

## Conclusion

La implementacion es adecuada para el volumen esperado hoy y deja soporte de indices para endurecer el acceso cuando el entorno lo requiera.
