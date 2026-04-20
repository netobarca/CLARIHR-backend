# Tenant Route Technical Debt

## Decision

For **new tenant-scoped endpoints**, the API contract must not include `companyId` in route path params.

New route convention:
- Collection: `/api/v1/<resource>`
- Item: `/api/v1/<resource>/{id}`

Tenant resolution must come from authenticated JWT claim `tid`.

## Technical Debt Registered

Debt ID: `TD-API-TENANT-ROUTE-001`  
Status: `Open`  
Priority: `High`  
Owner: `Backend Platform / API Governance`

Description:
- Existing endpoints under `/api/v1/companies/{companyId}/...` remain active for compatibility.
- This route style is now deprecated and must not be used for new endpoint design.

## Scope Affected (Legacy Pattern)

Main modules currently using `/companies/{companyId}` include:
- `OrgUnits`
- `PositionSlots`
- `JobProfiles` / `JobCatalogs`
- `SalaryTabulator`
- `CostCenters`
- `Locations` (`location-*`, `work-centers`, `work-center-types`)
- `PersonnelFiles` and reporting
- `LegalRepresentatives`
- `ReportExportJobs`
- `ReportsCapabilities`
- `PositionDescriptionCatalogs`
- `CompetencyFramework`
- `PersonnelEducationCatalogs`
- `GeneralCatalogs`

## Migration Plan (Postponed Work)

1. Introduce parallel routes without `companyId` for each legacy module.
2. Keep legacy routes temporarily with deprecation annotation in docs/OpenAPI.
3. Update frontend/mobile clients to consume new routes.
4. Add telemetry to detect remaining traffic on legacy routes.
5. Remove legacy `/companies/{companyId}` routes in next breaking API version.

## Acceptance Criteria for Future Work

- No new feature merges with routes requiring `companyId` in tenant-scoped APIs.
- Every new endpoint validates tenant context from `tid` only.
- Docs and OpenAPI examples use the new route convention by default.
