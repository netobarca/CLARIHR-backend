# Current State Validation Checklist

## Delivery scope

- [x] cuenta puede crear empresas adicionales
- [x] cuenta puede listar sus empresas
- [x] cuenta puede editar nombre de empresa
- [x] cuenta puede archivar y reactivar empresa
- [x] cuenta puede cambiar empresa activa con nuevo JWT
- [x] onboarding inicial sigue intacto

## Architecture

- [x] provisioning extraido a servicio reusable
- [x] modulo `AccountCompanies` separado
- [x] sin mezcla indebida entre ownership account-level y RBAC tenant-level

## Security

- [x] endpoints protegidos por autenticacion
- [x] ownership validado en backend
- [x] no hard delete
- [x] bloqueado archive de empresa activa
- [x] bloqueado switch a empresa no activa o no propia
- [x] auditoria de lifecycle de empresa

## Performance

- [x] listado paginado
- [x] indices definidos por script
- [x] estrategia actual aceptable para volumen esperado

## Testing

- [x] unit tests de reglas criticas
- [x] integration tests HTTP del flujo principal
- [x] build limpio
- [x] suite completa verde

## Readiness

- [x] ready for local validation
- [x] ready for QA
- [ ] ready for production

Motivo de no marcar produccion:

- aun no existe entorno real de despliegue ni decisiones operativas cerradas
