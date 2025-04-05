using Ceiro.Scripts.Core.Utilities;
using Godot;

namespace Ceiro.Scripts.Core.Entity.Components;

/// <summary>
/// Component that handles health and damage behavior for entities.
/// </summary>
public partial class HealthComponent : EntityComponent
{
	[Export] public float       DamageFlashDuration = 0.2f;
	[Export] public Color       DamageFlashColor    = new(1, 0, 0, 0.5f);
	[Export] public bool        ShowHealthBar       = true;
	[Export] public PackedScene HealthBarScene;

	private Sprite3D       _sprite;
	private ShaderMaterial _originalMaterial;
	private ShaderMaterial _damageMaterial;
	private float          _damageFlashTimer;
	private bool           _isFlashing;
	private Node3D         _healthBar;
	private ProgressBar    _progressBar;

	public override void Initialize(Entity entity)
	{
		base.Initialize(entity);

		if (HealthBarScene is null)
			throw new("Health bar scene not set");

		// Get the sprite reference
		_sprite = entity.GetNodeOrNull<Sprite3D>("Sprite3D") ?? throw new("Sprite3D node not found");

		// Store the original material
		_originalMaterial = _sprite.MaterialOverride as ShaderMaterial ?? throw new("Sprite3D material not found");

		// Create the damage flash material
		_damageMaterial = new();

		if (_originalMaterial is
				{
					Shader: not null
				})
		{
			_damageMaterial.Shader = _originalMaterial.Shader;

			// Copy parameters from original material
			foreach (var param in _originalMaterial.GetShaderParameterList())
				_damageMaterial.SetShaderParameter(param, _originalMaterial.GetShaderParameter(param));
		}
		else
		{
			// Create a basic shader if no original shader exists
			var shader = GD.Load<Shader>("res://shaders/damage_flash.gdshader");
			if (shader is not null)
				_damageMaterial.Shader = shader;
		}

		// Set the flash color parameter
		if (_damageMaterial.Shader is not null)
		{
			_damageMaterial.SetShaderParameter("flash_color",  DamageFlashColor);
			_damageMaterial.SetShaderParameter("flash_amount", 0.0f);
		}

		_healthBar         = GetNodeOrNull<Node3D>("HealthBarNode") ?? throw new("HealthBarNode node not found");
		_healthBar.Visible = ShowHealthBar;
		// Position the health bar above the entity
		_healthBar.Position = new(0, 2.0f, 0);
		_progressBar        = _healthBar.GetNodeOrNull<ProgressBar>("ProgressBar") ?? throw new("ProgressBar node not found");

		// Update the health bar
		UpdateHealthBar();

		// Connect to health changed signal
		entity.Connect(Entity.SignalName.EntityHealthChanged, new(this, nameof(OnHealthChanged)));
	}

	public override void Cleanup()
	{
		// Clean up health bar
		_healthBar.QueueFree();
		_healthBar = null!;

		// Disconnect signals
		Entity.Disconnect(Entity.SignalName.EntityHealthChanged, new(this, nameof(OnHealthChanged)));

		base.Cleanup();
	}

	public override void ProcessComponent(double delta)
	{
		// Handle damage flash effect
		if (!_isFlashing)
			return;

		_damageFlashTimer -= (float)delta;

		if (_damageFlashTimer <= 0)
		{
			// End flash effect
			_isFlashing = false;

			_sprite.MaterialOverride = _originalMaterial;
		}
		else
		{
			// Update flash amount based on remaining time
			var flashAmount = _damageFlashTimer / DamageFlashDuration;

			if (_damageMaterial is
					{
						Shader: not null
					})
				_damageMaterial.SetShaderParameter("flash_amount", flashAmount);
		}
	}

	public override void OnDamaged(float amount, Node source)
	{
		// Start damage flash effect
		_sprite.MaterialOverride = _damageMaterial;
		_damageFlashTimer        = DamageFlashDuration;
		_isFlashing              = true;

		// Set initial flash amount
		if (_damageMaterial.Shader is not null)
			_damageMaterial.SetShaderParameter("flash_amount", 1.0f);

		// Update health bar
		UpdateHealthBar();
	}

	public override void OnHealed(float amount) =>
			// Update health bar
			UpdateHealthBar();

	public override void OnDeath() =>
			// Update health bar
			UpdateHealthBar();

	/// <summary>
	/// Called when the entity's health changes.
	/// </summary>
	/// <param name="currentHealth">The current health.</param>
	/// <param name="maxHealth">The maximum health.</param>
	private void OnHealthChanged(float currentHealth, float maxHealth) =>
			// Update health bar
			UpdateHealthBar();

	/// <summary>
	/// Updates the health bar display.
	/// </summary>
	private void UpdateHealthBar()
	{
		_progressBar.Value = Entity.GetHealthPercentage() * 100;

		// Hide health bar if health is full
		_healthBar.Visible = Entity.GetHealthPercentage() < 1.0f;
	}
}