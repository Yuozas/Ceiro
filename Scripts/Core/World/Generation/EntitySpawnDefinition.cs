using Godot;

namespace Ceiro.Scripts.Core.World.Generation;

/// <summary>
/// Definition for entity spawning, used to configure how entities are spawned in the world.
/// </summary>
public partial class EntitySpawnDefinition : Resource
{
	[Export] public string      EntityName  = "";
	[Export] public Texture2D[] Textures    = [];
	[Export] public float       SpawnChance = 0.1f;
	[Export] public float       MinScale    = 0.8f;
	[Export] public float       MaxScale    = 1.2f;
	[Export] public bool        AllowOverlap;
	[Export] public float       MinHeight;
	[Export] public float       MaxHeight = 10.0f;
}