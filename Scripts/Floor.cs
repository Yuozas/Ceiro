// using Godot;
//
// public partial class Floor : Node3D
// {
// 	[Export] public PackedScene FloorTileScene; // Assign FloorTile.tscn
// 	[Export] public TileSet     TileSet;        // Your 2D TileSet resource
//
// 	// Define biome tile coordinate ranges in the TileSet
// 	[Export] public Vector2I ForestTilesStart = new(0, 0);
// 	[Export] public Vector2I ForestTilesEnd   = new(3, 0);
//
// 	[Export] public Vector2I GrasslandTilesStart = new(4, 0);
// 	[Export] public Vector2I GrasslandTilesEnd   = new(7, 0);
//
// 	[Export] public Vector2I MarshTilesStart = new(0, 1);
// 	[Export] public Vector2I MarshTilesEnd   = new(3, 1);
//
// 	[Export] public Vector2I RockyTilesStart = new(4, 1);
// 	[Export] public Vector2I RockyTilesEnd   = new(7, 1);
//
// 	private FastNoiseLite         _noise;
// 	private RandomNumberGenerator _rng = new();
//
// 	// Biome thresholds
// 	private const float FOREST_THRESHOLD    = 0.3f;
// 	private const float GRASSLAND_THRESHOLD = 0.0f;
// 	private const float MARSH_THRESHOLD     = -0.3f;
//
// 	public override void _Ready()
// 	{
// 		_rng.Randomize();
//
// 		// Initialize noise for biome generation
// 		_noise           = new();
// 		_noise.Seed      = (int)_rng.Randi();
// 		_noise.Frequency = 0.02f;
//
// 		GenerateFloor();
// 	}
//
// 	private void GenerateFloor()
// 	{
// 		if (TileSet is null)
// 			throw new("TileSet is not assigned in Floor script!");
//
// 		const int   size     = 20;
// 		const float tileSize = 1.0f;
//
// 		for (var x = -size; x <= size; x++)
// 		{
// 			for (var z = -size; z <= size; z++)
// 			{
// 				// Create a new tile instance
// 				var tile = FloorTileScene.Instantiate<FloorTile>();
// 				tile.Position = new(x * tileSize, 0, z * tileSize);
//
// 				// Generate noise value for this position to determine biome
// 				var noiseValue = _noise.GetNoise2D(x, z);
//
// 				// Assign a tile texture based on the noise value (biome)
// 				AssignTileFromTileSet(tile, noiseValue);
//
// 				AddChild(tile);
//
// 				// Optionally place decorations
// 				// PlaceDecorations(x, z, noiseValue);
// 			}
// 		}
// 	}
//
// 	private void AssignTileFromTileSet(FloorTile tile, float noiseValue)
// 	{
// 		const int sourceId = 0; // Assuming the first source in your TileSet
// 		var       coords   = Vector2I.Zero;
//
// 		// Determine which biome we're in based on the noise value
// 		switch (noiseValue)
// 		{
// 			case > FOREST_THRESHOLD:
// 				// Forest biome - select a random tile from the forest range
// 				coords.X = _rng.RandiRange(ForestTilesStart.X, ForestTilesEnd.X);
// 				coords.Y = _rng.RandiRange(ForestTilesStart.Y, ForestTilesEnd.Y);
// 				break;
// 			case > GRASSLAND_THRESHOLD:
// 				// Grassland biome
// 				coords.X = _rng.RandiRange(GrasslandTilesStart.X, GrasslandTilesEnd.X);
// 				coords.Y = _rng.RandiRange(GrasslandTilesStart.Y, GrasslandTilesEnd.Y);
// 				break;
// 			case > MARSH_THRESHOLD:
// 				// Marsh/Swamp biome
// 				coords.X = _rng.RandiRange(MarshTilesStart.X, MarshTilesEnd.X);
// 				coords.Y = _rng.RandiRange(MarshTilesStart.Y, MarshTilesEnd.Y);
// 				break;
// 			default:
// 				// Rocky/Desert biome
// 				coords.X = _rng.RandiRange(RockyTilesStart.X, RockyTilesEnd.X);
// 				coords.Y = _rng.RandiRange(RockyTilesStart.Y, RockyTilesEnd.Y);
// 				break;
// 		}
//
// 		// Apply the selected tile to the floor tile
// 		tile.SetTileCoordinates(TileSet, sourceId, coords);
// 	}
// }
