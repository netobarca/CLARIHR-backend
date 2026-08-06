#!/usr/bin/env bash
# Corre SOLO los tests de integración de una sección del producto.
#
# Por qué existe: la suite completa son 743 tests (~70 min) y durante el ajuste manual del flujo,
# sección por sección, correr lo que todavía no se ha probado a mano no aporta nada — solo tapa la
# señal de lo que sí estás mirando. Esto corre la tajada y nada más.
#
#   ./test-seccion.sh unidades-organizativas
#   ./test-seccion.sh --lista                 # ver todas las secciones
#   ./test-seccion.sh OrgUnits_Create         # cualquier texto suelto se usa como filtro tal cual
#   ./test-seccion.sh empresas --no-build     # saltarse la compilación si ya compilaste
#
# Los tests están nombrados `Seccion_LoQueHace`, así que la sección es el prefijo del método.
set -euo pipefail

RAIZ="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../../.." && pwd)"
PROYECTO="$RAIZ/tests/CLARIHR.Api.IntegrationTests"

# sección -> prefijos de test (separados por espacio)
secciones() {
  case "$1" in
    auth)                   echo "Register_ Login_ Logout_ EmailVerification_ Onboarding_ PlatformLogin_ CoreLogin_ CoreAndPlatformTokens_ CoreLoginAndRefreshTokens_ AccessToken_ ProtectedEndpoint_ Resolver_ RevokingRoleAssignments_ AuthRegistrationSecurityTests" ;;
    empresas)               echo "AccountCompanies_ AccountCompanyAuthorization_ AccountCompanySubscription CompanySubscriptions_ CompanySubscriptionAddons_ CompanyPreferences_ Archive_ SetPrimary_" ;;
    usuarios)               echo "CompanyUsers_ UserPreferences_ GetActiveAdministratorUserIds ProvisioningBatchRepositoryMethods" ;;
    representantes-legales) echo "LegalRepresentatives_" ;;
    unidades-organizativas) echo "OrgUnits_ OrgStructureCatalogs_" ;;
    centros-de-costo)       echo "CostCenters_ CostCenterTypes_" ;;
    centros-de-trabajo)     echo "WorkCenters_ WorkCenterTypes_" ;;
    ubicaciones)            echo "LocationGroups_ LocationLevels_ LocationHierarchy_" ;;
    puestos)                echo "JobProfile PositionSlots_ PositionCategor PositionDescription OccupationalPyramidLevel_ PostJob_" ;;
    competencias)           echo "CompetencyFramework_ CompetencyConduct" ;;
    expedientes)            echo "PersonnelFile" ;;
    contratacion)           echo "EmploymentAssignment_ Rehire_ Retirement_ Settlement Reversal_" ;;
    nomina)                 echo "PayrollRuns_ Recurring OneTime Overtime NotWorkedTime CompensatoryTime Indebtedness SalaryTabulator_ PersonnelTransactions_ TimeAvailability_" ;;
    ausencias)              echo "Vacation_ Incapacit Lactation_ LeaveMasters_ LeaveConfiguration_" ;;
    disciplina)             echo "DisciplinaryAction Recognition_" ;;
    catalogos)              echo "GeneralCatalog ReferenceCatalogs_ EducationCatalogs_ DocumentTypeCatalogs_ BankCatalogs_ CompanyBankCatalogs_ InternalCatalog" ;;
    reportes)               echo "Dashboard_ ReportExportJobs_ ExportJobProfilePdf_ ProcessJob_ PersonnelActionsBandeja_ SettlementsBandeja_" ;;
    auditoria)              echo "AuditLog" ;;
    backoffice)             echo "Backoffice CommercialPlans_ CommercialAddons_ CommercialModules_" ;;
    guardrails)             echo "Swagger_ EveryMarkerPolicyName AllowedActionsCoverage PublicContractGuardrails OpenApiContractGuardrails MigrationSeeding InfrastructureInitialization" ;;
    *)                      return 1 ;;
  esac
}

TODAS="auth empresas usuarios representantes-legales unidades-organizativas centros-de-costo centros-de-trabajo ubicaciones puestos competencias expedientes contratacion nomina ausencias disciplina catalogos reportes auditoria backoffice guardrails"

if [[ $# -eq 0 || "${1:-}" == "--lista" || "${1:-}" == "-l" ]]; then
  echo "Secciones disponibles:"
  for s in $TODAS; do printf '  %s\n' "$s"; done
  echo
  echo "Cualquier otro texto se pasa como filtro tal cual (ej: OrgUnits_Create)."
  exit 0
fi

SECCION="$1"; shift
CONSTRUIR=1
for arg in "$@"; do [[ "$arg" == "--no-build" ]] && CONSTRUIR=0; done

if PREFIJOS="$(secciones "$SECCION")"; then
  ETIQUETA="sección '$SECCION'"
else
  PREFIJOS="$SECCION"
  ETIQUETA="filtro libre '$SECCION'"
fi

FILTRO=""
for p in $PREFIJOS; do
  [[ -n "$FILTRO" ]] && FILTRO="$FILTRO|"
  FILTRO="${FILTRO}FullyQualifiedName~$p"
done

# Un build vivo sobrescribe DLL en uso y produce fallos irreproducibles: nunca compilar con una corrida activa.
if pgrep -f "testhost" > /dev/null 2>&1; then
  echo "Hay una corrida de tests viva. Espérala o mátala antes de compilar." >&2
  exit 1
fi

if [[ $CONSTRUIR -eq 1 ]]; then
  echo "Compilando…"
  dotnet build "$PROYECTO" --nologo -v q
fi

echo "Corriendo $ETIQUETA"
INICIO=$SECONDS
dotnet test "$PROYECTO" --no-build --nologo --filter "$FILTRO"
ESTADO=$?
echo "Duración: $(( (SECONDS - INICIO) / 60 ))m $(( (SECONDS - INICIO) % 60 ))s"
exit $ESTADO
