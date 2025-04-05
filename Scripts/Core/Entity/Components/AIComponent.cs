using System;
using Godot;

namespace Ceiro.Scripts.Core.Entity.Components;

/// <summary>
/// Component that handles AI behavior for entities.
/// </summary>
public partial class AiComponent : EntityComponent
{
	public enum AiBehaviorType
	{
		Idle,
		Wander,
		Follow,
		Flee,
		Attack,
		Patrol
	}

	[Export] public AiBehaviorType DefaultBehavior  = AiBehaviorType.Idle;
	[Export] public float          DetectionRange   = 10.0f;
	[Export] public float          AttackRange      = 2.0f;
	[Export] public float          WanderRadius     = 5.0f;
	[Export] public float          MinWanderTime    = 3.0f;
	[Export] public float          MaxWanderTime    = 10.0f;
	[Export] public float          AttackCooldown   = 2.0f;
	[Export] public float          DecisionInterval = 0.5f;

	private AiBehaviorType    _currentBehavior;
	private Node3D?           _target;
	private MovementComponent _movementComponent;
	private float             _wanderTimer;
	private float             _attackCooldownTimer;
	private float             _decisionTimer;
	private Vector3           _wanderTarget;
	private Vector3           _homePosition;
	private Vector3[]         _patrolPoints = [];
	private int               _currentPatrolIndex;

	public override void Initialize(Entity entity)
	{
		base.Initialize(entity);

		// Store the home position
		_homePosition = entity.GlobalPosition;

		// Get the movement component
		_movementComponent = entity.GetEntityComponent<MovementComponent>() ?? throw new("Entity does not have a MovementComponent.");

		// Set initial behavior
		_currentBehavior = DefaultBehavior;

		// Initialize patrol points if using patrol behavior
		if (DefaultBehavior is AiBehaviorType.Patrol)
			GeneratePatrolPoints();
	}

	public override void ProcessComponent(double delta)
	{
		// Update timers
		_decisionTimer -= (float)delta;

		if (_attackCooldownTimer > 0)
			_attackCooldownTimer -= (float)delta;

		// Make decisions at intervals
		if (_decisionTimer <= 0)
		{
			MakeDecision();
			_decisionTimer = DecisionInterval;
		}

		// Process current behavior
		ProcessBehavior(delta);
	}

	/// <summary>
	/// Makes a decision about what behavior to use.
	/// </summary>
	private void MakeDecision()
	{
		// Find potential targets
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

		// Decide behavior based on target and distance
		if (nearestPlayer is not null)
		{
			_target = nearestPlayer;

			if (nearestDistance <= AttackRange)
					// Attack if in range and cooldown is ready
					// Back up slightly while waiting for attack cooldown
				SetBehavior(_attackCooldownTimer <= 0 ? AiBehaviorType.Attack : AiBehaviorType.Idle);
			else
					// Follow the target
				SetBehavior(AiBehaviorType.Follow);
		}
		else
		{
			// No target, revert to default behavior
			SetBehavior(DefaultBehavior);
		}
	}

	/// <summary>
	/// Executes the logic associated with the current AI behavior of the entity.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame, used for timing and movement calculations.</param>
	/// <exception cref="ArgumentOutOfRangeException">Thrown if the current behavior is not defined in <see cref="AiBehaviorType"/>.</exception>
	private void ProcessBehavior(double delta)
	{
		switch (_currentBehavior)
		{
			case AiBehaviorType.Idle:
				ProcessIdleBehavior();
				break;

			case AiBehaviorType.Wander:
				ProcessWanderBehavior(delta);
				break;

			case AiBehaviorType.Follow:
				ProcessFollowBehavior();
				break;

			case AiBehaviorType.Flee:
				ProcessFleeBehavior();
				break;

			case AiBehaviorType.Attack:
				ProcessAttackBehavior();
				break;

			case AiBehaviorType.Patrol:
				ProcessPatrolBehavior();
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(_currentBehavior), _currentBehavior, "Invalid AI behavior type.");
		}
	}

	/// <summary>
	/// Processes idle behavior.
	/// </summary>
	private void ProcessIdleBehavior()
	{
		// Stop moving
		if (_movementComponent.IsMoving())
			_movementComponent.StopMoving();
	}

	/// <summary>
	/// Processes wander behavior.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void ProcessWanderBehavior(double delta)
	{
		// Update wander timer
		_wanderTimer -= (float)delta;

		// Check if we need a new wander target
		if (!(_wanderTimer <= 0) && _movementComponent.IsMoving())
			return;

		// Generate a new random point to wander to
		var random = new Random();
		var angle  = (float)random.NextDouble() * Mathf.Pi * 2;
		var radius = (float)random.NextDouble() * WanderRadius;

		_wanderTarget = _homePosition
		              + new Vector3(Mathf.Cos(angle) * radius,
		                            0,
		                            Mathf.Sin(angle) * radius);

		// Set a new wander timer
		_wanderTimer = MinWanderTime + (float)random.NextDouble() * (MaxWanderTime - MinWanderTime);

		// Move to the new target
		_movementComponent.MoveToPosition(_wanderTarget);
	}

	/// <summary>
	/// Processes follow behavior.
	/// </summary>
	private void ProcessFollowBehavior()
	{
		if (_target is null)
			return;

		// Move towards the target
		_movementComponent.MoveToPosition(_target.GlobalPosition);
	}

	/// <summary>
	/// Processes flee behavior.
	/// </summary>
	private void ProcessFleeBehavior()
	{
		if (_target is null)
			return;

		// Move away from the target
		var direction = Entity.GlobalPosition - _target.GlobalPosition;
		direction = direction.Normalized() * 10; // Flee distance

		_movementComponent.MoveToPosition(Entity.GlobalPosition + direction);
	}

	/// <summary>
	/// Processes attack behavior.
	/// </summary>
	private void ProcessAttackBehavior()
	{
		if (_target is null)
			return;

		// Stop moving
		if (_movementComponent.IsMoving())
			_movementComponent.StopMoving();

		// Face the target
		var direction = (_target.GlobalPosition - Entity.GlobalPosition).Normalized();
		direction.Y = 0;

		if (direction.Length() > 0.001f)
		{
			var lookRotation = new Transform3D().LookingAt(direction, Vector3.Up);
			Entity.Rotation = lookRotation.Basis.GetEuler();
		}

		// Perform attack
		Entity.SetState(Entity.EntityState.Attacking);

		// Apply damage to target if it's an entity
		if (_target is Entity targetEntity)
			targetEntity.TakeDamage(10, Entity);

		// Start attack cooldown
		_attackCooldownTimer = AttackCooldown;
	}

	/// <summary>
	/// Processes patrol behavior.
	/// </summary>
	private void ProcessPatrolBehavior()
	{
		if (_patrolPoints.Length is 0)
			return;

		// Check if we've reached the current patrol point
		if (_movementComponent.IsMoving())
			return;

		// Move to the next patrol point
		_currentPatrolIndex = (_currentPatrolIndex + 1) % _patrolPoints.Length;
		_movementComponent.MoveToPosition(_patrolPoints[_currentPatrolIndex]);
	}

	/// <summary>
	/// Sets the current behavior.
	/// </summary>
	/// <param name="behavior">The behavior to set.</param>
	public void SetBehavior(AiBehaviorType behavior)
	{
		if (_currentBehavior == behavior)
			return;

		_currentBehavior = behavior;

		// Initialize the new behavior
		switch (behavior)
		{
			case AiBehaviorType.Wander:
				_wanderTimer = 0; // Force new wander target
				break;

			case AiBehaviorType.Patrol:
				if (_patrolPoints.Length is 0)
					GeneratePatrolPoints();
				break;
			case AiBehaviorType.Idle:
			case AiBehaviorType.Follow:
			case AiBehaviorType.Flee:
			case AiBehaviorType.Attack:
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(behavior), behavior, null);
		}
	}

	/// <summary>
	/// Generates patrol points around the home position.
	/// </summary>
	private void GeneratePatrolPoints()
	{
		var random    = new Random();
		var numPoints = random.Next(3, 6);
		_patrolPoints = new Vector3[numPoints];

		for (var i = 0; i < numPoints; i++)
		{
			var angle  = (float)i     / numPoints * Mathf.Pi * 2;
			var radius = WanderRadius * 0.5f + (float)random.NextDouble() * WanderRadius * 0.5f;

			_patrolPoints[i] = _homePosition
			                 + new Vector3(Mathf.Cos(angle) * radius,
			                               0,
			                               Mathf.Sin(angle) * radius);
		}

		_currentPatrolIndex = 0;
	}

	/// <summary>
	/// Sets the target for the AI.
	/// </summary>
	/// <param name="target">The target to set.</param>
	public void SetTarget(Node3D target) => _target = target;

	/// <summary>
	/// Gets the current behavior.
	/// </summary>
	/// <returns>The current behavior.</returns>
	public AiBehaviorType GetCurrentBehavior() => _currentBehavior;
}