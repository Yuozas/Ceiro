using System;
using Godot;

namespace Ceiro.Scripts.Core.World.Generation;

/// <summary>
/// Represents a single chunk in the world.
/// </summary>
public partial class WorldChunk : Node3D
{
	[Export] public PackedScene TilePrefab;
	[Export] public PackedScene TreePrefab;
	[Export] public PackedScene RockPrefab;
	[Export] public PackedScene PlantPrefab;

	private ChunkData  _chunkData;
	private Resource[] _biomeDefinitions = [];
	private Node3D     _tilesRoot;
	private Node3D     _decorationsRoot;

	public override void _Ready()
	{
		// Create root nodes for organization
		_tilesRoot = new()
		{
			Name = "Tiles"
		};
		AddChild(_tilesRoot);

		_decorationsRoot = new()
		{
			Name = "Decorations"
		};
		AddChild(_decorationsRoot);
	}

	/// <summary>
	/// Initializes the chunk with the provided data and biome definitions.
	/// </summary>
	/// <param name="chunkData">The chunk data.</param>
	/// <param name="biomeDefinitions">The biome definitions.</param>
	public void Initialize(ChunkData chunkData, Resource[] biomeDefinitions)
	{
		_chunkData        = chunkData;
		_biomeDefinitions = biomeDefinitions;

		// Generate the terrain tiles
		GenerateTerrain();

		// Generate decorations (trees, rocks, plants)
		GenerateDecorations();

		if (TilePrefab is null)
			throw new("TilePrefab is not set!");
		if (TreePrefab is null)
			throw new("TreePrefab is not set!");
		if (RockPrefab is null)
			throw new("RockPrefab is not set!");
		if (PlantPrefab is null)
			throw new("PlantPrefab is not set!");
		if (biomeDefinitions is null)
			throw new("Biome definitions are not set!");
		if (biomeDefinitions.Length == 0)
			throw new("Biome definitions array is empty!");
	}

	/// <summary>
	/// Generates the terrain tiles for the chunk.
	/// </summary>
	private void GenerateTerrain()
	{
		for (var x = 0; x < _chunkData.Size; x++)
			for (var z = 0; z < _chunkData.Size; z++)
			{
				// Get the height and biome for this tile
				var height     = _chunkData.HeightMap[x, z];
				var biomeIndex = _chunkData.BiomeMap[x, z];

				// Create the tile
				var tile = TilePrefab.Instantiate<Node3D>();
				_tilesRoot.AddChild(tile);

				// Position the tile
				tile.Position = new(x, height, z);

				// Apply biome-specific appearance
				ApplyBiomeToTile(tile, biomeIndex, x, z);
			}
	}

	/// <summary>
	/// Applies biome-specific appearance to a tile.
	/// </summary>
	/// <param name="tile">The tile node.</param>
	/// <param name="biomeIndex">The biome index.</param>
	/// <param name="x">The local X coordinate.</param>
	/// <param name="z">The local Z coordinate.</param>
	private void ApplyBiomeToTile
	(
			Node3D tile,
			int    biomeIndex,
			int    x,
			int    z
	)
	{
		// Check if the biome index is valid
		if (biomeIndex < 0 || biomeIndex >= _biomeDefinitions.Length)
			throw new("Invalid biome index!");

		// Get the biome definition
		if (_biomeDefinitions[biomeIndex] is not BiomeDefinition biomeDefinition)
			throw new("Biome definition is not a BiomeDefinition!");

		// Get the sprite for the tile
		var sprite = tile.GetNodeOrNull<Sprite3D>("Sprite3D") ?? throw new("Tile does not have a Sprite3D child!");

		// Determine if this is an edge tile
		var isEdge = IsEdgeTile(x, z, biomeIndex);

		// Apply the appropriate texture
		if (isEdge && biomeDefinition.EdgeTiles.Length > 0)
		{
			// Select a random edge tile texture
			var textureIndex = new Random().Next(biomeDefinition.EdgeTiles.Length);
			sprite.Texture = biomeDefinition.EdgeTiles[textureIndex];
		}
		else if (biomeDefinition.CenterTiles.Length > 0)
		{
			// Select a random center tile texture
			var textureIndex = new Random().Next(biomeDefinition.CenterTiles.Length);
			sprite.Texture = biomeDefinition.CenterTiles[textureIndex];
		}
	}

	/// <summary>
	/// Determines if a tile is at the edge of a biome.
	/// </summary>
	/// <param name="x">The local X coordinate.</param>
	/// <param name="z">The local Z coordinate.</param>
	/// <param name="biomeIndex">The biome index.</param>
	/// <returns>True if the tile is at the edge of a biome, false otherwise.</returns>
	private bool IsEdgeTile(int x, int z, int biomeIndex)
	{
		// Check adjacent tiles (4-way)
		int[] dx = [-1, 1, 0, 0];
		int[] dz = [0, 0, -1, 1];

		for (var i = 0; i < 4; i++)
		{
			var nx = x + dx[i];
			var nz = z + dz[i];

			// Skip if out of bounds
			if (nx < 0 || nx >= _chunkData.Size || nz < 0 || nz >= _chunkData.Size)
				continue;

			// If adjacent tile has a different biome, this is an edge tile
			if (_chunkData.BiomeMap[nx, nz] != biomeIndex)
				return true;
		}

		return false;
	}

	/// <summary>
	/// Generates decorations (trees, rocks, plants) for the chunk.
	/// </summary>
	private void GenerateDecorations()
	{
		// Create a random number generator with a seed based on the chunk position
		var random = new Random(_chunkData.Position.X * 10000 + _chunkData.Position.Y);

		for (var x = 0; x < _chunkData.Size; x++)
		{
			for (var z = 0; z < _chunkData.Size; z++)
			{
				// Get the biome for this position
				var biomeIndex = _chunkData.BiomeMap[x, z];

				// Skip if the biome index is invalid
				if (biomeIndex < 0 || biomeIndex >= _biomeDefinitions.Length)
					continue;

				// Get the biome definition
				if (_biomeDefinitions[biomeIndex] is not BiomeDefinition biomeDefinition)
					continue;

				// Skip water tiles
				if (biomeIndex == 0) // Assuming 0 is water
					continue;

				// Get the height for this position
				var height = _chunkData.HeightMap[x, z];

				// Try to spawn trees
				if (biomeDefinition.Trees.Length > 0)
					foreach (var treeSpawnDef in biomeDefinition.Trees)
						if (random.NextDouble() < treeSpawnDef.SpawnChance)
						{
							SpawnDecoration(TreePrefab,
							                x,
							                height,
							                z,
							                treeSpawnDef);
							break; // Only spawn one tree per tile
						}

				// Try to spawn rocks
				if (biomeDefinition.Minerals.Length > 0)
					foreach (var rockSpawnDef in biomeDefinition.Minerals)
						if (random.NextDouble() < rockSpawnDef.SpawnChance)
						{
							SpawnDecoration(RockPrefab,
							                x,
							                height,
							                z,
							                rockSpawnDef);
							break; // Only spawn one rock per tile
						}

				// Try to spawn plants
				if (biomeDefinition.Plants.Length > 0)
					foreach (var plantSpawnDef in biomeDefinition.Plants)
						if (random.NextDouble() < plantSpawnDef.SpawnChance)
						{
							SpawnDecoration(PlantPrefab,
							                x,
							                height,
							                z,
							                plantSpawnDef);
							break; // Only spawn one plant per tile
						}
			}
		}
	}

	/// <summary>
	/// Spawns a decoration at the specified position.
	/// </summary>
	/// <param name="prefab">The decoration prefab.</param>
	/// <param name="x">The local X coordinate.</param>
	/// <param name="height">The height at the position.</param>
	/// <param name="z">The local Z coordinate.</param>
	/// <param name="spawnDef">The spawn definition.</param>
	private void SpawnDecoration
	(
			PackedScene           prefab,
			int                   x,
			float                 height,
			int                   z,
			EntitySpawnDefinition spawnDef
	)
	{
		// Create the decoration instance
		var decoration = prefab.Instantiate<Node3D>();
		_decorationsRoot.AddChild(decoration);

		// Position the decoration
		decoration.Position = new(x, height, z);

		// Apply random rotation
		var random = new Random();
		decoration.RotationDegrees = new(0, (float)random.NextDouble() * 360, 0);

		// Apply random scale within the defined range
		var scale = spawnDef.MinScale + (float)random.NextDouble() * (spawnDef.MaxScale - spawnDef.MinScale);
		decoration.Scale = new(scale, scale, scale);

		// Set the sprite texture if available
		var sprite = decoration.GetNodeOrNull<Sprite3D>("Sprite3D") ?? throw new("Decoration does not have a Sprite3D child!");

		if (spawnDef.Textures.Length <= 0)
			return;

		var textureIndex = random.Next(spawnDef.Textures.Length);
		sprite.Texture = spawnDef.Textures[textureIndex];
	}

	/// <summary>
	/// Gets the height at the specified local position.
	/// </summary>
	/// <param name="localX">The local X coordinate.</param>
	/// <param name="localZ">The local Z coordinate.</param>
	/// <returns>The height at the position.</returns>
	public float GetHeightAt(int localX, int localZ)
	{
		if (localX < 0 || localX >= _chunkData.Size || localZ < 0 || localZ >= _chunkData.Size)
			return 0;

		return _chunkData.HeightMap[localX, localZ];
	}

	/// <summary>
	/// Gets the biome at the specified local position.
	/// </summary>
	/// <param name="localX">The local X coordinate.</param>
	/// <param name="localZ">The local Z coordinate.</param>
	/// <returns>The biome index at the position.</returns>
	public int GetBiomeAt(int localX, int localZ)
	{
		if (localX < 0 || localX >= _chunkData.Size || localZ < 0 || localZ >= _chunkData.Size)
			return -1;

		return _chunkData.BiomeMap[localX, localZ];
	}
}