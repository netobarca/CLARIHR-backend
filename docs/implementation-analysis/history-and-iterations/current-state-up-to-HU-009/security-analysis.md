# Security Analysis

## Summary

La HU-009 agrego una superficie nueva sensible, pero quedo protegida con reglas claras.

## Controls implemented

1. Los endpoints multiempresa requieren JWT.
2. No usan RBAC tenant-scoped por diseño; usan ownership por cuenta.
3. Solo se puede operar una empresa si `CreatedByUserPublicId` coincide con el usuario autenticado.
4. No existe hard delete de empresa.
5. No se puede archivar la empresa activa del token actual.
6. No se puede hacer switch a una empresa archivada.
7. El switch requiere membership activa y reemite JWT con nuevo `tid`.
8. Las acciones quedan auditadas:
   - `COMPANY_CREATED`
   - `COMPANY_UPDATED`
   - `COMPANY_ARCHIVED`
   - `COMPANY_REACTIVATED`
   - `ACTIVE_COMPANY_SWITCHED`

## Security outcome

- No se debilito el enforcement RBAC existente.
- No se introdujo un bypass cross-tenant por header o query param.
- El ownership account-level quedo separado y explicito.

## Residual risks

- El limite de empresas activas aun no responde a tiers reales.
- La seguridad operativa de despliegue sigue pendiente de infraestructura real.

## Conclusion

Para el alcance actual, la HU es segura y consistente con las definiciones previas.
