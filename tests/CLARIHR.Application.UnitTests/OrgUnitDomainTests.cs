using CLARIHR.Application.Features.OrgUnits;
using CLARIHR.Application.Features.OrgUnits.Common;
using CLARIHR.Domain.OrgUnits;

namespace CLARIHR.Application.UnitTests;

public sealed class OrgUnitDomainTests
{
    [Fact]
    public void OrgUnit_Create_ShouldNormalizeCodeAndName()
    {
        var orgUnit = OrgUnit.Create(
            code: "  dir-001  ",
            name: "  Direccion General  ",
            orgUnitTypeCatalogItemId: 10,
            functionalAreaCatalogItemId: 20,
            parentId: null,
            sortOrder: 1,
            description: "  Description  ",
            costCenterCode: "  cc-01  ",
            managerEmployeeId: null);

        Assert.Equal("DIR-001", orgUnit.Code);
        Assert.Equal("DIR-001", orgUnit.NormalizedCode);
        Assert.Equal("Direccion General", orgUnit.Name);
        Assert.Equal("DIRECCION GENERAL", orgUnit.NormalizedName);
        Assert.Equal("Description", orgUnit.Description);
        Assert.Equal("cc-01", orgUnit.CostCenterCode);
        Assert.Equal(10, orgUnit.OrgUnitTypeCatalogItemId);
        Assert.Equal(20, orgUnit.FunctionalAreaCatalogItemId);
    }

    [Fact]
    public void OrgUnit_Move_WhenSortOrderIsNegative_ShouldThrow()
    {
        var orgUnit = OrgUnit.Create(
            code: "DIR-001",
            name: "Direccion General",
            orgUnitTypeCatalogItemId: 10,
            functionalAreaCatalogItemId: null,
            parentId: null,
            sortOrder: 1,
            description: null,
            costCenterCode: null,
            managerEmployeeId: null);

        Assert.Throws<ArgumentOutOfRangeException>(() => orgUnit.Move(parentId: null, sortOrder: -1));
    }

    [Fact]
    public void OrgUnit_Inactivate_ShouldRefreshConcurrencyToken()
    {
        var orgUnit = OrgUnit.Create(
            code: "DIR-001",
            name: "Direccion General",
            orgUnitTypeCatalogItemId: 10,
            functionalAreaCatalogItemId: null,
            parentId: null,
            sortOrder: 1,
            description: null,
            costCenterCode: null,
            managerEmployeeId: null);

        var tokenBefore = orgUnit.ConcurrencyToken;

        orgUnit.Inactivate();

        Assert.False(orgUnit.IsActive);
        Assert.NotEqual(tokenBefore, orgUnit.ConcurrencyToken);
    }

    [Fact]
    public void OrgUnitHierarchyBuilder_WouldCreateCycle_WhenParentIsDescendant_ShouldReturnTrue()
    {
        var root = CreateHierarchyNode(internalId: 1, code: "DIR-001", name: "Direccion General", parentInternalId: null, parentId: null, sortOrder: 1);
        var child = CreateHierarchyNode(internalId: 2, code: "GER-001", name: "Gerencia Finanzas", parentInternalId: 1, parentId: root.Id, sortOrder: 1);

        var byInternalId = new[] { root, child }.ToDictionary(static node => node.InternalId);

        var createsCycle = OrgUnitHierarchyBuilder.WouldCreateCycle(root.InternalId, child.InternalId, byInternalId);

        Assert.True(createsCycle);
    }

    [Fact]
    public void OrgUnitHierarchyBuilder_CalculateDepth_WhenPathExceedsLimit_ShouldReturnGreaterThanMaxDepth()
    {
        var now = DateTime.UtcNow;
        var nodes = Enumerable.Range(1, OrgUnitValidationRules.MaxDepth + 1)
            .Select(index => new OrgUnitHierarchyNodeData(
                index,
                Guid.NewGuid(),
                $"U-{index}",
                $"Unit {index}",
                10,
                Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
                "UT",
                "Unit Type",
                null,
                null,
                null,
                null,
                index == 1 ? null : index - 1,
                null,
                index,
                null,
                null,
                null,
                true,
                Guid.NewGuid(),
                now,
                null))
            .ToDictionary(static node => node.InternalId);

        var depth = OrgUnitHierarchyBuilder.CalculateDepth(parentInternalId: OrgUnitValidationRules.MaxDepth + 1, nodes);

        Assert.True(depth > OrgUnitValidationRules.MaxDepth);
    }

    // Contraparte de CalculateDepth: aquel sube, este baja. El guard de /move necesita los dos.
    [Theory]
    [InlineData(1, 1)]   // hoja suelta
    [InlineData(2, 2)]   // padre con un hijo
    [InlineData(4, 4)]   // cadena de cuatro
    public void OrgUnitHierarchyBuilder_CalculateSubtreeHeight_ShouldCountRootAndDescendants(int chainLength, int expectedHeight)
    {
        var nodes = Enumerable.Range(1, chainLength)
            .Select(index => CreateHierarchyNode(index, $"U-{index}", $"Unit {index}", index == 1 ? null : index - 1, null, index))
            .ToDictionary(static node => node.InternalId);

        var height = OrgUnitHierarchyBuilder.CalculateSubtreeHeight(1, nodes);

        Assert.Equal(expectedHeight, height);
    }

    [Fact]
    public void OrgUnitHierarchyBuilder_CalculateSubtreeHeight_WhenNodeIsDeeperInTheChain_ShouldMeasureOnlyItsOwnSubtree()
    {
        // Una cadena de 5: medida desde el nodo 4 la altura es 2, no 5. Si midiera el arbol entero,
        // el guard de /move rechazaria movimientos perfectamente validos.
        var nodes = Enumerable.Range(1, 5)
            .Select(index => CreateHierarchyNode(index, $"U-{index}", $"Unit {index}", index == 1 ? null : index - 1, null, index))
            .ToDictionary(static node => node.InternalId);

        Assert.Equal(2, OrgUnitHierarchyBuilder.CalculateSubtreeHeight(4, nodes));
        Assert.Equal(1, OrgUnitHierarchyBuilder.CalculateSubtreeHeight(5, nodes));
    }

    [Fact]
    public void OrgUnitHierarchyBuilder_CalculateSubtreeHeight_WhenDataIsCyclic_ShouldStopInsteadOfSpinning()
    {
        // Hay arboles ya corrompidos por el defecto que este guard corrige; el recorrido no debe colgarse.
        var nodes = new[]
        {
            CreateHierarchyNode(1, "U-1", "Unit 1", 2, null, 1),
            CreateHierarchyNode(2, "U-2", "Unit 2", 1, null, 2)
        }.ToDictionary(static node => node.InternalId);

        var height = OrgUnitHierarchyBuilder.CalculateSubtreeHeight(1, nodes);

        Assert.True(height <= OrgUnitValidationRules.MaxDepth + 1);
    }

    private static OrgUnitHierarchyNodeData CreateHierarchyNode(
        long internalId,
        string code,
        string name,
        long? parentInternalId,
        Guid? parentId,
        int? sortOrder)
    {
        return new OrgUnitHierarchyNodeData(
            internalId,
            Guid.NewGuid(),
            code,
            name,
            10,
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "UT",
            "Unit Type",
            null,
            null,
            null,
            null,
            parentInternalId,
            parentId,
            sortOrder,
            null,
            null,
            null,
            true,
            Guid.NewGuid(),
            DateTime.UtcNow,
            null);
    }
}
