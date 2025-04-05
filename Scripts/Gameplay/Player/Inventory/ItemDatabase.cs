using System.Collections.Generic;
using System.Linq;

namespace Ceiro.Scripts.Gameplay.Player.Inventory;

/// <summary>
/// Static database of all items in the game.
/// </summary>
public static class ItemDatabase
{
	private static readonly Dictionary<string, Item> _items = new();

	/// <summary>
	/// Initializes the item database.
	/// </summary>
	static ItemDatabase() =>
			// Register default items
			RegisterDefaultItems();

	/// <summary>
	/// Registers default items in the database.
	/// </summary>
	private static void RegisterDefaultItems()
	{
		// Resources
		RegisterItem(new()
		{
			ItemId       = "log",
			DisplayName  = "Log",
			Description  = "A wooden log harvested from a tree.",
			Type         = Item.ItemType.Resource,
			IsStackable  = true,
			MaxStackSize = 64
		});

		RegisterItem(new()
		{
			ItemId       = "wood_planks",
			DisplayName  = "Wood Planks",
			Description  = "Processed wooden planks used for crafting.",
			Type         = Item.ItemType.Resource,
			IsStackable  = true,
			MaxStackSize = 64
		});

		RegisterItem(new()
		{
			ItemId       = "stick",
			DisplayName  = "Stick",
			Description  = "A simple wooden stick used for crafting tools.",
			Type         = Item.ItemType.Resource,
			IsStackable  = true,
			MaxStackSize = 64
		});

		RegisterItem(new()
		{
			ItemId       = "stone",
			DisplayName  = "Stone",
			Description  = "A piece of stone that can be used for crafting.",
			Type         = Item.ItemType.Resource,
			IsStackable  = true,
			MaxStackSize = 64
		});

		// Tools
		RegisterItem(new()
		{
			ItemId        = "wooden_pickaxe",
			DisplayName   = "Wooden Pickaxe",
			Description   = "A basic pickaxe for mining stone.",
			Type          = Item.ItemType.Tool,
			IsStackable   = false,
			IsEquippable  = true,
			EquipSlot     = Item.EquipmentSlot.Hands,
			Durability    = 60,
			MaxDurability = 60,
			Properties = new()
			{
				{
					"mining_power", 1.0f
				}
			}
		});

		RegisterItem(new()
		{
			ItemId        = "wooden_axe",
			DisplayName   = "Wooden Axe",
			Description   = "A basic axe for chopping trees.",
			Type          = Item.ItemType.Tool,
			IsStackable   = false,
			IsEquippable  = true,
			EquipSlot     = Item.EquipmentSlot.Hands,
			Durability    = 60,
			MaxDurability = 60,
			Properties = new()
			{
				{
					"chopping_power", 1.0f
				}
			}
		});

		// Weapons
		RegisterItem(new()
		{
			ItemId        = "wooden_sword",
			DisplayName   = "Wooden Sword",
			Description   = "A basic sword for combat.",
			Type          = Item.ItemType.Weapon,
			IsStackable   = false,
			IsEquippable  = true,
			EquipSlot     = Item.EquipmentSlot.Hands,
			Durability    = 60,
			MaxDurability = 60,
			Properties = new()
			{
				{
					"damage", 3.0f
				}
			}
		});

		// Armor
		RegisterItem(new()
		{
			ItemId        = "leather_helmet",
			DisplayName   = "Leather Helmet",
			Description   = "A basic helmet that provides some protection.",
			Type          = Item.ItemType.Armor,
			IsStackable   = false,
			IsEquippable  = true,
			EquipSlot     = Item.EquipmentSlot.Head,
			Durability    = 80,
			MaxDurability = 80,
			Properties = new()
			{
				{
					"armor", 1.0f
				}
			}
		});

		// Food
		RegisterItem(new()
		{
			ItemId       = "apple",
			DisplayName  = "Apple",
			Description  = "A juicy apple that restores some hunger.",
			Type         = Item.ItemType.Food,
			IsStackable  = true,
			MaxStackSize = 16,
			Properties = new()
			{
				{
					"hunger_restore", 4.0f
				}
			}
		});

		// Placeable
		RegisterItem(new()
		{
			ItemId       = "wooden_wall",
			DisplayName  = "Wooden Wall",
			Description  = "A wooden wall that can be placed in the world.",
			Type         = Item.ItemType.Placeable,
			IsStackable  = true,
			MaxStackSize = 64,
			Properties = new()
			{
				{
					"health", 50.0f
				}
			}
		});
	}

	/// <summary>
	/// Registers an item in the database.
	/// </summary>
	/// <param name="item">The item to register.</param>
	public static void RegisterItem(Item item)
	{
		if (!string.IsNullOrEmpty(item.ItemId))
			_items[item.ItemId] = item;
	}

	/// <summary>
	/// Gets an item from the database.
	/// </summary>
	/// <param name="itemId">The ID of the item to get.</param>
	/// <returns>A new instance of the item, or null if not found.</returns>
	public static Item? GetItem(string itemId) => _items.TryGetValue(itemId, out var item) ? item.Clone() : null;

	/// <summary>
	/// Gets all items in the database.
	/// </summary>
	/// <returns>A list of all items.</returns>
	public static List<Item> GetAllItems() => _items.Values.Select(item => item.Clone()).ToList();
}