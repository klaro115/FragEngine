using FragEngine3.Resources.Data;

namespace FragEngine3.Resources
{
	public sealed class ResourceHandle : IEquatable<ResourceHandle>
	{
		#region Types

		internal delegate bool FuncAssignResourceCallback(Resource? _resourceObject);

		#endregion
		#region Constructors

		public ResourceHandle(ResourceManager _resourceManager, ResourceHandleData _data, string _dataFilePath)
		{
			if (_data == null) throw new ArgumentNullException(nameof(_data), "Resource handle data may not be null!");

			resourceManager = _resourceManager ?? throw new ArgumentNullException(nameof(_resourceManager), "Resource manager may not be null!");

			resourceKey = _data.ResourceKey ?? throw new ArgumentNullException(nameof(_data), "Resource key may not be null!");
			resourceType = _data.ResourceType;

			dataFilePath = _dataFilePath ?? string.Empty;
			dataOffset = _data.DataOffset;
			dataSize = _data.DataSize;

			resource = null;
			LoadState = ResourceLoadState.NotLoaded;
		}

		public ResourceHandle(Resource _resource)
		{
			if (_resource == null) throw new ArgumentNullException(nameof(_resource), "Resource may not be null!");

			resourceManager = _resource.resourceManager;

			resourceKey = _resource.resourceKey;
			resourceType = _resource.ResourceType;

			dataFilePath = string.Empty;
			dataOffset = 0;
			dataSize = 0;

			resource = _resource;
			LoadState = ResourceLoadState.Loaded;
		}

		private ResourceHandle()
		{
			resourceManager = null!;

			resourceKey = string.Empty;
			resourceType = ResourceType.Unknown;

			dataFilePath = string.Empty;
			dataOffset = 0;
			dataSize = 0;

			resource = null;
			LoadState = ResourceLoadState.NotLoaded;
		}

		#endregion
		#region Fields

		public readonly ResourceManager resourceManager;

		public readonly string resourceKey;
		public readonly ResourceType resourceType;

		public readonly string dataFilePath;
		public readonly ulong dataOffset;
		public readonly ulong dataSize;

		public Resource? resource = null;

		public readonly string[]? dependencies = null;

		private static readonly ResourceHandle none = new();

		#endregion
		#region Properties

		public bool IsValid => !string.IsNullOrEmpty(resourceKey) && resourceType != ResourceType.Unknown && resourceType != ResourceType.Ignored;
		public bool IsLoaded => LoadState == ResourceLoadState.Loaded && resource != null && !resource.IsDisposed;

		public ResourceLoadState LoadState { get; internal set; } = ResourceLoadState.NotLoaded;

		public int DependencyCount => dependencies != null ? dependencies.Length : 0;

		public static ResourceHandle None => none;

		#endregion
		#region Methods

		public Resource? GetResource(bool _loadImmediatelyIfNotReady = true, bool _loadResourceIfNotReady = true)
		{
			if (!IsLoaded && _loadResourceIfNotReady)
			{
				Load(_loadImmediatelyIfNotReady);
			}
			return IsLoaded ? resource : null;
		}

		public bool Load(bool _loadImmediately)
		{
			// If resource is already fully loaded, do nothing:
			if (IsLoaded) return true;

			// If async loading is requested but the resource is already queued up, do nothing:
			if (LoadState != ResourceLoadState.NotLoaded && !_loadImmediately) return true;

			// Load the resource via the resource manager, passing it a private callback to return the loaded value later:
			return resourceManager.LoadResource(this, _loadImmediately, AssignResourceCallback);
		}

		public void Unload()
		{
			if (LoadState != ResourceLoadState.NotLoaded)
			{
				resourceManager.UnloadResource(resourceKey);
			}
		}

		private bool AssignResourceCallback(Resource? _resourceObject)
		{
			if (_resourceObject == null || _resourceObject.IsDisposed || !_resourceObject.IsLoaded)
			{
				resourceManager.engine.Logger.LogError($"Cannot assign null or disposed resource to resource handle; loading of resource '{resourceKey}' failed!");
				return false;
			}

			// Purge any previously loaded (and hence, outdated) version of the resource:
			if (resource != null && !resource.IsDisposed && resource != _resourceObject)
			{
				resource.Dispose();
			}

			resource = _resourceObject;
			return true;
		}

		public bool GetResourceHandleData(out ResourceHandleData _outData)
		{
			_outData = new()
			{
				ResourceKey = resourceKey,
				ResourceType = resourceType,

				DataOffset = dataOffset,
				DataSize = dataSize,
			};
			return true;
		}

		public bool Equals(ResourceHandle? other) => other is not null && string.CompareOrdinal(resourceKey, other.resourceKey) == 0;
		public override bool Equals(object? obj) => obj is ResourceHandle other && Equals(other);
		public override int GetHashCode() => base.GetHashCode();

		public static bool operator ==(ResourceHandle left, ResourceHandle right) => ReferenceEquals(left, right) || string.CompareOrdinal(left?.resourceKey, right?.resourceKey) == 0;
		public static bool operator !=(ResourceHandle left, ResourceHandle right) => !ReferenceEquals(left, right) && string.CompareOrdinal(left?.resourceKey, right?.resourceKey) != 0;

		public override string ToString()
		{
			return $"{resourceKey} ({resourceType}, {LoadState}) [@{dataFilePath ?? "NULL"},{dataOffset},{dataSize}]";
		}

		#endregion
	}
}
