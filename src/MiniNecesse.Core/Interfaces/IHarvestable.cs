using System;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Interfaces;

/// <summary>
/// Contract for natural world resources (trees, boulders, ore veins) that can be harvested using tools.
/// </summary>
public interface IHarvestable
{
    /// <summary>Gets the preferred tool type required for full harvesting efficiency.</summary>
    ToolType RequiredTool { get; }

    /// <summary>Gets the item identifier dropped when this resource is depleted.</summary>
    string DropItemId { get; }

    /// <summary>Gets the quantity of items dropped upon depletion.</summary>
    int DropQuantity { get; }

    /// <summary>Raised when the resource is depleted and yields harvest drops.</summary>
    event Action<IHarvestable, string, int>? OnHarvested;

    /// <summary>Checks whether <paramref name="toolType"/> matches this resource's effective tool.</summary>
    bool CanHarvestWith(ToolType toolType);

    /// <summary>Applies harvesting effort from a tool and returns the effective damage dealt.</summary>
    /// <param name="toolType">The tool archetype used.</param>
    /// <param name="toolPower">Base harvesting power of the tool.</param>
    /// <returns>The actual damage dealt to the resource node.</returns>
    int Harvest(ToolType toolType, int toolPower);
}
