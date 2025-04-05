using Godot;

namespace Ceiro.Scripts.Core.World.Rendering;

/// <summary>
/// Manages the isometric rendering system for the game, handling camera positioning,
/// sprite billboarding, and proper layering of objects in the isometric view.
/// </summary>
public partial class IsometricRenderingSystem : Node3D
{
	[Export] public float CameraAngle        = 45.0f;
	[Export] public float CameraDistance     = 10.0f;
	[Export] public float CameraHeight       = 10.0f;
	[Export] public bool  LockCameraRotation = true;

	private Camera3D _camera;

	public override void _Ready()
	{
		// Get or create the camera
		_camera = GetNodeOrNull<Camera3D>("Camera3D");

		if (_camera is null)
		{
			_camera      = new();
			_camera.Name = "Camera3D";
			AddChild(_camera);
		}

		// Set up the isometric camera
		SetupIsometricCamera();
	}

	/// <summary>
	/// Sets up the camera for isometric rendering with the specified angle and distance.
	/// </summary>
	private void SetupIsometricCamera()
	{
		// Position the camera
		var cameraPosition = new Vector3(-Mathf.Sin(Mathf.DegToRad(CameraAngle)) * CameraDistance,
		                                 CameraHeight,
		                                 -Mathf.Cos(Mathf.DegToRad(CameraAngle)) * CameraDistance);

		_camera.GlobalPosition = cameraPosition;

		// Look at the origin (or can be set to follow a target)
		_camera.LookAt(Vector3.Zero, Vector3.Up);

		// Set to perspective projection for 3D world with 2D sprites
		_camera.Projection = Camera3D.ProjectionType.Perspective;

		// Optional: Adjust field of view for desired perspective
		_camera.Fov = 45.0f;
	}

	/// <summary>
	/// Updates the camera position to follow a target while maintaining the isometric angle.
	/// </summary>
	/// <param name="target">The target position to follow.</param>
	/// <param name="delta">The time delta for smooth movement.</param>
	/// <param name="smoothness">The smoothness factor for camera movement.</param>
	public void UpdateCameraPosition(Vector3 target, float delta, float smoothness = 5.0f)
	{
		// Calculate the desired camera position based on the target
		var desiredPosition = new Vector3(target.X - Mathf.Sin(Mathf.DegToRad(CameraAngle)) * CameraDistance,
		                                  target.Y + CameraHeight,
		                                  target.Z - Mathf.Cos(Mathf.DegToRad(CameraAngle)) * CameraDistance);

		// Apply smoothing
		var smoothFactor = Mathf.Clamp(smoothness * delta, 0f, 1f);
		_camera.GlobalPosition = _camera.GlobalPosition.Lerp(desiredPosition, smoothFactor);

		// Ensure the camera is always looking at the target
		if (LockCameraRotation)
			_camera.LookAt(target, Vector3.Up);
	}
}