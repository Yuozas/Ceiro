using Godot;
using Godot.Collections;
using CollectionExtensions = System.Collections.Generic.CollectionExtensions;

namespace Ceiro.Scripts.Gameplay.Player.Inventory;

/// <summary>
/// Represents an item in the game.
/// </summary>
public partial class Item : Resource
{
	public enum ItemType
	{
		Resource,
		Tool,
		Weapon,
		Armor,
		Food,
		Placeable
	}

	public enum EquipmentSlot
	{
		None,
		Head,
		Chest,
		Legs,
		Feet,
		Hands
	}

	[Export] public string                    ItemId        { get; set; }
	[Export] public string                    DisplayName   { get; set; }
	[Export] public string                    Description   { get; set; }
	[Export] public Texture2D                 Icon          { get; set; }
	[Export] public ItemType                  Type          { get; set; } = ItemType.Resource;
	[Export] public bool                      IsStackable   { get; set; } = true;
	[Export] public int                       MaxStackSize  { get; set; } = 64;
	[Export] public int                       StackCount    { get; set; } = 1;
	[Export] public bool                      IsEquippable  { get; set; }
	[Export] public EquipmentSlot             EquipSlot     { get; set; } = EquipmentSlot.None;
	[Export] public float                     Durability    { get; set; } = 100.0f;
	[Export] public float                     MaxDurability { get; set; } = 100.0f;
	[Export] public Dictionary<string, float> Properties    { get; set; } = new();

	/// <summary>
	/// Creates a copy of this item.
	/// </summary>
	/// <returns>A new item with the same properties.</returns>
	public Item Clone()
	{
		var clone = new Item
		{
			ItemId        = ItemId,
			DisplayName   = DisplayName,
			Description   = Description,
			Icon          = Icon,
			Type          = Type,
			IsStackable   = IsStackable,
			MaxStackSize  = MaxStackSize,
			StackCount    = StackCount,
			IsEquippable  = IsEquippable,
			EquipSlot     = EquipSlot,
			Durability    = Durability,
			MaxDurability = MaxDurability
		};

		// Copy properties
		foreach (var property in Properties)
			clone.Properties[property.Key] = property.Value;

		return clone;
	}

	/// <summary>
	/// Uses the item, reducing its durability.
	/// </summary>
	/// <param name="amount">The amount of durability to reduce.</param>
	/// <returns>True if the item is still usable, false if it broke.</returns>
	public bool Use(float amount = 1.0f)
	{
		if (!IsEquippable)
			return true;

		Durability -= amount;

		// Check if the item broke
		if (Durability <= 0)
		{
			Durability = 0;
			return false;
		}

		return true;
	}

	/// <summary>
	/// Repairs the item, increasing its durability.
	/// </summary>
	/// <param name="amount">The amount of durability to restore.</param>
	public void Repair(float amount)
	{
		if (!IsEquippable)
			return;

		Durability = Mathf.Min(Durability + amount, MaxDurability);
	}

	/// <summary>
	/// Gets the durability percentage of the item.
	/// </summary>
	/// <returns>The durability percentage (0-1).</returns>
	public float GetDurabilityPercentage()
	{
		if (MaxDurability <= 0)
			return 1.0f;

		return Mathf.Clamp(Durability / MaxDurability, 0.0f, 1.0f);
	}

	/// <summary>
	/// Gets a property value.
	/// </summary>
	/// <param name="propertyName">The name of the property.</param>
	/// <param name="defaultValue">The default value to return if the property doesn't exist.</param>
	/// <returns>The property value, or the default value if not found.</returns>
	public float GetProperty(string propertyName, float defaultValue = 0.0f) => CollectionExtensions.GetValueOrDefault(Properties, propertyName, defaultValue);

	/// <summary>
	/// Sets a property value.
	/// </summary>
	/// <param name="propertyName">The name of the property.</param>
	/// <param name="value">The value to set.</param>
	public void SetProperty(string propertyName, float value) => Properties[propertyName] = value;
}