using Godot;
using Ceiro.Scripts.Gameplay.Player.Inventory;
using Ceiro.Scripts.Gameplay.Player.Survival;
using System.Collections.Generic;
using System.Linq;
using Ceiro.Scripts.Core.Entity;
using Ceiro.Scripts.Core.Entity.Components;

// TODO: work in progress.
public partial class Player : CharacterBody3D
{
	[Export] public float Speed               = 5.0f;
	[Export] public float Gravity             = 9.8f;
	[Export] public float CameraSmoothness    = 5.0f;
	[Export] public float InteractionDistance = 3.0f;
	[Export] public float MaxHealth           = 100.0f;
	[Export] public int   HotbarSize          = 5;

	// Camera references
	private Camera3D _camera;
	private Vector3  _initialCameraOffset;

	// Animation references
	private AnimationPlayer _animationPlayer;

	// System references
	private InventorySystem? _inventorySystem;
	private SurvivalSystem?  _survivalSystem;
	private BuildingSystem?  _buildingSystem;

	// Health state
	private float _currentHealth;

	// Interaction state
	private List<Entity> _interactableEntitiesInRange = [];

	// Animation states
	private const string ANIM_IDLE_FORWARD  = "idle_forward";
	private const string ANIM_IDLE_BACKWARD = "idle_backward";
	private const string ANIM_IDLE_LEFT     = "idle_left";
	private const string ANIM_IDLE_RIGHT    = "idle_right";
	private const string ANIM_WALK_FORWARD  = "walk_forward";
	private const string ANIM_WALK_BACKWARD = "walk_backward";
	private const string ANIM_WALK_LEFT     = "walk_left";
	private const string ANIM_WALK_RIGHT    = "walk_right";

	// Keep track of the last direction for idle animations
	private enum FacingDirection
	{
		Forward,
		Backward,
		Left,
		Right
	}

	private FacingDirection _lastDirection = FacingDirection.Forward;

	// Signals
	[Signal]
	public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

	[Signal]
	public delegate void PlayerDeathEventHandler();

	[Signal]
	public delegate void PlayerInteractedWithEventHandler(NodePath entityPath);

	public override void _Ready()
	{
		// Add player to the "Player" group
		AddToGroup("Player");

		// Initialize health
		_currentHealth = MaxHealth;

		// Get references
		_camera          = GetNodeOrNull<Camera3D>("Camera3D")               ?? throw new("Camera3D not found!");
		_animationPlayer = GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? throw new("AnimationPlayer not found!");

		// Store the initial offset between camera and player
		_initialCameraOffset = _camera.GlobalPosition - GlobalPosition;

		// Find game systems
		_inventorySystem = GetTree().Root.FindChild("InventorySystem", true, false) as InventorySystem; // ?? throw new("InventorySystem not found!");
		_survivalSystem  = GetTree().Root.FindChild("SurvivalSystem",  true, false) as SurvivalSystem;  //  ?? throw new("SurvivalSystem not found!");
		_buildingSystem  = GetTree().Root.FindChild("BuildingSystem",  true, false) as BuildingSystem;

		// Initially play the forward idle animation
		PlayAnimationIfExists(ANIM_IDLE_FORWARD);
	}

	public override void _PhysicsProcess(double delta)
	{
		// Apply gravity
		var velocity = Velocity;
		if (!IsOnFloor())
			velocity.Y -= Gravity * (float)delta;

		// Get input direction
		var inputDir  = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
		var direction = new Vector3(inputDir.X, 0, inputDir.Y).Normalized();

		// Handle animations based on movement direction
		HandleDirectionalAnimations(inputDir);

		// Move in camera's orientation (optional - for more intuitive controls)
		if (direction != Vector3.Zero)
		{
			velocity.X = direction.X * Speed;
			velocity.Z = direction.Z * Speed;
		}
		else
		{
			velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);
			velocity.Z = Mathf.MoveToward(velocity.Z, 0, Speed);
		}

		// Apply movement
		Velocity = velocity;
		MoveAndSlide();

		// Detect interactable entities
		HandleInteractionDetection();
	}

	public override void _Process(double delta)
	{
		// Update camera position
		UpdateCameraPosition(delta);

		// Handle interaction input
		if (Input.IsActionJustPressed("interact"))
			TryInteract();

		// Handle hotbar selection
		for (var i = 1; i <= HotbarSize; i++)
			if (Input.IsActionJustPressed($"hotbar_{i}"))
				_inventorySystem.SetSelectedHotbarIndex(i - 1);

		// Handle building placement
		if (Input.IsActionJustPressed("place_building"))
			TryPlaceBuilding();
	}

	/// <summary>
	/// Handles directional animations based on movement input.
	/// </summary>
	/// <param name="inputDir">The input direction vector.</param>
	private void HandleDirectionalAnimations(Vector2 inputDir)
	{
		// If no movement, play idle animation based on the last direction
		if (inputDir == Vector2.Zero)
		{
			switch (_lastDirection)
			{
				case FacingDirection.Forward:
					PlayAnimationIfExists(ANIM_IDLE_FORWARD);
					break;
				case FacingDirection.Backward:
					PlayAnimationIfExists(ANIM_IDLE_BACKWARD);
					break;
				case FacingDirection.Left:
					PlayAnimationIfExists(ANIM_IDLE_LEFT);
					break;
				case FacingDirection.Right:
					PlayAnimationIfExists(ANIM_IDLE_RIGHT);
					break;
			}

			return;
		}

		// Determine the dominant direction
		var absX = Mathf.Abs(inputDir.X);
		var absY = Mathf.Abs(inputDir.Y);

		if (absX > absY)
		{
			// Horizontal movement is dominant
			if (inputDir.X > 0)
			{
				_lastDirection = FacingDirection.Right;
				PlayAnimationIfExists(ANIM_WALK_RIGHT);
			}
			else
			{
				_lastDirection = FacingDirection.Left;
				PlayAnimationIfExists(ANIM_WALK_LEFT);
			}
		}
		else
		{
			// Vertical movement is dominant
			if (inputDir.Y > 0) // In Godot, positive Y is backward
			{
				_lastDirection = FacingDirection.Backward;
				PlayAnimationIfExists(ANIM_WALK_BACKWARD);
			}
			else
			{
				_lastDirection = FacingDirection.Forward;
				PlayAnimationIfExists(ANIM_WALK_FORWARD);
			}
		}
	}

	/// <summary>
	/// Plays an animation if it exists in the animation player.
	/// </summary>
	/// <param name="animName">The name of the animation to play.</param>
	private void PlayAnimationIfExists(string animName)
	{
		// Only play the animation if it exists and isn't already playing
		if (_animationPlayer.HasAnimation(animName) && _animationPlayer.CurrentAnimation != animName)
			_animationPlayer.Play(animName);
	}

	/// <summary>
	/// Updates the camera position to follow the player.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void UpdateCameraPosition(double delta)
	{
		// Calculate target position based on initial offset
		var targetPos = GlobalPosition + _initialCameraOffset;

		// Apply smoothing with proper clamped delta
		var smoothFactor = Mathf.Clamp(CameraSmoothness * (float)delta, 0f, 1f);
		_camera.GlobalPosition = _camera.GlobalPosition.Lerp(targetPos, smoothFactor);
	}

	/// <summary>
	/// Handles interaction detection to find nearby interactable entities.
	/// </summary>
	private void HandleInteractionDetection()
	{
		// Clear the previous list
		_interactableEntitiesInRange.Clear();

		// Find interactable entities in range
		var spaceState = GetWorld3D().DirectSpaceState;

		// Create a sphere shape for the query
		var shape = new SphereShape3D();
		shape.Radius = InteractionDistance;

		// Set up the query parameters
		var parameters = new PhysicsShapeQueryParameters3D();
		parameters.ShapeRid          = shape.GetRid();
		parameters.Transform         = new(Basis.Identity, GlobalPosition);
		parameters.CollideWithBodies = true;

		// Perform the query
		var results = spaceState.IntersectShape(parameters);

		// Process the results
		foreach (var result in results)
		{
			if (!result.ContainsKey("collider"))
				continue;

			var collider = result["collider"].As<Node>();

			// Skip self
			if (collider == this)
				continue;

			// Find the entity associated with this collider
			var entity = collider as Entity;
			if (entity is null && collider.GetParent() is Entity parentEntity)
				entity = parentEntity;

			// Add interactable entities to the list
			if (entity is not null && entity.IsInteractable)
				_interactableEntitiesInRange.Add(entity);
		}

		// Highlight the closest entity
		HighlightClosestInteractable();
	}

	/// <summary>
	/// Highlights the closest interactable entity.
	/// </summary>
	private void HighlightClosestInteractable()
	{
		// Find the closest entity
		Entity? closestEntity = null;
		var     minDistance   = float.MaxValue;

		foreach (var entity in _interactableEntitiesInRange)
		{
			var distance = GlobalPosition.DistanceTo(entity.GlobalPosition);

			if (distance < minDistance)
			{
				minDistance   = distance;
				closestEntity = entity;
			}
		}

		// Highlight the closest entity
		foreach (var entity in _interactableEntitiesInRange)
		{
			var interactionComponents = entity.GetChildren().OfType<InteractionComponent>();
			foreach (var component in interactionComponents)
				component.SetHighlighted(entity == closestEntity);
		}
	}

	/// <summary>
	/// Attempts to interact with the closest interactable entity.
	/// </summary>
	private void TryInteract()
	{
		// Find the closest entity
		if (_interactableEntitiesInRange.Count == 0)
			return;

		var closestEntity = _interactableEntitiesInRange
							.OrderBy(e => GlobalPosition.DistanceTo(e.GlobalPosition))
							.First();

		// Since Player isn't an Entity, we can't pass 'this' directly
		// Instead, we'll use a more generic approach to interaction

		// Option 1: Direct method call without passing Player
		closestEntity.Interact(null);

		// Option 2: Look for an InteractionComponent and use it
		var interactionComponents = closestEntity.GetChildren().OfType<InteractionComponent>();
		foreach (var component in interactionComponents)
				// Just trigger the interaction without passing the player
			component.OnInteract(null);

		// Emit a signal that we're interacting with this entity
		// This allows other systems to respond if needed
		EmitSignal("PlayerInteractedWith", closestEntity.GetPath());
	}

	/// <summary>
	/// Attempts to place a building using the selected hotbar item.
	/// </summary>
	private void TryPlaceBuilding()
	{
		if (_buildingSystem is null)
			return;

		var selectedItem = _inventorySystem.GetSelectedHotbarItem();

		// Check if the selected item is a placeable
		if (selectedItem is not null && selectedItem.Type == Item.ItemType.Placeable)
			_buildingSystem.StartPlacement(selectedItem.ItemId);
	}

	/// <summary>
	/// Applies damage to the player.
	/// </summary>
	/// <param name="amount">The amount of damage to apply.</param>
	/// <param name="source">The source of the damage (optional).</param>
	public void TakeDamage(float amount, Node? source = null)
	{
		_currentHealth = Mathf.Max(0, _currentHealth - amount);

		// Update the survival system
		_survivalSystem.TakeDamage(amount);

		// Emit signal for UI updates
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		// Check for death
		if (_currentHealth <= 0)
			Die();
	}

	/// <summary>
	/// Heals the player.
	/// </summary>
	/// <param name="amount">The amount to heal.</param>
	public void Heal(float amount)
	{
		_currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);

		// Emit signal for UI updates
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	/// <summary>
	/// Kills the player.
	/// </summary>
	private void Die()
	{
		// Play death animation if available
		PlayAnimationIfExists("death");

		// Disable input
		ProcessMode = ProcessModeEnum.Disabled;

		// Emit death signal
		EmitSignal(SignalName.PlayerDeath);
	}

	/// <summary>
	/// Gets the player's current health.
	/// </summary>
	/// <returns>The current health value.</returns>
	public float GetHealth() => _currentHealth;

	/// <summary>
	/// Sets the player's health.
	/// </summary>
	/// <param name="value">The health value to set.</param>
	public void SetHealth(float value)
	{
		var previousHealth = _currentHealth;
		_currentHealth = Mathf.Clamp(value, 0, MaxHealth);

		// Emit signal for UI updates
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		// Check for death
		if (_currentHealth <= 0 && previousHealth > 0)
			Die();
	}

	/// <summary>
	/// Gets the player's current facing direction as a Vector3.
	/// </summary>
	/// <returns>A normalized vector representing the facing direction.</returns>
	public Vector3 GetFacingDirection() => _lastDirection switch
	{
		FacingDirection.Forward  => new(0, 0, -1),
		FacingDirection.Backward => new(0, 0, 1),
		FacingDirection.Left     => new(-1, 0, 0),
		FacingDirection.Right    => new(1, 0, 0),
		_                        => new(0, 0, -1)
	};

	/// <summary>
	/// Gets the hunger value from the survival system.
	/// </summary>
	/// <returns>The current hunger value.</returns>
	public float GetHunger() => _survivalSystem?.GetHunger() ?? 100.0f;

	/// <summary>
	/// Sets the hunger value in the survival system.
	/// </summary>
	/// <param name="value">The hunger value to set.</param>
	public void SetHunger(float value)
	{
		if (_survivalSystem is not null)
		{
			var currentHunger = _survivalSystem.GetHunger();
			var difference    = value - currentHunger;

			if (difference > 0)
				_survivalSystem.AddFood(difference);
		}
	}

	/// <summary>
	/// Consumes an item from the inventory.
	/// </summary>
	/// <param name="itemId">The ID of the item to consume.</param>
	/// <returns>True if the item was consumed, false otherwise.</returns>
	public bool ConsumeItem(string itemId)
	{
		// Find the item in inventory
		for (var i = 0; i < _inventorySystem.InventorySize; i++)
		{
			var item = _inventorySystem.GetItem(i);

			if (item is not null && item.ItemId == itemId)
			{
				// Handle food items
				if (item.Type == Item.ItemType.Food)
				{
					var hungerRestore = item.GetProperty("hunger_restore");
					_survivalSystem.AddFood(hungerRestore);
				}

				// Reduce stack count or remove item
				if (item is
						{
							IsStackable: true,
							StackCount : > 1
						})
					item.StackCount--;
				else
					_inventorySystem.RemoveItem(i);

				return true;
			}
		}

		return false;
	}
}
