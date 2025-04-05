using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Ceiro.Scripts.Core.Utilities;

public static class ResourceUtils
{
	/// <summary>
	/// Retrieves a collection of all parameter names defined in the given shader material.
	/// </summary>
	/// <param name="material">The ShaderMaterial instance to retrieve parameters from.</param>
	/// <returns>A collection of shader parameter names associated with the material.</returns>
	public static ICollection<StringName> GetShaderParameterList(this ShaderMaterial material)
	{
		var result = new List<StringName>();
		// Get all shader parameters using reflection through Godot's Object class
		var parameters = material.GetPropertyList();

		result.AddRange(from property in parameters
		                where property.ContainsKey("name") && property["name"].ToString().StartsWith("shader_parameter/")
		                select property["name"].ToString() into fullParamName
		                select new StringName(fullParamName));

		return result;
	}

	/// <summary>
	/// Loads all resources of a specific type from a directory
	/// </summary>
	/// <param name="path">Directory path (e.g. "res://resources/biomes/")</param>
	/// <param name="extension">File extension to filter by (e.g. "tres")</param>
	/// <returns>List of loaded resources</returns>
	public static IReadOnlyCollection<Resource> LoadAll(string path, string extension)
	{
		var resources = new List<Resource>();
		var dir       = DirAccess.Open(path);

		if (dir is null)
			throw new($"Failed to open directory: {path}");

		// Make sure extension doesn't start with a dot
		if (extension.StartsWith('.'))
			extension = extension[1..];

		dir.ListDirBegin();
		var fileName = dir.GetNext();

		while (!string.IsNullOrEmpty(fileName))
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith($".{extension}"))
			{
				var fullPath = path.EndsWith('/') ? $"{path}{fileName}" : $"{path}/{fileName}";
				var resource = ResourceLoader.Load(fullPath);
				if (resource != null)
					resources.Add(resource);
			}

			fileName = dir.GetNext();
		}

		dir.ListDirEnd();
		return resources;
	}

	/// <summary>
	/// Loads all resources of a specific type from a directory
	/// </summary>
	/// <typeparam name="T">Type of resource to load</typeparam>
	/// <param name="path">Directory path (e.g. "res://resources/biomes/")</param>
	/// <param name="extension">File extension to filter by (e.g. "tres")</param>
	/// <returns>List of loaded resources of the specified type</returns>
	public static IReadOnlyCollection<T> LoadAll<T>(string path, string extension) where T : Resource
	{
		var resources = new List<T>();
		var dir       = DirAccess.Open(path);

		if (dir is null)
			throw new($"Failed to open directory: {path}");

		// Make sure extension doesn't start with a dot
		if (extension.StartsWith('.'))
			extension = extension[1..];

		dir.ListDirBegin();
		var fileName = dir.GetNext();

		while (!string.IsNullOrEmpty(fileName))
		{
			if (!dir.CurrentIsDir() && fileName.EndsWith($".{extension}"))
			{
				var fullPath = path.EndsWith('/') ? $"{path}{fileName}" : $"{path}/{fileName}";
				var resource = ResourceLoader.Load<T>(fullPath);
				if (resource is not null)
					resources.Add(resource);
			}

			fileName = dir.GetNext();
		}

		dir.ListDirEnd();
		return resources;
	}
}