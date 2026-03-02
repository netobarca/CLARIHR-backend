# Remediation Plan Up To HU-008

## Objective

Convert the current baseline from "ready for local validation and QA" to a state that is materially closer to production readiness, without introducing premature complexity or redesigning the solution.

This plan is based on the gaps documented in:

- `./README.md`
- `./current-state-validation-checklist.md`
- `./architecture-analysis.md`
- `./security-analysis.md`
- `./performance-analysis.md`
- `./testing-analysis.md`

## Prioritization Criteria

The order below uses these rules:

1. Fix first what reduces security and regression risk the most.
2. Then reduce structural debt that makes future changes harder and less safe.
3. Finally address scale-oriented risks that matter when deployment topology or data volume grows.

## Priority Overview

| Priority | Workstream | Why now | Expected outcome |
| --- | --- | --- | --- |
| Completed | HTTP integration test suite | Highest confidence gap already addressed | Real end-to-end verification now exists for auth, RBAC, tenant isolation and contracts |
| Completed | Split oversized application and RBAC services | Maintainability hotspot already reduced | Lower regression risk and cleaner evolution path in Company Users and field permissions |
| Completed | Paginate RBAC permission audit endpoint | Operational risk already reduced | Controlled payload size and safer audit queries |
| Completed | Prepare distributed cache strategy for RBAC field permissions | Strategy and abstraction now exist | Single-node and distributed cache modes are defined without coupling authorization logic to one provider |
| Completed | Optimize search strategy for growth scenarios | Strategy and activation path now exist | Search hardening with `pg_trgm` can be activated without changing product semantics |

## Completed Work

### P0.1: HTTP integration tests

Status:

- completed

Delivered:

- `tests/CLARIHR.Api.IntegrationTests`
- real host-based tests with `WebApplicationFactory`
- coverage for:
  - `401 UNAUTHENTICATED`
  - `403 RBAC_DENIED`
  - `403 TENANT_MISMATCH`
  - `403 FIELD_EDIT_FORBIDDEN`
  - field visibility in company users
  - allowed update in company users
  - local register flow
  - audit log detail
  - IAM users create and role sync
  - IAM roles create, update, clone, permission sync and user sync
  - IAM permissions create
  - RBAC matrix update and field permission update

Outcome:

- The baseline no longer lacks HTTP end-to-end validation.
- During implementation, the suite also exposed and validated a real repository query issue in `UserCompanyRepository`.
- The most sensitive IAM/RBAC write paths now have end-to-end regression coverage.

### P1.1: Split oversized files and concentrated logic

Status:

- completed

Delivered:

- `src/CLARIHR.Application/Features/CompanyUsers/CompanyUserManagement.cs` reducido a contratos.
- `src/CLARIHR.Application/Features/CompanyUsers/CreateCompanyUser.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/UpdateCompanyUser.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/DeactivateCompanyUser.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/ReactivateCompanyUser.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/ResetInvitation.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/GetCompanyUsers.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/CompanyUserValidators.cs`
- `src/CLARIHR.Application/Features/CompanyUsers/CompanyUserManagementHelpers.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.Read.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.Write.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.Support.cs`

Outcome:

- The largest feature and RBAC service hotspots were decomposed without changing runtime behavior.
- Build and regression suites stayed green after the refactor.

### P1.2: Paginate RBAC permission audit endpoint

Status:

- completed

Delivered:

- paged query contract in `GetPermissionAuditQuery`
- paged repository access in `IIamAdministrationRepository` and `IamAdministrationRepository`
- paged API response in `RbacController`
- regression coverage for RBAC audit pagination in `IdentityAccessAdministrationTests`
- documentation and Postman updates

Outcome:

- The RBAC audit endpoint no longer returns unbounded lists.
- The contract now matches the API paging pattern used elsewhere in the system.

### P2.1: Define distributed cache strategy for permission data

Status:

- completed

Delivered:

- `src/CLARIHR.Application/Abstractions/IdentityAccess/IFieldPermissionOverrideCache.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionOverrideCache.cs`
- `src/CLARIHR.Infrastructure/Configuration/FieldPermissionCacheOptions.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldAccessProfileService.cs`
- `src/CLARIHR.Infrastructure/IdentityAccess/FieldPermissionService.cs`
- `docs/technical/api-output/permission-cache-strategy.md`
- config baseline in `src/CLARIHR.Api/appsettings.json` and `src/CLARIHR.Api/appsettings.Development.json`

Outcome:

- The code no longer depends directly on `IMemoryCache` for field permission overrides.
- The solution now has explicit `MemoryOnly` and `Distributed` modes.
- Multi-instance activation is an operational step, not a redesign.

### P2.2: Define search strategy for growth scenarios

Status:

- completed

Delivered:

- `docs/technical/api-output/search-growth-strategy.md`
- `docs/technical/sql/p2_search_growth_hardening.sql`

Outcome:

- The search behavior keeps current substring semantics.
- The scale path is explicitly defined around `pg_trgm` and GIN indexes for the real search fields used by the API.
- Activation is threshold-based, avoiding premature tuning.

## Phase 1: Reduce Structural And Operational Risk
## Phase 2: Prepare For Scale

## Recommended Execution Order

1. Continue expanding integration coverage opportunistically for non-critical endpoints still outside the suite.
2. Activate a concrete distributed cache provider only when deployment topology requires horizontal scale.
3. Apply the search hardening SQL only when the documented thresholds are reached.

## Suggested Delivery Slices

### Slice A

- Wider API integration coverage outside the critical IAM/RBAC surface

Expected benefit:

- Lower regression risk outside the currently hardened modules.

### Slice B

- Production-like hardening follow-up

Expected benefit:

- Better prioritization of the next operational improvements.

## Exit Condition For Production Readiness Review

The baseline should be reviewed again for production readiness only when at least these items are complete:

- Integration tests exist for critical authorization flows.
- Structural hotspots have been reduced.
- Unbounded audit read has been removed.
- Cache strategy is documented for the intended deployment topology.

## Final Recommendation

The highest-value sequence from this point is:

1. incremental expansion of integration coverage outside the critical IAM/RBAC surface
2. activation of a concrete distributed cache provider only if the deployment becomes multi-instance
3. application of the search hardening SQL only if the documented thresholds are reached

That sequence gives the best balance between current needs, scale preparation and avoiding premature infrastructure complexity.
