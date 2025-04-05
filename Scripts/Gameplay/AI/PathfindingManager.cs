using System.Collections.Generic;
using Ceiro.Scripts.Core.World.Generation;
using Godot;

namespace Ceiro.Scripts.Gameplay.AI;

/// <summary>
/// Manages AI pathfinding for entities in the isometric world.
/// </summary>
public partial class PathfindingManager : Node3D
{
	[Export] public float TileSize      = 1.0f;
	[Export] public int   MaxPathLength = 100;
	[Export] public bool  VisualizePathfinding;

	private AStar2D                   _pathfinder = new();
	private int                       _nextPointId;
	private ProceduralWorldGenerator? _worldGenerator;

	private readonly Dictionary<Vector2I, int> _pointIds = new();

	public override void _Ready()
	{
		// Find the world generator
		_worldGenerator = GetTree().Root.FindChild("WorldGenerator", true, false) as ProceduralWorldGenerator;

		// Initial setup of pathfinding grid
		if (_worldGenerator != null)
				// This will be called when chunks are loaded/unloaded
				// For now, we'll set up a basic grid around the origin
			SetupInitialGrid();
	}

	/// <summary>
	/// Sets up an initial pathfinding grid around the origin.
	/// </summary>
	private void SetupInitialGrid()
	{
		// Create a grid of points around the origin
		const int gridSize = 50; // Adjust based on your needs

		for (var x = -gridSize / 2; x < gridSize / 2; x++)
		{
			for (var z = -gridSize / 2; z < gridSize / 2; z++)
				AddPathfindingPoint(new(x, z));
		}

		// Connect adjacent points
		ConnectPathfindingPoints();
	}

	/// <summary>
	/// Adds a pathfinding point at the specified grid position.
	/// </summary>
	/// <param name="gridPos">The grid position.</param>
	public void AddPathfindingPoint(Vector2I gridPos)
	{
		// Skip if point already exists
		if (_pointIds.ContainsKey(gridPos))
			return;

		// Get world position and check if walkable
		var worldPos = new Vector3(gridPos.X * TileSize, 0, gridPos.Y * TileSize);

		// Check if the position is walkable
		var isWalkable = IsPositionWalkable(worldPos);

		// Add point to pathfinder
		var pointId = _nextPointId++;
		_pointIds[gridPos] = pointId;

		// Convert to Vector2 for AStar2D
		var point2D = new Vector2(gridPos.X, gridPos.Y);

		// Add point with weight based on walkability
		_pathfinder.AddPoint(pointId, point2D, isWalkable ? 1.0f : 1000.0f);
	}

	/// <summary>
	/// Connects pathfinding points to their neighbors.
	/// </summary>
	private void ConnectPathfindingPoints()
	{
		// Connect adjacent points
		foreach (var (gridPos, pointId) in _pointIds)
		{
			// Check 8 neighbors
			var neighbors = new Vector2I[]
			{
				new(gridPos.X            + 1, gridPos.Y),
				new(gridPos.X            - 1, gridPos.Y),
				new(gridPos.X, gridPos.Y + 1),
				new(gridPos.X, gridPos.Y - 1),
				new(gridPos.X            + 1, gridPos.Y + 1),
				new(gridPos.X            - 1, gridPos.Y - 1),
				new(gridPos.X            + 1, gridPos.Y - 1),
				new(gridPos.X            - 1, gridPos.Y + 1)
			};

			foreach (var neighbor in neighbors)
				if (_pointIds.TryGetValue(neighbor, out var neighborId))
						// Check if already connected
					if (!_pathfinder.ArePointsConnected(pointId, neighborId))
					{
						// Diagonal connections have higher cost
						var isDiagonal = gridPos.X != neighbor.X && gridPos.Y != neighbor.Y;
						var weight     = isDiagonal ? 1.4f : 1.0f;

						_pathfinder.ConnectPoints(pointId, neighborId);
						// Set the weight for this connection
						_pathfinder.SetPointWeightScale(neighborId, weight);
					}
		}
	}

	/// <summary>
	/// Updates the pathfinding grid when a chunk is loaded.
	/// </summary>
	/// <param name="chunkPos">The chunk position.</param>
	/// <param name="chunkSize">The chunk size.</param>
	public void UpdateGridForChunk(Vector2I chunkPos, int chunkSize)
	{
		// Add points for the chunk
		for (var x = 0; x < chunkSize; x++)
		{
			for (var z = 0; z < chunkSize; z++)
			{
				var gridPos = new Vector2I(chunkPos.X * chunkSize + x,
				                           chunkPos.Y * chunkSize + z);

				AddPathfindingPoint(gridPos);
			}
		}

		// Connect points
		ConnectPathfindingPoints();
	}

	/// <summary>
	/// Updates a specific point in the pathfinding grid.
	/// </summary>
	/// <param name="gridPos">The grid position to update.</param>
	public void UpdatePoint(Vector2I gridPos)
	{
		// Check if point exists
		if (!_pointIds.TryGetValue(gridPos, out var pointId))
			return;

		// Get world position
		var worldPos = new Vector3(gridPos.X * TileSize, 0, gridPos.Y * TileSize);

		// Check if the position is walkable
		var isWalkable = IsPositionWalkable(worldPos);

		// Update point weight
		_pathfinder.SetPointWeightScale(pointId, isWalkable ? 1.0f : 1000.0f);
	}

	/// <summary>
	/// Checks if a position is walkable.
	/// </summary>
	/// <param name="worldPos">The world position to check.</param>
	/// <returns>True if the position is walkable, false otherwise.</returns>
	private bool IsPositionWalkable(Vector3 worldPos)
	{
		// Check if the position is in water
		var biomeIndex = _worldGenerator?.GetBiomeAt(worldPos.X, worldPos.Z);

		// Assuming biome index 0 is water
		if (biomeIndex is 0)
			return false;

		// Check for obstacles using physics
		var spaceState = GetWorld3D().DirectSpaceState;

		// Create a shape for the query
		var shape = new SphereShape3D();
		shape.Radius = 0.4f; // Slightly smaller than a tile

		// Set up the query parameters
		var parameters = new PhysicsShapeQueryParameters3D();
		parameters.ShapeRid          = shape.GetRid();
		parameters.Transform         = new(Basis.Identity, new(worldPos.X, worldPos.Y + 0.5f, worldPos.Z));
		parameters.CollideWithBodies = true;
		parameters.CollisionMask     = 1; // Adjust based on your collision layers

		// Perform the query
		var results = spaceState.IntersectShape(parameters);

		// If there are any intersections with obstacles, the position is not walkable
		foreach (var result in results)
			if (result.ContainsKey("collider"))
			{
				var collider = result["collider"].As<Node>();

				// Check if the collider is an obstacle
				if (collider.IsInGroup("Obstacle"))
					return false;
			}

		return true;
	}

	/// <summary>
	/// Finds a path between two world positions.
	/// </summary>
	/// <param name="startPos">The starting world position.</param>
	/// <param name="endPos">The ending world position.</param>
	/// <returns>A list of world positions forming the path, or an empty list if no path is found.</returns>
	public List<Vector3> FindPath(Vector3 startPos, Vector3 endPos)
	{
		// Convert world positions to grid positions
		var startGrid = WorldToGrid(startPos);
		var endGrid   = WorldToGrid(endPos);

		// Ensure points exist
		if (!_pointIds.TryGetValue(startGrid, out var startId))
		{
			AddPathfindingPoint(startGrid);
			ConnectPathfindingPoints();
			startId = _pointIds[startGrid];
		}

		if (!_pointIds.TryGetValue(endGrid, out var endId))
		{
			AddPathfindingPoint(endGrid);
			ConnectPathfindingPoints();
			endId = _pointIds[endGrid];
		}

		// Find path
		var path2D = _pathfinder.GetPointPath(startId, endId);

		// Convert to 3D world positions
		var path = new List<Vector3>();

		foreach (var point in path2D)
		{
			// Get height at position if world generator is available
			float height = 0;
			if (_worldGenerator != null)
				height = _worldGenerator.GetHeightAt(point.X * TileSize, point.Y * TileSize);

			path.Add(new(point.X * TileSize, height, point.Y * TileSize));
		}

		// Visualize path if enabled
		if (VisualizePathfinding)
			VisualizePath(path);

		return path;
	}

	/// <summary>
	/// Converts a world position to a grid position.
	/// </summary>
	/// <param name="worldPos">The world position.</param>
	/// <returns>The grid position.</returns>
	private Vector2I WorldToGrid(Vector3 worldPos) => new(Mathf.FloorToInt(worldPos.X / TileSize),
	                                                      Mathf.FloorToInt(worldPos.Z / TileSize));

	/// <summary>
	/// Visualizes a path for debugging.
	/// </summary>
	/// <param name="path">The path to visualize.</param>
	private void VisualizePath(List<Vector3> path)
	{
		// Clear previous visualization
		foreach (var child in GetChildren())
			if (child.Name.ToString().StartsWith("PathVis"))
				child.QueueFree();

		// Create visualization for each point
		for (var i = 0; i < path.Count; i++)
		{
			var point = path[i];

			// Create a small sphere at each point
			var sphere = new CsgSphere3D();
			sphere.Name     = $"PathVis{i}";
			sphere.Radius   = 0.1f;
			sphere.Position = point;
			sphere.MaterialOverride = new StandardMaterial3D
			{
				AlbedoColor = new(1, 0, 0)
			};

			AddChild(sphere);
		}
	}
}