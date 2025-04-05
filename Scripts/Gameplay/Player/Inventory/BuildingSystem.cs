using System.Collections.Generic;
using Godot;

namespace Ceiro.Scripts.Gameplay.Player.Inventory;

/// <summary>
/// System for placing buildings and structures in the world.
/// </summary>
public partial class BuildingSystem : Node3D
{
	[Signal]
	public delegate void BuildingPlacedEventHandler(string buildingId, Vector3 position);

	[Export] public NodePath? PlayerPath;
	[Export] public NodePath? InventorySystemPath;
	[Export] public float     PlacementDistance     = 5.0f;
	[Export] public Color     ValidPlacementColor   = new(0, 1, 0, 0.5f);
	[Export] public Color     InvalidPlacementColor = new(1, 0, 0, 0.5f);

	private Node3D          _player;
	private InventorySystem _inventorySystem;
	private Node3D?         _placementPreview;
	private bool            _isPlacing;
	private string?         _currentBuildingId;

	private readonly Dictionary<string, PackedScene> _buildingPrefabs = new();

	public override void _Ready()
	{
		// Get the player reference
		if (!string.IsNullOrEmpty(PlayerPath))
		{
			_player = GetNode<Node3D>(PlayerPath);
		}
		else
		{
			// Try to find the player in the scene
			var players = GetTree().GetNodesInGroup("Player");
			if (players.Count > 0)
				_player = players[0] as Node3D ?? throw new("Player not found or unable to cast to type Node3D.");
		}

		// Get the inventory system
		if (!string.IsNullOrEmpty(InventorySystemPath))
			_inventorySystem = GetNode<InventorySystem>(InventorySystemPath);
		else
				// Try to find the inventory system in the scene
			_inventorySystem = GetTree().Root.FindChild("InventorySystem", true, false) as InventorySystem ?? throw new("InventorySystem not found or unable to cast to type InventorySystem.");

		// Load building prefabs
		LoadBuildingPrefabs();
	}

	public override void _Process(double delta)
	{
		if (!_isPlacing)
			return;

		UpdatePlacementPreview();

		// Check for placement input
		if (Input.IsActionJustPressed("place_building"))
			PlaceBuilding();

		// Check for cancel input
		if (Input.IsActionJustPressed("cancel_building"))
			CancelPlacement();
	}

	/// <summary>
	/// Loads building prefabs.
	/// </summary>
	private void LoadBuildingPrefabs()
	{
		// Load building prefabs from resources
		_buildingPrefabs["wooden_wall"]    = GD.Load<PackedScene>("res://prefabs/buildings/wooden_wall.tscn");
		_buildingPrefabs["wooden_floor"]   = GD.Load<PackedScene>("res://prefabs/buildings/wooden_floor.tscn");
		_buildingPrefabs["wooden_door"]    = GD.Load<PackedScene>("res://prefabs/buildings/wooden_door.tscn");
		_buildingPrefabs["crafting_table"] = GD.Load<PackedScene>("res://prefabs/buildings/crafting_table.tscn");
		_buildingPrefabs["furnace"]        = GD.Load<PackedScene>("res://prefabs/buildings/furnace.tscn");
	}

	/// <summary>
	/// Starts placing a building.
	/// </summary>
	/// <param name="buildingId">The ID of the building to place.</param>
	public void StartPlacement(string buildingId)
	{
		if (!_buildingPrefabs.TryGetValue(buildingId, out var value))
			throw new($"BuildingSystem: Building prefab '{buildingId}' not found!");

		// Cancel any existing placement
		CancelPlacement();

		// Create the placement preview
		_placementPreview = value.Instantiate<Node3D>();
		AddChild(_placementPreview);

		// Set up the preview material
		foreach (var child in _placementPreview.GetChildren())
			if (child is MeshInstance3D meshInstance)
			{
				// Create a transparent material for the preview
				var material = new StandardMaterial3D
				{
					AlbedoColor  = ValidPlacementColor,
					Transparency = BaseMaterial3D.TransparencyEnum.Alpha
				};

				meshInstance.MaterialOverride = material;
			}

		_isPlacing         = true;
		_currentBuildingId = buildingId;
	}

	/// <summary>
	/// Updates the placement preview position and appearance.
	/// </summary>
	private void UpdatePlacementPreview()
	{
		if (_placementPreview is null)
			return;

		// Cast a ray from the camera to find placement position
		var camera    = GetViewport().GetCamera3D();
		var mousePos  = GetViewport().GetMousePosition();
		var rayOrigin = camera.ProjectRayOrigin(mousePos);
		var rayEnd    = rayOrigin + camera.ProjectRayNormal(mousePos) * 1000;

		var spaceState = GetWorld3D().DirectSpaceState;
		var rayParameters = new PhysicsRayQueryParameters3D
		{
			From              = rayOrigin,
			To                = rayEnd,
			CollideWithBodies = true
		};

		var result = spaceState.IntersectRay(rayParameters);

		if (result.Count <= 0 || !result.TryGetValue("position", out var value))
			return;

		// Get the hit position
		var hitPosition = (Vector3)value;

		// Check if the player is close enough
		var isInRange = _player.GlobalPosition.DistanceTo(hitPosition) <= PlacementDistance;

		// Check if the placement is valid
		var isValidPlacement = CheckValidPlacement(hitPosition);

		// Update preview position
		_placementPreview.GlobalPosition = hitPosition;

		// Snap to grid if needed
		_placementPreview.GlobalPosition = new(Mathf.Round(_placementPreview.GlobalPosition.X),
		                                       _placementPreview.GlobalPosition.Y,
		                                       Mathf.Round(_placementPreview.GlobalPosition.Z));

		// Update preview material color based on validity
		var isValid = isInRange && isValidPlacement;
		foreach (var child in _placementPreview.GetChildren())
			if (child is MeshInstance3D
			             {
				             MaterialOverride: StandardMaterial3D material
			             })
				material.AlbedoColor = isValid ? ValidPlacementColor : InvalidPlacementColor;
	}

	/// <summary>
	/// Checks if a placement is valid at the specified position.
	/// </summary>
	/// <param name="position">The position to check.</param>
	/// <returns>True if the placement is valid, false otherwise.</returns>
	private bool CheckValidPlacement(Vector3 position)
	{
		if (_placementPreview is null)
			return false;

		// Check for collisions with existing buildings
		var shape = new BoxShape3D
		{
			Size = new(1, 1, 1) // Adjust based on building size
		};

		var spaceState = GetWorld3D().DirectSpaceState;
		var parameters = new PhysicsShapeQueryParameters3D
		{
			ShapeRid  = shape.GetRid(),
			Transform = new(Basis.Identity, position)
		};

		var results = spaceState.IntersectShape(parameters);

		// If there are any intersections, placement is invalid
		foreach (var result in results)
			if (result.ContainsKey("collider"))
			{
				var collider = result["collider"].As<Node>();

				// Skip the placement preview itself
				if (collider != _placementPreview && !IsChildOf(collider, _placementPreview))
					return false;
			}

		return true;
	}

	/// <summary>
	/// Checks if a node is a child of another node.
	/// </summary>
	/// <param name="child">The potential child node.</param>
	/// <param name="parent">The potential parent node.</param>
	/// <returns>True if the child is a descendant of the parent, false otherwise.</returns>
	private static bool IsChildOf(Node child, Node parent)
	{
		var current = child.GetParent();

		while (current != null)
		{
			if (current == parent)
				return true;

			current = current.GetParent();
		}

		return false;
	}

	/// <summary>
	/// Places the building at the current preview position.
	/// </summary>
	private void PlaceBuilding()
	{
		if (_placementPreview is null)
			return;

		// Check if the placement is valid
		var isInRange        = _player.GlobalPosition.DistanceTo(_placementPreview.GlobalPosition) <= PlacementDistance;
		var isValidPlacement = CheckValidPlacement(_placementPreview.GlobalPosition);

		if (!isInRange || !isValidPlacement)
			return;

		// Check if the player has the required item
		var selectedItem = _inventorySystem.GetSelectedHotbarItem();

		if (selectedItem is null || selectedItem.ItemId != _currentBuildingId)
			return;

		// Consume the item
		var selectedIndex = _inventorySystem.GetSelectedHotbarIndex();
		var item          = _inventorySystem.GetItem(selectedIndex);

		if (item is
				{
					StackCount: > 1
				})
			item.StackCount--;
		else
			_inventorySystem.RemoveItem(selectedIndex);

		// Create the actual building
		var building = _buildingPrefabs[_currentBuildingId].Instantiate<Node3D>();
		GetParent().AddChild(building);
		building.GlobalPosition = _placementPreview.GlobalPosition;

		// Emit signal
		EmitSignal(SignalName.BuildingPlaced, _currentBuildingId, building.GlobalPosition);

		// Continue placing if there are more items
		if (_inventorySystem.GetSelectedHotbarItem() == null || _inventorySystem.GetSelectedHotbarItem()?.ItemId != _currentBuildingId)
			CancelPlacement();
	}

	/// <summary>
	/// Cancels the current placement.
	/// </summary>
	public void CancelPlacement()
	{
		if (_placementPreview is not null)
		{
			_placementPreview.QueueFree();
			_placementPreview = null;
		}

		_isPlacing         = false;
		_currentBuildingId = null;
	}
}