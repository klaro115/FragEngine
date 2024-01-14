
namespace FragEngine3.Resources
{
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

				if (_resourceHandle.GetResource(true, true) is not T material || !material.IsLoaded)
				{
					_resourceHandle.resourceManager.engine.Logger.LogError($"Failed to load resource from handle '{_resourceHandle}'!");
					_outResourceIsReady = false;
					return false;
				}
				_resource = material;
			}
			_outResourceIsReady = true;
			return true;
		}

		#endregion
	}
}
