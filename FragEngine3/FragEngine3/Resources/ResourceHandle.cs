using FragEngine3.Resources.Data;

namespace FragEngine3.Resources;

/// <summary>
/// Handle type for managing and loading resources and assets. Ownership of handles lies with the engine's <see cref="ResourceManager"/>.
/// This handle allows safe referencing of resources, ensuring that each resource's data is loaded only once and when needed. The handle
/// obtains full ownership of its resource data upon loading.<para/>
/// To use the resource's data, call '<see cref="Load(bool)"/>', triggering either asynchronous loading in a background thread, or
/// immediate loading on the calling thread. Immediate loading will block until loading has concluded. To release a resource's data,
/// use '<see cref="Unload"/>', disposing the resource object and dropping all references to it.<para/>
/// Systems that use resources should only reference them through their handle as it exposes the resource's current state, and should
/// never dispose the resource data directly without going through the handle first. Multiple systems may reference a same resource object
/// and safely access it via its handle. Resources are uniquely identifiable by their '<see cref="resourceKey"/>'; duplicate resource keys
/// in resource files may overwrite any additional declarations.
/// </summary>
public sealed class ResourceHandle : IEquatable<ResourceHandle>
{
	#region Types

	/// <summary>
	/// Callback function for assigning a resource object to a handle after it has finished loading.
	/// </summary>
	/// <param name="_resourceObject">The resource object that has just finished import, or null, if loading failed.</param>
	/// <returns>True if the resource object could be loaded successfully, false otherwise.</returns>
	internal delegate bool FuncAssignResourceCallback(Resource? _resourceObject);

	#endregion
	#region Constructors

	public ResourceHandle(ResourceManager _resourceManager, ResourceHandleData _data, string _resourceFileKey)
	{
		if (_data is null) throw new ArgumentNullException(nameof(_data), "Resource handle data may not be null!");

		resourceManager = _resourceManager ?? throw new ArgumentNullException(nameof(_resourceManager), "Resource manager may not be null!");

		resourceKey = _data.ResourceKey ?? throw new ArgumentNullException(nameof(_data), "Resource key may not be null!");
		resourceType = _data.ResourceType;
		importFlags = _data.ImportFlags;

		fileKey = _resourceFileKey ?? string.Empty;
		dataOffset = _data.DataOffset;
		dataSize = _data.DataSize;

		if (_data.Dependencies is not null && _data.DependencyCount > 0)
		{
			if (_data.Dependencies.Length == _data.DependencyCount)
			{
				dependencies = _data.Dependencies;
			}
			else
			{
				int actualDependencyCount = Math.Min((int)_data.DependencyCount, _data.Dependencies.Length);
				dependencies = new string[actualDependencyCount];
				Array.Copy(_data.Dependencies, dependencies, actualDependencyCount);
			}
		}
		else
		{
			dependencies = null;
		}

		resource = null;
		LoadState = ResourceLoadState.NotLoaded;
	}

	public ResourceHandle(Resource _resource)
	{
		if (_resource is null) throw new ArgumentNullException(nameof(_resource), "Resource may not be null!");

		resourceManager = _resource.resourceManager;

		resourceKey = _resource.resourceKey;
		resourceType = _resource.ResourceType;
		importFlags = null;

		fileKey = string.Empty;
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
		importFlags = string.Empty;

		fileKey = string.Empty;
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
	public readonly string? importFlags;

	public readonly string fileKey;
	public readonly ulong dataOffset;
	public readonly ulong dataSize;

	private Resource? resource = null;

	public readonly string[]? dependencies = null;

	private static readonly ResourceHandle none = new();

	#endregion
	#region Properties

	/// <summary>
	/// Gets whether this resource handle is valid and usable.
	/// </summary>
	public bool IsValid => !string.IsNullOrEmpty(resourceKey) && resourceType != ResourceType.Unknown && resourceType != ResourceType.Ignored;
	/// <summary>
	/// Gets whether the resource is currently in a fully loaded and immediately usable state.
	/// </summary>
	public bool IsLoaded => LoadState == ResourceLoadState.Loaded && resource is not null && !resource.IsDisposed;

	public ResourceLoadState LoadState { get; internal set; } = ResourceLoadState.NotLoaded;

	/// <summary>
	/// Gets the number of other resources that this one is immediately dependent on.
	/// </summary>
	public int DependencyCount => dependencies is not null ? dependencies.Length : 0;

	/// <summary>
	/// Gets an invalid empty resource handle.
	/// </summary>
	public static ResourceHandle None => none;

	#endregion
	#region Methods

	/// <summary>
	/// Gets the resource object held by this handle.
	/// </summary>
	/// <param name="_loadImmediatelyIfNotReady">Whether to load the resource immediately on the current thread if it isn't fully loaded
	/// yet. If false, the method will return null until the resource has been loaded, either asynchronously or through some other call
	/// to load it. This parameter does nothing if '_loadResourceIfNotReady' is set to false.</param>
	/// <param name="_loadResourceIfNotReady">Whether to queue the resource up for immediate or asynchronous loading, if it isn't loaded
	/// yet. When in doubt, leave this on the default value.</param>
	/// <returns>The resource object held by this handle, or null, if the resource is not yet loaded, or if it could not be loaded.</returns>
	public Resource? GetResource(bool _loadImmediatelyIfNotReady = true, bool _loadResourceIfNotReady = true)
	{
		if (!IsLoaded && _loadResourceIfNotReady)
		{
			Load(_loadImmediatelyIfNotReady);
		}
		return IsLoaded ? resource : null;
	}

	/// <summary>
	/// Gets the resource object held by this handle.
	/// </summary>
	/// <typeparam name="T">The type of the resource you're expecting. Must be a non-abstract type inheriting from <see cref="Resource"/>.
	/// </typeparam>
	/// <param name="_loadImmediatelyIfNotReady">Whether to load the resource immediately on the current thread if it isn't fully loaded
	/// yet. If false, the method will return null until the resource has been loaded, either asynchronously or through some other call
	/// to load it. This parameter does nothing if '_loadResourceIfNotReady' is set to false.</param>
	/// <param name="_loadResourceIfNotReady">Whether to queue the resource up for immediate or asynchronous loading, if it isn't loaded
	/// yet. When in doubt, leave this on the default value.</param>
	/// <returns>The resource object held by this handle, or null, if the resource is not yet loaded, or if it could not be loaded, or
	/// if the resource's type did not match the generic parameter type T.</returns>
	public T? GetResource<T>(bool _loadImmediatelyIfNotReady = true, bool _loadResourceIfNotReady = true) where T : Resource
	{
		return GetResource(_loadImmediatelyIfNotReady, _loadResourceIfNotReady) as T;
	}

	/// <summary>
	/// Trigger loading of this handle's resource, if it wasn't loaded yet.
	/// </summary>
	/// <param name="_loadImmediately">Whether to load the resource immediately on the current thread. If false, the resource will instead
	/// be queued up for asynchronous loading and will be ready for use at some later time.</param>
	/// <returns>True if the resource was already loaded, or if immediate loaded succeeded, or if it was queued up for asynchronous loading.
	/// </returns>
	public bool Load(bool _loadImmediately)
	{
		// If resource is already fully loaded, do nothing:
		if (IsLoaded) return true;

		// If async loading is requested but the resource is already queued up, do nothing:
		if (LoadState != ResourceLoadState.NotLoaded && !_loadImmediately) return true;

		// Load the resource via the resource manager, passing it a private callback to return the loaded value later:
		return resourceManager.LoadResource(this, _loadImmediately, AssignResourceCallback);
	}

	/// <summary>
	/// Request the resource manager to unload the resourcee data held by this handle, or to cancel a pending import.
	/// </summary>
	public void Unload()
	{
		if (LoadState != ResourceLoadState.NotLoaded)
		{
			resourceManager.UnloadResource(resourceKey);
		}
	}

	private bool AssignResourceCallback(Resource? _resourceObject)
	{
		if (_resourceObject is null || _resourceObject.IsDisposed || !_resourceObject.IsLoaded)
		{
			resourceManager.engine.Logger.LogError($"Cannot assign null or disposed resource to resource handle; loading of resource file '{fileKey}' failed!");
			return false;
		}

		// Purge any previously loaded (and hence, outdated) version of the resource:
		if (resource is not null && !resource.IsDisposed && resource != _resourceObject)
		{
			resource.Dispose();
		}

		resource = _resourceObject;
		return true;
	}

	/// <summary>
	/// Gets a serializable data object representing this resource handle.
	/// </summary>
	/// <param name="_outData">Outputs a serializable data object.</param>
	/// <returns>True if data was prepared, false otherwise.</returns>
	public bool GetResourceHandleData(out ResourceHandleData _outData)
	{
		_outData = new()
		{
			ResourceKey = resourceKey,
			ResourceType = resourceType,
			ImportFlags = importFlags,

			DataOffset = dataOffset,
			DataSize = dataSize,
		};
		return true;
	}

	public bool Equals(ResourceHandle? other) => other is not null && string.CompareOrdinal(resourceKey, other.resourceKey) == 0;
	public override bool Equals(object? obj) => obj is ResourceHandle other && Equals(other);
	public override int GetHashCode() => base.GetHashCode();

	public static bool operator ==(ResourceHandle? left, ResourceHandle? right) => ReferenceEquals(left, right) || (left is not null && left.Equals(right));
	public static bool operator !=(ResourceHandle? left, ResourceHandle? right) => !ReferenceEquals(left, right) || (left is not null && !left.Equals(right));

	public override string ToString()
	{
		const int maxFileKeyLength = 48;
		string? fileKeyTrunc = fileKey is not null && fileKey.Length > maxFileKeyLength ? "..." + fileKey[(fileKey.Length - maxFileKeyLength - 2)..] : fileKey;
		string importFlagTxt = !string.IsNullOrEmpty(importFlags) ? $",Flags='{importFlags}'" : string.Empty;
		return $"{resourceKey} ({resourceType}, {LoadState}) [@{fileKeyTrunc ?? "NULL"},{dataOffset},{dataSize}{importFlagTxt}]";
	}

	#endregion
}
