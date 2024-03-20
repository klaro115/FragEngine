using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

internal abstract class LightInstance : IDisposable
{
	#region Constructors

	protected LightInstance(GraphicsCore _core)
	{
		core = _core;
	}

	~LightInstance()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	public readonly GraphicsCore core;

	public RgbaFloat lightColor = RgbaFloat.White;
	protected float lightIntensity = 1.0f;

	protected CameraInstance? shadowCameraInstance = null;
	protected ShadowCascadeResources[]? shadowCascades = null;

	protected bool castShadows = false;
	protected uint shadowCascadeCount = 0;
	private float shadowBias = 0.02f;

	public Pose worldPose = Pose.Identity;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;

	// LIGHT:

	public abstract LightType Type { get; }

	/// <summary>
	/// Gets or sets the intensity of light emitted by this light source. TODO: Figure out which unit to use for this.
	/// </summary>
	public virtual float LightIntensity
	{
		get => lightIntensity;
		set => lightIntensity = Math.Max(value, 0.0f);
	}

	public float MaxLightRange { get; protected set; } = MathF.Sqrt(1.0f / LightConstants.MIN_LIGHT_INTENSITY);

	// SHADOWS:

	public virtual bool CastShadows
	{
		get => castShadows;
		set
		{
			castShadows = value;
			if (!castShadows)
			{
				ShadowMapIdx = 0;

				shadowCameraInstance?.Dispose();
				shadowCameraInstance = null;
				DisposeShadowCascades();
			}
		}
	}

	public uint ShadowMapIdx { get; protected set; } = 0;

	/// <summary>
	/// Gets or sets the number of shadow cascades to create and render for this light source.
	/// Directional and spot lights only. Must be a value between 0 and 4, where 0 disables cascades for this light.
	/// </summary>
	public uint ShadowCascades
	{
		get => shadowCascadeCount;
		set => shadowCascadeCount = Math.Min(value, MaxShadowCascades);
	}
	public virtual uint MaxShadowCascades => 4;

	/// <summary>
	/// A bias for shadow map evaluation in the shader, which is implemented as a distance offset away from a mesh's surface.
	/// Setting this value too low may cause stair-stepping artifacts in lighting calculations.
	/// </summary>
	public float ShadowBias
	{
		get => shadowBias;
		set => shadowBias = Math.Clamp(value, 0, 10);
	}

	// MISC:

	protected Logger Logger => core.graphicsSystem.engine.Logger;

	#endregion
	#region Methods

	public void Dispose()
	{
		GC.SuppressFinalize(this);
		Dispose(true);
	}
	protected virtual void Dispose(bool _)
	{
		IsDisposed = true;

		shadowCameraInstance?.Dispose();
		DisposeShadowCascades();
	}

	private void DisposeShadowCascades()
	{
		if (shadowCascades != null)
		{
			foreach (ShadowCascadeResources cascade in shadowCascades)
			{
				cascade.Dispose();
			}
			shadowCascades = null;
		}
	}

	/// <summary>
	/// Get a nicely packed structure containing all information about this light source for upload to a GPU buffer.
	/// </summary>
	public abstract LightSourceData GetLightSourceData();

	public bool BeginDrawShadowMap(
			in SceneContext _sceneCtx,
			float _shadingFocalPointRadius,
			uint _newShadowMapIdx)
	{
		if (IsDisposed)
		{
			Logger.LogError("Can't begin drawing shadow map for disposed light instance!");
			return false;
		}
		if (!CastShadows)
		{
			return false;
		}
		if (_sceneCtx.texShadowMaps == null || _sceneCtx.texShadowMaps.IsDisposed)
		{
			Logger.LogError("Can't begin drawing shadow map using null shadow map texture array!");
			return false;
		}

		ShadowMapIdx = _newShadowMapIdx;

		// Ensure shadow cascades are all ready to go:
		if (shadowCascades == null || shadowCascades.Length < shadowCascadeCount + 1)
		{
			DisposeShadowCascades();

			shadowCascades = new ShadowCascadeResources[shadowCascadeCount + 1];
			for (uint i = 0; i < shadowCascadeCount + 1; ++i)
			{
				shadowCascades[i] = new ShadowCascadeResources(this, i);
			}
		}

		// Ensure a camera instance is ready for drawing the scene:
		if (!UpdateShadowMapCameraInstance(_shadingFocalPointRadius))
		{
			return false;
		}

		return true;
	}

	public bool BeginDrawShadowCascade(
			in SceneContext _sceneCtx,
			in CommandList _cmdList,
			in DeviceBuffer _dummyBufLights,
			in Pose _lightSourceWorldPose,
			Vector3 _shadingFocalPoint,
			uint _cascadeIdx,
			out CameraPassContext _outCameraPassCtx,
			bool _rebuildResSetCamera = false,
			bool _texShadowMapsHasChanged = false)
	{
		// Select the right shadow cascade resource container:
		_cascadeIdx = Math.Min(_cascadeIdx, shadowCascadeCount);

		ShadowCascadeResources cascade = shadowCascades![_cascadeIdx];

		// Recalculate projection for this cascade:
		cascade.mtxShadowWorld2Clip = RecalculateShadowProjectionMatrix(_shadingFocalPoint, _cascadeIdx);

		// Update framebuffer, constant buffers and resource sets:
		if (!cascade.UpdateResources(
			in _sceneCtx,
			in _dummyBufLights,
			in shadowCameraInstance!,
			in _lightSourceWorldPose,
			ShadowMapIdx,
			_rebuildResSetCamera,
			_texShadowMapsHasChanged,
			out bool _,
			out bool _))
		{
			Logger.LogError($"Failed to update shadow cascade resources for cascade {_cascadeIdx} of shadow map index {ShadowMapIdx}!");
			_outCameraPassCtx = null!;
			return false;
		}

		if (!shadowCameraInstance!.SetOverrideFramebuffer(cascade.ShadowMapFrameBuffer, true))
		{
			Logger.LogError($"Failed to set framebuffer for shadow cascade {_cascadeIdx} of shadow map index {ShadowMapIdx}!");
			_outCameraPassCtx = null!;
			return false;
		}

		// Bind framebuffers and clear targets:
		if (!shadowCameraInstance!.BeginDrawing(_cmdList, true, false, out _))
		{
			Logger.LogError("Failed to begin drawing light instance's shadow map!");
			_outCameraPassCtx = null!;
			return false;
		}

		// Assemble context object for renderers to reference when issuing draw calls:
		_outCameraPassCtx = new(
			shadowCameraInstance!,
			_cmdList,
			cascade.ShadowMapFrameBuffer!,
			cascade.ShadowResSetCamera!,
			cascade.ShadowCbCamera!,
			_dummyBufLights,
			0,
			ShadowMapIdx,
			0,
			0,
			in cascade.mtxShadowWorld2Clip);

		return true;
	}

	public bool EndDrawShadowCascade()
	{
		return !IsDisposed && shadowCameraInstance != null && shadowCameraInstance.EndDrawing();
	}

	protected abstract bool UpdateShadowMapCameraInstance(float _shadingFocalPointRadius);

	protected abstract Matrix4x4 RecalculateShadowProjectionMatrix(Vector3 _shadingFocalPoint, uint _cascadeIdx);

	public abstract bool CheckVisibilityByCamera(in Camera _camera);

	public abstract bool LoadFromData(in LightData _lightData);
	public abstract bool SaveToData(out LightData _outLightData);

	#endregion
}
