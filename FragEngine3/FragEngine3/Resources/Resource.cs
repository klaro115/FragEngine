using FragEngine3.EngineCore;

namespace FragEngine3.Resources;

/// <summary>
/// Abstract parent class for assets and resources. This type represents a data object to be used by the application's systems; loading and management
/// of reusable and imported resources should be done through a '<see cref="ResourceHandle"/>'.
/// </summary>
public abstract class Resource : IDisposable
{
	#region Constructors

	protected Resource(ResourceHandle _handle)
	{
		resourceKey = _handle?.resourceKey ?? throw new ArgumentNullException(nameof(_handle), "Resource handle and key may not be null!");
		resourceManager = _handle.resourceManager ?? throw new NullReferenceException("Resource manager may not be null!");
	}
	protected Resource(string _resourceKey, Engine _engine)
	{
		resourceKey = _resourceKey ?? throw new ArgumentNullException(nameof(_resourceKey), "Resource key may not be null!");
		resourceManager = _engine?.ResourceManager ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
	}

	~Resource()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly string resourceKey = string.Empty;
	public readonly ResourceManager resourceManager;

	#endregion
	#region Properties

	/// <summary>
	/// Whether the resource's unmanaged dependencies and data have been released safely. A resource instance can no longer be used after disposal,
	/// as it marks its end-of-life state. Disposal of a resource object is equivalent to it being completely unloaded; to access the resource data
	/// again, discard this instance, and import it anew from its handle, then update any references to the resource to use the newly loaded instance
	/// instead.
	/// </summary>
	public bool IsDisposed { get; protected set; } = false;
	/// <summary>
	/// Whether this resource object is fully loaded and ready for use. Resource objects may in rare cases exist in an incomplete, or not fully loaded state
	/// during their loading process. Using a resource handle to track their load state should however prevent any such premature usage of a resource.
	/// </summary>
	public virtual bool IsLoaded => !IsDisposed;

	/// <summary>
	/// The general type of resources this belongs to.
	/// </summary>
	public abstract ResourceType ResourceType { get; }

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	protected virtual void Dispose(bool _disposing)
	{
		IsDisposed = true;
		// further custom disposal logic in overrides as needed.
	}

	/// <summary>
	/// Get the resource handle tied to this resource.
	/// </summary>
	/// <param name="_outHandle">Outputs the resource handle that was used to create this resource. Under normal circumstances, this should never be null or invalid.</param>
	/// <returns>True if the resource handle could be retrieved from the resource manager, false otherwise.</returns>
	public bool GetResourceHandle(out ResourceHandle _outHandle) => resourceManager.GetResource(resourceKey, out _outHandle);

	/// <summary>
	/// Iterate over all dependencies of this resource, using a depth-first recursive search.
	/// </summary>
	/// <returns>Yields handles to all resources this resource depends on, including itself.</returns>
	public abstract IEnumerator<ResourceHandle> GetResourceDependencies();

	public static bool CompareKeys(Resource? _resourceA, Resource? _resourceB)
	{
		string resourceKeyA = _resourceA?.resourceKey ?? string.Empty;
		string resourceKeyB = _resourceB?.resourceKey ?? string.Empty;
		return resourceKeyA == resourceKeyB;
	}

	public static bool CompareKeys(Resource? _resourceA, ResourceHandle? _resourceB)
	{
		string resourceKeyA = _resourceA?.resourceKey ?? string.Empty;
		string resourceKeyB = _resourceB?.resourceKey ?? string.Empty;
		return resourceKeyA == resourceKeyB;
	}

	public static bool CompareKeys(ResourceHandle? _resourceA, ResourceHandle? _resourceB)
	{
		string resourceKeyA = _resourceA?.resourceKey ?? string.Empty;
		string resourceKeyB = _resourceB?.resourceKey ?? string.Empty;
		return resourceKeyA == resourceKeyB;
	}

	#endregion
}
