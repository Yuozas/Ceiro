using Godot;

namespace Ceiro.Scripts.Core.World.Generation;

/// <summary>
/// Data structure for chunk information.
/// </summary>
public class ChunkData
{
	public          Vector2I Position;
	public          int      Size;
	public required float[,] HeightMap;
	public required int[,]   BiomeMap;
	public required float[,] MoistureMap;
	public required float[,] TemperatureMap;
}