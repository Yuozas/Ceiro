using System.Collections.Generic;
using System.Linq;
using Ceiro.Scripts.Core.World.Generation;
using Godot;
using TimeOfDaySystem = Ceiro.Scripts.Core.World._Time.TimeOfDaySystem;

namespace Ceiro.Scripts.Gameplay.AI.Enemies;

/// <summary>
/// Manages enemy spawning in the world based on time, location, and player proximity.
/// </summary>
public partial class EnemySpawnManager : Node
{
	[Export] public NodePath? PlayerPath;
	[Export] public float     SpawnRadius         = 20.0f;
	[Export] public float     DespawnRadius       = 30.0f;
	[Export] public int       MaxEnemies          = 10;
	[Export] public float     SpawnInterval       = 5.0f;
	[Export] public bool      SpawnAtNight        = true;
	[Export] public bool      LimitSpawnsPerBiome = true;

	[ExportCategory("Enemy Types")]
	[Export]
	public PackedScene[] BasicEnemyPrefabs = [];

	[Export] public PackedScene[] RangedEnemyPrefabs = [];
	[Export] public PackedScene[] EliteEnemyPrefabs  = [];
	[Export] public PackedScene[] BossPrefabs        = [];

	private Node3D                   _player;
	private float                    _spawnTimer;
	private TimeOfDaySystem          _timeSystem;
	private ProceduralWorldGenerator _worldGenerator;

	private readonly List<Node>           _activeEnemies   = [];
	private readonly Dictionary<int, int> _enemiesPerBiome = new();

	public override void _Ready()
	{
		// Get the player reference
		if (!string.IsNullOrEmpty(PlayerPath))
		{
			_player = GetNodeOrNull<Node3D>(PlayerPath) ?? throw new("Player not found or unable to cast to type Node3D.");
		}
		else
		{
			// Try to find the player in the scene
			var players = GetTree().GetNodesInGroup("Player");
			if (players.Count > 0)
				_player = players[0] as Node3D ?? throw new("Player not found or unable to cast to type Node3D.");
		}

		// Find related systems
		_timeSystem = GetTree().Root.FindChild("TimeOfDaySystem", true, false) as TimeOfDaySystem ?? throw new("TimeOfDaySystem not found or unable to cast to type TimeOfDaySystem.");
		_worldGenerator = GetTree().Root.FindChild("WorldGenerator", true, false) as ProceduralWorldGenerator
		               ?? throw new("WorldGenerator not found or unable to cast to type ProceduralWorldGenerator.");
	}

	public override void _Process(double delta)
	{
		// Update spawn timer
		_spawnTimer -= (float)delta;

		// Clean up enemy list
		CleanupEnemyList();

		// Check if we should spawn enemies
		if (_spawnTimer <= 0 && _activeEnemies.Count < MaxEnemies)
		{
			// Check time of day if night spawning is enabled
			var canSpawn = true;
			if (SpawnAtNight)
				canSpawn = _timeSystem.IsNight();

			if (canSpawn)
				SpawnEnemy();

			// Reset timer
			_spawnTimer = SpawnInterval;
		}

		// Check for despawning
		CheckDespawnEnemies();
	}

	/// <summary>
	/// Spawns an enemy near the player.
	/// </summary>
	private void SpawnEnemy()
	{
		// Generate a random position around the player
		var angle  = (float)GD.RandRange(0, Mathf.Pi * 2);
		var radius = (float)GD.RandRange(SpawnRadius * 0.5f, SpawnRadius);

		var spawnPos = _player.GlobalPosition
		             + new Vector3(Mathf.Cos(angle) * radius,
		                           0,
		                           Mathf.Sin(angle) * radius);

		// Get height at position

		var height = _worldGenerator.GetHeightAt(spawnPos.X, spawnPos.Z);
		spawnPos.Y = height;

		// Get biome at position
		var biomeIndex = _worldGenerator.GetBiomeAt(spawnPos.X, spawnPos.Z);

		// Skip if water biome
		if (biomeIndex == 0)
			return;

		// Check biome spawn limits
		if (LimitSpawnsPerBiome)
		{
			var count = _enemiesPerBiome.GetValueOrDefault(biomeIndex, 0);

			const int maxPerBiome = 3; // Adjust as needed
			if (count >= maxPerBiome)
				return;

			_enemiesPerBiome[biomeIndex] = count + 1;
		}


		// Determine enemy type to spawn
		PackedScene? enemyPrefab = null;

		// Random chance for different enemy types
		var rand = (float)GD.RandRange(0, 1);

		switch (rand)
		{
			case < 0.7f when BasicEnemyPrefabs.Length > 0:
			{
				// 70% chance for basic enemy
				var index = GD.RandRange(0, BasicEnemyPrefabs.Length);
				enemyPrefab = BasicEnemyPrefabs[index];
				break;
			}
			case < 0.9f when RangedEnemyPrefabs.Length > 0:
			{
				// 20% chance for ranged enemy
				var index = GD.RandRange(0, RangedEnemyPrefabs.Length);
				enemyPrefab = RangedEnemyPrefabs[index];
				break;
			}
			case < 0.98f when EliteEnemyPrefabs.Length > 0:
			{
				// 8% chance for elite enemy
				var index = GD.RandRange(0, EliteEnemyPrefabs.Length);
				enemyPrefab = EliteEnemyPrefabs[index];
				break;
			}
			default:
			{
				if (BossPrefabs.Length > 0)
				{
					// 2% chance for boss
					var index = GD.RandRange(0, BossPrefabs.Length);
					enemyPrefab = BossPrefabs[index];
				}

				break;
			}
		}

		if (enemyPrefab == null)
			return;

		// Spawn the enemy
		var enemy = enemyPrefab.Instantiate<Node3D>();
		enemy.GlobalPosition = spawnPos;
		GetTree().Root.AddChild(enemy);

		// Add to active enemies list
		_activeEnemies.Add(enemy);
	}

	/// <summary>
	/// Checks if any enemies should be despawned.
	/// </summary>
	private void CheckDespawnEnemies()
	{
		for (var i = _activeEnemies.Count - 1; i >= 0; i--)
		{
			var enemy = _activeEnemies[i];

			if (!IsInstanceValid(enemy))
			{
				_activeEnemies.RemoveAt(i);
				continue;
			}

			if (enemy is not Node3D enemyNode)
				continue;

			var distance = enemyNode.GlobalPosition.DistanceTo(_player.GlobalPosition);

			if (!(distance > DespawnRadius))
				continue;

			// Despawn the enemy
			enemy.QueueFree();
			_activeEnemies.RemoveAt(i);

			// Update biome count if needed
			if (!LimitSpawnsPerBiome)
				continue;

			var biomeIndex = _worldGenerator.GetBiomeAt(enemyNode.GlobalPosition.X, enemyNode.GlobalPosition.Z);

			if (_enemiesPerBiome.TryGetValue(biomeIndex, out var count) && count > 0)
				_enemiesPerBiome[biomeIndex] = count - 1;
		}
	}

	/// <summary>
	/// Cleans up the enemy list, removing any invalid references.
	/// </summary>
	private void CleanupEnemyList()
	{
		for (var i = _activeEnemies.Count - 1; i >= 0; i--)
			if (!IsInstanceValid(_activeEnemies[i]))
				_activeEnemies.RemoveAt(i);
	}

	/// <summary>
	/// Gets the count of active enemies.
	/// </summary>
	/// <returns>The number of active enemies.</returns>
	public int GetEnemyCount() => _activeEnemies.Count;

	/// <summary>
	/// Despawns all enemies.
	/// </summary>
	public void DespawnAllEnemies()
	{
		foreach (var enemy in _activeEnemies.Where(IsInstanceValid))
			enemy.QueueFree();

		_activeEnemies.Clear();
		_enemiesPerBiome.Clear();
	}
}