using Godot;

namespace Ceiro.Scripts.Core.Entity;

/// <summary>
/// Base class for all entity components, providing a modular approach to entity behavior.
/// </summary>
public partial class EntityComponent : Node
{
	[Export] public bool IsEnabled = true;

	protected Entity Entity;

	/// <summary>
	/// Initializes the component with the parent entity.
	/// </summary>
	/// <param name="entity">The parent entity.</param>
	public virtual void Initialize(Entity entity) => Entity = entity;

	/// <summary>
	/// Cleans up the component when it's removed from the entity.
	/// </summary>
	public virtual void Cleanup() => Entity = null;

	/// <summary>
	/// Called every frame when the component is enabled.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	public virtual void ProcessComponent(double delta)
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called every physics frame when the component is enabled.
	/// </summary>
	/// <param name="delta">The time elapsed since the last physics frame.</param>
	public virtual void PhysicsProcessComponent(double delta)
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity's state changes.
	/// </summary>
	/// <param name="oldState">The previous state.</param>
	/// <param name="newState">The new state.</param>
	public virtual void OnStateChanged(Entity.EntityState oldState, Entity.EntityState newState)
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity is activated.
	/// </summary>
	public virtual void OnActivate()
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity is deactivated.
	/// </summary>
	public virtual void OnDeactivate()
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity takes damage.
	/// </summary>
	/// <param name="amount">The amount of damage taken.</param>
	/// <param name="source">The source of the damage, or null if none.</param>
	public virtual void OnDamaged(float amount, Node source)
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity is healed.
	/// </summary>
	/// <param name="amount">The amount healed.</param>
	public virtual void OnHealed(float amount)
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity dies.
	/// </summary>
	public virtual void OnDeath()
	{
		// Override in derived classes
	}

	/// <summary>
	/// Called when the entity is interacted with.
	/// </summary>
	/// <param name="interactor">The entity that is interacting with this entity.</param>
	/// <returns>True if the interaction was handled, false otherwise.</returns>
	public virtual bool OnInteract(Entity interactor) =>
			// Override in derived classes
			false;
}