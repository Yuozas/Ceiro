using System.Collections.Generic;
using System.Linq;
using Ceiro.Scripts.Core.Utilities;
using Ceiro.Scripts.Core.World.Generation;
using Godot;
using TimeOfDaySystem = Ceiro.Scripts.Core.World._Time.TimeOfDaySystem;
using WeatherSystem = Ceiro.Scripts.Core.World._Time.WeatherSystem;

namespace Ceiro.Scripts.Gameplay.Player.Survival;

/// <summary>
/// Manages the player's survival mechanics, including health, hunger, and other status effects.
/// </summary>
public partial class SurvivalSystem : Node
{
	[Signal]
	public delegate void HealthChangedEventHandler(float currentHealth, float maxHealth);

	[Signal]
	public delegate void HungerChangedEventHandler(float currentHunger, float maxHunger);

	[Signal]
	public delegate void TemperatureChangedEventHandler(float currentTemperature);

	[Signal]
	public delegate void PlayerDeathEventHandler();

	[Signal]
	public delegate void StatusEffectAddedEventHandler(StatusEffect effect);

	[Signal]
	public delegate void StatusEffectRemovedEventHandler(StatusEffect effect);

	[Export] public float MaxHealth             = 100.0f;
	[Export] public float MaxHunger             = 100.0f;
	[Export] public float HungerDepletionRate   = 0.5f;  // Per second
	[Export] public float StarvationDamage      = 1.0f;  // Per second when hunger is zero
	[Export] public float OptimalTemperature    = 20.0f; // Celsius
	[Export] public float TemperatureRange      = 10.0f; // Comfortable range around optimal
	[Export] public float TemperatureDamageRate = 1.0f;  // Per second when outside extreme range
	[Export] public float HealthRegenRate       = 1.0f;  // Per second when conditions are good
	[Export] public float HealthRegenThreshold  = 0.7f;  // Hunger must be above this percentage to regenerate health

	private float           _currentHealth;
	private float           _currentHunger;
	private float           _currentTemperature;
	private TimeOfDaySystem _timeOfDaySystem;
	private WeatherSystem   _weatherSystem;

	private readonly List<StatusEffect>                 _activeStatusEffects = [];
	private readonly Dictionary<BiomeDefinition, float> _biomeTemperatures   = new();

	public override void _Ready()
	{
		// Initialize survival stats
		_currentHealth      = MaxHealth;
		_currentHunger      = MaxHunger;
		_currentTemperature = OptimalTemperature;

		// Find related systems
		_timeOfDaySystem = GetTree().Root.FindChild("TimeOfDaySystem", true, false) as TimeOfDaySystem ?? throw new("TimeOfDaySystem not found or unable to cast to type TimeOfDaySystem.");
		_weatherSystem   = GetTree().Root.FindChild("WeatherSystem",   true, false) as WeatherSystem   ?? throw new("WeatherSystem not found or unable to cast to type WeatherSystem.");

		// Initialize biome temperatures
		InitializeBiomeTemperatures();
	}

	public override void _Process(double delta)
	{
		// Update hunger
		UpdateHunger(delta);

		// Update temperature based on environment
		UpdateTemperature(delta);

		// Update health based on conditions
		UpdateHealth(delta);

		// Process active status effects
		ProcessStatusEffects(delta);
	}

	/// <summary>
	/// Initializes the temperature values for different biomes.
	/// </summary>
	private void InitializeBiomeTemperatures()
	{
		// This would typically load from a configuration file or resource
		// For now, we'll hardcode some example values

		// Find all biome definitions in the game
		var biomeDefinitions = ResourceUtils.LoadAll("res://resources/biomes/", "tres");

		foreach (var biome in biomeDefinitions)
			if (biome is BiomeDefinition biomeDef)
			{
				// Calculate temperature based on biome properties
				var baseTemp = biomeDef.Temperature * 40.0f - 10.0f; // Map 0-1 to -10 to 30 Celsius
				_biomeTemperatures[biomeDef] = baseTemp;
			}
	}

	/// <summary>
	/// Updates the player's hunger over time.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void UpdateHunger(double delta)
	{
		// Reduce hunger over time
		var hungerReduction = HungerDepletionRate * (float)delta;

		// Apply modifiers from status effects
		hungerReduction = _activeStatusEffects.Where(effect => effect.AffectsHunger).Aggregate(hungerReduction, (current, effect) => current * effect.HungerModifier);

		// Apply the hunger reduction
		_currentHunger = Mathf.Max(0, _currentHunger - hungerReduction);

		// Emit signal for UI updates
		EmitSignal(SignalName.HungerChanged, _currentHunger, MaxHunger);
	}

	/// <summary>
	/// Updates the player's temperature based on environment.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void UpdateTemperature(double delta)
	{
		// Get the current biome the player is in
		var currentBiome = GetCurrentBiome();

		// Base temperature from biome
		var targetTemperature = OptimalTemperature;

		if (currentBiome != null && _biomeTemperatures.TryGetValue(currentBiome, out var biomeTemp))
			targetTemperature = biomeTemp;

		// Adjust for time of day
		var timeOfDayFactor = _timeOfDaySystem.GetTemperatureFactor();
		targetTemperature += timeOfDayFactor * 10.0f; // +/- 10 degrees based on time

		// Adjust for weather
		var weatherFactor = _weatherSystem.GetTemperatureFactor();
		targetTemperature += weatherFactor * 15.0f; // +/- 15 degrees based on weather

		// Adjust for status effects
		targetTemperature += _activeStatusEffects.Where(effect => effect.AffectsTemperature).Sum(effect => effect.TemperatureModifier);

		// Gradually move current temperature toward target
		var temperatureChangeRate = 2.0f * (float)delta; // Degrees per second

		if (_currentTemperature < targetTemperature)
			_currentTemperature = Mathf.Min(_currentTemperature + temperatureChangeRate, targetTemperature);
		else if (_currentTemperature > targetTemperature)
			_currentTemperature = Mathf.Max(_currentTemperature - temperatureChangeRate, targetTemperature);

		// Emit signal for UI updates
		EmitSignal(SignalName.TemperatureChanged, _currentTemperature);
	}

	/// <summary>
	/// Updates the player's health based on survival conditions.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void UpdateHealth(double delta)
	{
		float healthChange = 0;

		// Starvation damage when hunger is zero
		if (_currentHunger <= 0)
			healthChange -= StarvationDamage * (float)delta;

		// Temperature damage when outside comfortable range
		var tempDifference = Mathf.Abs(_currentTemperature - OptimalTemperature);

		if (tempDifference > TemperatureRange + 10.0f) // Extreme temperature
		{
			var severityFactor = (tempDifference - TemperatureRange - 10.0f) / 10.0f;
			healthChange -= TemperatureDamageRate * severityFactor * (float)delta;
		}

		// Health regeneration when conditions are good
		if (_currentHunger                  / MaxHunger > HealthRegenThreshold && tempDifference <= TemperatureRange && _currentHealth < MaxHealth)
			healthChange += HealthRegenRate * (float)delta;

		// Apply modifiers from status effects
		healthChange += _activeStatusEffects.Where(effect => effect.AffectsHealth).Sum(effect => effect.HealthModifier * (float)delta);

		// Apply the health change
		_currentHealth = Mathf.Clamp(_currentHealth + healthChange, 0, MaxHealth);

		// Emit signal for UI updates
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		// Check for death
		if (_currentHealth <= 0)
			EmitSignal(SignalName.PlayerDeath);
	}

	/// <summary>
	/// Processes active status effects, updating durations and removing expired effects.
	/// </summary>
	/// <param name="delta">The time elapsed since the last frame.</param>
	private void ProcessStatusEffects(double delta)
	{
		for (var i = _activeStatusEffects.Count - 1; i >= 0; i--)
		{
			var effect = _activeStatusEffects[i];

			// Update duration
			effect.RemainingDuration -= (float)delta;

			// Remove expired effects
			if (!(effect.RemainingDuration <= 0))
				continue;

			_activeStatusEffects.RemoveAt(i);
			EmitSignal(SignalName.StatusEffectRemoved, effect);
		}
	}

	/// <summary>
	/// Gets the current biome the player is in.
	/// </summary>
	/// <returns>The current biome definition, or null if not found.</returns>
	private static BiomeDefinition? GetCurrentBiome() =>
			// This would typically use the world generation system to determine the current biome
			// For now, we'll return null as a placeholder
			null;

	/// <summary>
	/// Applies damage to the player.
	/// </summary>
	/// <param name="amount">The amount of damage to apply.</param>
	public void TakeDamage(float amount)
	{
		_currentHealth = Mathf.Max(0, _currentHealth - amount);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);

		// Check for death
		if (_currentHealth <= 0)
			EmitSignal(SignalName.PlayerDeath);
	}

	/// <summary>
	/// Heals the player.
	/// </summary>
	/// <param name="amount">The amount to heal.</param>
	public void Heal(float amount)
	{
		_currentHealth = Mathf.Min(MaxHealth, _currentHealth + amount);
		EmitSignal(SignalName.HealthChanged, _currentHealth, MaxHealth);
	}

	/// <summary>
	/// Adds food to the player's hunger.
	/// </summary>
	/// <param name="amount">The amount of hunger to restore.</param>
	public void AddFood(float amount)
	{
		_currentHunger = Mathf.Min(MaxHunger, _currentHunger + amount);
		EmitSignal(SignalName.HungerChanged, _currentHunger, MaxHunger);
	}

	/// <summary>
	/// Adds a status effect to the player.
	/// </summary>
	/// <param name="effect">The status effect to add.</param>
	public void AddStatusEffect(StatusEffect effect)
	{
		// Check if a similar effect already exists
		for (var i = 0; i < _activeStatusEffects.Count; i++)
		{
			var existingEffect = _activeStatusEffects[i];

			if (existingEffect.EffectType != effect.EffectType)
				continue;

			// Replace if the new effect is stronger or has longer duration
			if (!(effect.Strength > existingEffect.Strength) && !(effect.RemainingDuration > existingEffect.RemainingDuration))
				return;

			_activeStatusEffects[i] = effect;
			EmitSignal(SignalName.StatusEffectAdded, effect);

			return;
		}

		// Add new effect
		_activeStatusEffects.Add(effect);
		EmitSignal(SignalName.StatusEffectAdded, effect);
	}

	/// <summary>
	/// Removes a status effect from the player.
	/// </summary>
	/// <param name="effectType">The type of effect to remove.</param>
	public void RemoveStatusEffect(StatusEffect.EffectTypes effectType)
	{
		for (var i = _activeStatusEffects.Count - 1; i >= 0; i--)
			if (_activeStatusEffects[i].EffectType == effectType)
			{
				var effect = _activeStatusEffects[i];
				_activeStatusEffects.RemoveAt(i);
				EmitSignal(SignalName.StatusEffectRemoved, effect);
			}
	}

	/// <summary>
	/// Gets the current health value.
	/// </summary>
	/// <returns>The current health.</returns>
	public float GetHealth() => _currentHealth;

	/// <summary>
	/// Gets the current hunger value.
	/// </summary>
	/// <returns>The current hunger.</returns>
	public float GetHunger() => _currentHunger;

	/// <summary>
	/// Gets the current temperature value.
	/// </summary>
	/// <returns>The current temperature.</returns>
	public float GetTemperature() => _currentTemperature;

	/// <summary>
	/// Gets all active status effects.
	/// </summary>
	/// <returns>A list of active status effects.</returns>
	public List<StatusEffect> GetActiveStatusEffects() => [.._activeStatusEffects];
}