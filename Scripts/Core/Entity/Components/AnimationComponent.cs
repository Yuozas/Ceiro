using System;
using Godot;

namespace Ceiro.Scripts.Core.Entity.Components;

/// <summary>
/// Component that handles animation behavior for entities.
/// </summary>
public partial class AnimationComponent : EntityComponent
{
	[Export] public string IdleAnimation     = "idle";
	[Export] public string WalkAnimation     = "walk";
	[Export] public string AttackAnimation   = "attack";
	[Export] public string DamageAnimation   = "damage";
	[Export] public string DeathAnimation    = "death";
	[Export] public string InteractAnimation = "interact";

	private AnimationPlayer _animationPlayer;
	private string          _currentAnimation = "";

	public override void Initialize(Entity entity)
	{
		base.Initialize(entity);

		// Get the animation player reference
		_animationPlayer = entity.GetNodeOrNull<AnimationPlayer>("AnimationPlayer") ?? throw new("AnimationPlayer not found");

		// Play idle animation initially
		PlayAnimation(IdleAnimation);
	}

	public override void OnStateChanged(Entity.EntityState oldState, Entity.EntityState newState)
	{
		// Play appropriate animation based on state
		switch (newState)
		{
			case Entity.EntityState.Idle:
				PlayAnimation(IdleAnimation);
				break;

			case Entity.EntityState.Moving:
				PlayAnimation(WalkAnimation);
				break;

			case Entity.EntityState.Attacking:
				PlayAnimation(AttackAnimation);
				break;

			case Entity.EntityState.TakingDamage:
				PlayAnimation(DamageAnimation);
				break;

			case Entity.EntityState.Dead:
				PlayAnimation(DeathAnimation);
				break;

			case Entity.EntityState.Interacting:
				PlayAnimation(InteractAnimation);
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
		}
	}

	/// <summary>
	/// Plays an animation if it exists.
	/// </summary>
	/// <param name="animName">The name of the animation to play.</param>
	/// <returns>True if the animation was played, false otherwise.</returns>
	public bool PlayAnimation(string animName)
	{
		if (string.IsNullOrEmpty(animName))
			return false;

		// Check if the animation exists
		if (!_animationPlayer.HasAnimation(animName))
			return false;

		// Don't replay the same animation
		if (_currentAnimation == animName && _animationPlayer.IsPlaying())
			return true;

		_currentAnimation = animName;
		_animationPlayer.Play(animName);
		return true;
	}

	/// <summary>
	/// Gets the current animation.
	/// </summary>
	/// <returns>The current animation name.</returns>
	public string GetCurrentAnimation() => _currentAnimation;

	/// <summary>
	/// Checks if an animation is currently playing.
	/// </summary>
	/// <returns>True if an animation is playing, false otherwise.</returns>
	public bool IsAnimationPlaying() => _animationPlayer.IsPlaying();
}