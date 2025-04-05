using System;
using System.Collections.Generic;
using Godot;

namespace Ceiro.Scripts.Core.World._Time;

/// <summary>
/// Manages weather effects in the game world.
/// </summary>
public partial class WeatherSystem : Node
{
	[Signal]
	public delegate void WeatherChangedEventHandler(WeatherType newWeather);

	public enum WeatherType
	{
		Clear,
		Cloudy,
		Rainy,
		Stormy,
		Snowy,
		Foggy
	}

	[Export] public float MinWeatherDuration    = 5.0f;  // Minutes
	[Export] public float MaxWeatherDuration    = 20.0f; // Minutes
	[Export] public float WeatherTransitionTime = 1.0f;  // Minutes

	private WeatherType       _currentWeather = WeatherType.Clear;
	private WeatherType       _targetWeather  = WeatherType.Clear;
	private float             _weatherTimer;
	private float             _transitionTimer;
	private bool              _isTransitioning;
	private TimeOfDaySystem   _timeSystem;
	private WorldEnvironment  _worldEnvironment;
	private GpuParticles3D    _rainParticles;
	private GpuParticles3D    _snowParticles;
	private AudioStreamPlayer _weatherAudio;

	private readonly Dictionary<WeatherType, AudioStream?>                               _weatherSounds        = new();
	private readonly Dictionary<TimeOfDaySystem.Seasons, Dictionary<WeatherType, float>> _weatherProbabilities = new();

	public override void _Ready()
	{
		// Find related systems
		_timeSystem       = GetTree().Root.FindChild("TimeOfDaySystem",  true, false) as TimeOfDaySystem  ?? throw new("TimeOfDaySystem not found");
		_worldEnvironment = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment ?? throw new("WorldEnvironment not found");

		// Find weather effect nodes
		_rainParticles = GetNodeOrNull<GpuParticles3D>("RainParticles")   ?? throw new("RainParticles not found");
		_snowParticles = GetNodeOrNull<GpuParticles3D>("SnowParticles")   ?? throw new("SnowParticles not found");
		_weatherAudio  = GetNodeOrNull<AudioStreamPlayer>("WeatherAudio") ?? throw new("WeatherAudio not found");

		// Initialize weather probabilities
		InitializeWeatherProbabilities();

		// Load weather sounds
		LoadWeatherSounds();

		// Set initial weather
		SetWeather(DetermineRandomWeather());
	}

	public override void _Process(double delta)
	{
		// Update weather timer
		_weatherTimer -= (float)delta;

		// Check if it's time to change weather
		if (_weatherTimer <= 0 && !_isTransitioning)
		{
			// Determine next weather
			_targetWeather = DetermineRandomWeather();

			// Start transition
			_isTransitioning = true;
			_transitionTimer = WeatherTransitionTime * 60; // Convert to seconds
		}

		// Handle weather transition
		if (!_isTransitioning)
			return;

		_transitionTimer -= (float)delta;
		var transitionProgress = 1.0f - _transitionTimer / (WeatherTransitionTime * 60);

		// Update weather effects based on transition
		UpdateWeatherEffects(transitionProgress);

		// Transition complete
		if (!(_transitionTimer <= 0))
			return;

		_isTransitioning = false;
		_currentWeather  = _targetWeather;
		EmitSignal(SignalName.WeatherChanged, (int)_currentWeather);

		// Set new weather duration
		var durationMinutes = MinWeatherDuration + (float)GD.RandRange(0, MaxWeatherDuration - MinWeatherDuration);
		_weatherTimer = durationMinutes * 60; // Convert to seconds
	}

	/// <summary>
	/// Initializes the weather probabilities for each season.
	/// </summary>
	private void InitializeWeatherProbabilities()
	{
		// Spring probabilities
		_weatherProbabilities[TimeOfDaySystem.Seasons.Spring] = new()
		{
			[WeatherType.Clear]  = 0.3f,
			[WeatherType.Cloudy] = 0.3f,
			[WeatherType.Rainy]  = 0.3f,
			[WeatherType.Stormy] = 0.05f,
			[WeatherType.Snowy]  = 0.0f,
			[WeatherType.Foggy]  = 0.05f
		};

		// Summer probabilities
		_weatherProbabilities[TimeOfDaySystem.Seasons.Summer] = new()
		{
			[WeatherType.Clear]  = 0.6f,
			[WeatherType.Cloudy] = 0.2f,
			[WeatherType.Rainy]  = 0.1f,
			[WeatherType.Stormy] = 0.1f,
			[WeatherType.Snowy]  = 0.0f,
			[WeatherType.Foggy]  = 0.0f
		};

		// Fall probabilities
		_weatherProbabilities[TimeOfDaySystem.Seasons.Fall] = new()
		{
			[WeatherType.Clear]  = 0.3f,
			[WeatherType.Cloudy] = 0.4f,
			[WeatherType.Rainy]  = 0.2f,
			[WeatherType.Stormy] = 0.05f,
			[WeatherType.Snowy]  = 0.0f,
			[WeatherType.Foggy]  = 0.05f
		};

		// Winter probabilities
		_weatherProbabilities[TimeOfDaySystem.Seasons.Winter] = new()
		{
			[WeatherType.Clear]  = 0.3f,
			[WeatherType.Cloudy] = 0.3f,
			[WeatherType.Rainy]  = 0.0f,
			[WeatherType.Stormy] = 0.0f,
			[WeatherType.Snowy]  = 0.3f,
			[WeatherType.Foggy]  = 0.1f
		};
	}

	/// <summary>
	/// Loads audio streams for different weather types.
	/// </summary>
	private void LoadWeatherSounds()
	{
		// Load weather sound effects
		_weatherSounds[WeatherType.Clear]  = null; // No sound for clear weather
		_weatherSounds[WeatherType.Cloudy] = null; // No sound for cloudy weather
		_weatherSounds[WeatherType.Rainy]  = GD.Load<AudioStream>("res://sounds/weather/rain.ogg");
		_weatherSounds[WeatherType.Stormy] = GD.Load<AudioStream>("res://sounds/weather/storm.ogg");
		_weatherSounds[WeatherType.Snowy]  = GD.Load<AudioStream>("res://sounds/weather/snow.ogg");
		_weatherSounds[WeatherType.Foggy]  = GD.Load<AudioStream>("res://sounds/weather/fog.ogg");
	}

	/// <summary>
	/// Determines a random weather type based on the current season.
	/// </summary>
	/// <returns>The selected weather type.</returns>
	private WeatherType DetermineRandomWeather()
	{
		// Get current season
		var currentSeason = _timeSystem.GetSeason();

		// Get probabilities for current season
		if (!_weatherProbabilities.TryGetValue(currentSeason, out var probabilities))
				// Fallback to spring if season not found
			probabilities = _weatherProbabilities[TimeOfDaySystem.Seasons.Spring];

		// Select random weather based on probabilities
		var randomValue           = (float)GD.RandRange(0.0, 1.0);
		var cumulativeProbability = 0.0f;

		foreach (var kvp in probabilities)
		{
			cumulativeProbability += kvp.Value;
			if (randomValue <= cumulativeProbability)
				return kvp.Key;
		}

		// Fallback to clear weather
		return WeatherType.Clear;
	}

	/// <summary>
	/// Sets the current weather.
	/// </summary>
	/// <param name="weatherType">The weather type to set.</param>
	public void SetWeather(WeatherType weatherType)
	{
		_currentWeather  = weatherType;
		_targetWeather   = weatherType;
		_isTransitioning = false;

		// Set weather duration
		var durationMinutes = MinWeatherDuration + (float)GD.RandRange(0, MaxWeatherDuration - MinWeatherDuration);
		_weatherTimer = durationMinutes * 60; // Convert to seconds

		// Update weather effects
		UpdateWeatherEffects(1.0f);

		// Emit signal
		EmitSignal(SignalName.WeatherChanged, (int)_currentWeather);
	}

	/// <summary>
	/// Updates the weather effects based on the current weather and transition progress.
	/// </summary>
	/// <param name="transitionProgress">The transition progress (0-1).</param>
	private void UpdateWeatherEffects(float transitionProgress)
	{
		// Update particle effects
		UpdateParticleEffects(transitionProgress);

		// Update environment effects
		UpdateEnvironmentEffects(transitionProgress);

		// Update audio
		UpdateWeatherAudio(transitionProgress);
	}

	/// <summary>
	/// Updates particle effects for weather.
	/// </summary>
	/// <param name="transitionProgress">The transition progress (0-1).</param>
	private void UpdateParticleEffects(float transitionProgress)
	{
		// Determine target emission rates
		var targetRainEmission = 0.0f;
		var targetSnowEmission = 0.0f;

		switch (_targetWeather)
		{
			case WeatherType.Rainy:
				targetRainEmission = 1.0f;
				break;

			case WeatherType.Stormy:
				targetRainEmission = 2.0f;
				break;

			case WeatherType.Snowy:
				targetSnowEmission = 1.0f;
				break;
			case WeatherType.Clear:
			case WeatherType.Cloudy:
			case WeatherType.Foggy:
			default:
				break;
		}

		// Determine current emission rates
		var currentRainEmission = 0.0f;
		var currentSnowEmission = 0.0f;

		switch (_currentWeather)
		{
			case WeatherType.Rainy:
				currentRainEmission = 1.0f;
				break;

			case WeatherType.Stormy:
				currentRainEmission = 2.0f;
				break;

			case WeatherType.Snowy:
				currentSnowEmission = 1.0f;
				break;
			case WeatherType.Clear:
			case WeatherType.Cloudy:
			case WeatherType.Foggy:
			default:
				break;
		}

		// Interpolate emission rates
		var rainEmission = Mathf.Lerp(currentRainEmission, targetRainEmission, transitionProgress);
		var snowEmission = Mathf.Lerp(currentSnowEmission, targetSnowEmission, transitionProgress);

		// Apply emission rates
		if (_rainParticles.ProcessMaterial is ParticleProcessMaterial rainProcessMaterial)
		{
			rainProcessMaterial.EmissionBoxExtents = new Vector3(20, 0.1f, 20) * rainEmission;
			_rainParticles.Amount                  = (int)(rainEmission * 100); // Adjust the multiplier as needed
			_rainParticles.Emitting                = rainEmission > 0.01f;
		}

		if (_snowParticles.ProcessMaterial is ParticleProcessMaterial snowProcessMaterial)
		{
			snowProcessMaterial.EmissionBoxExtents = new Vector3(20, 0.1f, 20) * snowEmission;
			_snowParticles.Amount                  = (int)(snowEmission * 100); // Adjust the multiplier as needed
			_snowParticles.Emitting                = snowEmission > 0.01f;
		}
	}

	/// <summary>
	/// Updates environment effects for weather.
	/// </summary>
	/// <param name="transitionProgress">The transition progress (0-1).</param>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	private void UpdateEnvironmentEffects(float transitionProgress)
	{
		if (_worldEnvironment.Environment is null)
			return;

		// Determine target fog density and color
		float targetFogDensity;
		var   targetFogColor = new Color(0.8f, 0.8f, 0.8f);
		float targetCloudiness;

		switch (_targetWeather)
		{
			case WeatherType.Clear:
				targetFogDensity = 0.0f;
				targetCloudiness = 0.0f;
				break;

			case WeatherType.Cloudy:
				targetFogDensity = 0.01f;
				targetCloudiness = 0.5f;
				break;

			case WeatherType.Rainy:
				targetFogDensity = 0.03f;
				targetFogColor   = new(0.7f, 0.7f, 0.7f);
				targetCloudiness = 0.8f;
				break;

			case WeatherType.Stormy:
				targetFogDensity = 0.05f;
				targetFogColor   = new(0.5f, 0.5f, 0.5f);
				targetCloudiness = 1.0f;
				break;

			case WeatherType.Snowy:
				targetFogDensity = 0.04f;
				targetFogColor   = new(0.9f, 0.9f, 0.95f);
				targetCloudiness = 0.9f;
				break;

			case WeatherType.Foggy:
				targetFogDensity = 0.1f;
				targetFogColor   = new(0.8f, 0.8f, 0.8f);
				targetCloudiness = 0.3f;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		// Determine current fog density and color
		float currentFogDensity;
		var   currentFogColor = new Color(0.8f, 0.8f, 0.8f);
		float currentCloudiness;

		switch (_currentWeather)
		{
			case WeatherType.Clear:
				currentFogDensity = 0.0f;
				currentCloudiness = 0.0f;
				break;

			case WeatherType.Cloudy:
				currentFogDensity = 0.01f;
				currentCloudiness = 0.5f;
				break;

			case WeatherType.Rainy:
				currentFogDensity = 0.03f;
				currentFogColor   = new(0.7f, 0.7f, 0.7f);
				currentCloudiness = 0.8f;
				break;

			case WeatherType.Stormy:
				currentFogDensity = 0.05f;
				currentFogColor   = new(0.5f, 0.5f, 0.5f);
				currentCloudiness = 1.0f;
				break;

			case WeatherType.Snowy:
				currentFogDensity = 0.04f;
				currentFogColor   = new(0.9f, 0.9f, 0.95f);
				currentCloudiness = 0.9f;
				break;

			case WeatherType.Foggy:
				currentFogDensity = 0.1f;
				currentFogColor   = new(0.8f, 0.8f, 0.8f);
				currentCloudiness = 0.3f;
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}

		// Interpolate fog parameters
		var fogDensity = Mathf.Lerp(currentFogDensity, targetFogDensity, transitionProgress);
		var fogColor   = currentFogColor.Lerp(targetFogColor, transitionProgress);
		var cloudiness = Mathf.Lerp(currentCloudiness, targetCloudiness, transitionProgress);

		// Apply fog parameters
		_worldEnvironment.Environment.FogEnabled          = fogDensity > 0.001f;
		_worldEnvironment.Environment.FogDensity          = fogDensity;
		_worldEnvironment.Environment.VolumetricFogAlbedo = fogColor;

		// Apply sky cloudiness if using procedural sky
		if (_worldEnvironment.Environment.Sky is
				not
				{
					SkyMaterial: ProceduralSkyMaterial skyMaterial
				})
			return;

		// Darken sky based on cloudiness
		skyMaterial.SkyTopColor     = skyMaterial.SkyTopColor.Darkened(cloudiness     * 0.5f);
		skyMaterial.SkyHorizonColor = skyMaterial.SkyHorizonColor.Darkened(cloudiness * 0.5f);

		// Reduce sun brightness based on cloudiness
		skyMaterial.SunAngleMax = Mathf.Max(1.0f, skyMaterial.SunAngleMax * (1.0f - cloudiness * 0.8f));
	}

	/// <summary>
	/// Updates weather audio.
	/// </summary>
	/// <param name="transitionProgress">The transition progress (0-1).</param>
	private void UpdateWeatherAudio(float transitionProgress)
	{
		// Get target audio stream
		AudioStream? targetStream = null;
		if (_weatherSounds.TryGetValue(_targetWeather, out var stream))
			targetStream = stream;

		// If transitioning to a new sound
		if (targetStream == _weatherAudio.Stream)
			return;

		// Fade out current sound
		_weatherAudio.VolumeDb = Mathf.Lerp(0, -80, transitionProgress);

		// Switch to new sound when transition is complete
		if (!(transitionProgress >= 1.0f))
			return;

		_weatherAudio.Stream   = targetStream;
		_weatherAudio.VolumeDb = 0;

		if (targetStream != null)
			_weatherAudio.Play();
		else
			_weatherAudio.Stop();
	}

	/// <summary>
	/// Gets the current weather type.
	/// </summary>
	/// <returns>The current weather type.</returns>
	public WeatherType GetCurrentWeather() => _currentWeather;

	/// <summary>
	/// Gets a temperature modifier factor based on the current weather.
	/// </summary>
	/// <returns>A temperature factor (-1 to 1).</returns>
	public float GetTemperatureFactor() => _currentWeather switch
	{
		WeatherType.Clear  => 0.5f,
		WeatherType.Cloudy => 0.0f,
		WeatherType.Rainy  => -0.5f,
		WeatherType.Stormy => -0.7f,
		WeatherType.Snowy  => -1.0f,
		WeatherType.Foggy  => -0.3f,
		_                  => 0.0f
	};
}
