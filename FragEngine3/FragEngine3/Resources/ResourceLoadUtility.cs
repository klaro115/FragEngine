using FragEngine3.Graphics.Renderers;
using FragEngine3.Graphics.Resources;

namespace FragEngine3.Resources;

/// <summary>
/// Helper class for loading or preparing resources for use.
/// </summary>
[Obsolete("not used at this time, methods may be outdated")]
public static class ResourceLoadUtility
{
	#region Methods

	/// <summary>
	/// Checks and ensures that a resource is actually loaded. If the resource is not yet loaded, loading is either queued up or executed immediately on the calling thread.
	/// </summary>
	/// <typeparam name="T">The type of the resource.</typeparam>
	/// <param name="_resourceHandle">A resource handle for which we wish to fetch and ensure loading of a resource. If null and the resource is not loaded, the method will return false.</param>
	/// <param name="_resource">A reference to the resource object itself. If null or disposed, it will be loaded from file. If non-null and ready, this method will do nothing.</param>
	/// <param name="_dontContinueUnlessFullyLoaded">Whether the caller should abort execution if the resource is not fully loaded when this function returns. If false, it may continue, but the
	/// resource may still be pending for asynchronous loading in the background. If true, the resource will be loaded immediately on the calling thread, possible inducing lag spikes.</param>
	/// <param name="_outResourceIsReady">Outputs whether the resource has been fully loaded and is ready for immediate use.</param>
	/// <returns>True if the resource is loaded, or if it has been queued for async loading. False if the resource is not loaded and loading has failed.</returns>
	[Obsolete($"Replaced by new implementation in {nameof(StaticMeshRenderer)}")]
	public static bool EnsureResourceIsLoaded<T>(ResourceHandle? _resourceHandle, ref T? _resource, bool _dontContinueUnlessFullyLoaded, out bool _outResourceIsReady) where T : Resource
	{
		// Check resource and load it now if necessary:
		if (_resource is null || _resource.IsDisposed)
		{
			if (_resourceHandle is null || !_resourceHandle.IsValid)
			{
				_outResourceIsReady = false;
				return false;
			}
			// Abort process until resource is ready, queue it up for background loading:
			if (_dontContinueUnlessFullyLoaded && !_resourceHandle.IsLoaded)
			{
				if (_resourceHandle.LoadState == ResourceLoadState.NotLoaded) _resourceHandle.Load(false);
				_outResourceIsReady = false;
				return true;
			}

			if (_resourceHandle.GetResource(true, true) is not T typedResource || !typedResource.IsLoaded)
			{
				_resourceHandle.resourceManager.engine.Logger.LogError($"Failed to load resource from handle '{_resourceHandle}'!");
				_outResourceIsReady = false;
				return false;
			}
			_resource = typedResource;
		}
		_outResourceIsReady = true;
		return true;
	}

	[Obsolete($"Replaced by new implementation in {nameof(StaticMeshRenderer)}")]
	public static bool EnsureMeshIsLoaded(ResourceHandle? _resourceHandle, ref Mesh? _mesh, ref float _boundingRadius, bool _dontContinueUnlessFullyLoaded, out bool _outResourceIsReady)
	{
		// Check resource and load it now if necessary:
		if (_mesh is null || _mesh.IsDisposed)
		{
			if (_resourceHandle is null || !_resourceHandle.IsValid)
			{
				_outResourceIsReady = false;
				return false;
			}
			// Abort process until mesh is ready, queue it up for background loading:
			if (_dontContinueUnlessFullyLoaded && !_resourceHandle.IsLoaded)
			{
				if (_resourceHandle.LoadState == ResourceLoadState.NotLoaded) _resourceHandle.Load(false);
				_outResourceIsReady = false;
				return true;
			}

			if (_resourceHandle.GetResource(true, true) is not Mesh typedMesh || !typedMesh.IsLoaded)
			{
				_resourceHandle.resourceManager.engine.Logger.LogError($"Failed to load mesh from handle '{_resourceHandle}'!");
				_outResourceIsReady = false;
				return false;
			}
			_mesh = typedMesh;
			_boundingRadius = _mesh.BoundingRadius;
		}
		_outResourceIsReady = true;
		return true;
	}

	#endregion
}
