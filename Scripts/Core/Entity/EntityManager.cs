using System;
using Godot;

namespace Ceiro.Scripts.Core.Entity;

/// <summary>
/// Manager class for handling entity activation and deactivation based on distance from player.
/// </summary>
public partial class EntityManager : Node
{
	[Export] public NodePath PlayerPath;
	[Export] public float    ActivationDistance   = 50.0f;
	[Export] public float    DeactivationDistance = 60.0f;
	[Export] public float    UpdateInterval       = 1.0f;

	private Node3D _player;
	private float  _updateTimer;

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
	}

	public override void _Process(double delta)
	{
		// Update at intervals to avoid performance issues
		_updateTimer += (float)delta;

		if (!(_updateTimer >= UpdateInterval))
			return;

		UpdateEntities();
		_updateTimer = 0.0f;
	}

	/// <summary>
	/// Updates the activation state of all entities based on distance from player.
	/// </summary>
	private void UpdateEntities()
	{
		// Find all entities in the scene
		var entities = GetTree().GetNodesInGroup("Entity");

		foreach (var entityNode in entities)
			if (entityNode is Entity entity and Node3D entityTransform)
			{
				var distance = entityTransform.GlobalPosition.DistanceTo(_player.GlobalPosition);

				// Activate entities within activation distance
				if (distance <= ActivationDistance && !entity.IsEntityActive())
					entity.Activate();
				// Deactivate entities beyond deactivation distance
				else if (distance > DeactivationDistance && entity.IsEntityActive())
					entity.Deactivate();
			}
	}

	/// <summary>
	/// Registers an entity with the manager.
	/// </summary>
	/// <param name="entity">The entity to register.</param>
	public void RegisterEntity(Entity entity)
	{
		// Add the entity to the "Entity" group
		entity.AddToGroup("Entity");

		// Set initial activation state
		if (entity is not Node3D entityTransform)
			return;

		var distance = entityTransform.GlobalPosition.DistanceTo(_player.GlobalPosition);

		if (distance <= ActivationDistance)
			entity.Activate();
		else
			entity.Deactivate();
	}

	/// <summary>
	/// Unregisters an entity from the manager.
	/// </summary>
	/// <param name="entity">The entity to unregister.</param>
	public void UnregisterEntity(Entity entity) =>
			// Remove the entity from the "Entity" group
			entity.RemoveFromGroup("Entity");
}