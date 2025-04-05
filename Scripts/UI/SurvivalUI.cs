using Ceiro.Scripts.Gameplay.Player.Survival;
using Godot;
using TimeOfDaySystem = Ceiro.Scripts.Core.World._Time.TimeOfDaySystem;
using WeatherSystem = Ceiro.Scripts.Core.World._Time.WeatherSystem;

namespace Ceiro.Scripts.UI;

/// <summary>
/// UI for displaying survival information to the player.
/// </summary>
public partial class SurvivalUi : Control
{
	[Export] public NodePath? SurvivalSystemPath;

	private SurvivalSystem     _survivalSystem;
	private ProgressBar        _healthBar;
	private ProgressBar        _hungerBar;
	private TextureProgressBar _temperatureGauge;
	private Label              _temperatureLabel;
	private HBoxContainer      _statusEffectsContainer;
	private Label              _timeLabel;
	private Label              _seasonLabel;
	private Label              _weatherLabel;

	public override void _Ready()
	{
		SurvivalSystem? survivalSystem;
		// Get the survival system
		if (!string.IsNullOrEmpty(SurvivalSystemPath))
			survivalSystem = GetNode<SurvivalSystem>(SurvivalSystemPath);
		else
				// Try to find the survival system in the scene
			survivalSystem = GetTree().Root.FindChild("SurvivalSystem", true, false) as SurvivalSystem;

		if (survivalSystem is null)
			throw new("SurvivalUI: Failed to find SurvivalSystem!");

		_survivalSystem = survivalSystem!;

		// Get UI elements
		_healthBar              = GetNodeOrNull<ProgressBar>("VBoxContainer/HealthBar")                ?? throw new("SurvivalUI: Failed to find HealthBar!");
		_hungerBar              = GetNodeOrNull<ProgressBar>("VBoxContainer/HungerBar")                ?? throw new("SurvivalUI: Failed to find HungerBar!");
		_temperatureGauge       = GetNodeOrNull<TextureProgressBar>("VBoxContainer/TemperatureGauge")  ?? throw new("SurvivalUI: Failed to find TemperatureGauge!");
		_temperatureLabel       = GetNodeOrNull<Label>("VBoxContainer/TemperatureLabel")               ?? throw new("SurvivalUI: Failed to find TemperatureLabel!");
		_statusEffectsContainer = GetNodeOrNull<HBoxContainer>("VBoxContainer/StatusEffectsContainer") ?? throw new("SurvivalUI: Failed to find StatusEffectsContainer!");
		_timeLabel              = GetNodeOrNull<Label>("VBoxContainer/TimeLabel")                      ?? throw new("SurvivalUI: Failed to find TimeLabel!");
		_seasonLabel            = GetNodeOrNull<Label>("VBoxContainer/SeasonLabel")                    ?? throw new("SurvivalUI: Failed to find SeasonLabel!");
		_weatherLabel           = GetNodeOrNull<Label>("VBoxContainer/WeatherLabel")                   ?? throw new("SurvivalUI: Failed to find WeatherLabel!");

		// Connect signals
		_survivalSystem.Connect(SurvivalSystem.SignalName.HealthChanged,       new(this, nameof(OnHealthChanged)));
		_survivalSystem.Connect(SurvivalSystem.SignalName.HungerChanged,       new(this, nameof(OnHungerChanged)));
		_survivalSystem.Connect(SurvivalSystem.SignalName.TemperatureChanged,  new(this, nameof(OnTemperatureChanged)));
		_survivalSystem.Connect(SurvivalSystem.SignalName.StatusEffectAdded,   new(this, nameof(OnStatusEffectAdded)));
		_survivalSystem.Connect(SurvivalSystem.SignalName.StatusEffectRemoved, new(this, nameof(OnStatusEffectRemoved)));

		// Connect to time and weather systems if available
		var timeSystem = GetTree().Root.FindChild("TimeOfDaySystem", true, false) as TimeOfDaySystem ?? throw new("SurvivalUI: Failed to find TimeOfDaySystem!");

		timeSystem.Connect(TimeOfDaySystem.SignalName.TimeChanged,   new(this, nameof(OnTimeChanged)));
		timeSystem.Connect(TimeOfDaySystem.SignalName.SeasonChanged, new(this, nameof(OnSeasonChanged)));

		var weatherSystem = GetTree().Root.FindChild("WeatherSystem", true, false) as WeatherSystem ?? throw new("SurvivalUI: Failed to find WeatherSystem!");
		weatherSystem.Connect(WeatherSystem.SignalName.WeatherChanged, new(this, nameof(OnWeatherChanged)));

		// Initialize UI
		UpdateUi();
	}

	/// <summary>
	/// Updates all UI elements.
	/// </summary>
	private void UpdateUi()
	{
		// Update health bar
		_healthBar.Value = _survivalSystem.GetHealth() / _survivalSystem.MaxHealth * 100;

		// Update hunger bar
		_hungerBar.Value = _survivalSystem.GetHunger() / _survivalSystem.MaxHunger * 100;

		// Update temperature gauge
		UpdateTemperatureGauge(_survivalSystem.GetTemperature());

		// Update status effects
		UpdateStatusEffects();

		// Update time and season
		UpdateTimeAndSeason();

		// Update weather
		UpdateWeather();
	}

	/// <summary>
	/// Updates the temperature gauge.
	/// </summary>
	/// <param name="temperature">The current temperature.</param>
	private void UpdateTemperatureGauge(float temperature)
	{
		// Map temperature to gauge range (e.g., -30 to 50 Celsius maps to 0-100%)
		var normalizedTemp = (temperature + 30) / 80;
		_temperatureGauge.Value = normalizedTemp * 100;

		// Update temperature label
		_temperatureLabel.Text = $"{temperature:F1}°C";

		// Set color based on temperature
		if (temperature < _survivalSystem.OptimalTemperature - _survivalSystem.TemperatureRange)
				// Cold
			_temperatureLabel.Modulate = new(0.5f, 0.5f, 1.0f);
		else if (temperature > _survivalSystem.OptimalTemperature + _survivalSystem.TemperatureRange)
				// Hot
			_temperatureLabel.Modulate = new(1.0f, 0.5f, 0.5f);
		else
				// Comfortable
			_temperatureLabel.Modulate = new(1.0f, 1.0f, 1.0f);
	}

	/// <summary>
	/// Updates the status effects display.
	/// </summary>
	private void UpdateStatusEffects()
	{
		// Clear existing status effects
		foreach (var child in _statusEffectsContainer.GetChildren())
			child.QueueFree();

		// Add current status effects
		foreach (var effect in _survivalSystem.GetActiveStatusEffects())
		{
			var effectIcon = new TextureRect
			{
				Texture     = effect.Icon ?? GD.Load<Texture2D>("res://textures/ui/default_effect_icon.png"),
				StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
				TooltipText = $"{effect.DisplayName}: {effect.Description}"
			};

			_statusEffectsContainer.AddChild(effectIcon);
		}
	}

	/// <summary>
	/// Updates the time and season display.
	/// </summary>
	private void UpdateTimeAndSeason()
	{
		var timeSystem = GetTree().Root.FindChild("TimeOfDaySystem", true, false) as TimeOfDaySystem;

		if (timeSystem is null)
			return;

		_timeLabel.Text = $"Time: {timeSystem.GetTimeString()}";

		var seasonName = timeSystem.GetSeason() switch
		{
			TimeOfDaySystem.Seasons.Spring => "Spring",
			TimeOfDaySystem.Seasons.Summer => "Summer",
			TimeOfDaySystem.Seasons.Fall   => "Fall",
			TimeOfDaySystem.Seasons.Winter => "Winter",
			_                              => ""
		};

		_seasonLabel.Text = $"Season: {seasonName} (Day {timeSystem.GetDay()})";
	}

	/// <summary>
	/// Updates the weather display.
	/// </summary>
	private void UpdateWeather()
	{
		var weatherSystem = GetTree().Root.FindChild("WeatherSystem", true, false) as WeatherSystem ?? throw new("SurvivalUI: Failed to find WeatherSystem!");

		var weatherName = weatherSystem.GetCurrentWeather() switch
		{
			WeatherSystem.WeatherType.Clear  => "Clear",
			WeatherSystem.WeatherType.Cloudy => "Cloudy",
			WeatherSystem.WeatherType.Rainy  => "Rainy",
			WeatherSystem.WeatherType.Stormy => "Stormy",
			WeatherSystem.WeatherType.Snowy  => "Snowy",
			WeatherSystem.WeatherType.Foggy  => "Foggy",
			_                                => ""
		};

		_weatherLabel.Text = $"Weather: {weatherName}";
	}

	/// <summary>
	/// Called when the player's health changes.
	/// </summary>
	/// <param name="currentHealth">The current health.</param>
	/// <param name="maxHealth">The maximum health.</param>
	private void OnHealthChanged(float currentHealth, float maxHealth) => _healthBar.Value = currentHealth / maxHealth * 100;

	/// <summary>
	/// Called when the player's hunger changes.
	/// </summary>
	/// <param name="currentHunger">The current hunger.</param>
	/// <param name="maxHunger">The maximum hunger.</param>
	private void OnHungerChanged(float currentHunger, float maxHunger) => _hungerBar.Value = currentHunger / maxHunger * 100;

	/// <summary>
	/// Called when the player's temperature changes.
	/// </summary>
	/// <param name="currentTemperature">The current temperature.</param>
	private void OnTemperatureChanged(float currentTemperature) => UpdateTemperatureGauge(currentTemperature);

	/// <summary>
	/// Called when a status effect is added.
	/// </summary>
	/// <param name="effect">The added effect.</param>
	private void OnStatusEffectAdded(StatusEffect effect) => UpdateStatusEffects();

	/// <summary>
	/// Called when a status effect is removed.
	/// </summary>
	/// <param name="effect">The removed effect.</param>
	private void OnStatusEffectRemoved(StatusEffect effect) => UpdateStatusEffects();

	/// <summary>
	/// Called when the time changes.
	/// </summary>
	/// <param name="time">The current time.</param>
	/// <param name="timeString">The time string.</param>
	private void OnTimeChanged(float time, string timeString) => _timeLabel.Text = $"Time: {timeString}";

	/// <summary>
	/// Called when the season changes.
	/// </summary>
	/// <param name="season">The new season.</param>
	private void OnSeasonChanged(int season) => UpdateTimeAndSeason();

	/// <summary>
	/// Called when the weather changes.
	/// </summary>
	/// <param name="weather">The new weather.</param>
	private void OnWeatherChanged(int weather) => UpdateWeather();
}