using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Materials;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Renderers.Internal;

/// <summary>
/// Helper class with common resource management functionality for renderers.
/// </summary>
public static class RendererResourceHelper
{
	#region Methods

	/// <summary>
	/// Checks if a resource is already loaded and, if not, proceeds to load it now.
	/// </summary>
	/// <typeparam name="T">The resource type.</typeparam>
	/// <param name="_handle">A resource handle through which the resource is identified and loaded.</param>
	/// <param name="_resource">A reference to the resource we need to load.</param>
	/// <param name="_loadImmediately">Whether to block and load the resource immediately on the calling thread if it isn't loaded yet.</param>
	/// <param name="_outProceed">Outputs whether subsequent operations that rely on the resource may proceed.
	/// False if the resource is not in a loaded state once this method returns.</param>
	/// <param name="_outResourceChanged">Outputs whether the resource instance has changed after this method returns.</param>
	/// <returns>True if the check concluded without issues, or false, if loading has failed or an error was encountered.</returns>
	public static bool EnsureResourceIsLoaded<T>(
		ResourceHandle _handle,
		ref T? _resource,
		bool _loadImmediately,
		out bool _outProceed,
		out bool _outResourceChanged) where T : Resource
	{
		bool success = true;
		_outProceed = true;
		_outResourceChanged = false;

		if (_resource is null)
		{
			if (_handle is null || !_handle.IsValid)
			{
				Logger.Instance?.LogError($"Cannot ensure resource of type '{typeof(T).Name}' is loaded; handle is null or invalid!");
				_outProceed = false;
				return false;
			}

			T? prevResource = _resource;
			_resource = _handle.GetResource<T>(_loadImmediately);
			_outProceed = _resource is not null;
			_outResourceChanged = _resource != prevResource;

			if (_loadImmediately && !_outProceed)
			{
				Logger.Instance?.LogError($"Failed to load resource '{_handle.resourceKey}' of type '{typeof(T).Name}'!");
				success = false;
			}
		}
		return success;
	}

	/// <summary>
	/// Checks if a shadow material is already loaded and, if not, proceeds to load it now.
	/// If the scene material which the shadow material is assigned to isn't loaded either, it will be loaded first.
	/// </summary>
	/// <param name="_sceneMaterialHandle">A resource handle through which the regular scene material is identified and loaded.</param>
	/// <param name="_shadowMaterialHandle">A resource handle through which the shadow material is identified and loaded.</param>
	/// <param name="_sceneMaterial">A reference to the regular scene material which the shadow material is paired with.</param>
	/// <param name="_shadowMaterial">A reference to the shadow material we need to load.</param>
	/// <param name="_loadImmediately">Whether to block and load the resource immediately on the calling thread if it isn't loaded yet.</param>
	/// <param name="_outProceed">Outputs whether subsequent operations that rely on the shadow material may proceed.
	/// False if the material is not in a loaded state once this method returns.</param>
	/// <param name="_outMaterialsChanged">Outputs whether the scene or shadow material instances have changed after this method returns.</param>
	/// <returns>True if the check concluded without issues, or false, if loading has failed or an error was encountered.</returns>
	public static bool EnsureShadowMaterialIsLoaded(
		ResourceHandle _sceneMaterialHandle,
		ref ResourceHandle _shadowMaterialHandle,
		ref Material? _sceneMaterial,
		ref Material? _shadowMaterial,
		bool _loadImmediately,
		out bool _outProceed,
		out bool _outMaterialsChanged)
	{
		bool success = true;
		_outProceed = true;
		_outMaterialsChanged = false;

		if (_shadowMaterial is null)
		{
			if (_shadowMaterialHandle is null || !_shadowMaterialHandle.IsValid)
			{
				// Get shadow material from scene material:
				if (!EnsureResourceIsLoaded(_sceneMaterialHandle, ref _sceneMaterial, _loadImmediately, out _outProceed, out _outMaterialsChanged))
				{
					return false;
				}
				if (!_outProceed || !_sceneMaterial!.HasShadowMapMaterialVersion)
				{
					_outProceed = false;
					return true;
				}

				_shadowMaterialHandle = _sceneMaterial.ShadowMaterialHandle ?? ResourceHandle.None;
			}

			Material? prevShadowMaterial = _shadowMaterial;
			_shadowMaterial = _shadowMaterialHandle.GetResource<Material>(_loadImmediately);
			_outProceed = _shadowMaterial is not null;
			_outMaterialsChanged |= _shadowMaterial != prevShadowMaterial;

			if (_loadImmediately && !_outProceed)
			{
				Logger.Instance?.LogError($"Failed to load shadow material '{_shadowMaterialHandle.resourceKey}'!");
				success = false;
			}
		}
		return success;
	}

	#endregion
}
