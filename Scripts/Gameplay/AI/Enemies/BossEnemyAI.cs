using System.Collections.Generic;
using Ceiro.Scripts.Core.Entity;
using Godot;

namespace Ceiro.Scripts.Gameplay.AI.Enemies;

/// <summary>
/// Specialized AI for boss enemies with multiple attack patterns.
/// </summary>
public partial class BossEnemyAi : EnemyAi
{
	[Export] public float        Phase2HealthThreshold = 0.6f;
	[Export] public float        Phase3HealthThreshold = 0.3f;
	[Export] public float        SpecialAttackCooldown = 10.0f;
	[Export] public float        AreaAttackRange       = 5.0f;
	[Export] public float        AreaAttackDamage      = 20.0f;
	[Export] public PackedScene? AreaAttackEffectPrefab;
	[Export] public PackedScene  MinionPrefab;
	[Export] public int          MaxMinions    = 3;
	[Export] public float        TelegraphTime = 1.5f;

	private int   _currentPhase = 1;
	private float _specialAttackCooldownTimer;
	private bool  _isTelegraphing;
	private float _telegraphTimer;
	private int   _nextAttackType;

	private readonly List<Node> _activeMinions = [];

	// Attack types
	private enum AttackType
	{
		Basic,
		AreaAttack,
		SummonMinions,
		ChargeAttack
	}

	public override void _Ready()
	{
		base._Ready();

		// Start with special attack ready
		_specialAttackCooldownTimer = 0.0f;
	}

	public override void _Process(double delta)
	{
		base._Process(delta);

		// Update special attack cooldown
		if (_specialAttackCooldownTimer > 0)
			_specialAttackCooldownTimer -= (float)delta;

		// Update telegraph timer
		if (_isTelegraphing)
		{
			_telegraphTimer -= (float)delta;

			if (_telegraphTimer <= 0)
			{
				_isTelegraphing = false;
				ExecuteSpecialAttack(_nextAttackType);
			}
		}

		// Check for phase transitions
		UpdatePhase();

		// Clean up minion list
		CleanupMinionList();
	}

	protected override void UpdateState()
	{
		base.UpdateState();

		// Check for special attack opportunity
		if (CurrentState == AiState.Attack && _specialAttackCooldownTimer <= 0 && !_isTelegraphing)
			StartSpecialAttack();
	}

	protected override void ProcessAttackState()
	{
		// Skip normal attack processing if telegraphing
		if (_isTelegraphing)
			return;

		base.ProcessAttackState();
	}

	/// <summary>
	/// Updates the boss phase based on health.
	/// </summary>
	private void UpdatePhase()
	{
		var healthPercentage = Entity.GetHealthPercentage();

		switch (_currentPhase)
		{
			case 1 when healthPercentage <= Phase2HealthThreshold:
				TransitionToPhase(2);
				break;
			case 2 when healthPercentage <= Phase3HealthThreshold:
				TransitionToPhase(3);
				break;
		}
	}

	/// <summary>
	/// Transitions to a new phase.
	/// </summary>
	/// <param name="phase">The phase to transition to.</param>
	private void TransitionToPhase(int phase)
	{
		_currentPhase = phase;

		// Phase transition effects
		switch (phase)
		{
			case 2:
				// Increase attack damage
				AttackDamage *= 1.5f;

				// Reduce attack cooldown
				AttackCooldown *= 0.8f;

				// Visual effect
				SpawnPhaseTransitionEffect();
				break;

			case 3:
				// Further increase attack damage
				AttackDamage *= 1.5f;

				// Further reduce attack cooldown
				AttackCooldown *= 0.8f;

				// Reduce special attack cooldown
				SpecialAttackCooldown *= 0.7f;

				// Visual effect
				SpawnPhaseTransitionEffect();
				break;
		}
	}

	/// <summary>
	/// Spawns a visual effect for phase transition.
	/// </summary>
	private void SpawnPhaseTransitionEffect()
	{
		// Create a shockwave effect
		var particles = new GpuParticles3D();
		particles.Emitting       = true;
		particles.OneShot        = true;
		particles.Explosiveness  = 0.8f;
		particles.GlobalPosition = Entity.GlobalPosition;

		// Set up particle material
		var particleMaterial = new ParticleProcessMaterial();
		particleMaterial.Direction          = new(0, 1, 0);
		particleMaterial.Spread             = 180.0f;
		particleMaterial.InitialVelocityMin = 5.0f;
		particleMaterial.InitialVelocityMax = 10.0f;
		particleMaterial.Color              = new(1, 0.5f, 0);

		particles.ProcessMaterial = particleMaterial;

		GetTree().Root.AddChild(particles);

		// Auto-remove after effect completes
		var timer = new Timer();
		timer.WaitTime = 3.0f;
		timer.OneShot  = true;
		particles.AddChild(timer);
		timer.Start();
		timer.Timeout += () => particles.QueueFree();
	}

	/// <summary>
	/// Starts a special attack sequence.
	/// </summary>
	private void StartSpecialAttack()
	{
		// Stop movement
		MovementComponent.StopMoving();

		// Determine attack type based on phase and situation

		var attackType = _currentPhase switch
		{
			// Phase 1: Only basic attacks and area attacks
			1 => GD.RandRange(0, 1),
			// Phase 2: Add summon minions
			2 => GD.RandRange(0, 2),
			// Phase 3: All attack types
			_ => GD.RandRange(0, 3)
		};

		// Override if minions are at max
		if (attackType is 2 && _activeMinions.Count >= MaxMinions)
			attackType = GD.RandRange(0, 1);

		_nextAttackType = attackType;

		// Start telegraph
		_isTelegraphing = true;
		_telegraphTimer = TelegraphTime;

		// Visual telegraph effect
		SpawnTelegraphEffect(attackType);
	}

	/// <summary>
	/// Spawns a visual effect to telegraph the upcoming attack.
	/// </summary>
	/// <param name="attackType">The type of attack being telegraphed.</param>
	private void SpawnTelegraphEffect(int attackType)
	{
		// Different effects based on attack type
		switch (attackType)
		{
			case 0: // Basic attack
				// Simple flash
				var flashEffect = new OmniLight3D();
				flashEffect.LightColor  = new(1, 0, 0);
				flashEffect.LightEnergy = 2.0f;
				Entity.AddChild(flashEffect);

				// Auto-remove after telegraph
				var timer = new Timer();
				timer.WaitTime = TelegraphTime;
				timer.OneShot  = true;
				flashEffect.AddChild(timer);
				timer.Start();
				timer.Timeout += () => flashEffect.QueueFree();
				break;

			case 1: // Area attack
				// Circle on ground
				var areaIndicator = new CsgCylinder3D();
				areaIndicator.Radius         = AreaAttackRange;
				areaIndicator.Height         = 0.1f;
				areaIndicator.GlobalPosition = Entity.GlobalPosition + new Vector3(0, 0.05f, 0);

				var material = new StandardMaterial3D();
				material.AlbedoColor           = new(1, 0, 0, 0.5f);
				material.Transparency          = BaseMaterial3D.TransparencyEnum.Alpha;
				areaIndicator.MaterialOverride = material;

				GetTree().Root.AddChild(areaIndicator);

				// Auto-remove after telegraph
				var areaTimer = new Timer();
				areaTimer.WaitTime = TelegraphTime;
				areaTimer.OneShot  = true;
				areaIndicator.AddChild(areaTimer);
				areaTimer.Start();
				areaTimer.Timeout += () => areaIndicator.QueueFree();
				break;

			case 2: // Summon minions
				// Summoning circles
				for (var i = 0; i < 3; i++)
				{
					var angle  = i * (Mathf.Pi                * 2 / 3);
					var offset = new Vector3(Mathf.Cos(angle) * 3, 0, Mathf.Sin(angle) * 3);

					var summonCircle = new CsgCylinder3D();
					summonCircle.Radius         = 1.0f;
					summonCircle.Height         = 0.1f;
					summonCircle.GlobalPosition = Entity.GlobalPosition + offset + new Vector3(0, 0.05f, 0);

					var summonMaterial = new StandardMaterial3D();
					summonMaterial.AlbedoColor    = new(0, 0, 1, 0.5f);
					summonMaterial.Transparency   = BaseMaterial3D.TransparencyEnum.Alpha;
					summonCircle.MaterialOverride = summonMaterial;

					GetTree().Root.AddChild(summonCircle);

					// Auto-remove after telegraph
					var summonTimer = new Timer();
					summonTimer.WaitTime = TelegraphTime;
					summonTimer.OneShot  = true;
					summonCircle.AddChild(summonTimer);
					summonTimer.Start();
					summonTimer.Timeout += () => summonCircle.QueueFree();
				}

				break;

			case 3: // Charge attack
				// Direction indicator
				if (Target != null)
				{
					var direction = (Target.GlobalPosition - Entity.GlobalPosition).Normalized();

					var chargeIndicator = new CsgBox3D();
					chargeIndicator.Size           = new(1, 0.1f, 10);
					chargeIndicator.GlobalPosition = Entity.GlobalPosition + direction * 5 + new Vector3(0, 0.05f, 0);

					// Rotate to face target
					chargeIndicator.LookAt(Target.GlobalPosition, Vector3.Up);

					var chargeMaterial = new StandardMaterial3D();
					chargeMaterial.AlbedoColor       = new(1, 0.5f, 0, 0.5f);
					chargeMaterial.Transparency      = BaseMaterial3D.TransparencyEnum.Alpha;
					chargeIndicator.MaterialOverride = chargeMaterial;

					GetTree().Root.AddChild(chargeIndicator);

					// Auto-remove after telegraph
					var chargeTimer = new Timer();
					chargeTimer.WaitTime = TelegraphTime;
					chargeTimer.OneShot  = true;
					chargeIndicator.AddChild(chargeTimer);
					chargeTimer.Start();
					chargeTimer.Timeout += () => chargeIndicator.QueueFree();
				}

				break;
		}
	}

	/// <summary>
	/// Executes the special attack.
	/// </summary>
	/// <param name="attackType">The type of attack to execute.</param>
	private void ExecuteSpecialAttack(int attackType)
	{
		switch (attackType)
		{
			case 0: // Basic attack with increased damage
				if (Target is Entity targetEntity)
					targetEntity.TakeDamage(AttackDamage * 1.5f, Entity);
				break;

			case 1: // Area attack
				ExecuteAreaAttack();
				break;

			case 2: // Summon minions
				SummonMinions();
				break;

			case 3: // Charge attack
				ExecuteChargeAttack();
				break;
		}

		// Start cooldown
		_specialAttackCooldownTimer = SpecialAttackCooldown;
	}

	/// <summary>
	/// Executes an area attack that damages all entities within range.
	/// </summary>
	private void ExecuteAreaAttack()
	{
		// Spawn visual effect
		if (AreaAttackEffectPrefab is not null)
		{
			var effect = AreaAttackEffectPrefab.Instantiate<Node3D>();
			effect.GlobalPosition = Entity.GlobalPosition;
			GetTree().Root.AddChild(effect);

			// Auto-remove after effect completes
			var timer = new Timer();
			timer.WaitTime = 2.0f;
			timer.OneShot  = true;
			effect.AddChild(timer);
			timer.Start();
			timer.Timeout += () => effect.QueueFree();
		}

		// Find all entities in range
		var spaceState = GetWorld3D().DirectSpaceState;

		// Create a sphere shape for the query
		var shape = new SphereShape3D();
		shape.Radius = AreaAttackRange;

		// Set up the query parameters
		var parameters = new PhysicsShapeQueryParameters3D();
		parameters.ShapeRid          = shape.GetRid();
		parameters.Transform         = new(Basis.Identity, Entity.GlobalPosition);
		parameters.CollideWithBodies = true;

		// Perform the query
		var results = spaceState.IntersectShape(parameters);

		// Apply damage to all entities in range
		foreach (var result in results)
			if (result.ContainsKey("collider"))
			{
				var collider = result["collider"].As<Node>();

				if (collider == Entity)
					continue;

				// Find the entity associated with this collider
				var entity = collider as Entity;
				if (entity == null && collider.GetParent() is Entity parentEntity)
					entity = parentEntity;

				// Apply damage
				entity?.TakeDamage(AreaAttackDamage, Entity);
			}
	}

	/// <summary>
	/// Summons minions to assist the boss.
	/// </summary>
	private void SummonMinions()
	{
		// Determine how many minions to summon
		var minionsToSummon = MaxMinions - _activeMinions.Count;
		minionsToSummon = Mathf.Min(minionsToSummon, 3); // Max 3 at once

		for (var i = 0; i < minionsToSummon; i++)
		{
			// Calculate spawn position
			var angle    = i * (Mathf.Pi                * 2 / minionsToSummon);
			var offset   = new Vector3(Mathf.Cos(angle) * 3, 0, Mathf.Sin(angle) * 3);
			var spawnPos = Entity.GlobalPosition + offset;

			// Spawn minion
			var minion = MinionPrefab.Instantiate<Node3D>();
			minion.GlobalPosition = spawnPos;
			GetTree().Root.AddChild(minion);

			// Add to active minions list
			_activeMinions.Add(minion);

			// Spawn effect
			var particles = new GpuParticles3D();
			particles.Emitting       = true;
			particles.OneShot        = true;
			particles.Explosiveness  = 0.8f;
			particles.GlobalPosition = spawnPos;

			// Set up particle material
			var particleMaterial = new ParticleProcessMaterial();
			particleMaterial.Direction          = new(0, 1, 0);
			particleMaterial.Spread             = 180.0f;
			particleMaterial.InitialVelocityMin = 2.0f;
			particleMaterial.InitialVelocityMax = 5.0f;
			particleMaterial.Color              = new(0, 0, 1);

			particles.ProcessMaterial = particleMaterial;

			GetTree().Root.AddChild(particles);

			// Auto-remove after effect completes
			var timer = new Timer();
			timer.WaitTime = 2.0f;
			timer.OneShot  = true;
			particles.AddChild(timer);
			timer.Start();
			timer.Timeout += () => particles.QueueFree();
		}
	}

	/// <summary>
	/// Executes a charge attack toward the target.
	/// </summary>
	private void ExecuteChargeAttack()
	{
		if (Target == null)
			return;

		// Store original speed
		var originalSpeed = MovementComponent.MoveSpeed;

		// Increase speed for charge
		MovementComponent.MoveSpeed = originalSpeed * 3;

		// Charge toward target
		MovementComponent.MoveToPosition(Target.GlobalPosition);

		// Create a timer to reset speed
		var timer = new Timer();
		timer.WaitTime = 1.0f;
		timer.OneShot  = true;
		Entity.AddChild(timer);
		timer.Start();
		timer.Timeout += () =>
		{
			MovementComponent.MoveSpeed = originalSpeed;

			// Apply damage if close to target
			if (Target == null || !(Entity.GlobalPosition.DistanceTo(Target.GlobalPosition) < 2.0f))
				return;

			if (Target is Entity targetEntity)
				targetEntity.TakeDamage(AttackDamage * 2, Entity);
		};
	}

	/// <summary>
	/// Cleans up the minion list, removing any invalid references.
	/// </summary>
	private void CleanupMinionList()
	{
		for (var i = _activeMinions.Count - 1; i >= 0; i--)
			if (!IsInstanceValid(_activeMinions[i]))
				_activeMinions.RemoveAt(i);
	}
}