using System.Collections.Frozen;

namespace FragEngine3.Resources;

internal static class ResourceFileConstants
{
	#region Fields

	private static readonly FrozenDictionary<ResourceType, HashSet<string>> resourceTypeExtensionDict = new KeyValuePair<ResourceType, HashSet<string>>[]
	{
		new(ResourceType.Unknown, []),
		new(ResourceType.Ignored,
		[
			ResourceConstants.FILE_EXT_METADATA,				// Resource descriptor/metadata file
			ResourceConstants.FILE_EXT_BATCH_NORMAL_COMPRESSED,	// Resource package files, contiguously compressed
			ResourceConstants.FILE_EXT_BATCH_BLOCK_COMPRESSED,	// Resource package files, block-compressed
			//...
		]),
		new(ResourceType.Texture,
		[
			".bmp",
			".dds",
			".exr",
			".gif",
			".jpg",
			".jpeg",
			".png",
			".psd",
			".svg",
			".tga",
			".tif",
			".tiff",
			".qoi",
			".webp",
			//...
		]),
		new(ResourceType.Video,
		[
			".3gp",
			".3g2",
			".avi",
			".mov",
			".m4v",
			".mp4",
			".mpg",
			".mpeg",
			".avi",
			".webm",
			".wmv",
			//...
		]),
		new(ResourceType.Shader,
		[
			".fx",
			".glsl",
			".hlsl",
			".incl",
			".metal",
			".shader",
		]),
		new(ResourceType.Material,
		[
			".mtl",
		]),
		new(ResourceType.Animation, []),
		new(ResourceType.Model,
		[
			".3ds",
			".blend",
			".dae",
			".fbx",
			".gltf",
			".mb",
			".ma",
			".obj",
			".stl",
			".x",
			//...
		]),
		new(ResourceType.Audio,
		[
			".aif",
			".m4a",
			".mp3",
			".ogg",
			".wav",
			".webm",
			//...
		]),
		new(ResourceType.Prefab,
		[
			".prefab",
		]),
		new(ResourceType.Font,
		[
			"otf",
			"ttf",
			//...
		]),
		new(ResourceType.Data,
		[
			".csv",
			".json",
			".md",
			".txt",
			".xaml",
			".xls",
			".xlsx",
			".xml",
			".yml",
			//...
		]),
		new(ResourceType.Scene,
		[
			".scene"
		]),
		new(ResourceType.Script,
		[
			".cs",
			".js",	// who in their right mind would use JS anyways?
			".lua",
			".py",
			".rb",
			".ru",
			".ts",
			".vb",
			//...
		]),
	}.ToFrozenDictionary();

	#endregion
	#region Methods

	/// <summary>
	/// Determine the type of a resource from the file extension of its data file.
	/// </summary>
	/// <param name="_ext">The file extension of the resource's data file. Must be non-null and lower-case.</param>
	/// <returns>The most likely resource type.</returns>
	public static ResourceType GetResourceTypeFromFileExtension(string _ext)
	{
		if (!string.IsNullOrEmpty(_ext))
		{
			foreach (var kvp in resourceTypeExtensionDict)
			{
				if (kvp.Value.Contains(_ext)) return kvp.Key;
			}
		}
		return ResourceType.Unknown;
	}

	/// <summary>
	/// Determine the type of a resource from the file extension of its data file.
	/// </summary>
	/// <param name="_dataFilePath">The path to a resource data file. May not be null.</param>
	/// <returns>The most likely resource type.</returns>
	public static ResourceType GetResourceTypeFromFilePath(string _dataFilePath)
	{
		if (string.IsNullOrEmpty(_dataFilePath)) return ResourceType.Unknown;

		string ext = Path.GetExtension(_dataFilePath).ToLowerInvariant();

		return GetResourceTypeFromFileExtension(ext);
	}

	/// <summary>
	/// Check whether a given file extension is known at all.<para/>
	/// NOTE: Not all known format extensions are supported, but all unknown extensions are definitely unsupported by the engine.
	/// </summary>
	/// <param name="_ext">A file format extension. Must be non-null and lower-case.</param>
	/// <returns></returns>
	public static bool IsFileExtensionKnown(string _ext)
	{
		if (!string.IsNullOrEmpty(_ext))
		{
			foreach (var kvp in resourceTypeExtensionDict)
			{
				if (kvp.Value.Contains(_ext)) return true;
			}
		}
		return false;
	}

	#endregion
}
