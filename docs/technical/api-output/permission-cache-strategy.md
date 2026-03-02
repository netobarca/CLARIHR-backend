# Permission Cache Strategy

## Objective

Definir una estrategia de cache para permisos por campo que funcione bien en local/single-node y que permita escalar a multi-instancia sin reescribir la logica de autorizacion.

## Current Design

La solucion usa una abstraccion dedicada:

- `IFieldPermissionOverrideCache`
- `FieldPermissionOverrideCache`
- `FieldPermissionCacheOptions`

Esta abstraccion desacopla `FieldAccessProfileService` y `FieldPermissionService` de `IMemoryCache` directo.

## Supported Modes

### 1. `MemoryOnly`

Uso esperado:

- desarrollo local
- QA en una sola instancia
- despliegues simples single-node

Comportamiento:

- usa `IMemoryCache`
- TTL configurable por `Caching:FieldPermissions:EntryTtlMinutes`
- invalidacion inmediata en el nodo actual cuando cambian permisos por campo

Ventaja:

- cero dependencias externas
- menor complejidad operativa

Limite:

- no comparte estado entre nodos

### 2. `Distributed`

Uso esperado:

- despliegues multi-instancia
- escenarios donde los cambios de permisos deben propagarse entre nodos sin esperar reinicios o expiraciones locales

Comportamiento:

- usa `IDistributedCache` como store de overrides por `(tenant, role, resource)`
- invalida la entrada distribuida al actualizar permisos
- no depende de `IMemoryCache` para servir lecturas en este modo

Ventaja:

- coherencia de cache entre nodos al compartir el mismo backend distribuido

Requisito:

- el host debe registrar una implementacion concreta de `IDistributedCache`

## Configuration

Configuracion base:

```json
{
  "Caching": {
    "FieldPermissions": {
      "Mode": "MemoryOnly",
      "EntryTtlMinutes": 10
    }
  }
}
```

Para multi-instancia:

```json
{
  "Caching": {
    "FieldPermissions": {
      "Mode": "Distributed",
      "EntryTtlMinutes": 10
    }
  }
}
```

## Activation Rule

`Distributed` no debe activarse si la aplicacion no tiene un `IDistributedCache` real registrado.

En ese caso, la solucion falla rapido con un error de configuracion explicito:

- `"Field permission cache mode 'Distributed' requires an IDistributedCache registration."`

Esto evita caer silenciosamente a un modo inconsistente.

## Why The Repo Does Not Force Redis Today

El repositorio deja la integracion concreta del cache distribuido como una decision de entorno para no:

- introducir dependencias de infraestructura antes de necesitarlas
- acoplar el backend a un proveedor especifico sin un requerimiento operativo real
- romper el flujo local/QA actual que funciona correctamente con `MemoryOnly`

## Operational Recommendation

- Mantener `MemoryOnly` en local y entornos single-node.
- Cambiar a `Distributed` solo cuando el despliegue objetivo sea multi-instancia.
- Registrar en ese entorno una implementacion concreta de `IDistributedCache` antes de activar el modo.

## Code References

- `src/CLARIHR.Application/Abstractions/IdentityAccess/IFieldPermissionOverrideCache.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionOverrideCache.cs`
- `src/CLARIHR.Infrastructure/Configuration/FieldPermissionCacheOptions.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldAccessProfileService.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.cs`
