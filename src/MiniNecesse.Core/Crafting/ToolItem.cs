using System;
using MiniNecesse.Core.Entities;
using MiniNecesse.Core.Enums;

namespace MiniNecesse.Core.Crafting;

/// <summary>
/// Represents a harvesting tool item such as an axe or pickaxe.
/// </summary>
public sealed class ToolItem : Item
{
    private readonly ToolType _toolType;
    private readonly int _harvestPower;
    private readonly float _harvestRange;
    private readonly float _swingCooldown;

    /// <summary>Gets the tool archetype (e.g., Axe or Pickaxe).</summary>
    public ToolType ToolType => _toolType;

    /// <summary>Gets the harvesting damage applied per swing against matching resources.</summary>
    public int HarvestPower => _harvestPower;

    /// <summary>Gets the harvesting reach in world units.</summary>
    public float HarvestRange => _harvestRange;

    /// <summary>Gets the delay between consecutive tool swings in seconds.</summary>
    public float SwingCooldown => _swingCooldown;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolItem"/> class.
    /// </summary>
    public ToolItem(
        string id,
        string name,
        ToolType toolType,
        int harvestPower,
        float harvestRange = 48f,
        float swingCooldown = 0.35f,
        string description = "")
        : base(id, name, ItemCategory.Tool, maxStackSize: 1, description)
    {
        if (toolType == ToolType.None)
        {
            throw new ArgumentException("ToolItem requires a specific ToolType.", nameof(toolType));
        }

        if (harvestPower <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(harvestPower), "Harvest power must be positive.");
        }

        if (harvestRange <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(harvestRange), "Harvest range must be positive.");
        }

        if (swingCooldown <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(swingCooldown), "Swing cooldown must be positive.");
        }

        _toolType = toolType;
        _harvestPower = harvestPower;
        _harvestRange = harvestRange;
        _swingCooldown = swingCooldown;
    }

    /// <inheritdoc />
    public override void ApplyHotbarSelection(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        player.EquipWeapon(null);
        player.EquipTool(_toolType, _harvestPower);
    }
}
