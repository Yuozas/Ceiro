using Godot;

namespace Ceiro.Scripts.Core.World._Time;

/// <summary>
/// Manages the time of day cycle, affecting lighting, temperature, and gameplay.
/// </summary>
public partial class TimeOfDaySystem : Node
{
	[Signal]
	public delegate void TimeChangedEventHandler(float time, string timeString);

	[Signal]
	public delegate void DayChangedEventHandler(int day);

	[Signal]
	public delegate void SeasonChangedEventHandler(Seasons season);

	public enum Seasons
	{
		Spring,
		Summer,
		Fall,
		Winter
	}

	[Export] public float DayLengthInMinutes = 20.0f;
	[Export] public int   DaysPerSeason      = 10;
	[Export] public float StartTime          = 6.0f; // 6 AM
	[Export] public bool  PauseTimeAtNight;

	private float              _currentTime; // 0-24 hours
	private int                _currentDay    = 1;
	private Seasons            _currentSeason = Seasons.Spring;
	private DirectionalLight3D _sunLight;
	private WorldEnvironment   _worldEnvironment;
	private bool               _isNight;

	// Time factors
	private const float DAWN_START = 5.0f;
	private const float DAWN_END   = 7.0f;
	private const float DUSK_START = 18.0f;
	private const float DUSK_END   = 20.0f;

	public override void _Ready()
	{
		// Initialize time
		_currentTime = StartTime;

		// Find the sunlight
		_sunLight = GetTree().Root.FindChild("SunLight", true, false) as DirectionalLight3D ?? throw new("Sun light not found!");

		// Find the world environment
		_worldEnvironment = GetTree().Root.FindChild("WorldEnvironment", true, false) as WorldEnvironment ?? throw new("World environment not found!");

		// Update initial lighting
		UpdateLighting();

		// Emit initial signals
		EmitSignal(SignalName.TimeChanged,   _currentTime, GetTimeString());
		EmitSignal(SignalName.DayChanged,    _currentDay);
		EmitSignal(SignalName.SeasonChanged, (int)_currentSeason);
	}

	public override void _Process(double delta)
	{
		// Skip time progression if paused at night
		if (PauseTimeAtNight && _isNight)
			return;

		// Calculate time progression
		var timeIncrement = 24.0f / (DayLengthInMinutes * 60.0f) * (float)delta;
		_currentTime += timeIncrement;

		// Check for day change
		if (_currentTime >= 24.0f)
		{
			_currentTime -= 24.0f;
			_currentDay++;
			EmitSignal(SignalName.DayChanged, _currentDay);

			// Check for season change
			if (_currentDay > DaysPerSeason)
			{
				_currentDay    = 1;
				_currentSeason = (Seasons)(((int)_currentSeason + 1) % 4);
				EmitSignal(SignalName.SeasonChanged, (int)_currentSeason);
			}
		}

		// Update lighting
		UpdateLighting();

		// Emit time changed signal (only on significant changes to avoid spam)
		if (Mathf.FloorToInt(_currentTime * 60) % 15 == 0) // Every 15 in-game minutes
			EmitSignal(SignalName.TimeChanged, _currentTime, GetTimeString());
	}

	/// <summary>
	/// Updates the lighting based on the current time of day.
	/// </summary>
	private void UpdateLighting()
	{
		// Calculate sun angle based on time
		var sunAngle = (_currentTime - 12.0f) * 15.0f; // 15 degrees per hour, noon is 0

		// Set sun rotation
		_sunLight.Rotation = new(Mathf.DegToRad(sunAngle), Mathf.DegToRad(-30), 0);

		// Calculate light intensity based on time
		float lightIntensity;

		switch (_currentTime)
		{
			case >= DAWN_START and <= DAWN_END:
				// Dawn transition
				lightIntensity = Mathf.InverseLerp(DAWN_START, DAWN_END, _currentTime);
				_isNight       = false;
				break;
			case > DAWN_END and < DUSK_START:
				// Daytime
				lightIntensity = 1.0f;
				_isNight       = false;
				break;
			case >= DUSK_START and <= DUSK_END:
				// Dusk transition
				lightIntensity = Mathf.InverseLerp(DUSK_END, DUSK_START, _currentTime);
				_isNight       = true;
				break;
			default:
				// Nighttime
				lightIntensity = 0.0f;
				_isNight       = true;
				break;
		}

		// Apply light intensity
		_sunLight.LightEnergy = lightIntensity * 1.5f;

		// Update environment (sky, ambient light, etc.)
		UpdateEnvironment(lightIntensity);
	}

	/// <summary>
	/// Updates the world environment based on the time of day.
	/// </summary>
	/// <param name="lightIntensity">The current light intensity (0-1).</param>
	private void UpdateEnvironment(float lightIntensity)
	{
		if (_worldEnvironment.Environment is null)
			return;

		// Update ambient light
		_worldEnvironment.Environment.AmbientLightEnergy = 0.2f + lightIntensity * 0.3f;

		// Update sky
		if (_worldEnvironment.Environment.Sky is not
				{
					SkyMaterial: ProceduralSkyMaterial skyMaterial
				})
			return;

		// Day sky color
		var daySkyColor = new Color(0.4f, 0.6f, 1.0f);

		// Night sky color
		var nightSkyColor = new Color(0.05f, 0.05f, 0.1f);

		// Dawn/dusk sky color
		var sunsetSkyColor = new Color(0.8f, 0.5f, 0.2f);

		// Calculate sky color based on time
		Color skyColor;

		switch (_currentTime)
		{
			case >= DAWN_START and <= DAWN_END:
			{
				// Dawn transition
				var t = Mathf.InverseLerp(DAWN_START, DAWN_END, _currentTime);
				skyColor = nightSkyColor.Lerp(sunsetSkyColor, t * 0.5f).Lerp(daySkyColor, t * 0.5f);
				break;
			}
			case > DAWN_END and < DUSK_START:
				// Daytime
				skyColor = daySkyColor;
				break;
			case >= DUSK_START and <= DUSK_END:
			{
				// Dusk transition
				var t = Mathf.InverseLerp(DUSK_START, DUSK_END, _currentTime);
				skyColor = daySkyColor.Lerp(sunsetSkyColor, t * 0.5f).Lerp(nightSkyColor, t * 0.5f);
				break;
			}
			default:
				// Nighttime
				skyColor = nightSkyColor;
				break;
		}

		// Apply sky color
		skyMaterial.SkyTopColor = skyColor;

		// Adjust horizon color
		skyMaterial.SkyHorizonColor = skyColor.Lerp(new(1, 1, 1), 0.2f);

		// Adjust ground colors
		skyMaterial.GroundBottomColor  = skyColor.Darkened(0.4f);
		skyMaterial.GroundHorizonColor = skyMaterial.SkyHorizonColor.Darkened(0.2f);

		// Adjust sun properties
		// Deprecated.
		// skyMaterial.SunAngleDegrees = 1.0f + lightIntensity * 4.0f;
		skyMaterial.SetSunAngleMax(1.0f + lightIntensity * 4.0f); // Sun size from 1° to 5°
		skyMaterial.SetSunCurve(0.1f);                            // Fixed curve for sun edge softness, adjust as needed
	}

	/// <summary>
	/// Gets a string representation of the current time.
	/// </summary>
	/// <returns>The time string in 12-hour format.</returns>
	public string GetTimeString()
	{
		var hours   = Mathf.FloorToInt(_currentTime);
		var minutes = Mathf.FloorToInt((_currentTime - hours) * 60);

		var period       = hours >= 12 ? "PM" : "AM";
		var displayHours = hours % 12;
		if (displayHours == 0)
			displayHours = 12;

		return $"{displayHours}:{minutes:D2} {period}";
	}

	/// <summary>
	/// Gets the current time of day.
	/// </summary>
	/// <returns>The current time (0-24).</returns>
	public float GetTime() => _currentTime;

	/// <summary>
	/// Gets the current day.
	/// </summary>
	/// <returns>The current day.</returns>
	public int GetDay() => _currentDay;

	/// <summary>
	/// Gets the current season.
	/// </summary>
	/// <returns>The current season.</returns>
	public Seasons GetSeason() => _currentSeason;

	/// <summary>
	/// Checks if it's currently night time.
	/// </summary>
	/// <returns>True if it's night, false otherwise.</returns>
	public bool IsNight() => _isNight;

	/// <summary>
	/// Gets a temperature modifier factor based on the time of day.
	/// </summary>
	/// <returns>A temperature factor (-1 to 1).</returns>
	public float GetTemperatureFactor()
	{
		switch (_currentTime)
		{
			// Coldest at night/early morning (3-5 AM), warmest in afternoon (1-3 PM)
			case >= 3 and <= 5:
				return -1.0f; // Coldest
			case >= 13 and <= 15:
				return 1.0f; // Warmest
			// Morning warming
			case > 5 and < 13:
				return Mathf.Lerp(-1.0f, 1.0f, (_currentTime - 5) / 8);
			case > 15 and < 3 + 24:
			{
				// Evening cooling
				var t = _currentTime;
				if (t < 24)
					return Mathf.Lerp(1.0f, -1.0f, (t - 15) / 12);

				return Mathf.Lerp(1.0f, -1.0f, (t - 15 - 24) / 12);
			}
			default:
				return 0.0f; // Fallback
		}
	}

	/// <summary>
	/// Sets the time of day.
	/// </summary>
	/// <param name="time">The time to set (0-24).</param>
	public void SetTime(float time)
	{
		_currentTime = Mathf.Clamp(time, 0, 24);
		UpdateLighting();
		EmitSignal(SignalName.TimeChanged, _currentTime, GetTimeString());
	}

	/// <summary>
	/// Sets the current day.
	/// </summary>
	/// <param name="day">The day to set.</param>
	public void SetDay(int day)
	{
		if (day < 1)
			return;

		_currentDay = day;

		// Update season if needed
		var seasonIndex = (day - 1) / DaysPerSeason;
		var newSeason   = (Seasons)(seasonIndex % 4);

		if (newSeason != _currentSeason)
		{
			_currentSeason = newSeason;
			EmitSignal(SignalName.SeasonChanged, (int)_currentSeason);
		}

		EmitSignal(SignalName.DayChanged, _currentDay);
	}

	/// <summary>
	/// Sets the current season.
	/// </summary>
	/// <param name="season">The season to set.</param>
	public void SetSeason(Seasons season)
	{
		if (_currentSeason == season)
			return;

		_currentSeason = season;
		EmitSignal(SignalName.SeasonChanged, (int)_currentSeason);
	}
}