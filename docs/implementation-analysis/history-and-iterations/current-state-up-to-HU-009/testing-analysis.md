# Testing Analysis

## Validation executed

- `dotnet build CLARIHR.slnx`
- `dotnet test CLARIHR.slnx --no-build`

## Current numbers

- Unit tests passing: `95`
- Integration tests passing: `38`
- Total passing: `133`

## HU-009 coverage added

### Unit tests

- crear empresa adicional dentro del limite
- rechazar creacion por limite
- mantener `IsPrimary = false` al crear empresa adicional
- bloquear archive de empresa activa
- bloquear archive de empresa no propia
- reactivar empresa archivada
- bloquear reactivacion si excede limite
- cambiar primaria y reemitir token al hacer switch
- editar solo `name`

### HTTP integration tests

- listar empresas propias
- crear empresa adicional sin cambiar contexto activo
- rechazar creacion cuando se excede limite
- editar empresa propia
- bloquear archive de empresa activa
- reactivar empresa archivada
- bloquear acceso a empresa no propia
- devolver JWT con nuevo `tid` al hacer switch

## Regression confidence

- onboarding inicial sigue funcionando
- refresh token sigue siendo coherente con la empresa primaria vigente
- la superficie previa de IAM/RBAC sigue verde

## Conclusion

La cobertura para esta HU es buena para local y QA. El gap restante sigue siendo de madurez productiva, no de regresion funcional inmediata.
