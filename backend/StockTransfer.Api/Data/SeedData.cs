using StockTransfer.Api.Domain;

namespace StockTransfer.Api.Data;

/// <summary>
/// Fixed reference data, applied through EF migrations (HasData) so it exists as soon as the app has migrated on startup.
/// </summary>
public static class SeedData
{
    public const int WarehouseId = 1;
    public const int ColomboId = 2;
    public const int KandyId = 3;
    public const int GalleId = 4;

    public const int RiceId = 1;
    public const int DhalId = 2;
    public const int CoconutOilId = 3;
    public const int TeaId = 4;
    public const int SugarId = 5;

    public static readonly Branch[] Branches =
    [
        new() { Id = WarehouseId, Name = "Warehouse", Type = BranchType.Warehouse },
        new() { Id = ColomboId, Name = "Colombo", Type = BranchType.Branch },
        new() { Id = KandyId, Name = "Kandy", Type = BranchType.Branch },
        new() { Id = GalleId, Name = "Galle", Type = BranchType.Branch },
    ];

    public static readonly Product[] Products =
    [
        new() { Id = RiceId, Sku = "RICE-5KG", Name = "Rice 5kg" },
        new() { Id = DhalId, Sku = "DHAL-1KG", Name = "Red Dhal 1kg" },
        new() { Id = CoconutOilId, Sku = "COIL-1L", Name = "Coconut Oil 1L" },
        new() { Id = TeaId, Sku = "TEA-400G", Name = "Ceylon Tea 400g" },
        new() { Id = SugarId, Sku = "SUGAR-1KG", Name = "Sugar 1kg" },
    ];

    // Starting stock per branch, in product Id order. Deliberately uneven, with some low and zero
    // figures, so shortages (and the "not enough stock" rule) are easy to demonstrate.
    private static readonly Dictionary<int, int[]> StartingQuantities = new()
    {
        [WarehouseId] = [500, 300, 200, 250, 400],
        [ColomboId] = [40, 25, 10, 60, 0],
        [KandyId] = [5, 50, 30, 0, 20],
        [GalleId] = [15, 0, 3, 35, 25],
    };

    /// <summary>One row for every branch/product pair, including zeros.</summary>
    public static IEnumerable<Stock> Stock() =>
        from branch in Branches
        from product in Products
        select new Stock
        {
            BranchId = branch.Id,
            ProductId = product.Id,
            Quantity = StartingQuantities[branch.Id][product.Id - 1],
        };
}
