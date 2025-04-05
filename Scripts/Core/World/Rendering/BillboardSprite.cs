using System;
using Godot;

namespace Ceiro.Scripts.Core.World.Rendering;

/// <summary>
/// Handles the billboarding behavior for sprites in the isometric world,
/// ensuring they always face the camera or maintain a consistent angle.
/// </summary>
public partial class BillboardSprite : Node3D
{
	public enum BillboardMode
	{
		FullBillboard,  // Always faces camera directly (for characters)
		YAxisBillboard, // Rotates only around Y axis (for trees, structures)
		FixedAngle      // Maintains a fixed angle relative to the world (for environment)
	}

	[Export] public BillboardMode Mode = BillboardMode.FullBillboard;
	[Export] public float         FixedAngle;
	[Export] public bool          UsePixelSnap = true;
	[Export] public float         PixelSize    = 0.1f;

	private Sprite3D _sprite;
	private Camera3D _camera;

	public override void _Ready()
	{
		// Get the sprite and camera references
		_sprite = GetNodeOrNull<Sprite3D>("Sprite3D");

		if (_sprite is null)
			throw new("BillboardSprite: No Sprite3D found in scene!");

		_camera = GetViewport().GetCamera3D();

		if (_camera is null)
			throw new("BillboardSprite: No camera found in scene!");

		// Configure the sprite based on the billboard mode
		ConfigureSprite();
	}

	/// <summary>
	/// Configures the sprite based on the selected billboard mode.
	/// </summary>
	private void ConfigureSprite()
	{
		switch (Mode)
		{
			case BillboardMode.FullBillboard:
				_sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Enabled;
				break;
			case BillboardMode.YAxisBillboard:
				_sprite.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
				break;

			case BillboardMode.FixedAngle:
				_sprite.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
				// Set the initial rotation based on the fixed angle
				_sprite.Rotation = new(0, Mathf.DegToRad(FixedAngle), 0);
				break;
			default:
				throw new InvalidOperationException($"Invalid BillboardMode: {Mode}");
		}

		// Configure pixel snapping if enabled
		if (UsePixelSnap)
			_sprite.PixelSize = PixelSize;
	}

	public override void _Process(double delta)
	{
		switch (Mode)
		{
			// For fixed angle mode, we need to manually maintain the angle
			case BillboardMode.FixedAngle:
				_sprite.Rotation = new(0, Mathf.DegToRad(FixedAngle), 0);
				break;
			// For Y-axis billboard, we need to ensure it's always facing the camera horizontally
			case BillboardMode.YAxisBillboard:
			{
				var dirToCamera = (_camera.GlobalPosition - GlobalPosition).Normalized();
				dirToCamera.Y = 0; // Ignore vertical component

				if (dirToCamera.Length() > 0.001f)
				{
					var lookRotation = new Transform3D().LookingAt(dirToCamera, Vector3.Up);
					_sprite.GlobalTransform = new(lookRotation.Basis, _sprite.GlobalPosition);
				}

				break;
			}
			case BillboardMode.FullBillboard:
				// No need for manual handling - Godot's built-in billboard mode handles this
				break;
			default:
				throw new InvalidOperationException($"Invalid BillboardMode: {Mode}");
		}
	}
}