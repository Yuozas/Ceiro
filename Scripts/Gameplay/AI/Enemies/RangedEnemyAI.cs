using Ceiro.Scripts.Core.Entity;
using Godot;

namespace Ceiro.Scripts.Gameplay.AI.Enemies;

/// <summary>
/// Specialized AI for ranged enemies.
/// </summary>
public partial class RangedEnemyAi : EnemyAi
{
	[Export] public float       RangedAttackRange = 8.0f;
	[Export] public float       ProjectileSpeed   = 10.0f;
	[Export] public PackedScene ProjectilePrefab;
	[Export] public float       RetreatDistance = 3.0f;

	private bool _isRetreating;

	public override void _Ready()
	{
		base._Ready();

		// Override attack range with ranged attack range
		AttackRange = RangedAttackRange;
	}

	protected override void UpdateState()
	{
		base.UpdateState();

		// Check if we need to retreat
		if (CurrentState == AiState.Attack && Target != null)
		{
			var distanceToTarget = Entity.GlobalPosition.DistanceTo(Target.GlobalPosition);

			if (distanceToTarget < RetreatDistance)
				_isRetreating = true;
			else
				_isRetreating = false;
		}
		else
		{
			_isRetreating = false;
		}
	}

	protected override void ProcessAttackState()
	{
		if (Target == null)
			return;

		// Handle retreat if too close
		if (_isRetreating)
		{
			ProcessRetreatBehavior();
			return;
		}

		// Stop movement
		if (MovementComponent != null)
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
		if (AttackCooldownTimer <= 0)
		{
			// Set attacking state
			Entity.SetState(Entity.EntityState.Attacking);

			// Fire projectile
			FireProjectile();

			// Start attack cooldown
			AttackCooldownTimer = AttackCooldown;
		}
	}

	/// <summary>
	/// Processes retreat behavior when target is too close.
	/// </summary>
	private void ProcessRetreatBehavior()
	{
		if (Target == null || MovementComponent == null)
			return;

		// Move away from target
		var direction = (Entity.GlobalPosition - Target.GlobalPosition).Normalized();
		direction.Y = 0;

		var retreatTarget = Entity.GlobalPosition + direction * 5;

		// Use pathfinding if available
		if (PathfindingManager != null)
		{
			if (CurrentPath.Count == 0 || PathUpdateTimer <= 0)
			{
				CurrentPath      = PathfindingManager.FindPath(Entity.GlobalPosition, retreatTarget);
				CurrentPathIndex = 0;
				PathUpdateTimer  = PathUpdateInterval;
			}

			if (CurrentPath.Count > 0 && CurrentPathIndex < CurrentPath.Count)
			{
				MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);

				if (Entity.GlobalPosition.DistanceTo(CurrentPath[CurrentPathIndex]) < 0.5f)
				{
					CurrentPathIndex++;

					if (CurrentPathIndex < CurrentPath.Count)
						MovementComponent.MoveToPosition(CurrentPath[CurrentPathIndex]);
				}
			}
			else
			{
				MovementComponent.MoveToPosition(retreatTarget);
			}
		}
		else
		{
			MovementComponent.MoveToPosition(retreatTarget);
		}
	}

	/// <summary>
	/// Fires a projectile at the target.
	/// </summary>
	private void FireProjectile()
	{
		if (ProjectilePrefab == null || Target == null)
			return;

		// Create projectile
		var projectile = ProjectilePrefab.Instantiate<Projectile>();
		GetTree().Root.AddChild(projectile);

		// Position at entity
		projectile.GlobalPosition = Entity.GlobalPosition + Vector3.Up * 1.0f; // Offset to fire from "head" height

		// Set direction and speed
		var direction = (Target.GlobalPosition - projectile.GlobalPosition).Normalized();
		projectile.Initialize(direction, ProjectileSpeed, AttackDamage, Entity);
	}
}