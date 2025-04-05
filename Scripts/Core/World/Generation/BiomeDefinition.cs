using Godot;

namespace Ceiro.Scripts.Core.World.Generation;

/// <summary>
/// Definition for a biome, used to configure the appearance and properties of different areas in the world.
/// </summary>
public partial class BiomeDefinition : Resource
{
	[Export] public Texture2D[] CenterTiles = [];
	[Export] public Texture2D[] EdgeTiles   = [];
	[Export] public float       Temperature;
	[Export] public float       Humidity;

	[ExportCategory("Vegetation")][Export] public EntitySpawnDefinition[] Trees  = [];
	[Export]                               public EntitySpawnDefinition[] Plants = [];

	[ExportCategory("Resources")][Export] public EntitySpawnDefinition[] Minerals         = [];
	[Export]                              public EntitySpawnDefinition[] SpecialResources = [];

	// Simple interface for defining noise parameters
	[ExportCategory("Generation")][Export] public float NoiseScale  = 0.1f;
	[Export]                               public int   Octaves     = 4;
	[Export]                               public float Persistence = 0.5f;
}