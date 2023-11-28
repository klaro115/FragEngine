using FragEngine3.Resources.Data;

namespace FragEngine3.Resources
{
	/// <summary>
	/// Identifier and descriptor type for resources and assets used by the application. The resource handle essentially represents a known resource that may be
	/// used and referenced across different systems of the app, while providing means to load said resources both synchronously and asynchronously through the
	/// '<see cref="ResourceManager"/>'.
	/// </summary>
	[Obsolete("Replaced")]
	public sealed class ResourceHandleOld : IEquatable<ResourceHandleOld>
	{
		#region Types

		public delegate bool FuncAssignResourceCallback(Resource? _resourceObject);

		#endregion
		#region Constructors

		public ResourceHandleOld(ResourceManager _resourceManager, string _resourceName, string _filePath, ResourceType _resourceType, List<string>? _dependencies, ResourceSource _resourceSource)
		{
			resourceManager = _resourceManager;
			resourceName = _resourceName ?? throw new ArgumentNullException(nameof(_resourceName), "Resource name may not be null!");
			filePath = _filePath ?? throw new ArgumentNullException(nameof(_filePath), "Resource file path may not be null!");
			resourceType = _resourceType;
			resourceSource = _resourceSource;
			dependencies = _dependencies != null && _dependencies.Count != 0 ? _dependencies : null;
		}

		public ResourceHandleOld(ResourceManager _resourceManager, string _resourceName, string _filePath, ResourceType _resourceType, List<string>? _dependencies, ResourceFileType _fileType, ResourceSource _resourceSource, ulong _fileOffset, ulong _fileSize)
		{
			resourceManager = _resourceManager;
			resourceName = _resourceName ?? throw new ArgumentNullException(nameof(_resourceName), "Resource name may not be null!");
			filePath = _filePath ?? throw new ArgumentNullException(nameof(_filePath), "Resource file path may not be null!");
			resourceType = _resourceType;
			resourceSource = _resourceSource;
			dependencies = _dependencies != null && _dependencies.Count != 0 ? _dependencies : null;

			fileType = _fileType;
			fileOffset = _fileOffset;
			fileSize = _fileSize;
		}

		public ResourceHandleOld(ResourceManager _resourceManager, ResourceHandleMetadataOld _metadata, ResourceFileHandleOld _fileHandle)
		{
			if (_metadata == null) throw new ArgumentNullException(nameof(_metadata), "Resource metadata may not be null!");
			if (_fileHandle == null) throw new ArgumentNullException(nameof(_fileHandle), "Resource file handle may not be null!");

			resourceManager = _resourceManager;
			resourceName = _metadata.ResourceName;
			filePath = _fileHandle.Key ?? string.Empty;
			resourceType = _metadata.ResourceType;
			resourceSource = _fileHandle.fileSource;
			dependencies = _metadata.GetDependencyCount() != 0 ? new List<string>(_metadata.Dependencies) : null;

			fileType = _fileHandle.fileType;
			fileOffset = _metadata.ResourceOffset;
			fileSize = _metadata.ResourceSize;
		}

		#endregion
		#region Fields

		public readonly ResourceManager resourceManager;

		// Resource metadata:
		public readonly string resourceName = string.Empty;
		public readonly string filePath = string.Empty;
		public readonly ResourceType resourceType = ResourceType.Unknown;
		public readonly ResourceSource resourceSource = ResourceSource.Runtime;
		public readonly List<string>? dependencies = null;

		// Source metadata:
		public readonly ResourceFileType fileType = ResourceFileType.Single;
		public readonly ulong fileOffset = 0;
		public readonly ulong fileSize = 0;

		// State:
		public ResourceLoadState loadState = ResourceLoadState.NotLoaded;
		private Resource? resource = null;

		private static readonly ResourceHandleOld none = new(null!, string.Empty, string.Empty, ResourceType.Unknown, null, ResourceFileType.None, ResourceSource.Runtime, 0, 0);

		#endregion
		#region Properties

		/// <summary>
		/// A sorting key and identifier for this resource.
		/// </summary>
		public string Key => resourceName ?? string.Empty;
		public ResourceFileHandleOld.ResourceInfo FileInfo => new(Key, fileOffset, fileSize);

		public bool IsLoaded => loadState == ResourceLoadState.Loaded && resource != null && !resource.IsDisposed;
		public bool IsValid => resourceManager != null && !string.IsNullOrEmpty(Key) && filePath != null && resourceType != ResourceType.Unknown && (fileType == ResourceFileType.None | !string.IsNullOrEmpty(filePath));

		public int DependencyCount => dependencies != null ? dependencies.Count : 0;

		/// <summary>
		/// Returns an empty, invalid, and unassigned resource handle.
		/// </summary>
		public static ResourceHandleOld None => none;

		#endregion
		#region Methods

		/// <summary>
		/// Retrieve the resource object that this handle manages.
		/// </summary>
		/// <param name="_loadImmediatelyIfNotReady">Whether to load the resource immediately if it isn't loaded and ready. If true, the import may be started
		/// now, and the main thread will block until the loading process is completed. If false, null will be returned for now, but asynchromous background
		/// loading of the resource will be queued up.<para/>
		/// NOTE: This parameter does nothing if '<see cref="_loadResourceIfNotReady"/>' is set to false.</param>
		/// <param name="_loadResourceIfNotReady">Whether to trigger loading of the resource if it hasn't been loaded yet. True should be the default behaviour
		/// is most all situations, since it'll just queue up background loading for later use. If false, no loading will be triggered and the resource shall
		/// remain unusable until loading is triggered elsewhere.<para/>
		/// NOTE: Don't touch this unless you have a good reason for it. Only unimportant single-use background assets should ever disable this.</param>
		/// <returns>The fully loaded resource object, or null, if it hasn't been imported yet.</returns>
		public Resource? GetResource(bool _loadImmediatelyIfNotReady = true, bool _loadResourceIfNotReady = true)
		{
			if (!IsLoaded && _loadResourceIfNotReady)
			{
				Load(_loadImmediatelyIfNotReady);
			}
			return IsLoaded ? resource : null;
		}

		/// <summary>
		/// Explicitly trigger loading of the resource.
		/// </summary>
		/// <param name="_loadImmediately">Whether to load the resource immediately. If true, it will be loaded now, blocking the main thread until done. If
		/// false, the resource will be queued up for asynchronous background loading, and will be ready for use at a later time.</param>
		/// <returns>True if the resource is already loaded, or if loading was triggered successfully. False otherwise.</returns>
		public bool Load(bool _loadImmediately)
		{
			if (IsLoaded && resource != null)
			{
				return true;
			}
			if (resourceManager != null && !resourceManager.IsDisposed)
			{
				// If the resource is already loading or queued up, and we don't need it right away, all is good:
				if (!_loadImmediately && loadState != ResourceLoadState.NotLoaded)
				{
					return true;
				}
				// If we need it now, or the resource is not yet queued, tell resource manager to do so now:
				//else if (resourceManager.LoadResource(this, _loadImmediately, CallbackAssignResourceObject))
				{
					// We're using this weird getter here, since multiple instances of a same resource handle may exist.
					// This is undesirable and unlikely, but can happen through either user meddling, hot-reloading of
					// resources, or obscure bugs. This way, we can be sure that we're getting the most up-to-date instance
					// of the resource object straight from the source.
					//return resourceManager.GetResourceObject(Key, CallbackAssignResourceObject);
				}
			}
			return false;
		}

		/// <summary>
		/// Unload the resource, releasing all its data and resetting its state to '<see cref="ResourceLoadState.NotLoaded"/>'.<para/>
		/// WARNING: This will only unload this exact resource, but will never release its dependencies, as they may still be in use elsewhere.
		/// </summary>
		public void Unload()
		{
			resourceManager?.UnloadResource(Key);
			resource = null;
			loadState = ResourceLoadState.NotLoaded;
		}

		public bool Reload()
		{
			// Nothing loaded/loading yet, nothing to fix:
			if (loadState == ResourceLoadState.NotLoaded) return true;

			if (resourceManager != null && !resourceManager.IsDisposed)
			{
				// Update the resource object's value straight from the resource manager:
				//return resourceManager.GetResourceObject(Key, CallbackAssignResourceObject);
			}
			return false;
		}

		private bool CallbackAssignResourceObject(Resource? _newResource)
		{
			if (resource != null && !resource.IsDisposed && _newResource != resource)
			{
				resource.Dispose();
			}
			resource = _newResource != null && !_newResource.IsDisposed ? _newResource : null;
			if (resource != null && resource.IsLoaded)
			{
				loadState = ResourceLoadState.Loaded;
			}
			return true;
		}

		public bool Equals(ResourceHandleOld? other) => string.CompareOrdinal(Key, other?.Key) == 0;
		public override bool Equals(object? obj) => Equals(obj as ResourceHandleOld);
		public override int GetHashCode() => base.GetHashCode();

		public static bool operator ==(ResourceHandleOld? left, ResourceHandleOld? right) => ReferenceEquals(left, right) || (left is not null && left.Equals(right));
		public static bool operator !=(ResourceHandleOld? left, ResourceHandleOld? right) => !ReferenceEquals(left, right) || (left is not null && !left.Equals(right));

		public override string ToString()
		{
			return $"{resourceName} ({resourceType}, {loadState}) [@{filePath ?? "NULL"},{fileOffset},{fileSize}]";
		}

		#endregion
	}
}
