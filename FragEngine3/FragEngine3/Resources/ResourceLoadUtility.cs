using FragEngine3.Graphics.Resources;

namespace FragEngine3.Resources;

public static class ResourceLoadUtility
{
	#region Methods

	public static bool EnsureResourceIsLoaded<T>(ResourceHandle? _resourceHandle, ref T? _resource, bool _dontContinueUnlessFullyLoaded, out bool _outResourceIsReady) where T : Resource
	{
		// Check resource and load it now if necessary:
		if (_resource == null || _resource.IsDisposed)
		{
			if (_resourceHandle == null || !_resourceHandle.IsValid)
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

	public static bool EnsureMeshIsLoaded<T>(ResourceHandle? _resourceHandle, ref T? _mesh, ref float _boundingRadius, bool _dontContinueUnlessFullyLoaded, out bool _outResourceIsReady) where T : Mesh
	{
		// Check resource and load it now if necessary:
		if (_mesh == null || _mesh.IsDisposed)
		{
			if (_resourceHandle == null || !_resourceHandle.IsValid)
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

			if (_resourceHandle.GetResource(true, true) is not T typedMesh || !typedMesh.IsLoaded)
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
