using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Ceiro.Scripts.Core.World.Generation;

/// <summary>
/// Core class for procedural world generation, handling terrain, biomes, and chunk management.
/// </summary>
public partial class ProceduralWorldGenerator : Node3D
{
	[Export] public int         Seed;
	[Export] public int         ChunkSize    = 16;
	[Export] public int         ViewDistance = 5;
	[Export] public float       NoiseScale   = 0.1f;
	[Export] public NodePath    PlayerPath;
	[Export] public PackedScene ChunkPrefab;
	[Export] public Resource[]  BiomeDefinitions;

	private FastNoiseLite                _noise;
	private Node3D                       _player;
	private Vector2I                     _currentPlayerChunk = new(0, 0);
	private Dictionary<Vector2I, Node3D> _loadedChunks       = new();
	private Queue<Vector2I>              _chunkLoadQueue     = new();
	private Queue<Vector2I>              _chunkUnloadQueue   = new();
	private bool                         _isProcessingChunks;

	public override void _Ready()
	{
		// Initialize the noise generator
		_noise             = new();
		_noise.Seed        = Seed;
		_noise.NoiseType   = FastNoiseLite.NoiseTypeEnum.Perlin;
		_noise.FractalType = FastNoiseLite.FractalTypeEnum.Fbm;
		_noise.Frequency   = NoiseScale;

		// Get the player reference
		if (!string.IsNullOrEmpty(PlayerPath))
			throw new("PlayerPath is not set!");

		_player = GetNodeOrNull<Node3D>(PlayerPath) ?? throw new("Player not found or unable to cast to type Node3D.");

		// Start generating the initial chunks
		UpdateChunks();
	}

	public override void _Process(double delta)
	{
		// Check if player has moved to a new chunk
		var playerChunkPos = WorldToChunkPosition(_player.GlobalPosition);

		if (playerChunkPos != _currentPlayerChunk)
		{
			_currentPlayerChunk = playerChunkPos;
			UpdateChunks();
		}

		// Process chunk loading/unloading queue
		ProcessChunkQueue();
	}

	/// <summary>
	/// Updates the chunks around the player based on view distance.
	/// </summary>
	private void UpdateChunks()
	{
		// Calculate which chunks should be loaded
		var chunksToLoad = new HashSet<Vector2I>();

		for (var x = -ViewDistance; x <= ViewDistance; x++)
		{
			for (var z = -ViewDistance; z <= ViewDistance; z++)
			{
				var chunkPos = new Vector2I(_currentPlayerChunk.X + x, _currentPlayerChunk.Y + z);
				chunksToLoad.Add(chunkPos);
			}
		}

		// Queue chunks to load
		foreach (var chunkPos in chunksToLoad.Where(chunkPos => !_loadedChunks.ContainsKey(chunkPos)))
			_chunkLoadQueue.Enqueue(chunkPos);

		// Queue chunks to unload
		foreach (var loadedChunk in _loadedChunks.Keys.Where(loadedChunk => !chunksToLoad.Contains(loadedChunk)))
			_chunkUnloadQueue.Enqueue(loadedChunk);
	}

	/// <summary>
	/// Processes the chunk loading and unloading queues.
	/// </summary>
	private async Task ProcessChunkQueue()
	{
		if (_isProcessingChunks)
			return;

		_isProcessingChunks = true;

		// Process a limited number of chunks per frame to avoid stuttering
		var       chunksProcessed   = 0;
		const int maxChunksPerFrame = 2;

		// Process unload queue first
		while (_chunkUnloadQueue.Count > 0 && chunksProcessed < maxChunksPerFrame)
		{
			var chunkPos = _chunkUnloadQueue.Dequeue();

			if (!_loadedChunks.TryGetValue(chunkPos, out var chunk))
				continue;

			chunk.QueueFree();
			_loadedChunks.Remove(chunkPos);
			chunksProcessed++;
		}

		// Process load queue
		while (_chunkLoadQueue.Count > 0 && chunksProcessed < maxChunksPerFrame)
		{
			var chunkPos = _chunkLoadQueue.Dequeue();

			if (_loadedChunks.ContainsKey(chunkPos))
				continue;

			await GenerateChunk(chunkPos);
			chunksProcessed++;
		}

		_isProcessingChunks = false;
	}

	/// <summary>
	/// Generates a chunk at the specified position.
	/// </summary>
	/// <param name="chunkPos">The chunk position to generate.</param>
	private async Task GenerateChunk(Vector2I chunkPos)
	{
		if (ChunkPrefab is null)
			throw new("ChunkPrefab is not set!");

		// Create the chunk instance
		var chunk = ChunkPrefab.Instantiate<Node3D>();
		AddChild(chunk);

		// Set the chunk position
		var worldPos = ChunkToWorldPosition(chunkPos);
		chunk.GlobalPosition = new(worldPos.X, 0, worldPos.Y);

		// Generate the chunk data
		var chunkData = new ChunkData
		{
			Position       = chunkPos,
			Size           = ChunkSize,
			HeightMap      = new float[ChunkSize, ChunkSize],
			BiomeMap       = new int[ChunkSize, ChunkSize],
			MoistureMap    = new float[ChunkSize, ChunkSize],
			TemperatureMap = new float[ChunkSize, ChunkSize]
		};

		// Generate the chunk data on a separate thread
		await Task.Run(() => GenerateChunkData(chunkData));

		// Apply the chunk data to the chunk instance
		if (chunk is WorldChunk worldChunk)
			worldChunk.Initialize(chunkData, BiomeDefinitions);

		// Add the chunk to the loaded chunks dictionary
		_loadedChunks[chunkPos] = chunk;
	}

	/// <summary>
	/// Generates the data for a chunk.
	/// </summary>
	/// <param name="chunkData">The chunk data to populate.</param>
	private void GenerateChunkData(ChunkData chunkData)
	{
		// Generate height map
		for (var x = 0; x < chunkData.Size; x++)
		{
			for (var z = 0; z < chunkData.Size; z++)
			{
				// Calculate world coordinates
				float worldX = chunkData.Position.X * chunkData.Size + x;
				float worldZ = chunkData.Position.Y * chunkData.Size + z;

				// Generate height using multiple octaves of noise
				float height       = 0;
				float amplitude    = 1;
				float frequency    = 1;
				float maxAmplitude = 0;

				for (var i = 0; i < 4; i++)
				{
					height       += _noise.GetNoise2D(worldX * frequency * NoiseScale, worldZ * frequency * NoiseScale) * amplitude;
					maxAmplitude += amplitude;
					amplitude    *= 0.5f;
					frequency    *= 2;
				}

				// Normalize height
				height /= maxAmplitude;

				// Scale height to desired range (e.g., 0 to 10)
				height = (height + 1) * 5; // Range 0 to 10

				chunkData.HeightMap[x, z] = height;

				// Generate moisture map (for biome determination)
				var moisture = _noise.GetNoise2D(worldX * NoiseScale * 0.5f, worldZ * NoiseScale * 0.5f);
				moisture                    = (moisture + 1) * 0.5f; // Normalize to 0-1
				chunkData.MoistureMap[x, z] = moisture;

				// Generate temperature map (for biome determination)
				var temperature = _noise.GetNoise2D(worldX * NoiseScale * 0.25f, worldZ * NoiseScale * 0.25f);
				temperature                    = (temperature + 1) * 0.5f; // Normalize to 0-1
				chunkData.TemperatureMap[x, z] = temperature;

				// Determine biome based on height, moisture, and temperature
				chunkData.BiomeMap[x, z] = DetermineBiome(height, moisture, temperature);
			}
		}
	}

	/// <summary>
	/// Determines the biome index based on height, moisture, and temperature.
	/// </summary>
	/// <param name="height">The terrain height.</param>
	/// <param name="moisture">The moisture value.</param>
	/// <param name="temperature">The temperature value.</param>
	/// <returns>The biome index.</returns>
	private int DetermineBiome(float height, float moisture, float temperature) => height switch
	{
		// Simple biome determination based on height, moisture, and temperature
		< 1.5f => 0 // Water/Ocean
	   ,
		< 2.5f when moisture < 0.3f => 1 // Beach/Desert
	   ,
		< 2.5f => 2 // Coastal
	   ,
		< 6f when temperature < 0.3f => moisture < 0.4f
				? 3 // Tundra
				: 4 // Taiga
	   ,
		< 6f when temperature < 0.7f => moisture switch
		{
			< 0.4f => 5, // Plains
			< 0.7f => 6, // Forest
			_      => 7  // Swamp
		},
		< 6f when moisture < 0.3f => 8 // Desert
	   ,
		< 6f when moisture < 0.6f => 9 // Savanna
	   ,
		< 6f => 10 // Jungle
	   ,
		_ => temperature switch
		{
			< 0.3f => 11, // Snow Mountains
			< 0.7f => 12, // Mountains
			_      => 13  // Volcanic Mountains
		}
	};

	/// <summary>
	/// Converts a world position to a chunk position.
	/// </summary>
	/// <param name="worldPos">The world position.</param>
	/// <returns>The chunk position.</returns>
	public Vector2I WorldToChunkPosition(Vector3 worldPos)
	{
		var x = Mathf.FloorToInt(worldPos.X / ChunkSize);
		var z = Mathf.FloorToInt(worldPos.Z / ChunkSize);
		return new(x, z);
	}

	/// <summary>
	/// Converts a chunk position to a world position.
	/// </summary>
	/// <param name="chunkPos">The chunk position.</param>
	/// <returns>The world position (X and Z coordinates).</returns>
	public Vector2 ChunkToWorldPosition(Vector2I chunkPos)
	{
		float x = chunkPos.X * ChunkSize;
		float z = chunkPos.Y * ChunkSize;
		return new(x, z);
	}

	/// <summary>
	/// Gets the height at the specified world position.
	/// </summary>
	/// <param name="worldX">The world X coordinate.</param>
	/// <param name="worldZ">The world Z coordinate.</param>
	/// <returns>The height at the position, or 0 if the chunk is not loaded.</returns>
	public float GetHeightAt(float worldX, float worldZ)
	{
		// Convert world position to chunk position
		var chunkX   = Mathf.FloorToInt(worldX / ChunkSize);
		var chunkZ   = Mathf.FloorToInt(worldZ / ChunkSize);
		var chunkPos = new Vector2I(chunkX, chunkZ);

		// Check if the chunk is loaded
		if (!_loadedChunks.TryGetValue(chunkPos, out var chunk))
			return 0;

		// Calculate local coordinates within the chunk
		var localX = Mathf.FloorToInt(worldX) % ChunkSize;
		if (localX < 0)
			localX += ChunkSize;

		var localZ = Mathf.FloorToInt(worldZ) % ChunkSize;
		if (localZ < 0)
			localZ += ChunkSize;

		// Get the height from the chunk
		if (chunk is WorldChunk worldChunk)
			return worldChunk.GetHeightAt(localX, localZ);

		return 0;
	}

	/// <summary>
	/// Gets the biome at the specified world position.
	/// </summary>
	/// <param name="worldX">The world X coordinate.</param>
	/// <param name="worldZ">The world Z coordinate.</param>
	/// <returns>The biome index at the position, or -1 if the chunk is not loaded.</returns>
	public int GetBiomeAt(float worldX, float worldZ)
	{
		// Convert world position to chunk position
		var chunkX   = Mathf.FloorToInt(worldX / ChunkSize);
		var chunkZ   = Mathf.FloorToInt(worldZ / ChunkSize);
		var chunkPos = new Vector2I(chunkX, chunkZ);

		// Check if the chunk is loaded
		if (!_loadedChunks.TryGetValue(chunkPos, out var chunk))
			return -1;

		// Calculate local coordinates within the chunk
		var localX = Mathf.FloorToInt(worldX) % ChunkSize;
		if (localX < 0)
			localX += ChunkSize;

		var localZ = Mathf.FloorToInt(worldZ) % ChunkSize;
		if (localZ < 0)
			localZ += ChunkSize;

		// Get the biome from the chunk
		if (chunk is WorldChunk worldChunk)
			return worldChunk.GetBiomeAt(localX, localZ);

		return -1;
	}
}