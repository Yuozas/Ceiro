using Godot;

namespace Ceiro.Scripts.Core.World.Rendering;

/// <summary>
/// Manages the water system in the isometric world, handling rendering,
/// collision, and special effects for water areas.
/// </summary>
public partial class WaterSystem : Node3D
{
	[Export] public PackedScene WaterTilePrefab;
	[Export] public float       WaveSpeed  = 1.0f;
	[Export] public float       WaveHeight = 0.1f;
	[Export] public float       WaterLevel;
	[Export] public Color       WaterColor = new(0.2f, 0.5f, 0.8f, 0.7f);

	private ShaderMaterial _waterMaterial;

	public override void _Ready()
	{
		if (WaterTilePrefab is null)
			throw new("WaterSystem: WaterTilePrefab is not set!");

		// Create the water shader material
		_waterMaterial = new();

		// Load the water shader
		var shader = GD.Load<Shader>("res://shaders/water_shader.gdshader") ?? throw new("Failed to load water shader!");

		_waterMaterial.Shader = shader;

		// Set shader parameters
		_waterMaterial.SetShaderParameter("wave_speed",  WaveSpeed);
		_waterMaterial.SetShaderParameter("wave_height", WaveHeight);
		_waterMaterial.SetShaderParameter("water_color", WaterColor);
	}

	public override void _Process(double delta)
	{
		// Update time parameter for wave animation
		if (_waterMaterial.Shader != null)
			_waterMaterial.SetShaderParameter("time", (float)Time.GetTicksMsec() / 1000.0f);
	}

	/// <summary>
	/// Creates a water tile at the specified position.
	/// </summary>
	/// <param name="position">The position to place the water tile.</param>
	/// <returns>The created water tile node.</returns>
	public Node3D CreateWaterTile(Vector3 position)
	{
		// Instance the water tile
		var waterTile = WaterTilePrefab.Instantiate<Node3D>();
		AddChild(waterTile);

		// Position the water tile
		position.Y               = WaterLevel; // Ensure water is at the correct height
		waterTile.GlobalPosition = position;

		// Apply the water material to the mesh
		var meshInstance = waterTile.GetNodeOrNull<MeshInstance3D>("MeshInstance3D");
		if (meshInstance != null)
			meshInstance.MaterialOverride = _waterMaterial;

		// Set up collision for the water tile
		_ = waterTile.GetNodeOrNull<CollisionShape3D>("CollisionShape3D") ?? throw new("Water tile does not have a CollisionShape3D!");

		// Adjust collision shape if needed.

		return waterTile;
	}

	/// <summary>
	/// Checks if a position is over water.
	/// </summary>
	/// <param name="position">The position to check.</param>
	/// <returns>True if the position is over water, false otherwise.</returns>
	public bool IsPositionOverWater(Vector3 position)
	{
		// Simple check based on Y position and water level
		// In a real implementation, you would use raycasting or area detection

		// Create a ray from slightly above the position downward
		var spaceState = GetWorld3D().DirectSpaceState;
		var rayOrigin  = new Vector3(position.X, position.Y + 1.0f, position.Z);
		var rayEnd     = new Vector3(position.X, position.Y - 1.0f, position.Z);

		// Create the ray parameters
		var rayParameters = new PhysicsRayQueryParameters3D();
		rayParameters.From              = rayOrigin;
		rayParameters.To                = rayEnd;
		rayParameters.CollideWithBodies = true;

		// Cast the ray
		var result = spaceState.IntersectRay(rayParameters);

		// Check if we hit a water collider
		if (result.Count <= 0 || !result.ContainsKey("collider"))
			return false;

		var collider = result["collider"].As<Node>();
		return collider.IsInGroup("Water");
	}
}