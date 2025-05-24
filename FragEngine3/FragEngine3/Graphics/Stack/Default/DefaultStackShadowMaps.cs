using FragEngine3.EngineCore;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack.Default;

internal sealed class DefaultStackShadowMaps(GraphicsCore _graphicsCore, DefaultStackResources _resources)
{
	#region Fields

	private readonly GraphicsCore graphicsCore = _graphicsCore;
	private readonly Logger logger = _graphicsCore.graphicsSystem.Engine.Logger;
	private readonly DefaultStackResources resources = _resources;

	#endregion
	#region Methods

	public bool DrawShadowMaps(
		Scene _scene,
		in List<IRenderer> _renderers,
		in IList<CameraComponent> _cameras,
		in IList<ILightSource> _lights,
		out uint _outLightCount,
		out uint _outLightCountShadowMapped)
	{
		_outLightCount = (uint)_lights.Count;

		if (!IdentifyShadowFocalPoint(_scene, _cameras, out Vector3 focalPoint))
		{
			logger.LogError("Unable to determine focal point around which shadow maps rendering should be centered!");
			_outLightCountShadowMapped = 0;
			return false;
		}

		if (!graphicsCore.CreateCommandList(out CommandList? cmdList))
		{
			logger.LogError("Failed to create command list for rendering shadow maps!");
			_outLightCountShadowMapped = 0;
			return false;
		}

		if (!resources.CreateSceneContext(_scene, 0, 0, 0, out SceneContext? sceneCtx))
		{
			logger.LogError("Failed to create scene context for rendering shadow maps!");
			_outLightCountShadowMapped = 0;
			return false;
		}

		List<ILightSource> shadowCastingLights = _lights.Where(o => o.IsVisible && o.CastShadows).ToList();
		_outLightCountShadowMapped = (uint)shadowCastingLights.Count;
		if (_outLightCountShadowMapped == 0)
		{
			return true;
		}

		bool success = true;

		uint shadowMapCounter = 0u;
		foreach (ILightSource lightSource in shadowCastingLights)
		{
			success &= DrawShadowMapsForLightSource(
				in sceneCtx!,
				in cmdList!,
				lightSource,
				_renderers,
				focalPoint,
				ref shadowMapCounter);
		}

		// Upload shadow projection matrices, and commit command list:
		if (success)
		{
			success &= sceneCtx!.ShadowMapArray.FinalizeProjectionMatrices(cmdList!);
		}
		if (success)
		{
			success &= graphicsCore.CommitCommandList(cmdList!);
		}
		return success;
	}

	private static bool IdentifyShadowFocalPoint(Scene _scene, in IList<CameraComponent> _cameras, out Vector3 _outFocalPoint)
	{
		// Set focus around main camera, if it is located in the same scene:
		CameraComponent? mainCamera = CameraComponent.MainCamera;

		if (mainCamera is not null &&
			!mainCamera.IsDisposed &&
			mainCamera.node.scene == _scene)
		{
			_outFocalPoint = mainCamera.node.WorldPosition;
			return true;
		}

		// Fallback to the first live camera in the scene:
		CameraComponent? activeCamera = _cameras.FirstOrDefault(o => !o.IsDisposed);
		if (activeCamera is not null)
		{
			_outFocalPoint = activeCamera.node.WorldPosition;
			return true;
		}

		_outFocalPoint = Vector3.Zero;
		return false;
	}

	private bool DrawShadowMapsForLightSource(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		ILightSource _lightSource,
		List<IRenderer> _renderers,
		Vector3 _focalPoint,
		ref uint _shadowMapIdx)
	{
		// Identify which renderers are visible to this light's shadow camera:
		List<IRenderer> visibleRenderers = _renderers.Where(o => o.IsVisible && (_lightSource.LayerMask & o.LayerFlags) != 0).ToList();

		// Start drawing:
		if (!_lightSource.BeginDrawShadowMap(in _sceneCtx, LightConstants.directionalLightSize / 2, _shadowMapIdx))
		{
			logger.LogError($"Failed to begin drawing shadow maps for light source '{_lightSource}'!");
			return false;
		}

		bool success = true;

		// Draw shadow cascades:
		uint cascadeCount = _lightSource.ShadowCascades + 1;
		for (uint cascadeIdx = 0u; cascadeIdx < cascadeCount; ++cascadeIdx)
		{
			// Start camera pass for cascade:
			if (!_lightSource.BeginDrawShadowCascade(in _sceneCtx, in _cmdList, _focalPoint, cascadeIdx, out CameraPassContext shadowCameraCtx))
			{
				logger.LogError($"Failed to begin drawing shadow cascade '{cascadeIdx}' of light source '{_lightSource}'!");
				break;
			}

			// Render scene:
			foreach (IRenderer renderer in visibleRenderers)
			{
				success &= renderer.DrawShadowMap(_sceneCtx, shadowCameraCtx);
			}

			success &= _lightSource.EndDrawShadowCascade();

			// Store projection matrices:
			uint cascadeShadowMapIdx = _shadowMapIdx + cascadeIdx;
			success &= _sceneCtx.ShadowMapArray.SetShadowProjectionMatrices(cascadeShadowMapIdx, shadowCameraCtx.MtxWorld2Clip);
		}
		_shadowMapIdx += cascadeCount;

		// End drawing:
		success &= _lightSource.EndDrawShadowMap();
		return success;
	}

	#endregion
}
