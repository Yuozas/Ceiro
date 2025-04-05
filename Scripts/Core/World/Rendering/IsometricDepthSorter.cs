using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ceiro.Scripts.Core.World.Rendering;

/// <summary>
/// Handles the depth sorting of objects in the isometric view to ensure
/// proper occlusion and layering.
/// </summary>
public partial class IsometricDepthSorter : Node
{
	[Export] public NodePath WorldRoot;
	[Export] public bool     AutoSort     = true;
	[Export] public float    SortInterval = 0.5f;

	private Node3D _worldRootNode;
	private float  _sortTimer;

	public override void _Ready()
	{
		// Get the world root node
		if (!string.IsNullOrEmpty(WorldRoot))
			_worldRootNode = GetNodeOrNull<Node3D>(WorldRoot) ?? throw new("World root not found!");
		else
				// Default to parent if no world root is specified
			_worldRootNode = GetParent<Node3D>() ?? throw new("World root not found!");
	}

	public override void _Process(double delta)
	{
		if (!AutoSort)
			return;

		// Update the sort timer
		_sortTimer += (float)delta;

		// Sort objects at the specified interval
		if (!(_sortTimer >= SortInterval))
			return;

		SortObjectsByDepth();
		_sortTimer = 0.0f;
	}

	/// <summary>
	/// Sorts objects in the isometric view based on their Y position to ensure proper layering.
	/// </summary>
	public void SortObjectsByDepth()
	{
		// Get all renderable objects in the world
		var renderables = new List<Node>();
		FindRenderableObjects(_worldRootNode, renderables);

		// Sort the renderables by their Y position (depth)
		renderables.Sort((a, b) =>
		{
			if (a is not Node3D nodeA || b is not Node3D nodeB)
				return 0;

			// In isometric view, objects with higher Y (further back) should be drawn first
			// Objects with lower Y (closer to camera) should be drawn on top
			return nodeB.GlobalPosition.Z.CompareTo(nodeA.GlobalPosition.Z);
		});

		// Apply the sorting by adjusting the Z index of sprites
		for (var i = 0; i < renderables.Count; i++)
			if (renderables[i] is Node3D node3D)
					// Find any Sprite3D children and adjust their Z index
				foreach (var child in node3D.GetChildren())
					if (child is Sprite3D sprite)
							// Set the Z index based on the sorting order
							// Higher Z index means drawn on top
						sprite.SortingOffset = i;
	}

	/// <summary>
	/// Recursively finds all renderable objects in the scene.
	/// </summary>
	/// <param name="node">The current node to check.</param>
	/// <param name="renderables">The collection to store renderable objects.</param>
	private static void FindRenderableObjects(Node node, IList<Node> renderables)
	{
		// Check if this node has a Sprite3D child
		var hasSprite = node.GetChildren().OfType<Sprite3D>().Any();

		// If this node has a sprite, add it to the renderables
		if (hasSprite && node is Node3D)
			renderables.Add(node);

		// Recursively check all children
		foreach (var child in node.GetChildren())
			FindRenderableObjects(child, renderables);
	}

	/// <summary>
	/// Manually triggers a depth sort of all objects.
	/// </summary>
	public void ManualSort() => SortObjectsByDepth();
}