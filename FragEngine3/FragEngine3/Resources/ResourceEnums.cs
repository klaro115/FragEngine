using System;

namespace FragEngine3.Resources
{
	/// <summary>
	/// Different types of resources, differentiated by media type, usage, or data structure.
	/// </summary>
	public enum ResourceType
	{
		/// <summary>
		/// Unknown or unspecified data type. This should not happen, and is a placeholder value reserved to signify something went wrong.
		/// </summary>
		Unknown,
		/// <summary>
		/// Internal and auto-generated files that should be ignored and skipped. This is a placeholder to indicate that a resource/file should not be processed by resource management.
		/// </summary>
		Ignored,

		/// <summary>
		/// A texture resource, can be 1D, 2D, 3D, a cubemap, or an array of textures. Basically any kind of regularly organized data of discrete datapoints.
		/// </summary>
		Texture,
		/// <summary>
		/// A video resource, can be any 2D rasterized animation. Video will be treated as a special case of 2D textures by most systems that support it.<para/>
		/// NOTE: Animated gifs should also be loaded as video.
		/// </summary>
		Video,
		/// <summary>
		/// A shader program, used by the GPU to process graphics resources, and usually to compute pixel data.
		/// Types of shaders include: vertex, geometry, pixel, compute.
		/// </summary>
		Shader,
		/// <summary>
		/// A serialized data object describing a composition of 1 or more shaders and various graphics resources such as textures.
		/// </summary>
		Material,
		/// <summary>
		/// A 3D model's bone animation data.
		/// </summary>
		Animation,
		/// <summary>
		/// A 3D object, typically in the form of polygonal geometry data and bone animation.
		/// A model may include other subresources specific to this geometry, such as animations or materials.
		/// </summary>
		Model,
		/// <summary>
		/// Audio data of some kind. Can be either music, sound effects, etc.
		/// </summary>
		Audio,
		/// <summary>
		/// A serialized object, encoding the data graph and dependencies for recreating a scene node from scratch.
		/// One prefab may be dependent on many other resources of all types.
		/// </summary>
		Prefab,
		/// <summary>
		/// A font style, for displaying text contents in UI or in the scene.
		/// </summary>
		Font,
		/// <summary>
		/// Any kind of raw or serialized data for use by miscellaneous systems.
		/// </summary>
		Data,
		/// <summary>
		/// A scene definition or a save file's full description of a scene's state.
		/// </summary>
		Scene,
	}

	/// <summary>
	/// Different types of resource data files. This indicates whether and how data is compressed, and if one or more resources are encoded in the data of one file.
	/// </summary>
	public enum ResourceFileType
	{
		/// <summary>
		/// The data file contains only one resource.
		/// Note that some resources may still contain subresources that are quietly imported along with and as part of the main resource.
		/// </summary>
		Single,
		/// <summary>
		/// The data file contains multiple resources, encoded or compressed in one contiguous data block.
		/// Full decoding and decompression of the whole file must occur before any one of the resources can be loaded.
		/// </summary>
		Batch_Compressed,
		/// <summary>
		/// The data file contains multiple resources, encoded or compressed in blocks of a known or predictable size and location.
		/// Resources can be loaded individually and on demand, as only the blocks containing the resource in question need decoding.
		/// </summary>
		Batch_BlockCompressed,
		/// <summary>
		/// Resource is not sources from a file.
		/// This may be the case if the data is procedurally generated but still requires handling as if it depended on a file, or if the file was only temporary.
		/// </summary>
		None,
	}

	/// <summary>
	/// Different sources from where a resource was drawn. Any resources created at run-time should be flagged as '<see cref="Runtime"/>',
	/// all others should reflect the type of content directory they were loaded from.
	/// </summary>
	public enum ResourceSource
	{
		/// <summary>
		/// Resource or file were generated or modified at run-time and are not tied to any persistent resource files.
		/// </summary>
		Runtime,
		/// <summary>
		/// Resource or file is part of the engine's core resource set. This resource should never be modified or removed at run-time.
		/// </summary>
		Core,
		/// <summary>
		/// Resource or file is part of the application's main resources. Most of these resources should not be modified or removed at run-time, configs and profile data being the exception.
		/// </summary>
		Application,
		/// <summary>
		/// Resource or file was added as part of a mod (short for modification), which aims to add additional or custom content post-release.
		/// </summary>
		Mod,
		/// <summary>
		/// The resource was downloaded or streamed from some remote network platform, and has no persistent data representation on this device.
		/// </summary>
		Network,
	}

	/// <summary>
	/// The status of a resource, whether it has been loaded/imported, or still awaiting processing.
	/// A resource is only ready to be used and accessed by other systems once its status has reached '<see cref="Loaded"/>'.
	/// </summary>
	public enum ResourceLoadState
	{
		/// <summary>
		/// The resource has not yet been queued up for loading and is not ready for use. The resource system merely knows it exists and is available if needed.
		/// It can be queued up for asynchronous loading, or loaded immediately, as needed.
		/// </summary>
		NotLoaded,

		/// <summary>
		/// The resource has been queued up for import once resource processing capacity allows. It should be ready for use within a few seconds at the most.
		/// If the resource is needed immediately, it is removed from the queue and imported immediately on the main thread instead.
		/// </summary>
		Queued,
		/// <summary>
		/// The resource is currently being imported, likely by one of the resource system's worker threads. It should be ready any moment now.
		/// If the resource is needed immediately, the main thread will block until the import has concluded.
		/// </summary>
		Loading,
		/// <summary>
		/// The resource has been loaded and is ready for use.
		/// </summary>
		Loaded
	}

	public static class ResourceEnumsExt
	{
		#region Methods

		/// <summary>
		/// Whether the file type refers to a compressed or encoded resource file.
		/// </summary>
		/// <param name="_fileType">This file type.</param>
		/// <returns></returns>
		public static bool IsCompressed(this ResourceFileType _fileType)
		{
			return _fileType == ResourceFileType.Batch_Compressed || _fileType == ResourceFileType.Batch_BlockCompressed;
		}

		/// <summary>
		/// Whether a resource's source refers to one that was loaded from file. All other resources will have been created procedurally and at run-time.
		/// </summary>
		/// <param name="_source"></param>
		/// <returns></returns>
		public static bool IsLoadedFromFile(this ResourceSource _source)
		{
			return _source != ResourceSource.Runtime && _source != ResourceSource.Network;
		}
		
		#endregion
	}
}
