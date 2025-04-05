// using Godot;
//
// public partial class Player : CharacterBody3D
// {
// 	private         Camera3D        _camera;
// 	private         Vector3         _initialCameraOffset;
// 	private         AnimationPlayer _animationPlayer;
// 	[Export] public float           Speed            = 5.0f;
// 	[Export] public float           Gravity          = 9.8f;
// 	[Export] public float           CameraSmoothness = 5.0f;
//
// 	// Animation states
// 	private const string ANIM_IDLE_FORWARD  = "idle_forward";
// 	private const string ANIM_IDLE_BACKWARD = "idle_backward";
// 	private const string ANIM_IDLE_LEFT     = "idle_left";
// 	private const string ANIM_IDLE_RIGHT    = "idle_right";
// 	private const string ANIM_WALK_FORWARD  = "walk_forward";
// 	private const string ANIM_WALK_BACKWARD = "walk_backward";
// 	private const string ANIM_WALK_LEFT     = "walk_left";
// 	private const string ANIM_WALK_RIGHT    = "walk_right";
//
// 	// Keep track of the last direction for idle animations
// 	private enum FacingDirection
// 	{
// 		Forward,
// 		Backward,
// 		Left,
// 		Right
// 	}
//
// 	private FacingDirection _lastDirection = FacingDirection.Forward;
//
// 	public override void _Ready()
// 	{
// 		_camera          = GetNode<Camera3D>("Camera3D");
// 		_animationPlayer = GetNode<AnimationPlayer>("AnimationPlayer");
// 		// Store the initial offset between camera and player
// 		_initialCameraOffset = _camera.GlobalPosition - GlobalPosition;
//
// 		// Initially play the forward idle animation
// 		PlayAnimationIfExists(ANIM_IDLE_FORWARD);
// 	}
//
// 	public override void _PhysicsProcess(double delta)
// 	{
// 		// Apply gravity
// 		var velocity = Velocity;
// 		if (!IsOnFloor())
// 			velocity.Y -= Gravity * (float)delta;
//
// 		// Get input direction
// 		var inputDir  = Input.GetVector("move_left", "move_right", "move_forward", "move_backward");
// 		var direction = new Vector3(inputDir.X, 0, inputDir.Y).Normalized();
//
// 		// Handle animations based on movement direction
// 		HandleDirectionalAnimations(inputDir);
//
// 		// Move in camera's forward direction (optional tweak)
// 		if (direction != Vector3.Zero)
// 		{
// 			velocity.X = direction.X * Speed;
// 			velocity.Z = direction.Z * Speed;
// 		}
// 		else
// 		{
// 			velocity.X = Mathf.MoveToward(velocity.X, 0, Speed);
// 			velocity.Z = Mathf.MoveToward(velocity.Z, 0, Speed);
// 		}
//
// 		// Apply movement
// 		Velocity = velocity;
// 		MoveAndSlide();
// 	}
//
// 	private void HandleDirectionalAnimations(Vector2 inputDir)
// 	{
// 		// If no movement, play idle animation based on the last direction
// 		if (inputDir == Vector2.Zero)
// 		{
// 			switch (_lastDirection)
// 			{
// 				case FacingDirection.Forward:
// 					PlayAnimationIfExists(ANIM_IDLE_FORWARD);
// 					break;
// 				case FacingDirection.Backward:
// 					PlayAnimationIfExists(ANIM_IDLE_BACKWARD);
// 					break;
// 				case FacingDirection.Left:
// 					PlayAnimationIfExists(ANIM_IDLE_LEFT);
// 					break;
// 				case FacingDirection.Right:
// 					PlayAnimationIfExists(ANIM_IDLE_RIGHT);
// 					break;
// 			}
//
// 			return;
// 		}
//
// 		// Determine the dominant direction
// 		var absX = Mathf.Abs(inputDir.X);
// 		var absY = Mathf.Abs(inputDir.Y);
//
// 		if (absX > absY)
// 		{
// 			// Horizontal movement is dominant
// 			if (inputDir.X > 0)
// 			{
// 				_lastDirection = FacingDirection.Right;
// 				PlayAnimationIfExists(ANIM_WALK_RIGHT);
// 			}
// 			else
// 			{
// 				_lastDirection = FacingDirection.Left;
// 				PlayAnimationIfExists(ANIM_WALK_LEFT);
// 			}
// 		}
// 		else
// 		{
// 			// Vertical movement is dominant
// 			if (inputDir.Y > 0) // In Godot, positive Y is backward
// 			{
// 				_lastDirection = FacingDirection.Backward;
// 				PlayAnimationIfExists(ANIM_WALK_BACKWARD);
// 			}
// 			else
// 			{
// 				_lastDirection = FacingDirection.Forward;
// 				PlayAnimationIfExists(ANIM_WALK_FORWARD);
// 			}
// 		}
// 	}
//
// 	private void PlayAnimationIfExists(string animName)
// 	{
// 		// Only play the animation if it exists and isn't already playing
// 		if (_animationPlayer.HasAnimation(animName) && _animationPlayer.CurrentAnimation != animName)
// 			_animationPlayer.Play(animName);
// 	}
//
// 	public override void _Process(double delta)
// 	{
// 		// Calculate target position based on initial offset
// 		var targetPos = GlobalPosition + _initialCameraOffset;
//
// 		// Apply smoothing with proper clamped delta
// 		var smoothFactor = Mathf.Clamp(CameraSmoothness * (float)delta, 0f, 1f);
// 		_camera.GlobalPosition = _camera.GlobalPosition.Lerp(targetPos, smoothFactor);
// 	}
// }
