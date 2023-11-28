
namespace FragEngine3.Resources
{
	internal class ResourceFileConstants
	{
		#region Fields

		private static readonly Dictionary<ResourceType, HashSet<string>> resourceTypeExtensionDict = new()
		{

			[ResourceType.Unknown] = [],
			[ResourceType.Ignored] =
			[
				".res",		// Resource descriptor file
				".pkg",		// Resource package files
				//...
			],
			[ResourceType.Texture] =
			[
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
			],
			[ResourceType.Video] =
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
				".wmv",
				//...
			],
			[ResourceType.Shader] =
			[
				".glsl",
				".hlsl",
				".incl",
			],
			[ResourceType.Material] =
			[
				".mtl",
			],
			[ResourceType.Animation] = [],
			[ResourceType.Model] =
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
			],
			[ResourceType.Audio] =
			[
				".aif",
				".m4a",
				".mp3",
				".ogg",
				".wav",
				".webm",
				//...
			],
			[ResourceType.Prefab] =
			[
				".prefab",
			],
			[ResourceType.Data] =
			[
				".csv",
				".json",
				".txt",
				".xml",
				".yml",
				//...
			],
			[ResourceType.Scene] =
			[
				".scene"
			],
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
