using Godot;

namespace Ceiro.Scripts.Core.Entity.Components;

/// <summary>
/// Component that handles movement behavior for entities.
/// </summary>
public partial class MovementComponent : EntityComponent
{
	[Export] public float MoveSpeed     = 5.0f;
	[Export] public float RotationSpeed = 5.0f;
	[Export] public float Gravity       = 9.8f;
	[Export] public bool  UseNavigation;
	[Export] public float PathUpdateInterval = 0.5f;

	private Vector3           _targetPosition;
	private bool              _isMovingToTarget;
	private float             _pathUpdateTimer;
	private NavigationAgent3D _navigationAgent;

	public override void Initialize(Entity entity)
	{
		base.Initialize(entity);

		// Create navigation agent if using navigation
		if (!UseNavigation)
			return;

		_navigationAgent = new();
		entity.AddChild(_navigationAgent);

		// Configure navigation agent
		_navigationAgent.PathDesiredDistance   = 0.5f;
		_navigationAgent.TargetDesiredDistance = 1.0f;
	}

	public override void Cleanup()
	{
		// Clean up navigation agent
		_navigationAgent.QueueFree();
		_navigationAgent = null;

		base.Cleanup();
	}

	public override void PhysicsProcessComponent(double delta)
	{
		// Apply gravity
		if (!Entity.IsOnFloor())
		{
			var velocity = Entity.Velocity;
			velocity.Y      -= Gravity * (float)delta;
			Entity.Velocity =  velocity;
		}

		// Handle movement to target
		if (!_isMovingToTarget)
			return;


		if (UseNavigation)
		{
			// Update path at intervals
			_pathUpdateTimer += (float)delta;

			if (_pathUpdateTimer >= PathUpdateInterval)
			{
				_navigationAgent.TargetPosition = _targetPosition;
				_pathUpdateTimer                = 0.0f;
			}

			// Move along the path
			if (!_navigationAgent.IsNavigationFinished())
			{
				var nextPathPosition = _navigationAgent.GetNextPathPosition();
				var direction        = Entity.GlobalPosition.DirectionTo(nextPathPosition).Normalized();

				// Set velocity
				var velocity = Entity.Velocity;
				velocity.X      = direction.X * MoveSpeed;
				velocity.Z      = direction.Z * MoveSpeed;
				Entity.Velocity = velocity;

				// Rotate towards movement direction
				RotateTowards(direction, delta);

				// Set state to moving if not already
				if (Entity.GetState() is not Entity.EntityState.Moving)
					Entity.SetState(Entity.EntityState.Moving);
			}
			else
			{
				// Stop moving when reached destination
				StopMoving();
			}
		}
		else
		{
			// Direct movement without navigation
			var direction = Entity.GlobalPosition.DirectionTo(_targetPosition).Normalized();

			// Check if close enough to target
			if (Entity.GlobalPosition.DistanceTo(_targetPosition) < 0.5f)
			{
				StopMoving();
			}
			else
			{
				// Set velocity
				var velocity = Entity.Velocity;
				velocity.X      = direction.X * MoveSpeed;
				velocity.Z      = direction.Z * MoveSpeed;
				Entity.Velocity = velocity;

				// Rotate towards movement direction
				RotateTowards(direction, delta);

				// Set state to moving if not already
				if (Entity.GetState() is not Entity.EntityState.Moving)
					Entity.SetState(Entity.EntityState.Moving);
			}
		}
	}

	/// <summary>
	/// Rotates the entity towards the specified direction.
	/// </summary>
	/// <param name="direction">The direction to rotate towards.</param>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void RotateTowards(Vector3 direction, double delta)
	{
		if (direction == Vector3.Zero)
			return;

		// Only rotate around Y axis
		direction.Y = 0;

		if (!(direction.Length() > 0.001f))
			return;

		// Calculate target rotation
		var lookRotation   = new Transform3D().LookingAt(direction, Vector3.Up);
		var targetRotation = lookRotation.Basis.GetEuler();

		// Smoothly rotate towards target
		var currentRotation = Entity.Rotation;
		currentRotation.Y = Mathf.LerpAngle(currentRotation.Y, targetRotation.Y, RotationSpeed * (float)delta);
		Entity.Rotation   = currentRotation;
	}

	/// <summary>
	/// Moves the entity to the specified position.
	/// </summary>
	/// <param name="position">The position to move to.</param>
	public void MoveToPosition(Vector3 position)
	{
		_targetPosition   = position;
		_isMovingToTarget = true;

		// Set initial path if using navigation
		if (UseNavigation)
			_navigationAgent.TargetPosition = _targetPosition;
	}

	/// <summary>
	/// Stops the entity's movement.
	/// </summary>
	public void StopMoving()
	{
		_isMovingToTarget = false;

		// Stop velocity
		var velocity = Entity.Velocity;
		velocity.X      = 0;
		velocity.Z      = 0;
		Entity.Velocity = velocity;

		// Set state to idle if currently moving
		if (Entity.GetState() is Entity.EntityState.Moving)
			Entity.SetState(Entity.EntityState.Idle);
	}

	/// <summary>
	/// Checks if the entity is currently moving.
	/// </summary>
	/// <returns>True if the entity is moving, false otherwise.</returns>
	public bool IsMoving() => _isMovingToTarget;

	/// <summary>
	/// Gets the target position.
	/// </summary>
	/// <returns>The target position.</returns>
	public Vector3 GetTargetPosition() => _targetPosition;

	public override void OnStateChanged(Entity.EntityState oldState, Entity.EntityState newState)
	{
		// Stop moving if the entity is no longer in a state that allows movement
		if (newState is not Entity.EntityState.Moving && newState is not Entity.EntityState.Idle)
			StopMoving();
	}
}