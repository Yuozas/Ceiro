using Ceiro.Scripts.Core.Utilities;
using Godot;

namespace Ceiro.Scripts.Core.Entity.Components;

/// <summary>
/// Component that handles interaction behavior for entities.
/// </summary>
public partial class InteractionComponent : EntityComponent
{
	[Export] public string InteractionPrompt   = "Press E to interact";
	[Export] public float  InteractionCooldown = 1.0f;
	[Export] public bool   HighlightOnHover    = true;
	[Export] public Color  HighlightColor      = new(1, 1, 0, 0.3f);

	private float          _interactionCooldownTimer;
	private bool           _isInteractable = true;
	private bool           _isHighlighted;
	private Sprite3D       _sprite;
	private ShaderMaterial _originalMaterial;
	private ShaderMaterial _highlightMaterial;

	public override void Initialize(Entity entity)
	{
		base.Initialize(entity);

		// Get the sprite reference
		_sprite = entity.GetNodeOrNull<Sprite3D>("Sprite3D") ?? throw new("Sprite3D node not found");

		if (!HighlightOnHover)
			return;

		// Store the original material
		_originalMaterial = _sprite.MaterialOverride as ShaderMaterial ?? throw new("Sprite3D material not found");

		// Create the highlight material
		_highlightMaterial = new();

		if (_originalMaterial is
				{
					Shader: not null
				})
		{
			_highlightMaterial.Shader = _originalMaterial.Shader;

			// Copy parameters from original material
			foreach (var param in _originalMaterial.GetShaderParameterList())
				_highlightMaterial.SetShaderParameter(param, _originalMaterial.GetShaderParameter(param));
		}
		else
		{
			// Create a basic shader if no original shader exists
			var shader = GD.Load<Shader>("res://shaders/highlight.gdshader");
			if (shader is not null)
				_highlightMaterial.Shader = shader;
		}

		// Set the highlight color parameter
		if (_highlightMaterial.Shader is null)
			return;

		_highlightMaterial.SetShaderParameter("highlight_color",  HighlightColor);
		_highlightMaterial.SetShaderParameter("highlight_amount", 0.0f);
	}

	public override void ProcessComponent(double delta)
	{
		// Handle interaction cooldown
		if (_isInteractable)
			return;

		_interactionCooldownTimer -= (float)delta;

		if (_interactionCooldownTimer <= 0)
			_isInteractable = true;
	}

	public override bool OnInteract(Entity interactor)
	{
		if (!_isInteractable)
			return false;

		// Start interaction cooldown
		_isInteractable           = false;
		_interactionCooldownTimer = InteractionCooldown;

		return true;
	}

	/// <summary>
	/// Sets the highlight state of the entity.
	/// </summary>
	/// <param name="highlighted">Whether the entity should be highlighted.</param>
	public void SetHighlighted(bool highlighted)
	{
		if (_isHighlighted == highlighted || !HighlightOnHover)
			return;

		_isHighlighted = highlighted;

		if (highlighted)
		{
			_sprite.MaterialOverride = _highlightMaterial;

			// Set highlight amount
			if (_highlightMaterial.Shader is not null)
				_highlightMaterial.SetShaderParameter("highlight_amount", 1.0f);
		}
		else
		{
			_sprite.MaterialOverride = _originalMaterial;
		}
	}

	/// <summary>
	/// Gets the interaction prompt.
	/// </summary>
	/// <returns>The interaction prompt.</returns>
	public string GetInteractionPrompt() => InteractionPrompt;

	/// <summary>
	/// Checks if the entity is currently interactable.
	/// </summary>
	/// <returns>True if the entity is interactable, false otherwise.</returns>
	public bool IsEntityInteractable() => _isInteractable
	                                   && Entity is
	                                      {
		                                      IsInteractable: true
	                                      };
}