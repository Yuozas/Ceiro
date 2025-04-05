using System;
using System.Collections.Generic;
using Ceiro.Scripts.Core.Entity;
using Ceiro.Scripts.Core.Entity.Components;
using Godot;

namespace Ceiro.Scripts.Gameplay.AI.Enemies;

/// <summary>
/// Base class for enemy AI behavior.
/// </summary>
public partial class EnemyAi : Node3D
{
	[Export] public float DetectionRange     = 10.0f;
	[Export] public float AttackRange        = 2.0f;
	[Export] public float WanderRadius       = 5.0f;
	[Export] public float PathUpdateInterval = 0.5f;
	[Export] public float AttackCooldown     = 2.0f;
	[Export] public float AttackDamage       = 10.0f;

	protected Entity             Entity;
	protected MovementComponent  MovementComponent;
	protected Node3D?            Target;
	protected List<Vector3>      CurrentPath = [];
	protected int                CurrentPathIndex;
	protected float              PathUpdateTimer;
	protected float              AttackCooldownTimer;
	protected Vector3            HomePosition;
	protected PathfindingManager PathfindingManager;

	// State machine
	public enum AiState
	{
		Idle,
		Wander,
		Chase,
		Attack,
		Return,
		Flee
	}

	protected AiState CurrentState = AiState.Idle;

	public override void _Ready()
	{
		// Get the entity
		Entity = GetParentOrNull<Entity>() ?? throw new("Entity not found in parent node.");

		// Get the movement component
		MovementComponent = Entity.GetEntityComponent<MovementComponent>();

		// Store home position
		HomePosition = Entity.GlobalPosition;

		// Find the pathfinding manager
		PathfindingManager = GetTree().Root.FindChild("PathfindingManager", true, false) as PathfindingManager ?? throw new("Pathfinding manager not found.");
	}

	public override void _Process(double delta)
	{
		if (!Entity.IsEntityActive())
			return;

		// Update timers
		PathUpdateTimer -= (float)delta;

		if (AttackCooldownTimer > 0)
			AttackCooldownTimer -= (float)delta;

		// Find potential targets
		if (Target is null || !IsInstanceValid(Target))
			FindTarget();

		// Update state
		UpdateState();

		// Process current state
		ProcessState(delta);

		// Update path if needed
		if (!(PathUpdateTimer <= 0) || CurrentState is not (AiState.Chase or AiState.Return))
			return;

		UpdatePath();
		PathUpdateTimer = PathUpdateInterval;
	}

	/// <summary>
	/// Finds a potential target.
	/// </summary>
	protected virtual void FindTarget()
	{
		// Find players in range
		var     players         = GetTree().GetNodesInGroup("Player");
		Node3D? nearestPlayer   = null;
		var     nearestDistance = float.MaxValue;

		foreach (var player in players)
			if (player is Node3D playerNode)
			{
				var distance = Entity.GlobalPosition.DistanceTo(playerNode.GlobalPosition);

				if (!(distance < nearestDistance) || !(distance < DetectionRange))
					continue;

				nearestPlayer   = playerNode;
				nearestDistance = distance;
			}

		Target = nearestPlayer;
	}

	/// <summary>
	/// Updates the AI state based on current conditions.
	/// </summary>
	protected virtual void UpdateState()
	{
		// Get distance to target if available
		var distanceToTarget = float.MaxValue;
		if (Target is not null)
			distanceToTarget = Entity.GlobalPosition.DistanceTo(Target.GlobalPosition);

		// Get distance to home
		var distanceToHome = Entity.GlobalPosition.DistanceTo(HomePosition);

		// Update state based on conditions
		switch (CurrentState)
		{
			case AiState.Idle:
				// Transition to wander after some time
				if (GD.Randf() < 0.01) // Small chance each frame
					SetState(AiState.Wander);

				// Transition to chase if target in range
				if (Target != null && distanceToTarget < DetectionRange)
					SetState(AiState.Chase);
				break;

			case AiState.Wander:
				// Transition to idle sometimes
				if (GD.Randf() < 0.005) // Small chance each frame
					SetState(AiState.Idle);

				// Transition to chase if target in range
				if (Target != null && distanceToTarget < DetectionRange)
					SetState(AiState.Chase);

				// Transition to return if too far from home
				if (distanceToHome > WanderRadius * 2)
					SetState(AiState.Return);
				break;

			case AiState.Chase:
				// Transition to attack if in range
				if (Target != null && distanceToTarget <= AttackRange)
					SetState(AiState.Attack);

				// Transition to return if target lost or too far from home
				if (Target == null || distanceToHome > DetectionRange * 1.5f)
					SetState(AiState.Return);
				break;

			case AiState.Attack:
				// Transition to chase if target moves out of range
				if (Target == null || distanceToTarget > AttackRange)
					SetState(AiState.Chase);
				break;

			case AiState.Return:
				// Transition to idle if back home
				if (distanceToHome < 1.0f)
					SetState(AiState.Idle);

				// Transition to chase if target in range
				if (Target != null && distanceToTarget < DetectionRange)
					SetState(AiState.Chase);
				break;

			case AiState.Flee:
				// Transition to return if safe
				if (Target == null || distanceToTarget > DetectionRange)
					SetState(AiState.Return);
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	/// <summary>
	/// Processes the current AI state.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	protected virtual void ProcessState(double delta)
	{
		switch (CurrentState)
		{
			case AiState.Idle:
				ProcessIdleState();
				break;

			case AiState.Wander:
				ProcessWanderState();
				break;

			case AiState.Chase:
				ProcessChaseState();
				break;

			case AiState.Attack:
				ProcessAttackState();
				break;

			case AiState.Return:
				ProcessReturnState();
				break;

			case AiState.Flee:
				ProcessFleeState();
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	/// <summary>
	/// Processes the idle state.
	/// </summary>
	protected virtual void ProcessIdleState()
	{
		// Stop movement
		MovementComponent.StopMoving();

		// Play idle animation
		Entity.SetState(Entity.EntityState.Idle);
	}

	/// <summary>
	/// Processes the wander state.
	/// </summary>
	protected virtual void ProcessWanderState()
	{
		// If not moving, pick a random point to move to
		if (!MovementComponent.IsMoving())
		{
			// Generate a random point within wander radius
			var angle  = (float)GD.RandRange(0, Mathf.Pi * 2);
			var radius = (float)GD.RandRange(0, WanderRadius);

			var targetPos = HomePosition
			              + new Vector3(Mathf.Cos(angle) * radius,
			                            0,
			                            Mathf.Sin(angle) * radius);

			// Move to the point
			CurrentPath      = PathfindingManager.FindPath(Entity.GlobalPosition, targetPos);
			CurrentPathIndex = 0;

			if (CurrentPath.Count > 0)
				MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);
		}

		// Check if reached current path point
		if (CurrentPath.Count <= 0)
			return;

		if (!(Entity.GlobalPosition.DistanceTo(CurrentPath[CurrentPathIndex]) < 0.5f))
			return;

		CurrentPathIndex++;

		if (CurrentPathIndex < CurrentPath.Count)
			MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);
	}

	/// <summary>
	/// Processes the chase state.
	/// </summary>
	protected virtual void ProcessChaseState()
	{
		if (Target == null)
			return;

		// Follow the path to the target
		if (CurrentPath.Count > 0 && CurrentPathIndex < CurrentPath.Count)
		{
			// Move to the current path point
			MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);

			// Check if reached current path point
			if (!(Entity.GlobalPosition.DistanceTo(CurrentPath[CurrentPathIndex]) < 0.5f))
				return;

			CurrentPathIndex++;

			if (CurrentPathIndex < CurrentPath.Count)
				MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);
		}
		else
		{
			// Direct movement if no path
			MovementComponent.MoveToPosition(Target.GlobalPosition);
		}
	}

	/// <summary>
	/// Processes the attack state.
	/// </summary>
	protected virtual void ProcessAttackState()
	{
		if (Target == null)
			return;

		// Stop movement
		MovementComponent.StopMoving();

		// Face the target
		var direction = (Target.GlobalPosition - Entity.GlobalPosition).Normalized();
		direction.Y = 0;

		if (direction.Length() > 0.001f)
		{
			var lookRotation = new Transform3D().LookingAt(direction, Vector3.Up);
			Entity.Rotation = lookRotation.Basis.GetEuler();
		}

		// Attack if cooldown is ready
		if (!(AttackCooldownTimer <= 0))
			return;

		// Set attacking state
		Entity.SetState(Entity.EntityState.Attacking);

		// Apply damage to target if it's an entity
		if (Target is Entity targetEntity)
			targetEntity.TakeDamage(AttackDamage, Entity);

		// Start attack cooldown
		AttackCooldownTimer = AttackCooldown;
	}

	/// <summary>
	/// Processes the return state.
	/// </summary>
	protected virtual void ProcessReturnState()
	{
		// Follow the path home
		if (CurrentPath.Count > 0 && CurrentPathIndex < CurrentPath.Count)
		{
			// Move to the current path point
			MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);

			// Check if reached current path point
			if (!(Entity.GlobalPosition.DistanceTo(CurrentPath[CurrentPathIndex]) < 0.5f))
				return;

			CurrentPathIndex++;

			if (CurrentPathIndex < CurrentPath.Count)
				MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);
		}
		else
		{
			// Direct movement if no path
			MovementComponent.MoveToPosition(HomePosition);
		}
	}

	/// <summary>
	/// Processes the flee state.
	/// </summary>
	protected virtual void ProcessFleeState()
	{
		if (Target == null)
			return;

		// Move away from target
		var direction = (Entity.GlobalPosition - Target.GlobalPosition).Normalized();
		direction.Y = 0;

		var fleeTarget = Entity.GlobalPosition + direction * 10;

		// Use pathfinding if available
		if (CurrentPath.Count == 0 || PathUpdateTimer <= 0)
		{
			CurrentPath      = PathfindingManager.FindPath(Entity.GlobalPosition, fleeTarget);
			CurrentPathIndex = 0;
			PathUpdateTimer  = PathUpdateInterval;
		}

		if (CurrentPath.Count > 0 && CurrentPathIndex < CurrentPath.Count)
		{
			MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);

			if (!(Entity.GlobalPosition.DistanceTo(CurrentPath[CurrentPathIndex]) < 0.5f))
				return;

			CurrentPathIndex++;

			if (CurrentPathIndex < CurrentPath.Count)
				MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);
		}
		else
		{
			MovementComponent.MoveToPosition(fleeTarget);
		}
	}

	/// <summary>
	/// Updates the path to the current target.
	/// </summary>
	protected virtual void UpdatePath()
	{
		Vector3 targetPos;

		switch (CurrentState)
		{
			case AiState.Chase when Target != null:
				targetPos = Target.GlobalPosition;
				break;
			case AiState.Return:
				targetPos = HomePosition;
				break;
			case AiState.Idle:
			case AiState.Wander:
			case AiState.Attack:
			case AiState.Flee:
			default:
				return;
		}

		CurrentPath      = PathfindingManager.FindPath(Entity.GlobalPosition, targetPos);
		CurrentPathIndex = 0;
	}

	/// <summary>
	/// Sets the AI state.
	/// </summary>
	/// <param name="newState">The new state.</param>
	public virtual void SetState(AiState newState)
	{
		if (CurrentState == newState)
			return;

		CurrentState = newState;

		// Reset path when changing states
		CurrentPath.Clear();
		CurrentPathIndex = 0;

		// State-specific initialization
		switch (newState)
		{
			case AiState.Idle:
				MovementComponent.StopMoving();
				break;

			case AiState.Return:
			case AiState.Chase:
				UpdatePath();
				break;

			case AiState.Wander:
			case AiState.Attack:
			case AiState.Flee:
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
		}
	}

	/// <summary>
	/// Gets the current AI state.
	/// </summary>
	/// <returns>The current state.</returns>
	public AiState GetState() => CurrentState;
}