using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ceiro.Scripts.Gameplay.Player.Inventory;

/// <summary>
/// Manages the player's inventory, including item storage, equipping, and crafting.
/// </summary>
public partial class InventorySystem : Node
{
	[Signal]
	public delegate void InventoryChangedEventHandler();

	[Signal]
	public delegate void ItemEquippedEventHandler(Item item, int slotIndex);

	[Signal]
	public delegate void ItemUnequippedEventHandler(Item item, int slotIndex);

	[Export] public int InventorySize  = 20;
	[Export] public int HotbarSize     = 5;
	[Export] public int EquipmentSlots = 5;

	private List<Item?>          _inventoryItems = [];
	private List<Item?>          _equipmentItems = [];
	private List<CraftingRecipe> _knownRecipes   = [];
	private int                  _selectedHotbarIndex;

	public override void _Ready()
	{
		// Initialize inventory
		_inventoryItems = new(new Item[InventorySize]);
		_equipmentItems = new(new Item[EquipmentSlots]);
		_knownRecipes   = [];

		// Load default recipes
		LoadDefaultRecipes();
	}

	/// <summary>
	/// Adds an item to the inventory.
	/// </summary>
	/// <param name="item">The item to add.</param>
	/// <returns>True if the item was added, false if the inventory is full.</returns>
	public bool AddItem(Item item)
	{
		// Check if we can stack this item with an existing one
		if (item.IsStackable)
			foreach (var existingItem in _inventoryItems)
			{
				if (existingItem == null || existingItem.ItemId != item.ItemId || existingItem.StackCount >= existingItem.MaxStackSize)
					continue;

				// Calculate how many items we can add to this stack
				var spaceInStack = existingItem.MaxStackSize - existingItem.StackCount;
				var amountToAdd  = Mathf.Min(item.StackCount, spaceInStack);

				// Add to the existing stack
				existingItem.StackCount += amountToAdd;

				// If we added all items, we're done
				if (amountToAdd >= item.StackCount)
				{
					EmitSignal(SignalName.InventoryChanged);
					return true;
				}

				// Otherwise, reduce the count and continue looking for more stacks
				item.StackCount -= amountToAdd;
			}

		// Find an empty slot for the item
		for (var i = 0; i < _inventoryItems.Count; i++)
			if (_inventoryItems[i] is null)
			{
				_inventoryItems[i] = item;
				EmitSignal(SignalName.InventoryChanged);
				return true;
			}

		// Inventory is full
		return false;
	}

	/// <summary>
	/// Removes an item from the inventory.
	/// </summary>
	/// <param name="index">The index of the item to remove.</param>
	public void RemoveItem(int index)
	{
		if (index < 0 || index >= _inventoryItems.Count)
			return;

		_inventoryItems[index] = null;

		EmitSignal(SignalName.InventoryChanged);
	}

	/// <summary>
	/// Gets an item from the inventory without removing it.
	/// </summary>
	/// <param name="index">The index of the item to get.</param>
	/// <returns>The item, or null if the index is invalid or empty.</returns>
	public Item? GetItem(int index)
	{
		if (index < 0 || index >= _inventoryItems.Count)
			return null;

		return _inventoryItems[index];
	}

	/// <summary>
	/// Moves an item from one slot to another.
	/// </summary>
	/// <param name="fromIndex">The source slot index.</param>
	/// <param name="toIndex">The destination slot index.</param>
	/// <returns>True if the move was successful, false otherwise.</returns>
	public bool MoveItem(int fromIndex, int toIndex)
	{
		if (fromIndex < 0 || fromIndex >= _inventoryItems.Count || toIndex < 0 || toIndex >= _inventoryItems.Count)
			return false;

		// Get the items
		var fromItem = _inventoryItems[fromIndex];
		var toItem   = _inventoryItems[toIndex];

		// If source slot is empty, nothing to move
		if (fromItem == null)
			return false;

		// If destination slot is empty, simple move
		if (toItem == null)
		{
			_inventoryItems[toIndex]   = fromItem;
			_inventoryItems[fromIndex] = null;
			EmitSignal(SignalName.InventoryChanged);
			return true;
		}

		// If both slots have items, check if they can be stacked
		if (fromItem.ItemId == toItem.ItemId && toItem.IsStackable && toItem.StackCount < toItem.MaxStackSize)
		{
			// Calculate how many items we can add to the destination stack
			var spaceInStack = toItem.MaxStackSize - toItem.StackCount;
			var amountToMove = Mathf.Min(fromItem.StackCount, spaceInStack);

			// Add to the destination stack
			toItem.StackCount += amountToMove;

			// Reduce or remove the source stack
			fromItem.StackCount -= amountToMove;
			if (fromItem.StackCount <= 0)
				_inventoryItems[fromIndex] = null;

			EmitSignal(SignalName.InventoryChanged);
			return true;
		}

		// If items can't be stacked, swap them
		_inventoryItems[toIndex]   = fromItem;
		_inventoryItems[fromIndex] = toItem;

		EmitSignal(SignalName.InventoryChanged);
		return true;
	}

	/// <summary>
	/// Equips an item from the inventory to an equipment slot.
	/// </summary>
	/// <param name="inventoryIndex">The inventory index of the item to equip.</param>
	/// <param name="equipmentSlot">The equipment slot to equip to.</param>
	/// <returns>True if the item was equipped, false otherwise.</returns>
	public bool EquipItem(int inventoryIndex, int equipmentSlot)
	{
		if (inventoryIndex < 0 || inventoryIndex >= _inventoryItems.Count || equipmentSlot < 0 || equipmentSlot >= _equipmentItems.Count)
			return false;

		// Get the item to equip
		var item = _inventoryItems[inventoryIndex];

		// Check if the item is equippable
		if (item is not
				{
					IsEquippable: true
				})
			return false;

		// Check if the item can be equipped in this slot
		if (!CanEquipInSlot(item, equipmentSlot))
			return false;

		// Unequip any existing item in the slot
		var existingItem = _equipmentItems[equipmentSlot];

		if (existingItem is not null)
		{
			// Add the existing item back to inventory
			if (!AddItem(existingItem))
					// If inventory is full, can't equip the new item
				return false;

			// Emit unequipped signal
			EmitSignal(SignalName.ItemUnequipped, existingItem, equipmentSlot);
		}

		// Equip the new item
		_equipmentItems[equipmentSlot]  = item;
		_inventoryItems[inventoryIndex] = null;

		// Emit equipped signal
		EmitSignal(SignalName.ItemEquipped, item, equipmentSlot);
		EmitSignal(SignalName.InventoryChanged);

		return true;
	}

	/// <summary>
	/// Unequips an item from an equipment slot.
	/// </summary>
	/// <param name="equipmentSlot">The equipment slot to unequip from.</param>
	/// <returns>True if the item was unequipped, false otherwise.</returns>
	public bool UnequipItem(int equipmentSlot)
	{
		if (equipmentSlot < 0 || equipmentSlot >= _equipmentItems.Count)
			return false;

		// Get the item to unequip
		var item = _equipmentItems[equipmentSlot];

		if (item == null)
			return false;

		// Add the item back to inventory
		if (!AddItem(item))
				// If inventory is full, can't unequip
			return false;

		// Remove from equipment slot
		_equipmentItems[equipmentSlot] = null;

		// Emit unequipped signal
		EmitSignal(SignalName.ItemUnequipped, item, equipmentSlot);
		EmitSignal(SignalName.InventoryChanged);

		return true;
	}

	/// <summary>
	/// Gets an equipped item.
	/// </summary>
	/// <param name="equipmentSlot">The equipment slot to get from.</param>
	/// <returns>The equipped item, or null if the slot is empty or invalid.</returns>
	public Item? GetEquippedItem(int equipmentSlot)
	{
		if (equipmentSlot < 0 || equipmentSlot >= _equipmentItems.Count)
			return null;

		return _equipmentItems[equipmentSlot];
	}

	/// <summary>
	/// Sets the selected hotbar index.
	/// </summary>
	/// <param name="index">The index to select.</param>
	/// <returns>True if the index was set, false if it's out of range.</returns>
	public bool SetSelectedHotbarIndex(int index)
	{
		if (index < 0 || index >= HotbarSize)
			return false;

		_selectedHotbarIndex = index;
		return true;
	}

	/// <summary>
	/// Gets the selected hotbar item.
	/// </summary>
	/// <returns>The selected item, or null if the slot is empty.</returns>
	public Item? GetSelectedHotbarItem() => GetItem(_selectedHotbarIndex);

	/// <summary>
	/// Gets the selected hotbar index.
	/// </summary>
	/// <returns>The selected hotbar index.</returns>
	public int GetSelectedHotbarIndex() => _selectedHotbarIndex;

	/// <summary>
	/// Checks if an item can be equipped in a specific slot.
	/// </summary>
	/// <param name="item">The item to check.</param>
	/// <param name="slotIndex">The slot index to check.</param>
	/// <returns>True if the item can be equipped in the slot, false otherwise.</returns>
	private static bool CanEquipInSlot(Item item, int slotIndex)
	{
		if (!item.IsEquippable)
			return false;

		// Define slot types (e.g., 0 = head, 1 = chest, 2 = legs, 3 = feet, 4 = hands)
		return slotIndex switch
		{
			0 => // Head slot
					item.EquipSlot == Item.EquipmentSlot.Head,
			1 => // Chest slot
					item.EquipSlot == Item.EquipmentSlot.Chest,
			2 => // Legs slot
					item.EquipSlot == Item.EquipmentSlot.Legs,
			3 => // Feet slot
					item.EquipSlot == Item.EquipmentSlot.Feet,
			4 => // Hands slot
					item.EquipSlot == Item.EquipmentSlot.Hands,
			_ => false
		};
	}

	/// <summary>
	/// Loads the default crafting recipes.
	/// </summary>
	private void LoadDefaultRecipes()
	{
		// Example recipes
		// Wood Planks (4) from Log (1)
		var woodPlanksRecipe = new CraftingRecipe
		{
			ResultItemId = "wood_planks",
			ResultAmount = 4,
			Ingredients = new()
			{
				{
					"log", 1
				}
			}
		};

		// Stick (4) from Wood Planks (2)
		var stickRecipe = new CraftingRecipe
		{
			ResultItemId = "stick",
			ResultAmount = 4,
			Ingredients = new()
			{
				{
					"wood_planks", 2
				}
			}
		};

		// Wooden Pickaxe from Wood Planks (3) and Stick (2)
		var woodenPickaxeRecipe = new CraftingRecipe
		{
			ResultItemId = "wooden_pickaxe",
			ResultAmount = 1,
			Ingredients = new()
			{
				{
					"wood_planks", 3
				},
				{
					"stick", 2
				}
			}
		};

		// Add recipes to known recipes
		_knownRecipes.Add(woodPlanksRecipe);
		_knownRecipes.Add(stickRecipe);
		_knownRecipes.Add(woodenPickaxeRecipe);
	}

	/// <summary>
	/// Gets all known crafting recipes.
	/// </summary>
	/// <returns>The list of known recipes.</returns>
	public List<CraftingRecipe> GetKnownRecipes() => _knownRecipes;

	/// <summary>
	/// Adds a new crafting recipe.
	/// </summary>
	/// <param name="recipe">The recipe to add.</param>
	public void AddRecipe(CraftingRecipe recipe)
	{
		if (!_knownRecipes.Contains(recipe))
			_knownRecipes.Add(recipe);
	}

	/// <summary>
	/// Checks if a recipe can be crafted with the current inventory.
	/// </summary>
	/// <param name="recipe">The recipe to check.</param>
	/// <returns>True if the recipe can be crafted, false otherwise.</returns>
	public bool CanCraftRecipe(CraftingRecipe recipe)
	{
		// Check if we have all the required ingredients
		foreach (var (itemId, requiredAmount) in recipe.Ingredients)
		{
			// Count how many of this item we have in inventory
			var availableAmount = _inventoryItems.OfType<Item>().Where(item => item.ItemId == itemId).Sum(item => item.StackCount);

			// If we don't have enough, can't craft
			if (availableAmount < requiredAmount)
				return false;
		}

		// Check if we have space for the result
		// This is a simplified check that doesn't account for stacking
		var emptySlots = _inventoryItems.Count(item => item == null);
		return emptySlots >= 1;
	}

	/// <summary>
	/// Crafts a recipe, consuming ingredients and adding the result to inventory.
	/// </summary>
	/// <param name="recipe">The recipe to craft.</param>
	/// <returns>True if the recipe was crafted, false otherwise.</returns>
	public bool CraftRecipe(CraftingRecipe recipe)
	{
		if (!CanCraftRecipe(recipe))
			return false;

		// Consume ingredients
		foreach (var (itemId, requiredAmount) in recipe.Ingredients)
		{
			var remainingToConsume = requiredAmount;

			// Find and consume items
			for (var i = 0; i < _inventoryItems.Count && remainingToConsume > 0; i++)
			{
				var item = _inventoryItems[i];

				if (item == null || item.ItemId != itemId)
					continue;

				// Determine how many to consume from this stack
				var toConsume = Mathf.Min(remainingToConsume, item.StackCount);

				// Consume from the stack
				item.StackCount    -= toConsume;
				remainingToConsume -= toConsume;

				// Remove the item if stack is empty
				if (item.StackCount <= 0)
					_inventoryItems[i] = null;
			}
		}

		// Create the result item
		var resultItem = ItemDatabase.GetItem(recipe.ResultItemId);

		if (resultItem is not null)
		{
			resultItem.StackCount = recipe.ResultAmount;

			// Add to inventory
			if (!AddItem(resultItem))
			{
				// This shouldn't happen since we checked for space, but just in case
				GD.PrintErr("Failed to add crafted item to inventory!");
				return false;
			}
		}

		EmitSignal(SignalName.InventoryChanged);
		return true;
	}
}