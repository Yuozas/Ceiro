using Godot;
using System.Collections.Generic;
using System.Linq;
using Ceiro.Scripts.Core.World.Rendering;

namespace Ceiro.Scripts.Core.Entity;

/// <summary>
/// Base entity class that provides common functionality for all game entities.
/// Implements billboard sprite handling, component-based design, and state machine.
/// </summary>
public partial class Entity : CharacterBody3D
{
	[Export] public string EntityName          = "Entity";
	[Export] public float  MaxHealth           = 100.0f;
	[Export] public bool   IsInteractable      = true;
	[Export] public float  InteractionDistance = 2.0f;
	[Export] public bool   AutoActivate        = true;
	[Export] public float  ActivationDistance  = 30.0f;

	// State machine
	public enum EntityState
	{
		Idle,
		Moving,
		Interacting,
		Attacking,
		TakingDamage,
		Dead
	}

	[Signal]
	public delegate void EntityStateChangedEventHandler(EntityState newState);

	[Signal]
	public delegate void EntityHealthChangedEventHandler(float currentHealth, float maxHealth);

	[Signal]
	public delegate void EntityInteractionEventHandler(Entity interactor);

	[Signal]
	public delegate void EntityDeathEventHandler();

	protected EntityState           CurrentState = EntityState.Idle;
	protected float                 CurrentHealth;
	protected bool                  IsActive;
	protected Node3D                Player;
	protected List<EntityComponent> Components = [];

	// References to common components
	protected Sprite3D         Sprite;
	protected AnimationPlayer  AnimationPlayer;
	protected CollisionShape3D CollisionShape;
	protected BillboardSprite  BillboardComponent;

	public override void _Ready()
	{
		// Initialize health
		CurrentHealth = MaxHealth;

		// Get references to common components
		Sprite             = GetNodeOrNull<Sprite3D>("Sprite3D")                 ?? throw new("Sprite3D not found");
		AnimationPlayer    = GetNodeOrNull<AnimationPlayer>("AnimationPlayer")   ?? throw new("AnimationPlayer not found");
		CollisionShape     = GetNodeOrNull<CollisionShape3D>("CollisionShape3D") ?? throw new("CollisionShape3D not found");
		BillboardComponent = GetNodeOrNull<BillboardSprite>("BillboardSprite")   ?? throw new("BillboardSprite not found");

		var playerNodes = GetTree().GetNodesInGroup("Player");
		// Find the player in the scene
		Player = playerNodes.Count > 0
				? playerNodes[0] as Node3D ?? throw new("Player not found")
				: throw new("Player not found");

		// Find and register all entity components
		foreach (var child in GetChildren())
			if (child is EntityComponent component)
				RegisterComponent(component);

		// Set initial state
		SetState(EntityState.Idle);

		// Activate if auto-activate is enabled
		if (AutoActivate)
			Activate();
	}

	public override void _Process(double delta)
	{
		if (!IsActive)
			return;

		// Check activation distance if player is available
		var distanceToPlayer = GlobalPosition.DistanceTo(Player.GlobalPosition);

		// Deactivate if too far from player
		if (distanceToPlayer > ActivationDistance)
		{
			Deactivate();
			return;
		}

		// Process all components
		foreach (var component in Components.Where(component => component.IsEnabled))
			component.ProcessComponent(delta);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (!IsActive)
			return;

		// Process physics for all components
		foreach (var component in Components.Where(component => component.IsEnabled))
			component.PhysicsProcessComponent(delta);

		// Apply movement if velocity is set
		if (Velocity != Vector3.Zero)
			MoveAndSlide();
	}

	/// <summary>
	/// Registers a component with this entity.
	/// </summary>
	/// <param name="component">The component to register.</param>
	public void RegisterComponent(EntityComponent component)
	{
		if (Components.Contains(component))
			return;

		Components.Add(component);
		component.Initialize(this);
	}

	/// <summary>
	/// Unregisters a component from this entity.
	/// </summary>
	/// <param name="component">The component to unregister.</param>
	public void UnregisterComponent(EntityComponent component)
	{
		if (!Components.Contains(component))
			return;

		Components.Remove(component);
		component.Cleanup();
	}

	/// <summary>
	/// Gets a component of the specified type.
	/// </summary>
	/// <typeparam name="T">The type of component to get.</typeparam>
	/// <returns>The component, or null if not found.</returns>
	public T GetEntityComponent<T>() where T : EntityComponent
	{
		foreach (var component in Components)
			if (component is T typedComponent)
				return typedComponent;

		throw new("Component not found");
	}

	/// <summary>
	/// Sets the entity's state and notifies components.
	/// </summary>
	/// <param name="newState">The new state.</param>
	public virtual void SetState(EntityState newState)
	{
		if (CurrentState == newState)
			return;

		var oldState = CurrentState;
		CurrentState = newState;

		// Notify all components of the state change
		foreach (var component in Components)
			component.OnStateChanged(oldState, newState);

		// Emit signal for external listeners
		EmitSignal(SignalName.EntityStateChanged, (int)newState);
	}

	/// <summary>
	/// Gets the current state of the entity.
	/// </summary>
	/// <returns>The current state.</returns>
	public EntityState GetState() => CurrentState;

	/// <summary>
	/// Activates the entity, enabling processing.
	/// </summary>
	public virtual void Activate()
	{
		if (IsActive)
			return;

		IsActive = true;

		// Notify all components
		foreach (var component in Components)
			component.OnActivate();

		// Enable processing
		ProcessMode = ProcessModeEnum.Inherit;
	}

	/// <summary>
	/// Deactivates the entity, disabling processing.
	/// </summary>
	public virtual void Deactivate()
	{
		if (!IsActive)
			return;

		IsActive = false;

		// Notify all components
		foreach (var component in Components)
			component.OnDeactivate();

		// Disable processing
		ProcessMode = ProcessModeEnum.Disabled;
	}

	/// <summary>
	/// Applies damage to the entity.
	/// </summary>
	/// <param name="amount">The amount of damage to apply.</param>
	/// <param name="source">The source of the damage.</param>
	public virtual void TakeDamage(float amount, Node source)
	{
		if (CurrentState is EntityState.Dead)
			return;

		CurrentHealth -= amount;

		// Notify all components
		foreach (var component in Components)
			component.OnDamaged(amount, source);

		// Emit signal for external listeners
		EmitSignal(SignalName.EntityHealthChanged, CurrentHealth, MaxHealth);

		// Change state to taking damage
		SetState(EntityState.TakingDamage);

		// Check if dead
		if (CurrentHealth <= 0)
			Die();
	}

	/// <summary>
	/// Heals the entity.
	/// </summary>
	/// <param name="amount">The amount to heal.</param>
	public virtual void Heal(float amount)
	{
		if (CurrentState is EntityState.Dead)
			return;

		CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);

		// Notify all components
		foreach (var component in Components)
			component.OnHealed(amount);

		// Emit signal for external listeners
		EmitSignal(SignalName.EntityHealthChanged, CurrentHealth, MaxHealth);
	}

	/// <summary>
	/// Kills the entity.
	/// </summary>
	public virtual void Die()
	{
		if (CurrentState is EntityState.Dead)
			return;

		CurrentHealth = 0;

		// Change state to dead
		SetState(EntityState.Dead);

		// Notify all components
		foreach (var component in Components)
			component.OnDeath();

		// Emit signal for external listeners
		EmitSignal(SignalName.EntityDeath);
	}

	/// <summary>
	/// Handles interaction with this entity.
	/// </summary>
	/// <param name="interactor">The entity that is interacting with this entity.</param>
	/// <returns>True if the interaction was handled, false otherwise.</returns>
	public virtual bool Interact(Entity interactor)
	{
		if (!IsInteractable || CurrentState is EntityState.Dead)
			return false;

		// Change state to interacting
		SetState(EntityState.Interacting);

		// Notify all components
		var handled = false;
		foreach (var component in Components)
			if (component.OnInteract(interactor))
				handled = true;

		// Emit signal for external listeners
		EmitSignal(SignalName.EntityInteraction, interactor);

		return handled;
	}

	/// <summary>
	/// Plays an animation if it exists.
	/// </summary>
	/// <param name="animName">The name of the animation to play.</param>
	/// <returns>True if the animation was played, false otherwise.</returns>
	public virtual bool PlayAnimation(string animName)
	{
		if (!AnimationPlayer.HasAnimation(animName))
			return false;

		AnimationPlayer.Play(animName);
		return true;
	}

	/// <summary>
	/// Gets the current health of the entity.
	/// </summary>
	/// <returns>The current health.</returns>
	public float GetHealth() => CurrentHealth;

	/// <summary>
	/// Gets the health percentage of the entity.
	/// </summary>
	/// <returns>The health percentage (0-1).</returns>
	public float GetHealthPercentage() => CurrentHealth / MaxHealth;

	/// <summary>
	/// Checks if the entity is active.
	/// </summary>
	/// <returns>True if the entity is active, false otherwise.</returns>
	public bool IsEntityActive() => IsActive;
}
