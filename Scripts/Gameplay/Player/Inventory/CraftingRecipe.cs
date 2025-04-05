using System.Collections.Generic;
using System.Text;

namespace Ceiro.Scripts.Gameplay.Player.Inventory;

/// <summary>
/// Represents a crafting recipe.
/// </summary>
public class CraftingRecipe
{
	public required string                  ResultItemId { get; init; }
	public          int                     ResultAmount { get; init; }
	public          Dictionary<string, int> Ingredients  { get; init; } = new();

	/// <summary>
	/// Gets a description of the recipe.
	/// </summary>
	/// <returns>A string describing the recipe.</returns>
	public string GetDescription()
	{
		var sb = new StringBuilder();

		sb.Append($"{ResultAmount}x {ResultItemId} = ");

		foreach (var ingredient in Ingredients)
			sb.Append($"{ingredient.Value}x {ingredient.Key}, ");

		// Remove trailing comma and space if there are ingredients
		if (Ingredients.Count > 0)
			sb.Length -= 2; // Remove the last ", "

		return sb.ToString();
	}
}