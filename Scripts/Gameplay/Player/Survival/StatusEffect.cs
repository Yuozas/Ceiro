using System;
using Godot;

namespace Ceiro.Scripts.Gameplay.Player.Survival;

/// <summary>
/// Represents a status effect that can be applied to the player.
/// </summary>
[Tool]
public partial class StatusEffect : Resource
{
	public enum EffectTypes
	{
		Poison,
		Regeneration,
		Warmth,
		Cooling,
		Satiated,
		Hungry,
		Burning,
		Freezing,
		WellFed
	}

	public EffectTypes EffectType        { get; private init; }
	public string      DisplayName       { get; private set; } = "";
	public string      Description       { get; private set; } = "";
	public float       RemainingDuration { get; set; }
	public float       Strength          { get; private init; } = 1.0f;
	public Texture2D?  Icon              { get; private init; }

	// Effect modifiers
	public bool  AffectsHealth  { get; private set; }
	public float HealthModifier { get; private set; }

	public bool  AffectsHunger  { get; private set; }
	public float HungerModifier { get; private set; } = 1.0f;

	public bool  AffectsTemperature  { get; private set; }
	public float TemperatureModifier { get; private set; }

	/// <summary>
	/// Creates a predefined status effect.
	/// </summary>
	/// <param name="type">The type of effect to create.</param>
	/// <param name="duration">The duration of the effect in seconds.</param>
	/// <param name="icon">The icon representing the visual appearance of the effect.</param>
	/// <param name="strength">The strength of the effect (1.0 is normal).</param>
	/// <returns>The created status effect.</returns>
	public static StatusEffect CreateEffect
	(
			EffectTypes type,
			float       duration,
			float       strength = 1.0f,
			Texture2D?  icon     = null
	)
	{
		var effect = new StatusEffect
		{
			EffectType        = type,
			RemainingDuration = duration,
			Strength          = strength,
			Icon              = icon
		};

		switch (type)
		{
			case EffectTypes.Poison:
				effect.DisplayName    = "Poison";
				effect.Description    = "Taking damage over time";
				effect.AffectsHealth  = true;
				effect.HealthModifier = -2.0f * strength;
				break;

			case EffectTypes.Regeneration:
				effect.DisplayName    = "Regeneration";
				effect.Description    = "Healing over time";
				effect.AffectsHealth  = true;
				effect.HealthModifier = 2.0f * strength;
				break;

			case EffectTypes.Warmth:
				effect.DisplayName         = "Warmth";
				effect.Description         = "Increased body temperature";
				effect.AffectsTemperature  = true;
				effect.TemperatureModifier = 10.0f * strength;
				break;

			case EffectTypes.Cooling:
				effect.DisplayName         = "Cooling";
				effect.Description         = "Decreased body temperature";
				effect.AffectsTemperature  = true;
				effect.TemperatureModifier = -10.0f * strength;
				break;

			case EffectTypes.Satiated:
				effect.DisplayName    = "Satiated";
				effect.Description    = "Reduced hunger depletion";
				effect.AffectsHunger  = true;
				effect.HungerModifier = 0.5f / strength; // Lower value = slower depletion
				break;

			case EffectTypes.Hungry:
				effect.DisplayName    = "Hungry";
				effect.Description    = "Increased hunger depletion";
				effect.AffectsHunger  = true;
				effect.HungerModifier = 1.5f * strength;
				break;

			case EffectTypes.Burning:
				effect.DisplayName         = "Burning";
				effect.Description         = "Taking fire damage";
				effect.AffectsHealth       = true;
				effect.HealthModifier      = -5.0f * strength;
				effect.AffectsTemperature  = true;
				effect.TemperatureModifier = 15.0f * strength;
				break;

			case EffectTypes.Freezing:
				effect.DisplayName         = "Freezing";
				effect.Description         = "Taking cold damage";
				effect.AffectsHealth       = true;
				effect.HealthModifier      = -3.0f * strength;
				effect.AffectsTemperature  = true;
				effect.TemperatureModifier = -15.0f * strength;
				break;

			case EffectTypes.WellFed:
				effect.DisplayName    = "Well Fed";
				effect.Description    = "Increased health regeneration";
				effect.AffectsHealth  = true;
				effect.HealthModifier = 1.0f * strength;
				effect.AffectsHunger  = true;
				effect.HungerModifier = 0.8f;
				break;
			default:
				throw new ArgumentOutOfRangeException(nameof(type), type, null);
		}

		return effect;
	}
}