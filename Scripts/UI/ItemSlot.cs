using Ceiro.Scripts.Gameplay.Player.Inventory;
using Godot;

namespace Ceiro.Scripts.UI;

/// <summary>
/// Represents a slot in the inventory UI.
/// </summary>
public partial class ItemSlot : Control
{
	[Signal]
	public delegate void SlotClickedEventHandler(ItemSlot slot);

	[Signal]
	public delegate void SlotRightClickedEventHandler(ItemSlot slot);

	public enum SlotTypes
	{
		Inventory,
		Hotbar,
		Equipment
	}

	[Export] public int       SlotIndex { get; set; }
	[Export] public SlotTypes SlotType  { get; set; } = SlotTypes.Inventory;

	private Item?       _item;
	private TextureRect _background;
	private TextureRect _itemIcon;
	private Label       _stackLabel;
	private TextureRect _selectionIndicator;
	private ProgressBar _durabilityBar;

	public override void _Ready()
	{
		// Create UI elements
		_background = new()
		{
			ExpandMode          = TextureRect.ExpandModeEnum.KeepSize,
			StretchMode         = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical   = SizeFlags.ExpandFill
		};
		AddChild(_background);

		_itemIcon = new()
		{
			ExpandMode          = TextureRect.ExpandModeEnum.KeepSize,
			StretchMode         = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical   = SizeFlags.ExpandFill
		};
		AddChild(_itemIcon);

		_stackLabel = new()
		{
			HorizontalAlignment = HorizontalAlignment.Right,
			VerticalAlignment   = VerticalAlignment.Bottom
		};
		AddChild(_stackLabel);

		_selectionIndicator = new()
		{
			ExpandMode          = TextureRect.ExpandModeEnum.KeepSize,
			StretchMode         = TextureRect.StretchModeEnum.KeepAspectCentered,
			SizeFlagsHorizontal = SizeFlags.ExpandFill,
			SizeFlagsVertical   = SizeFlags.ExpandFill,
			Visible             = false
		};
		AddChild(_selectionIndicator);

		_durabilityBar = new()
		{
			MinValue       = 0,
			MaxValue       = 100,
			Value          = 100,
			ShowPercentage = false,
			Visible        = false
		};
		AddChild(_durabilityBar);

		// Set up background texture based on slot type
		_background.Texture = SlotType switch
		{
			SlotTypes.Inventory => GD.Load<Texture2D>("res://textures/ui/inventory_slot.png"),
			SlotTypes.Hotbar    => GD.Load<Texture2D>("res://textures/ui/hotbar_slot.png"),
			SlotTypes.Equipment => GD.Load<Texture2D>("res://textures/ui/equipment_slot.png"),
			_                   => _background.Texture
		};

		// Set up selection indicator
		_selectionIndicator.Texture = GD.Load<Texture2D>("res://textures/ui/selection_indicator.png");

		// Connect input events
		GuiInput += OnGuiInput;
	}

	/// <summary>
	/// Sets the item in this slot.
	/// </summary>
	/// <param name="item">The item to set, or null to clear.</param>
	public void SetItem(Item? item)
	{
		_item = item;

		if (item is not null)
		{
			_itemIcon.Texture = item.Icon;
			_itemIcon.Visible = true;

			// Show stack count if stackable and more than 1
			if (item is
					{
						IsStackable: true,
						StackCount : > 1
					})
			{
				_stackLabel.Text    = item.StackCount.ToString();
				_stackLabel.Visible = true;
			}
			else
			{
				_stackLabel.Visible = false;
			}

			// Show durability bar if equippable
			if (item.IsEquippable)
			{
				_durabilityBar.Value   = item.GetDurabilityPercentage() * 100;
				_durabilityBar.Visible = true;
			}
			else
			{
				_durabilityBar.Visible = false;
			}
		}
		else
		{
			_itemIcon.Visible      = false;
			_stackLabel.Visible    = false;
			_durabilityBar.Visible = false;
		}
	}

	/// <summary>
	/// Gets the item in this slot.
	/// </summary>
	/// <returns>The item, or null if empty.</returns>
	public Item? GetItem() => _item;

	/// <summary>
	/// Checks if this slot has an item.
	/// </summary>
	/// <returns>True if the slot has an item, false otherwise.</returns>
	public bool HasItem() => _item is not null;

	/// <summary>
	/// Sets whether this slot is selected.
	/// </summary>
	/// <param name="selected">Whether the slot is selected.</param>
	public void SetSelected(bool selected) => _selectionIndicator.Visible = selected;

	/// <summary>
	/// Called when the slot receives input.
	/// </summary>
	/// <param name="event">The input event.</param>
	private void OnGuiInput(InputEvent @event)
	{
		if (@event is not InputEventMouseButton
		                  {
			                  Pressed: true
		                  } mouseButton)
			return;

		switch (mouseButton.ButtonIndex)
		{
			case MouseButton.Left:
				EmitSignal(SignalName.SlotClicked, this);
				break;
			case MouseButton.Right:
				EmitSignal(SignalName.SlotRightClicked, this);
				break;
		}
	}
}