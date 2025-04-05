using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ceiro.Scripts.Core.World.Generation;
using Ceiro.Scripts.Gameplay.Player;
using Ceiro.Scripts.Gameplay.Player.Inventory;
using Godot;
using TimeOfDaySystem = Ceiro.Scripts.Core.World._Time.TimeOfDaySystem;
using WeatherSystem = Ceiro.Scripts.Core.World._Time.WeatherSystem;

namespace Ceiro.Scripts.Core.Systems;

/// <summary>
/// Manages saving and loading game data.
/// </summary>
public partial class SaveLoadSystem : Node
{
	[Signal]
	public delegate void SaveCompletedEventHandler(bool success);

	[Signal]
	public delegate void LoadCompletedEventHandler(bool success);

	[Signal]
	public delegate void AutosaveCompletedEventHandler();

	[Export] public string SaveDirectory     = "user://saves/";
	[Export] public string SaveFileExtension = ".json";
	[Export] public bool   EnableAutosave    = true;
	[Export] public float  AutosaveInterval  = 300.0f; // 5 minutes
	[Export] public bool   CompressSaveFiles = true;
	[Export] public int    MaxSaveSlots      = 5;

	private float                      _autosaveTimer;
	private bool                       _isSaving;
	private bool                       _isLoading;
	private Dictionary<string, object> _cachedGameData = new();

	// Systems to save
	private ProceduralWorldGenerator _worldGenerator;
	private TimeOfDaySystem          _timeOfDaySystem;
	private WeatherSystem            _weatherSystem;
	private Player                   _player;
	private InventorySystem          _inventorySystem;

	private readonly JsonSerializerOptions _userEditableSaveJsonSerializerOptions = new()
	{
		WriteIndented = true
	};

	private readonly JsonSerializerOptions _userNonEditableSaveJsonSerializerOptions = new()
	{
		WriteIndented = false
	};

	public override void _Ready()
	{
		// Create save directory if it doesn't exist
		DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);

		// Find systems to save
		_worldGenerator  = GetTree().Root.FindChild("WorldGenerator",  true, false) as ProceduralWorldGenerator ?? throw new("Failed to find world generator.");
		_timeOfDaySystem = GetTree().Root.FindChild("TimeOfDaySystem", true, false) as TimeOfDaySystem          ?? throw new("Failed to find time of day system.");
		_weatherSystem   = GetTree().Root.FindChild("WeatherSystem",   true, false) as WeatherSystem            ?? throw new("Failed to find weather system.");

		// Find player
		var players = GetTree().GetNodesInGroup("Player");
		if (players.Count > 0)
			_player = players[0] as Player ?? throw new("Failed to find player.");

		// Find inventory system
		_inventorySystem = GetTree().Root.FindChild("InventorySystem", true, false) as InventorySystem ?? throw new("Failed to find inventory system.");

		// Reset autosave timer
		_autosaveTimer = AutosaveInterval;
	}

	public override void _Process(double delta)
	{
		// Update autosave timer
		if (!EnableAutosave || _isSaving || _isLoading)
			return;

		_autosaveTimer -= (float)delta;

		if (!(_autosaveTimer <= 0))
			return;

		Task.Run(Autosave);
		_autosaveTimer = AutosaveInterval;
	}

	/// <summary>
	/// Saves the game to the specified slot.
	/// </summary>
	/// <param name="slotName">The name of the save slot.</param>
	public async Task SaveGame(string slotName)
	{
		if (_isSaving || _isLoading)
		{
			GD.PrintErr("SaveLoadSystem: Cannot save while another save/load operation is in progress.");
			EmitSignal(SignalName.SaveCompleted, false);
			return;
		}

		_isSaving = true;

		try
		{
			// Collect game data
			var gameData = CollectGameData();

			// Save metadata
			gameData["metadata"] = new Dictionary<string, object>
			{
				{
					"version", "1.0"
				},
				{
					"timestamp", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
				},
				{
					"playerName", _player?.Name ?? "Unknown"
				},
				{
					"gameTime", _timeOfDaySystem?.GetTimeString() ?? "Unknown"
				}
			};

			// Convert to JSON
			var jsonData = JsonSerializer.Serialize(gameData, _userEditableSaveJsonSerializerOptions);

			// Ensure the save directory exists
			DirAccess.MakeDirRecursiveAbsolute(SaveDirectory);

			// Save file path
			var savePath = SaveDirectory + slotName + SaveFileExtension;

			// Save the file
			await SaveFileAsync(savePath, jsonData);

			// Save chunks separately
			await SaveWorldChunksAsync(slotName);

			GD.Print($"Game saved to slot: {slotName}");
			EmitSignal(SignalName.SaveCompleted, true);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error saving game: {e.Message}");
			EmitSignal(SignalName.SaveCompleted, false);
		}
		finally
		{
			_isSaving = false;
		}
	}

	/// <summary>
	/// Loads the game from the specified slot.
	/// </summary>
	/// <param name="slotName">The name of the save slot.</param>
	public async Task LoadGame(string slotName)
	{
		if (_isSaving || _isLoading)
		{
			GD.PrintErr("SaveLoadSystem: Cannot load while another save/load operation is in progress.");
			EmitSignal(SignalName.LoadCompleted, false);
			return;
		}

		_isLoading = true;

		try
		{
			// Save file path
			var savePath = SaveDirectory + slotName + SaveFileExtension;

			// Check if the file exists
			if (!FileAccess.FileExists(savePath))
			{
				GD.PrintErr($"Save file not found: {savePath}");
				EmitSignal(SignalName.LoadCompleted, false);
				return;
			}

			// Load the file
			var jsonData = await LoadFileAsync(savePath);

			// Parse JSON
			var gameData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonData) ?? throw new("Failed to parse save file.");

			// Convert JsonElement to appropriate types
			var convertedGameData = ConvertJsonElementsToDictionary(gameData);

			// Apply game data
			ApplyGameData(convertedGameData);

			// Load chunks separately
			await LoadWorldChunksAsync(slotName);

			GD.Print($"Game loaded from slot: {slotName}");
			EmitSignal(SignalName.LoadCompleted, true);
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error loading game: {e.Message}");
			EmitSignal(SignalName.LoadCompleted, false);
		}
		finally
		{
			_isLoading = false;
		}
	}

	/// <summary>
	/// Performs an autosave.
	/// </summary>
	private async Task Autosave()
	{
		await SaveGame("autosave");
		EmitSignal(SignalName.AutosaveCompleted);
	}

	/// <summary>
	/// Collects all game data to be saved.
	/// </summary>
	/// <returns>A dictionary containing all game data.</returns>
	private Dictionary<string, object> CollectGameData()
	{
		// Save player data
		var gameData = new Dictionary<string, object>
		{
			["player"] = new Dictionary<string, object>
			{
				{
					"position", new[]
					{
						_player.GlobalPosition.X, _player.GlobalPosition.Y, _player.GlobalPosition.Z
					}
				},
				{
					"rotation", new[]
					{
						_player.Rotation.X, _player.Rotation.Y, _player.Rotation.Z
					}
				},
				{
					"health", _player.GetHealth()
				},
				{
					"hunger", _player.GetHunger()
				}
			}
		};

		// Save inventory data
		var inventoryData = new Dictionary<string, object>();
		var items         = _inventorySystem.GetAllItems();

		var itemsList = items.Select(item => new Dictionary<string, object>
		                     {
			                     {
				                     "id", item.ItemId
			                     },
			                     {
				                     "count", item.StackCount
			                     },
			                     {
				                     "slot", item.EquipSlot
			                     },
			                     {
				                     "durability", item.Durability
			                     }
		                     })
		                     .ToList();

		inventoryData["items"]         = itemsList;
		inventoryData["equippedItems"] = _inventorySystem.GetEquippedItemIDs();

		gameData["inventory"] = inventoryData;


		// Save time and weather data
		gameData["timeOfDay"] = new Dictionary<string, object>
		{
			{
				"time", _timeOfDaySystem.GetTime()
			},
			{
				"day", _timeOfDaySystem.GetDay()
			},
			{
				"season", (int)_timeOfDaySystem.GetSeason()
			}
		};

		gameData["weather"] = new Dictionary<string, object>
		{
			{
				"currentWeather", (int)_weatherSystem.GetCurrentWeather()
			}
		};

		// Save world generator seed and settings
		gameData["worldGenerator"] = new Dictionary<string, object>
		{
			{
				"seed", _worldGenerator.Seed
			},
			{
				"chunkSize", _worldGenerator.ChunkSize
			},
			{
				"viewDistance", _worldGenerator.ViewDistance
			}
		};

		return gameData;
	}

	/// <summary>
	/// Applies loaded game data to the game systems.
	/// </summary>
	/// <param name="gameData">The game data to apply.</param>
	private void ApplyGameData(Dictionary<string, object> gameData)
	{
		// Apply player data
		if (gameData.TryGetValue("player", out var playerObj) && playerObj is Dictionary<string, object> playerData)
		{
			if (playerData.TryGetValue("position", out var posObj) && posObj is List<object> posList && posList.Count >= 3)
				_player.GlobalPosition = new(Convert.ToSingle(posList[0]),
				                             Convert.ToSingle(posList[1]),
				                             Convert.ToSingle(posList[2]));

			if (playerData.TryGetValue("rotation", out var rotObj) && rotObj is List<object> rotList && rotList.Count >= 3)
				_player.Rotation = new(Convert.ToSingle(rotList[0]),
				                       Convert.ToSingle(rotList[1]),
				                       Convert.ToSingle(rotList[2]));

			if (playerData.TryGetValue("health", out var healthObj))
				_player.SetHealth(Convert.ToSingle(healthObj));

			if (playerData.TryGetValue("hunger", out var hungerObj))
				_player.SetHunger(Convert.ToSingle(hungerObj));
		}

		// Apply inventory data
		if (gameData.TryGetValue("inventory", out var invObj) && invObj is Dictionary<string, object> inventoryData)
		{
			// Clear current inventory
			_inventorySystem.ClearInventory();

			// Add items
			if (inventoryData.TryGetValue("items", out var itemsObj) && itemsObj is List<object> itemsList)
				foreach (var itemObj in itemsList)
					if (itemObj is Dictionary<string, object> itemData)
					{
						var id         = itemData["id"].ToString() ?? throw new("Item ID is missing.");
						var count      = Convert.ToInt32(itemData["count"]);
						var slot       = Convert.ToInt32(itemData["slot"]);
						var durability = Convert.ToSingle(itemData["durability"]);

						_inventorySystem.AddItemToSlot(id, count, slot, durability);
					}

			// Equip items
			if (inventoryData.TryGetValue("equippedItems", out var equippedObj) && equippedObj is List<object> equippedList)
				foreach (var itemId in equippedList)
					_inventorySystem.EquipItem(itemId.ToString() ?? throw new("Item ID is missing."));
		}

		// Apply time and weather data
		if (gameData.TryGetValue("timeOfDay", out var timeObj) && timeObj is Dictionary<string, object> timeData)
		{
			if (timeData.TryGetValue("time", out var timeValue))
				_timeOfDaySystem.SetTime(Convert.ToSingle(timeValue));

			if (timeData.TryGetValue("day", out var dayValue))
				_timeOfDaySystem.SetDay(Convert.ToInt32(dayValue));

			if (timeData.TryGetValue("season", out var seasonValue))
				_timeOfDaySystem.SetSeason((TimeOfDaySystem.Seasons)Convert.ToInt32(seasonValue));
		}

		if (gameData.TryGetValue("weather", out var weatherObj) && weatherObj is Dictionary<string, object> weatherData)
			if (weatherData.TryGetValue("currentWeather", out var weatherValue))
				_weatherSystem.SetWeather((WeatherSystem.WeatherType)Convert.ToInt32(weatherValue));

		// Apply world generator data
		if (!gameData.TryGetValue("worldGenerator", out var worldObj) || worldObj is not Dictionary<string, object> worldData)
			return;

		if (worldData.TryGetValue("seed", out var seedValue))
			_worldGenerator.Seed = Convert.ToInt32(seedValue);

		if (worldData.TryGetValue("viewDistance", out var viewDistanceValue))
			_worldGenerator.ViewDistance = Convert.ToInt32(viewDistanceValue);
	}

	/// <summary>
	/// Saves world chunks to separate files.
	/// </summary>
	/// <param name="slotName">The name of the save slot.</param>
	private async Task SaveWorldChunksAsync(string slotName)
	{
		// Create chunks directory
		var chunksDir = SaveDirectory + slotName + "_chunks/";
		DirAccess.MakeDirRecursiveAbsolute(chunksDir);

		// Get loaded chunks
		var loadedChunks = _worldGenerator.GetLoadedChunks();

		// Save each chunk
		foreach (var chunkPos in loadedChunks)
		{
			var chunkData = _worldGenerator.GetChunkData(chunkPos);


			var chunkFileName = $"{chunkPos.X}_{chunkPos.Y}.json";
			var chunkPath     = chunksDir + chunkFileName;

			var jsonData = JsonSerializer.Serialize(chunkData, _userNonEditableSaveJsonSerializerOptions);

			await SaveFileAsync(chunkPath, jsonData);
		}
	}

	/// <summary>
	/// Loads world chunks from separate files.
	/// </summary>
	/// <param name="slotName">The name of the save slot.</param>
	private async Task LoadWorldChunksAsync(string slotName)
	{
		// Chunks directory
		var chunksDir = SaveDirectory + slotName + "_chunks/";

		// Check if directory exists
		if (!DirAccess.DirExistsAbsolute(chunksDir))
			return;

		// Get all chunk files
		var dir = DirAccess.Open(chunksDir);
		if (dir == null)
			return;

		dir.ListDirBegin();
		var fileName = dir.GetNext();

		while (!string.IsNullOrEmpty(fileName))
		{
			if (!fileName.StartsWith('.') && fileName.EndsWith(".json"))
			{
				var chunkPath = chunksDir + fileName;

				// Parse chunk position from filename
				var parts = fileName.Replace(".json", "").Split('_');

				if (parts.Length >= 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y))
				{
					var chunkPos = new Vector2I(x, y);

					// Load chunk data
					var jsonData  = await LoadFileAsync(chunkPath);
					var chunkData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonData) ?? throw new("Failed to parse chunk file.");

					// Convert JsonElement to appropriate types
					var convertedChunkData = ConvertJsonElementsToDictionary(chunkData);

					// Apply chunk data
					_worldGenerator.LoadChunkData(chunkPos, convertedChunkData);
				}
			}

			fileName = dir.GetNext();
		}

		dir.ListDirEnd();
	}

	/// <summary>
	/// Saves a file asynchronously.
	/// </summary>
	/// <param name="path">The file path.</param>
	/// <param name="content">The file content.</param>
	private async Task SaveFileAsync(string path, string content) => await Task.Run(() =>
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Write);
		if (file is null)
			throw new($"Failed to open file for writing: {path}");

		file.StoreString(content);
	});

	/// <summary>
	/// Loads a file asynchronously.
	/// </summary>
	/// <param name="path">The file path.</param>
	/// <returns>The file content.</returns>
	private async Task<string> LoadFileAsync(string path) => await Task.Run(() =>
	{
		using var file = FileAccess.Open(path, FileAccess.ModeFlags.Read);
		if (file is null)
			throw new($"Failed to open file for reading: {path}");

		return file.GetAsText();
	});

	/// <summary>
	/// Gets a list of available save slots.
	/// </summary>
	/// <returns>A list of save slot information.</returns>
	public List<Dictionary<string, object>> GetSaveSlots()
	{
		var saveSlots = new List<Dictionary<string, object>>();

		// Check if directory exists
		if (!DirAccess.DirExistsAbsolute(SaveDirectory))
			return saveSlots;

		// Get all save files
		var dir = DirAccess.Open(SaveDirectory);
		if (dir == null)
			return saveSlots;

		dir.ListDirBegin();
		var fileName = dir.GetNext();

		while (!string.IsNullOrEmpty(fileName))
		{
			if (!fileName.StartsWith('.') && fileName.EndsWith(SaveFileExtension) && !fileName.Contains("_chunks"))
			{
				var slotName = fileName.Replace(SaveFileExtension, "");
				var savePath = SaveDirectory + fileName;

				// Get file info
				var fileInfo = FileAccess.GetModifiedTime(savePath);

				// Try to load metadata
				try
				{
					using var file = FileAccess.Open(savePath, FileAccess.ModeFlags.Read);

					if (file != null)
					{
						var jsonData = file.GetAsText();
						var gameData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonData) ?? throw new("Failed to parse save file.");

						if (gameData.TryGetValue("metadata", out var metadataElement) && metadataElement.ValueKind == JsonValueKind.Object)
						{
							var metadata = new Dictionary<string, object>();

							foreach (var prop in metadataElement.EnumerateObject())
								metadata[prop.Name] = prop.Value.ToString();

							saveSlots.Add(new()
							{
								{
									"name", slotName
								},
								{
									"timestamp", fileInfo.ToString()
								},
								{
									"metadata", metadata
								}
							});

							continue;
						}
					}
				}
				catch
				{
					// TODO: If metadata loading fails, fall back to basic info
				}

				// Fallback if metadata loading failed
				saveSlots.Add(new()
				{
					{
						"name", slotName
					},
					{
						"timestamp", fileInfo.ToString()
					},
					{
						"metadata", new Dictionary<string, object>
						{
							{
								"version", "Unknown"
							},
							{
								"timestamp", fileInfo.ToString()
							},
							{
								"playerName", "Unknown"
							},
							{
								"gameTime", "Unknown"
							}
						}
					}
				});
			}

			fileName = dir.GetNext();
		}

		dir.ListDirEnd();

		// Sort by timestamp (newest first)
		saveSlots.Sort((a, b) =>
		{
			var aMetadata = a["metadata"] as Dictionary<string, object> ?? throw new("Metadata is missing.");
			var bMetadata = b["metadata"] as Dictionary<string, object> ?? throw new("Metadata is missing.");

			var aTimestamp = aMetadata.TryGetValue("timestamp", out var valueA) ? valueA.ToString() : "";
			var bTimestamp = bMetadata.TryGetValue("timestamp", out var valueB) ? valueB.ToString() : "";

			return string.CompareOrdinal(bTimestamp, aTimestamp);
		});

		return saveSlots;
	}

	/// <summary>
	/// Deletes a save slot.
	/// </summary>
	/// <param name="slotName">The name of the save slot.</param>
	/// <returns>True if the save was deleted successfully, false otherwise.</returns>
	public bool DeleteSaveSlot(string slotName)
	{
		try
		{
			// Delete main save file
			var savePath = SaveDirectory + slotName + SaveFileExtension;
			if (FileAccess.FileExists(savePath))
				DirAccess.RemoveAbsolute(savePath);

			// Delete chunks directory
			var chunksDir = SaveDirectory + slotName + "_chunks/";

			if (!DirAccess.DirExistsAbsolute(chunksDir))
				return true;

			// Delete all files in the directory
			var dir = DirAccess.Open(chunksDir);

			if (dir != null)
			{
				dir.ListDirBegin();
				var fileName = dir.GetNext();

				while (!string.IsNullOrEmpty(fileName))
				{
					if (!fileName.StartsWith('.'))
						DirAccess.RemoveAbsolute(chunksDir + fileName);

					fileName = dir.GetNext();
				}

				dir.ListDirEnd();
			}

			// Delete the directory
			DirAccess.RemoveAbsolute(chunksDir);

			return true;
		}
		catch (Exception e)
		{
			GD.PrintErr($"Error deleting save slot: {e.Message}");
			return false;
		}
	}

	/// <summary>
	/// Converts JsonElement objects to appropriate Dictionary and List types.
	/// </summary>
	/// <param name="data">The data to convert.</param>
	/// <returns>The converted data.</returns>
	private Dictionary<string, object> ConvertJsonElementsToDictionary(Dictionary<string, JsonElement> data)
	{
		var result = new Dictionary<string, object>();

		foreach (var kvp in data)
			result[kvp.Key] = ConvertJsonElement(kvp.Value);

		return result;
	}

	/// <summary>
	/// Converts a JsonElement to an appropriate type.
	/// </summary>
	/// <param name="element">The JsonElement to convert.</param>
	/// <returns>The converted object.</returns>
	private object ConvertJsonElement(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				var obj = new Dictionary<string, object>();
				foreach (var prop in element.EnumerateObject())
					obj[prop.Name] = ConvertJsonElement(prop.Value);
				return obj;

			case JsonValueKind.Array:
				return element.EnumerateArray().Select(ConvertJsonElement).ToList();

			case JsonValueKind.String:
				return element.GetString() ?? throw new("String value is null.");

			case JsonValueKind.Number:
				if (element.TryGetInt32(out var intValue))
					return intValue;
				if (element.TryGetInt64(out var longValue))
					return longValue;

				return element.GetDouble();

			case JsonValueKind.True:
				return true;

			case JsonValueKind.False:
				return false;

			case JsonValueKind.Null:
				throw new("Null value is not allowed.");

			case JsonValueKind.Undefined:
				throw new("Undefined value is not allowed.");
			default:
				throw new("Unknown value kind.");
		}
	}
}