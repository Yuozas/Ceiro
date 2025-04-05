using System.Collections.Generic;
using Ceiro.Scripts.Core.World.Generation;
using Godot;

namespace Ceiro.Scripts.Editor;

/// <summary>
/// Editor tool for creating and managing biome definitions.
/// </summary>
[Tool]
public partial class BiomeEditor : EditorPlugin
{
	private Control      _mainPanel;
	private Button       _createButton;
	private Button       _saveButton;
	private Button       _loadButton;
	private OptionButton _biomeSelector;
	private LineEdit     _biomeName;
	private SpinBox      _temperatureSlider;
	private SpinBox      _humiditySlider;
	private TextureRect  _previewRect;

	private          BiomeDefinition?      _currentBiome;
	private readonly List<BiomeDefinition> _biomes = [];

	public override void _EnterTree()
	{
		// Create the main panel
		_mainPanel      = new();
		_mainPanel.Name = "BiomeEditor";

		// Create UI elements
		_createButton = new()
		{
			Text = "Create New Biome"
		};
		_saveButton = new()
		{
			Text = "Save Biome"
		};
		_loadButton = new()
		{
			Text = "Load Biome"
		};
		_biomeSelector = new();
		_biomeName = new()
		{
			PlaceholderText = "Biome Name"
		};
		_temperatureSlider = new()
		{
			MinValue = 0,
			MaxValue = 1,
			Step     = 0.1f
		};
		_humiditySlider = new()
		{
			MinValue = 0,
			MaxValue = 1,
			Step     = 0.1f
		};
		_previewRect = new()
		{
			ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
		};

		// Connect signals
		_createButton.Pressed       += OnCreateButtonPressed;
		_saveButton.Pressed         += OnSaveButtonPressed;
		_loadButton.Pressed         += OnLoadButtonPressed;
		_biomeSelector.ItemSelected += OnBiomeSelected;

		// Add elements to the panel
		var vbox = new VBoxContainer();
		_mainPanel.AddChild(vbox);

		var hbox1 = new HBoxContainer();
		hbox1.AddChild(_createButton);
		hbox1.AddChild(_saveButton);
		hbox1.AddChild(_loadButton);
		vbox.AddChild(hbox1);

		var hbox2 = new HBoxContainer();
		hbox2.AddChild(new Label
		{
			Text = "Biome: "
		});
		hbox2.AddChild(_biomeSelector);
		vbox.AddChild(hbox2);

		var hbox3 = new HBoxContainer();
		hbox3.AddChild(new Label
		{
			Text = "Name: "
		});
		hbox3.AddChild(_biomeName);
		vbox.AddChild(hbox3);

		var hbox4 = new HBoxContainer();
		hbox4.AddChild(new Label
		{
			Text = "Temperature: "
		});
		hbox4.AddChild(_temperatureSlider);
		vbox.AddChild(hbox4);

		var hbox5 = new HBoxContainer();
		hbox5.AddChild(new Label
		{
			Text = "Humidity: "
		});
		hbox5.AddChild(_humiditySlider);
		vbox.AddChild(hbox5);

		vbox.AddChild(new Label
		{
			Text = "Preview: "
		});
		vbox.AddChild(_previewRect);

		// Add the panel to the editor
		AddControlToBottomPanel(_mainPanel, "Biome Editor");
	}

	public override void _ExitTree()
	{
		// Remove the panel from the editor
		RemoveControlFromBottomPanel(_mainPanel);
		_mainPanel.QueueFree();
	}

	private void OnCreateButtonPressed()
	{
		// Create a new biome definition
		_currentBiome = new()
		{
			Temperature = (float)_temperatureSlider.Value,
			Humidity    = (float)_humiditySlider.Value
		};

		// Add it to the list
		_biomes.Add(_currentBiome);

		// Update the UI
		UpdateBiomeSelector();
	}

	private void OnSaveButtonPressed()
	{
		if (_currentBiome is null)
			return;

		// Update the biome properties
		_currentBiome.Temperature = (float)_temperatureSlider.Value;
		_currentBiome.Humidity    = (float)_humiditySlider.Value;

		// Save the biome as a resource
		var resourcePath = $"res://resources/biomes/{_biomeName.Text}.tres";
		var error        = ResourceSaver.Save(_currentBiome, resourcePath);

		if (error != Error.Ok)
			GD.PrintErr($"Failed to save biome: {error}");
		else
			GD.Print($"Biome saved to {resourcePath}");
	}

	private void OnLoadButtonPressed()
	{
		// Create a file dialog
		var dialog = new FileDialog
		{
			FileMode = FileDialog.FileModeEnum.OpenFile,
			Access   = FileDialog.AccessEnum.Resources,
			Filters  = ["*.tres"]
		};

		// Show the dialog
		AddChild(dialog);
		dialog.PopupCentered();

		// Connect the file selected signal
		dialog.FileSelected += (string path) =>
		{
			// Load the biome
			var biome = ResourceLoader.Load<BiomeDefinition>(path);

			if (biome == null)
				return;

			_currentBiome = biome;
			_biomes.Add(biome);
			UpdateBiomeSelector();
			UpdateUi();
		};
	}

	private void OnBiomeSelected(long index)
	{
		if (index < 0 || index >= _biomes.Count)
			return;

		_currentBiome = _biomes[(int)index];
		UpdateUi();
	}

	private void UpdateBiomeSelector()
	{
		_biomeSelector.Clear();

		for (var i = 0; i < _biomes.Count; i++)
		{
			var biome = _biomes[i];
			_biomeSelector.AddItem(biome.ResourceName);

			if (biome == _currentBiome)
				_biomeSelector.Selected = i;
		}
	}

	private void UpdateUi()
	{
		if (_currentBiome is null)
			return;

		_biomeName.Text          = _currentBiome.ResourceName;
		_temperatureSlider.Value = _currentBiome.Temperature;
		_humiditySlider.Value    = _currentBiome.Humidity;

		// Update preview if there are center tiles
		if (_currentBiome.CenterTiles.Length > 0)
			_previewRect.Texture = _currentBiome.CenterTiles[0];
	}
}