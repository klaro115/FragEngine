using FragEngine3.EngineCore;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Stack.ForwardPlusLights;

internal sealed class ForwardPlusLightsShadowMaps(ForwardPlusLightsStack _stack, ForwardPlusLightsSceneObjects _sceneObjects) : IDisposable
{
	#region Constructors

	~ForwardPlusLightsShadowMaps()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core = _stack.core;
	public readonly ForwardPlusLightsStack stack = _stack;
	private readonly ForwardPlusLightsSceneObjects sceneObjects = _sceneObjects;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	public ShadowMapArray? shadowMapArray { get; private set; } = null;
	public LightDataBuffer? dummyLightDataBuffer { get; private set; } = null;

	private Logger Logger => core.graphicsSystem.engine.Logger ?? Logger.Instance!;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}

	private void Dispose(bool _)
	{
		IsDisposed = true;

		dummyLightDataBuffer?.Dispose();
		shadowMapArray?.Dispose();
	}

	public bool Initialize()
	{
		if (dummyLightDataBuffer is null || dummyLightDataBuffer.IsDisposed)
		{
			dummyLightDataBuffer = new(core, 1);
		}

		if (shadowMapArray is null || shadowMapArray.IsDisposed)
		{
			shadowMapArray = new(core, 1);
		}

		return true;
	}

	public bool PrepareShadowMaps(uint _maxActiveLightCount, ref bool _outRebuildResSetCamera, ref ushort _sceneResourceVersion, out bool _outTexShadowsHasChanged)
	{
		_outTexShadowsHasChanged = false;

		uint minLightCountShadowMapped = Math.Min(sceneObjects.ActiveShadowMappedLightsCount, _maxActiveLightCount);
		uint totalShadowCascadeCount = minLightCountShadowMapped;

		foreach (ILightSource light in sceneObjects.activeLightsShadowMapped)
		{
			totalShadowCascadeCount += Math.Max(light.ShadowCascades, 1);
		}

		// Prepare shadow map texture arrays, sampler, and matrix buffer:
		if (!shadowMapArray!.PrepareTextureArrays(totalShadowCascadeCount, out _outTexShadowsHasChanged))
		{
			return false;
		}
		_outRebuildResSetCamera |= _outTexShadowsHasChanged;

		if (_outTexShadowsHasChanged)
		{
			_sceneResourceVersion++;
		}

		return true;
	}

	public bool DrawShadowMaps(
		in SceneContext _sceneCtx,
		Vector3 _renderFocalPoint,
		float _renderFocalRadius,
		uint _maxActiveLightCount,
		bool _rebuildResSetCamera,
		bool _texShadowsHasChanged,
		out uint _outShadowMapLightCount)
	{
		// No visible shadow-casting light? We're done here:
		_outShadowMapLightCount = 0;
		if (sceneObjects.ActiveShadowMappedLightsCount == 0)
		{
			return true;
		}

		// Fetch or create a command list for shadow rendering:
		if (!stack.GetOrCreateCommandList(out CommandList cmdList))
		{
			return false;
		}
		cmdList.Begin();

		bool success = true;

		int shadowMappedLightCount = Math.Min((int)sceneObjects.ActiveShadowMappedLightsCount, (int)_maxActiveLightCount);

		List<IRenderer> filteredShadowCasters = new(sceneObjects.ActiveShadowCasterCount);

		try
		{
			bool result = true;
			int i = 0;
			uint shadowMapArrayIdx = 0;

			while (i < shadowMappedLightCount && result)
			{
				// Begin drawing shadow maps for the current light source:
				ILightSource light = sceneObjects.activeLightsShadowMapped[i];
				result &= light.BeginDrawShadowMap(
					in _sceneCtx,
					_renderFocalRadius,
					_outShadowMapLightCount);

				if (!result) break;

				// Draw renderers only for non-static or dirty lights:
				bool redrawShadowMap = !light.IsStaticLight || light.IsStaticLightDirty;

				// Exclude renderers that are entirely outside of point/spot lights' maximum range:
				if (redrawShadowMap)
				{
					filteredShadowCasters.Clear();
					foreach (IRenderer renderer in sceneObjects.activeShadowCasters)
					{
						if (renderer is IPhysicalRenderer physicalRenderer)
						{
							if (light.CheckIsRendererInRange(in physicalRenderer))
							{
								filteredShadowCasters.Add(renderer);
							}
						}
						else
						{
							filteredShadowCasters.Add(renderer);
						}
					}
				}

				// Render shadow cascades one at after the other:
				uint shadowCascadeCount = light.ShadowCascades + 1;

				for (uint cascadeIdx = 0; cascadeIdx < shadowCascadeCount; ++cascadeIdx)
				{
					// Begin drawing shadow maps for the current casacade:
					result &= light.BeginDrawShadowCascade(
						in _sceneCtx,
						in cmdList,
						_renderFocalPoint,
						cascadeIdx,
						out CameraPassContext lightCtx,
						_rebuildResSetCamera,
						_texShadowsHasChanged);

					if (redrawShadowMap)
					{
						// Draw renderers for opaque and tranparent geometry, ignore UI:
						foreach (IRenderer renderer in filteredShadowCasters)
						{
							if ((light.LayerMask & renderer.LayerFlags) != 0)
							{
								result &= renderer.DrawShadowMap(_sceneCtx, lightCtx);
							}
						}
					}

					result &= light.EndDrawShadowCascade();

					// Store projection matrix for later scene rendering calls:
					shadowMapArray!.SetShadowProjectionMatrices(shadowMapArrayIdx++, lightCtx.MtxWorld2Clip);
				}

				result &= light.EndDrawShadowMap();
				_outShadowMapLightCount += shadowCascadeCount;
				i++;
			}
		}
		catch (Exception ex)
		{
			Logger.LogException($"An unhandled exception was caught while drawing shadow maps, around shadow map index {_outShadowMapLightCount}!", ex);
			success = false;
		}

		// Upload all shadow projection matrices to GPU buffer:
		success &= shadowMapArray!.FinalizeProjectionMatrices();

		// If any shadows maps were rendered, submit command list for execution:
		cmdList.End();
		if (_outShadowMapLightCount != 0)
		{
			success &= core.CommitCommandList(cmdList);
		}
		return success;
	}

	#endregion
}
