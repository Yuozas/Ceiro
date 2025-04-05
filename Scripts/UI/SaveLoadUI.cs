using System.Collections.Generic;
using Ceiro.Scripts.Core.Systems;
using Godot;

namespace Ceiro.Scripts.UI;

/// <summary>
/// UI for the save/load system.
/// </summary>
public partial class SaveLoadUi : Control
{
	[Export] public NodePath SaveLoadSystemPath;

	private SaveLoadSystem _saveLoadSystem;
	private VBoxContainer  _saveSlotContainer;
	private Button         _saveButton;
	private Button         _loadButton;
	private Button         _deleteButton;
	private LineEdit       _newSaveNameInput;
	private Button         _createNewSaveButton;
	private Label          _statusLabel;

	private string _selectedSlot;

	public override void _Ready()
	{
		// Get the save/load system
		if (!string.IsNullOrEmpty(SaveLoadSystemPath))
			_saveLoadSystem = GetNode<SaveLoadSystem>(SaveLoadSystemPath);
		else
				// Try to find the save/load system in the scene
			_saveLoadSystem = GetTree().Root.FindChild("SaveLoadSystem", true, false) as SaveLoadSystem;

		if (_saveLoadSystem == null)
		{
			GD.PrintErr("SaveLoadUI: Failed to find SaveLoadSystem!");
			return;
		}

		// Get UI elements
		_saveSlotContainer   = GetNode<VBoxContainer>("Panel/VBoxContainer/SaveSlotContainer");
		_saveButton          = GetNode<Button>("Panel/VBoxContainer/ButtonContainer/SaveButton");
		_loadButton          = GetNode<Button>("Panel/VBoxContainer/ButtonContainer/LoadButton");
		_deleteButton        = GetNode<Button>("Panel/VBoxContainer/ButtonContainer/DeleteButton");
		_newSaveNameInput    = GetNode<LineEdit>("Panel/VBoxContainer/NewSaveContainer/NameInput");
		_createNewSaveButton = GetNode<Button>("Panel/VBoxContainer/NewSaveContainer/CreateButton");
		_statusLabel         = GetNode<Label>("Panel/VBoxContainer/StatusLabel");

		// Connect signals
		_saveButton.Pressed          += OnSaveButtonPressed;
		_loadButton.Pressed          += OnLoadButtonPressed;
		_deleteButton.Pressed        += OnDeleteButtonPressed;
		_createNewSaveButton.Pressed += OnCreateNewSaveButtonPressed;

		_saveLoadSystem.Connect(SaveLoadSystem.SignalName.SaveCompleted, new(this, nameof(OnSaveCompleted)));
		_saveLoadSystem.Connect(SaveLoadSystem.SignalName.LoadCompleted, new(this, nameof(OnLoadCompleted)));

		// Refresh save slots
		RefreshSaveSlots();

		// Initialize button states
		UpdateButtonStates();
	}

	/// <summary>
	/// Refreshes the list of save slots.
	/// </summary>
	private void RefreshSaveSlots()
	{
		// Clear existing slots
		foreach (var child in _saveSlotContainer.GetChildren())
			child.QueueFree();

		// Get save slots
		var saveSlots = _saveLoadSystem.GetSaveSlots();

		// Add slots to container
		foreach (var slot in saveSlots)
		{
			var slotButton = new Button();
			var slotName   = slot["name"].ToString();

			// Get metadata
			var metadata   = slot["metadata"] as Dictionary<string, object>;
			var timestamp  = metadata != null && metadata.ContainsKey("timestamp") ? metadata["timestamp"].ToString() : "";
			var playerName = metadata != null && metadata.ContainsKey("playerName") ? metadata["playerName"].ToString() : "Unknown";
			var gameTime   = metadata != null && metadata.ContainsKey("gameTime") ? metadata["gameTime"].ToString() : "";

			// Set button text
			slotButton.Text = $"{slotName} - {playerName} - {gameTime} - {timestamp}";

			// Set button properties
			slotButton.ToggleMode  = true;
			slotButton.ButtonGroup = new();

			// Connect signal
			slotButton.Pressed += () => OnSaveSlotSelected(slotName);

			// Add to container
			_saveSlotContainer.AddChild(slotButton);
		}

		// Reset selected slot
		_selectedSlot = null;
		UpdateButtonStates();
	}

	/// <summary>
	/// Called when a save slot is selected.
	/// </summary>
	/// <param name="slotName">The name of the selected slot.</param>
	private void OnSaveSlotSelected(string slotName)
	{
		_selectedSlot = slotName;
		UpdateButtonStates();
	}

	/// <summary>
	/// Updates the states of the buttons based on the current selection.
	/// </summary>
	private void UpdateButtonStates()
	{
		var hasSelection = !string.IsNullOrEmpty(_selectedSlot);

		_saveButton.Disabled   = !hasSelection;
		_loadButton.Disabled   = !hasSelection;
		_deleteButton.Disabled = !hasSelection;
	}

	/// <summary>
	/// Called when the save button is pressed.
	/// </summary>
	private void OnSaveButtonPressed()
	{
		if (string.IsNullOrEmpty(_selectedSlot))
			return;

		// Confirm overwrite
		var confirmDialog = new ConfirmationDialog();
		confirmDialog.Title      = "Confirm Save";
		confirmDialog.DialogText = $"Are you sure you want to overwrite the save '{_selectedSlot}'?";

		AddChild(confirmDialog);
		confirmDialog.PopupCentered();

		confirmDialog.Confirmed += () =>
		{
			_statusLabel.Text = "Saving...";
			_saveLoadSystem.SaveGame(_selectedSlot);
		};
	}

	/// <summary>
	/// Called when the load button is pressed.
	/// </summary>
	private void OnLoadButtonPressed()
	{
		if (string.IsNullOrEmpty(_selectedSlot))
			return;

		// Confirm load
		var confirmDialog = new ConfirmationDialog();
		confirmDialog.Title      = "Confirm Load";
		confirmDialog.DialogText = "Loading will discard any unsaved progress. Continue?";

		AddChild(confirmDialog);
		confirmDialog.PopupCentered();

		confirmDialog.Confirmed += () =>
		{
			_statusLabel.Text = "Loading...";
			_saveLoadSystem.LoadGame(_selectedSlot);
		};
	}

	/// <summary>
	/// Called when the delete button is pressed.
	/// </summary>
	private void OnDeleteButtonPressed()
	{
		if (string.IsNullOrEmpty(_selectedSlot))
			return;

		// Confirm delete
		var confirmDialog = new ConfirmationDialog();
		confirmDialog.Title      = "Confirm Delete";
		confirmDialog.DialogText = $"Are you sure you want to delete the save '{_selectedSlot}'? This cannot be undone.";

		AddChild(confirmDialog);
		confirmDialog.PopupCentered();

		confirmDialog.Confirmed += () =>
		{
			if (_saveLoadSystem.DeleteSaveSlot(_selectedSlot))
			{
				_statusLabel.Text = $"Deleted save '{_selectedSlot}'.";
				RefreshSaveSlots();
			}
			else
			{
				_statusLabel.Text = $"Failed to delete save '{_selectedSlot}'.";
			}
		};
	}

	/// <summary>
	/// Called when the create new save button is pressed.
	/// </summary>
	private void OnCreateNewSaveButtonPressed()
	{
		var newSaveName = _newSaveNameInput.Text.Trim();

		if (string.IsNullOrEmpty(newSaveName))
		{
			_statusLabel.Text = "Please enter a save name.";
			return;
		}

		// Check if name contains invalid characters
		if (newSaveName.Contains("/")
		 || newSaveName.Contains("\\")
		 || newSaveName.Contains(":")
		 || newSaveName.Contains("*")
		 || newSaveName.Contains("?")
		 || newSaveName.Contains("\"")
		 || newSaveName.Contains("<")
		 || newSaveName.Contains(">")
		 || newSaveName.Contains("|"))
		{
			_statusLabel.Text = "Save name contains invalid characters.";
			return;
		}

		// Save to new slot
		_statusLabel.Text = "Saving...";
		_saveLoadSystem.SaveGame(newSaveName);

		// Clear input
		_newSaveNameInput.Text = "";
	}

	/// <summary>
	/// Called when a save operation completes.
	/// </summary>
	/// <param name="success">Whether the save was successful.</param>
	private void OnSaveCompleted(bool success)
	{
		if (success)
		{
			_statusLabel.Text = "Save completed successfully.";
			RefreshSaveSlots();
		}
		else
		{
			_statusLabel.Text = "Save failed.";
		}
	}

	/// <summary>
	/// Called when a load operation completes.
	/// </summary>
	/// <param name="success">Whether the load was successful.</param>
	private void OnLoadCompleted(bool success)
	{
		if (success)
		{
			_statusLabel.Text = "Load completed successfully.";

			// Hide the UI
			Visible = false;
		}
		else
		{
			_statusLabel.Text = "Load failed.";
		}
	}

	/// <summary>
	/// Shows the save/load UI.
	/// </summary>
	public void Show()
	{
		RefreshSaveSlots();
		Visible = true;
	}

	/// <summary>
	/// Hides the save/load UI.
	/// </summary>
	public void Hide() => Visible = false;
}