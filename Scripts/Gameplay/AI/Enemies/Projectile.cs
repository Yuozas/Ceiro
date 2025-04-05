using Ceiro.Scripts.Core.Entity;
using Godot;

namespace Ceiro.Scripts.Gameplay.AI.Enemies;

/// <summary>
/// Represents a projectile fired by a ranged enemy.
/// </summary>
public partial class Projectile : Node3D
{
	[Export] public float Lifetime = 5.0f;

	private Vector3 _direction;
	private float   _speed;
	private float   _damage;
	private Node    _source;
	private bool    _hasHit;

	public override void _Ready()
	{
		// Create visual representation
		var mesh = new MeshInstance3D();
		mesh.Mesh  = new SphereMesh();
		mesh.Scale = new(0.2f, 0.2f, 0.2f);
		AddChild(mesh);

		// Create material
		var material = new StandardMaterial3D();
		material.AlbedoColor       = new(1, 0, 0);
		material.EmissionEnabled   = true;
		material.EmissionIntensity = 2.0f;
		material.Emission          = new(1, 0.5f, 0);
		mesh.MaterialOverride      = material;

		// Create collision shape
		var area = new Area3D();
		AddChild(area);

		var collisionShape = new CollisionShape3D();
		collisionShape.Shape                         = new SphereShape3D();
		((SphereShape3D)collisionShape.Shape).Radius = 0.2f;
		area.AddChild(collisionShape);

		// Connect signals
		area.BodyEntered += OnBodyEntered;

		// Set up lifetime timer
		var timer = new Timer();
		timer.WaitTime = Lifetime;
		timer.OneShot  = true;
		AddChild(timer);
		timer.Start();
		timer.Timeout += () => QueueFree();
	}

	public override void _Process(double delta)
	{
		if (_hasHit)
			return;

		// Move in direction
		GlobalPosition += _direction * _speed * (float)delta;
	}

	/// <summary>
	/// Initializes the projectile.
	/// </summary>
	/// <param name="direction">The direction of travel.</param>
	/// <param name="speed">The speed of the projectile.</param>
	/// <param name="damage">The damage the projectile deals.</param>
	/// <param name="source">The source of the projectile.</param>
	public void Initialize
	(
			Vector3 direction,
			float   speed,
			float   damage,
			Node    source
	)
	{
		_direction = direction;
		_speed     = speed;
		_damage    = damage;
		_source    = source;

		// Look in the direction of travel
		LookAt(GlobalPosition + direction);
	}

	/// <summary>
	/// Called when the projectile collides with a body.
	/// </summary>
	/// <param name="body">The body that was hit.</param>
	private void OnBodyEntered(Node3D body)
	{
		if (_hasHit || body == _source)
			return;

		_hasHit = true;

		// Apply damage if the body is an entity
		var entity = body as Entity;
		if (entity == null && body.GetParent() is Entity parentEntity)
			entity = parentEntity;

		if (entity != null)
			entity.TakeDamage(_damage, _source);

		// Spawn hit effect
		SpawnHitEffect();

		// Remove projectile
		QueueFree();
	}

	/// <summary>
	/// Spawns a visual effect when the projectile hits something.
	/// </summary>
	private void SpawnHitEffect()
	{
		// Create particles
		var particles = new GpuParticles3D();
		particles.Emitting       = true;
		particles.OneShot        = true;
		particles.Explosiveness  = 0.8f;
		particles.GlobalPosition = GlobalPosition;

		// Set up particle material
		var particleMaterial = new ParticleProcessMaterial();
		particleMaterial.Direction          = new(0, 1, 0);
		particleMaterial.Spread             = 180.0f;
		particleMaterial.InitialVelocityMin = 2.0f;
		particleMaterial.InitialVelocityMax = 5.0f;
		particleMaterial.Color              = new(1, 0.5f, 0, 1);

		particles.ProcessMaterial = particleMaterial;

		GetTree().Root.AddChild(particles);

		// Auto-remove after effect completes
		var timer = new Timer();
		timer.WaitTime = 1.0f;
		timer.OneShot  = true;
		particles.AddChild(timer);
		timer.Start();
		timer.Timeout += () => particles.QueueFree();
	}
}