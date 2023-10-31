using FragEngine3.Resources.Data;

namespace FragEngine3.Resources
{
	public static class ResourceConstants
	{
		#region Constants

		/// <summary>
		/// File extension for all resource metadata files. The extension must be lower-case. The contents must be a JSON-serialized object of type '<see cref="ResourceFileMetadata"/>'.
		/// </summary>
		public const string FILE_EXT_METADATA = ".res";
		public const string FILE_EXT_BATCH_NORMAL_COMPRESSED = ".cpkg";
		public const string FILE_EXT_BATCH_BLOCK_COMPRESSED = ".bpkg";

		public const string RESOURCE_ROOT_DIR_REL_PATH = "data";
		public const string RESOURCE_CORE_DIR_REL_PATH = "core";
		public const string RESOURCE_MODS_DIR_REL_PATH = "mods";

		#endregion
		#region Fields

		public static readonly string[] coreResourceLibraries = new string[]
		{
			"textures",
			"videos",
			"shaders",
			"materials",
			"animations",
			"models",
			"audio",
			"prefabs",
			"data",
		};

		private static readonly Dictionary<ResourceType, HashSet<string>> resourceTypeExtensionDict = new()
		{
			[ResourceType.Unknown] = new HashSet<string>(),
			[ResourceType.Texture] = new HashSet<string>()
			{
				".bmp",
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
			},
			[ResourceType.Video] = new HashSet<string>()
			{
				".3gp",
				".3g2",
				".avi",
				".mov",
				".m4v",
				".mp4",
				".mpg",
				".mpeg",
				".avi",
				".wmv",
				//...
			},
			[ResourceType.Shader] = new HashSet<string>()
			{
				".glsl",
				".hlsl",
				".incl",
			},
			[ResourceType.Material] = new HashSet<string>()
			{
				".mtl",
			},
			[ResourceType.Animation] = new HashSet<string>(),
			[ResourceType.Model] = new HashSet<string>()
			{
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
			},
			[ResourceType.Audio] = new HashSet<string>()
			{
				".aif",
				".m4a",
				".mp3",
				".ogg",
				".wav",
				".webm",
				//...
			},
			[ResourceType.Prefab] = new HashSet<string>()
			{
				".prefab",
			},
			[ResourceType.Data] = new HashSet<string>()
			{
				".csv",
				".json",
				".txt",
				".xml",
				".yml",
				//...
			},
			[ResourceType.Scene] = new HashSet<string>()
			{
				".scene"
			},
		};

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

			string ext = Path.GetExtension(_dataFilePath);

			return GetResourceTypeFromFileExtension(ext);
		}

		#endregion
	}
}
