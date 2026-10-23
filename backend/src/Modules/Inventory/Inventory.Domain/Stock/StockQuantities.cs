using FreshOrbit.Inventory.Domain.Shared;
using FreshOrbit.SharedKernel.Errors;
using FreshOrbit.SharedKernel.Units;

namespace FreshOrbit.Inventory.Domain.Stock;

/// <summary>Gross and net quantity rules (INV-R11, INV-R12).</summary>
public static class StockQuantities
{
    /// <summary>
    /// Raw stock needed for a net (prepared) quantity: GRAM → ceil(net × 10 000 / yield_bp); PIECE → net (yield ignored).
    /// 500 g at 6 500 bp → 770 g.
    /// </summary>
    public static int Gross(StockUnit unit, int netQuantity, int yieldBp)
    {
        if (netQuantity < 0)
        {
            throw new DomainException(InventoryErrors.ValidationFailed, "Quantity must not be negative.", "quantity");
        }

        if (unit == StockUnit.Piece)
        {
            return netQuantity;
        }

        EnsureYield(yieldBp);
        return Weight.GrossForNet(netQuantity, yieldBp);
    }

    /// <summary>Largest net quantity whose gross fits in <paramref name="available"/>: floor(available × yield_bp / 10 000) for GRAM.</summary>
    public static int MaxNetOrderable(StockUnit unit, int available, int yieldBp)
    {
        if (available <= 0)
        {
            return 0;
        }

        if (unit == StockUnit.Piece)
        {
            return available;
        }

        EnsureYield(yieldBp);
        return (int)((long)available * yieldBp / BasisPoints.Whole);
    }

    private static void EnsureYield(int yieldBp)
    {
        if (yieldBp is < 1 or > BasisPoints.Whole)
        {
            throw new DomainException(InventoryErrors.ValidationFailed, "Yield must be between 1 and 10000 basis points.", "yieldBp");
        }
    }
}
