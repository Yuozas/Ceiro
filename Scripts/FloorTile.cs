// using System;
// using Godot;
//
// public partial class FloorTile : StaticBody3D
// {
// 	// Instead of directly exposing a texture, we'll use tile indices
// 	[Export] public int      TileSourceId { get; set; }
// 	[Export] public Vector2I TileCoords   { get; set; } = Vector2I.Zero;
//
// 	private MeshInstance3D _meshInstance;
//
// 	public override void _Ready() => _meshInstance = GetNodeOrNull<MeshInstance3D>("MeshInstance3D") ?? throw new("MeshInstance3D not found.");
//
// 	public void ApplyTileFromTileSet(TileSet tileSet)
// 	{
// 		// Get the texture from the tile atlas in the TileSet
// 		var tileSetAtlasSource = tileSet.GetSource(TileSourceId) as TileSetAtlasSource ?? throw new("TileSetAtlasSource not found.");
//
// 		// Get the texture for this specific tile
// 		var atlasTexture = tileSetAtlasSource.Texture;
// 		if (atlasTexture == null)
// 			return;
//
// 		// Create a material with the tile texture
// 		var material = new StandardMaterial3D();
//
// 		// Create an AtlasTexture to use just this specific tile from the atlas
// 		var tileAtlasTexture = new AtlasTexture();
// 		tileAtlasTexture.Atlas = atlasTexture;
//
// 		// Set the region to the specific tile
// 		var tileSize = tileSetAtlasSource.TextureRegionSize;
// 		tileAtlasTexture.Region = new(TileCoords.X * tileSize.X,
// 									  TileCoords.Y * tileSize.Y,
// 									  tileSize.X,
// 									  tileSize.Y);
//
// 		material.AlbedoTexture = tileAtlasTexture;
//
// 		// Optional: Configure material properties
// 		material.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
//
// 		// Apply the material to the mesh
// 		_meshInstance.MaterialOverride = material;
// 	}
//
// 	// Method to set the tile coordinates and update the texture
// 	public void SetTileCoordinates(TileSet tileSet, int sourceId, Vector2I coords)
// 	{
// 		TileSourceId = sourceId;
// 		TileCoords   = coords;
// 		ApplyTileFromTileSet(tileSet);
// 	}
// }

