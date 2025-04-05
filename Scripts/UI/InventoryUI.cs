using System;
using Ceiro.Scripts.Gameplay.Player.Inventory;
using Godot;

namespace Ceiro.Scripts.UI;

/// <summary>
/// UI for displaying and interacting with the inventory.
/// </summary>
public partial class InventoryUi : Control
{
	[Export] public NodePath? InventorySystemPath;

	private InventorySystem _inventorySystem;
	private GridContainer   _inventoryGrid;
	private GridContainer   _hotbarGrid;
	private GridContainer   _equipmentGrid;
	private Control         _craftingPanel;
	private ItemSlot?       _draggedSlot;
	private TextureRect     _draggedItemIcon;

	public override void _Ready()
	{
		InventorySystem? inventorySystem;
		// Get the inventory system
		if (!string.IsNullOrEmpty(InventorySystemPath))
			inventorySystem = GetNodeOrNull<InventorySystem>(InventorySystemPath);
		else
				// Try to find the inventory system in the scene
			inventorySystem = GetTree().Root.FindChild("InventorySystem", true, false) as InventorySystem;

		if (_inventorySystem is null)
			throw new("InventoryUI: Failed to find InventorySystem!");

		_inventorySystem = inventorySystem!;

		// Connect to inventory changed signal
		_inventorySystem.Connect(InventorySystem.SignalName.InventoryChanged, new(this, nameof(UpdateUi)));

		// Get UI elements
		_inventoryGrid = GetNode<GridContainer>("InventoryPanel/InventoryGrid");
		_hotbarGrid    = GetNode<GridContainer>("HotbarPanel/HotbarGrid");
		_equipmentGrid = GetNode<GridContainer>("EquipmentPanel/EquipmentGrid");
		_craftingPanel = GetNode<Control>("CraftingPanel");

		// Create dragged item icon
		_draggedItemIcon = new()
		{
			Visible     = false,
			MouseFilter = MouseFilterEnum.Ignore
		};
		AddChild(_draggedItemIcon);

		// Initialize UI
		InitializeInventorySlots();
		InitializeHotbarSlots();
		InitializeEquipmentSlots();
		InitializeCraftingUi();

		// Update UI
		UpdateUi();
	}

	public override void _Process(double delta)
	{
		// Update dragged item position
		if (_draggedSlot is not null && _draggedItemIcon.Visible)
			_draggedItemIcon.GlobalPosition = GetViewport().GetMousePosition() - _draggedItemIcon.Size / 2;
	}

	/// <summary>
	/// Initializes the inventory slots.
	/// </summary>
	private void InitializeInventorySlots()
	{
		// Clear existing slots
		foreach (var child in _inventoryGrid.GetChildren())
			child.QueueFree();

		// Create slots
		for (var i = 0; i < _inventorySystem.InventorySize; i++)
		{
			var slot = new ItemSlot
			{
				SlotIndex = i,
				SlotType  = ItemSlot.SlotTypes.Inventory
			};

			_inventoryGrid.AddChild(slot);

			// Connect signals
			slot.Connect(ItemSlot.SignalName.SlotClicked,      new(this, nameof(OnSlotClicked)));
			slot.Connect(ItemSlot.SignalName.SlotRightClicked, new(this, nameof(OnSlotRightClicked)));
		}
	}

	/// <summary>
	/// Initializes the hotbar slots.
	/// </summary>
	private void InitializeHotbarSlots()
	{
		// Clear existing slots
		foreach (var child in _hotbarGrid.GetChildren())
			child.QueueFree();

		// Create slots
		for (var i = 0; i < _inventorySystem.HotbarSize; i++)
		{
			var slot = new ItemSlot
			{
				SlotIndex = i,
				SlotType  = ItemSlot.SlotTypes.Hotbar
			};

			_hotbarGrid.AddChild(slot);

			// Connect signals
			slot.Connect(ItemSlot.SignalName.SlotClicked,      new(this, nameof(OnSlotClicked)));
			slot.Connect(ItemSlot.SignalName.SlotRightClicked, new(this, nameof(OnSlotRightClicked)));
		}
	}

	/// <summary>
	/// Initializes the equipment slots.
	/// </summary>
	private void InitializeEquipmentSlots()
	{
		// Clear existing slots
		foreach (var child in _equipmentGrid.GetChildren())
			child.QueueFree();

		// Create slots
		for (var i = 0; i < _inventorySystem.EquipmentSlots; i++)
		{
			var slot = new ItemSlot
			{
				SlotIndex = i,
				SlotType  = ItemSlot.SlotTypes.Equipment
			};

			_equipmentGrid.AddChild(slot);

			// Connect signals
			slot.Connect(ItemSlot.SignalName.SlotClicked,      new(this, nameof(OnSlotClicked)));
			slot.Connect(ItemSlot.SignalName.SlotRightClicked, new(this, nameof(OnSlotRightClicked)));
		}
	}

	/// <summary>
	/// Initializes the crafting UI.
	/// </summary>
	private void InitializeCraftingUi()
	{
		// Get UI elements
		var recipeList  = _craftingPanel.GetNode<ItemList>("RecipeList");
		var craftButton = _craftingPanel.GetNode<Button>("CraftButton");

		// Clear existing recipes
		recipeList.Clear();

		// Add recipes
		foreach (var recipe in _inventorySystem.GetKnownRecipes())
			recipeList.AddItem(recipe.GetDescription());

		// Connect signals
		craftButton.Connect(BaseButton.SignalName.Pressed, new(this, nameof(OnCraftButtonPressed)));
	}

	/// <summary>
	/// Updates the UI to reflect the current inventory state.
	/// </summary>
	private void UpdateUi()
	{
		UpdateInventorySlots();
		UpdateHotbarSlots();
		UpdateEquipmentSlots();
		UpdateCraftingUi();
	}

	/// <summary>
	/// Updates the inventory slots.
	/// </summary>
	private void UpdateInventorySlots()
	{
		// Update each slot
		for (var i = 0; i < _inventorySystem.InventorySize; i++)
		{
			var item = _inventorySystem.GetItem(i);
			var slot = _inventoryGrid.GetChild<ItemSlot>(i);

			slot?.SetItem(item);
		}
	}

	/// <summary>
	/// Updates the hotbar slots.
	/// </summary>
	private void UpdateHotbarSlots()
	{
		// Update each slot
		for (var i = 0; i < _inventorySystem.HotbarSize; i++)
		{
			var item = _inventorySystem.GetItem(i);
			var slot = _hotbarGrid.GetChild<ItemSlot>(i);

			if (slot is null)
				continue;

			slot.SetItem(item);

			// Highlight selected slot
			slot.SetSelected(i == _inventorySystem.GetSelectedHotbarIndex());
		}
	}

	/// <summary>
	/// Updates the equipment slots.
	/// </summary>
	private void UpdateEquipmentSlots()
	{
		// Update each slot
		for (var i = 0; i < _inventorySystem.EquipmentSlots; i++)
		{
			var item = _inventorySystem.GetEquippedItem(i);
			var slot = _equipmentGrid.GetChild<ItemSlot>(i);

			slot?.SetItem(item);
		}
	}

	/// <summary>
	/// Updates the crafting UI.
	/// </summary>
	private void UpdateCraftingUi()
	{
		// Get UI elements
		var recipeList = _craftingPanel.GetNode<ItemList>("RecipeList");

		// Update recipe availability
		for (var i = 0; i < recipeList.ItemCount; i++)
		{
			var recipe   = _inventorySystem.GetKnownRecipes()[i];
			var canCraft = _inventorySystem.CanCraftRecipe(recipe);

			// Set item color based on availability
			recipeList.SetItemCustomFgColor(i, canCraft ? new(1, 1, 1) : new Color(0.5f, 0.5f, 0.5f));
		}
	}

	/// <summary>
	/// Called when a slot is clicked.
	/// </summary>
	/// <param name="slot">The clicked slot.</param>
	private void OnSlotClicked(ItemSlot slot)
	{
		// If we're dragging an item
		if (_draggedSlot != null)
		{
			// Handle based on slot types
			switch (_draggedSlot.SlotType)
			{
				// Move item within inventory
				case ItemSlot.SlotTypes.Inventory when slot.SlotType == ItemSlot.SlotTypes.Inventory:
					_inventorySystem.MoveItem(_draggedSlot.SlotIndex, slot.SlotIndex);
					break;
				// Equip item
				case ItemSlot.SlotTypes.Inventory when slot.SlotType == ItemSlot.SlotTypes.Equipment:
					_inventorySystem.EquipItem(_draggedSlot.SlotIndex, slot.SlotIndex);
					break;
				// Unequip item
				case ItemSlot.SlotTypes.Equipment when slot.SlotType == ItemSlot.SlotTypes.Inventory:
					_inventorySystem.UnequipItem(_draggedSlot.SlotIndex);
					break;
				// Move item from hotbar to inventory
				case ItemSlot.SlotTypes.Hotbar when slot.SlotType == ItemSlot.SlotTypes.Inventory:
				// Move item from inventory to hotbar
				case ItemSlot.SlotTypes.Inventory when slot.SlotType == ItemSlot.SlotTypes.Hotbar:
					_inventorySystem.MoveItem(_draggedSlot.SlotIndex, slot.SlotIndex);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			// End dragging
			_draggedSlot             = null;
			_draggedItemIcon.Visible = false;
		}
		else
		{
			// Start dragging if the slot has an item
			if (slot.HasItem())
			{
				_draggedSlot             = slot;
				_draggedItemIcon.Texture = slot.GetItem().Icon;
				_draggedItemIcon.Visible = true;
			}

			// If it's a hotbar slot, select it
			if (slot.SlotType is not ItemSlot.SlotTypes.Hotbar)
				return;

			_inventorySystem.SetSelectedHotbarIndex(slot.SlotIndex);
			UpdateHotbarSlots();
		}
	}

	/// <summary>
	/// Called when a slot is right-clicked.
	/// </summary>
	/// <param name="slot">The right-clicked slot.</param>
	private void OnSlotRightClicked(ItemSlot slot)
	{
		// Handle based on slot type
		switch (slot.SlotType)
		{
			case ItemSlot.SlotTypes.Equipment:
				// Unequip item
				_inventorySystem.UnequipItem(slot.SlotIndex);
				break;
			case ItemSlot.SlotTypes.Inventory or ItemSlot.SlotTypes.Hotbar:
			{
				// Split stack if stackable
				var item = _inventorySystem.GetItem(slot.SlotIndex);

				if (item is not
						{
							IsStackable: true,
							StackCount : > 1
						})
					return;

				// Create a new item with half the stack
				var newItem   = item.Clone();
				var halfStack = item.StackCount / 2;
				newItem.StackCount =  halfStack;
				item.StackCount    -= halfStack;

				// Find an empty slot
				for (var i = 0; i < _inventorySystem.InventorySize; i++)
					if (_inventorySystem.GetItem(i) == null)
					{
						// Add the new item to the empty slot
						_inventorySystem.RemoveItem(i);
						_inventorySystem.AddItem(newItem);
						break;
					}

				break;
			}
		}
	}

	/// <summary>
	/// Called when the craft button is pressed.
	/// </summary>
	private void OnCraftButtonPressed()
	{
		// Get the selected recipe
		var recipeList    = _craftingPanel.GetNode<ItemList>("RecipeList");
		var selectedIndex = recipeList.GetSelectedItems().Length > 0 ? recipeList.GetSelectedItems()[0] : -1;

		if (selectedIndex < 0 || selectedIndex >= _inventorySystem.GetKnownRecipes().Count)
			return;

		var recipe = _inventorySystem.GetKnownRecipes()[selectedIndex];

		// Craft the recipe
		_inventorySystem.CraftRecipe(recipe);
	}
}