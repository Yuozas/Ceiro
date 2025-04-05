using Ceiro.Scripts.Core.Entity;
using Godot;

namespace Ceiro.Scripts.Gameplay.AI.Enemies;

/// <summary>
/// Specialized AI for melee enemies.
/// </summary>
public partial class MeleeEnemyAi : EnemyAi
{
	[Export] public float ChargeDistance = 5.0f;
	[Export] public float ChargeCooldown = 5.0f;
	[Export] public float ChargeSpeed    = 10.0f;

	private float   _chargeCooldownTimer;
	private bool    _isCharging;
	private Vector3 _chargeTarget;
	private float   _originalMoveSpeed;

	public override void _Ready()
	{
		base._Ready();

		// Store original move speed
		if (MovementComponent != null)
			_originalMoveSpeed = MovementComponent.MoveSpeed;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		// Update charge cooldown
		if (_chargeCooldownTimer > 0)
			_chargeCooldownTimer -= (float)delta;

		// Handle charging state
		if (_isCharging)
			ProcessChargingState();
	}

	protected override void UpdateState()
	{
		// Skip state update if charging
		if (_isCharging)
			return;

		base.UpdateState();

		// Check for charge opportunity
		if (CurrentState == AiState.Chase && Target != null)
		{
			var distanceToTarget = Entity.GlobalPosition.DistanceTo(Target.GlobalPosition);

			if (distanceToTarget <= ChargeDistance && distanceToTarget > AttackRange && _chargeCooldownTimer <= 0)
				StartCharge();
		}
	}

	/// <summary>
	/// Starts a charge attack.
	/// </summary>
	private void StartCharge()
	{
		if (Target == null || MovementComponent == null)
			return;

		_isCharging   = true;
		_chargeTarget = Target.GlobalPosition;

		// Increase move speed for the charge
		MovementComponent.MoveSpeed = ChargeSpeed;

		// Move directly to the target
		MovementComponent.MoveToPosition(_chargeTarget);
	}

	/// <summary>
	/// Processes the charging state.
	/// </summary>
	private void ProcessChargingState()
	{
		if (MovementComponent == null)
			return;

		// Check if charge is complete
		var distanceToTarget = Entity.GlobalPosition.DistanceTo(_chargeTarget);

		if (distanceToTarget < 0.5f || !MovementComponent.IsMoving())
			EndCharge();

		// Check if we hit the target
		if (Target != null)
		{
			var distanceToActualTarget = Entity.GlobalPosition.DistanceTo(Target.GlobalPosition);

			if (distanceToActualTarget <= AttackRange)
			{
				// Apply damage
				if (Target is Entity targetEntity)
					targetEntity.TakeDamage(AttackDamage * 2, Entity); // Double damage for charge attack

				EndCharge();
			}
		}
	}

	/// <summary>
	/// Ends the charge attack.
	/// </summary>
	private void EndCharge()
	{
		_isCharging = false;

		// Restore original move speed
		if (MovementComponent != null)
			MovementComponent.MoveSpeed = _originalMoveSpeed;

		// Start cooldown
		_chargeCooldownTimer = ChargeCooldown;
	}
}