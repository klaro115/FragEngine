using FragEngine3.EngineCore;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting.Data;
using FragEngine3.Graphics.Lighting.Internal;
using FragEngine3.Scenes;
using System.Numerics;
using Veldrid;

namespace FragEngine3.Graphics.Lighting;

internal abstract class Light(GraphicsCore _core) : ILightSource
{
	#region Types

	[Flags]
	protected enum StaticLightDirtyFlags
	{
		Data			= 1,
		Frame			= 2,

		All				= Data | Frame,
	}

	#endregion
	#region Constructors

	~Light()
	{
		Dispose(false);
	}

	#endregion
	#region Fields

	protected LightSourceData data = new()
	{
		color = Vector3.One,
		intensity = 5.0f,
		position = Vector3.Zero,
		type = (uint)LightType.Point,
		direction = Vector3.UnitZ,
		spotMinDot = 0,
		shadowMapIdx = 0,
		shadowNormalBias = 0.015f,
		shadowCascades = 0,
		shadowCascadeRange = LightConstants.directionalLightSize,
		shadowDepthBias = Vector3.Zero,
	};
	protected float shadowDepthBias = 0.01f;

	protected CameraInstance? shadowCameraInstance = null;
	protected ShadowCascadeResources[]? shadowCascades = null;
	protected ShadowMapArray? staticShadowMapArray = null;

	protected bool castShadows = false;
	protected bool isStaticLight = false;
	protected StaticLightDirtyFlags staticLightDirtyFlags = 0;

	protected Pose worldPose = Pose.Identity;

	#endregion
	#region Properties

	public bool IsDisposed { get; private set; } = false;
	public bool IsVisible => !IsDisposed;

	// LIGHT:

	public int LightPriority { get; set; } = 1;
	public uint LayerMask { get; set; } = 0xFFu;
	public abstract LightType Type { get; }

	/// <summary>
	/// Gets or sets the intensity of light emitted by this light source. TODO: Figure out which unit to use for this. (unit may be entirely dependent on shader logic)
	/// </summary>
	public abstract float LightIntensity { get; set; }

	public float MaxLightRange { get; protected set; } = MathF.Sqrt(1.0f / LightConstants.MIN_LIGHT_INTENSITY);

	public Pose WorldPose
	{
		get => worldPose;
		set
		{
			worldPose = value;
			worldPose.scale = Vector3.One;
		}
	}

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
			else
			{
				staticLightDirtyFlags = isStaticLight ? StaticLightDirtyFlags.All : 0;
			}
		}
	}

	public uint ShadowMapIdx
	{
		get => data.shadowMapIdx;
		protected set => data.shadowMapIdx = value;
	}

	public uint ShadowCascades
	{
		get => data.shadowCascades;
		set => data.shadowCascades = Math.Min(value, MaxShadowCascades);
	}
	public virtual uint MaxShadowCascades => 4;

	/// <summary>
	/// A bias for shadow map evaluation in the shader, which is implemented as a distance offset away from a mesh's surface.
	/// Setting this value too low may cause stair-stepping artifacts in lighting calculations, commonly referred to as "shadow acne".
	/// </summary>
	public float ShadowNormalBias
	{
		get => data.shadowNormalBias;
		set => data.shadowNormalBias = Math.Clamp(value, 0, 10);
	}

	/// <summary>
	/// A bias for shadow map evaluation in shadow projection, which is implemented as a distance offset towards the light source.
	/// Setting this value too high may cause "Peter-Panning" artifacts in lighting calculations, where objects appear detached from their shadow.
	/// </summary>
	public float ShadowDepthBias
	{
		get => shadowDepthBias;
		set => shadowDepthBias = Math.Clamp(value, -10.0f, 10.0f);
	}

	// STATIC SHADOWS:

	public bool IsStaticLight
	{
		get => isStaticLight;
		set
		{
			isStaticLight = value;
			staticLightDirtyFlags = CastShadows && isStaticLight ? StaticLightDirtyFlags.All : 0;
		}
	}

	public bool IsStaticLightDirty => isStaticLight && staticLightDirtyFlags.HasFlag(StaticLightDirtyFlags.Frame);

	// MISC:

	public GraphicsCore GraphicsCore { get; } = _core;

	protected Logger Logger => GraphicsCore.graphicsSystem.Engine.Logger;

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
		if (shadowCascades is not null)
		{
			foreach (ShadowCascadeResources cascade in shadowCascades)
			{
				cascade.Dispose();
			}
			shadowCascades = null;
		}

		staticShadowMapArray?.Dispose();
	}

	/// <summary>
	/// For static lights (i.e. <see cref="IsStaticLight"/> is true), a redraw of all shadow maps and lighting data is scheduled
	/// for the next frame. On non-static lights, this does nothing.
	/// </summary>
	public void RequestRedrawStaticLighting()
	{
		if (IsDisposed || !IsStaticLight) return;

		staticLightDirtyFlags = StaticLightDirtyFlags.All;
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
		if (_sceneCtx.ShadowMapArray.TexDepthMapArray is null || _sceneCtx.ShadowMapArray.TexDepthMapArray.IsDisposed)
		{
			Logger.LogError("Can't begin drawing shadow map using null shadow map texture array!");
			return false;
		}

		ShadowMapIdx = _newShadowMapIdx;

		// Ensure shadow cascades are all ready to go:
		if (shadowCascades is null || shadowCascades.Length < data.shadowCascades + 1)
		{
			DisposeShadowCascades();

			shadowCascades = new ShadowCascadeResources[data.shadowCascades + 1];
			for (uint i = 0; i < data.shadowCascades + 1; ++i)
			{
				shadowCascades[i] = new ShadowCascadeResources(this, i);
			}
		}

		if (isStaticLight)
		{
			// For static lights, ensure local light render targets are ready to go:
			if (!UpdateStaticLightingTargets())
			{
				return false;
			}
		}

		if (!isStaticLight || IsStaticLightDirty)
		{
			// Calculate offset vector for shadow depth bias:
			data.shadowDepthBias = worldPose.Forward * -shadowDepthBias;

			// Ensure a camera instance is ready for drawing the scene:
			if (!UpdateShadowMapCameraInstance(_shadingFocalPointRadius))
			{
				return false;
			}
		}

		return true;
	}

	public bool BeginDrawShadowCascade(
		in SceneContext _sceneCtx,
		in CommandList _cmdList,
		Vector3 _shadingFocalPoint,
		uint _cascadeIdx,
		out CameraPassContext _outCameraPassCtx,
		bool _rebuildResSetCamera = false,
		bool _texShadowMapsHasChanged = false)
	{
		// Select the right shadow cascade resource container:
		_cascadeIdx = Math.Min(_cascadeIdx, data.shadowCascades);

		ShadowCascadeResources cascade = shadowCascades![_cascadeIdx];
		Framebuffer? framebuffer = null;

		bool drawShadowPass = !isStaticLight || staticLightDirtyFlags.HasFlag(StaticLightDirtyFlags.Frame);

		// Non-static light or redraw of a static light:
		if (drawShadowPass)
		{
			// Recalculate projection for this cascade:
			cascade.mtxShadowWorld2Clip = RecalculateShadowProjectionMatrix(_shadingFocalPoint, _cascadeIdx);

			// Update framebuffer, constant buffers and resource sets:
			if (!cascade.UpdateResources(
				in _sceneCtx,
				in shadowCameraInstance!,
				in worldPose,
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

			// Draw static lights to local framebuffer first:
			if (isStaticLight)
			{
				staticShadowMapArray!.GetFramebuffer(_cascadeIdx, out framebuffer);
			}
			else
			{
				framebuffer = cascade.ShadowMapFrameBuffer!;
			}

			if (!shadowCameraInstance!.SetOverrideFramebuffer(framebuffer, true))
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
		}
		// Cached static light:
		else if (isStaticLight && !staticLightDirtyFlags.HasFlag(StaticLightDirtyFlags.Frame))
		{
			uint shadowResolution = staticShadowMapArray!.TexDepthMapArray.Width;

			// Copy contents of local targets to shadow map array:
			CopyShadowTexture(_cmdList, staticShadowMapArray.TexNormalMapArray, _sceneCtx.ShadowMapArray.TexNormalMapArray);
			CopyShadowTexture(_cmdList, staticShadowMapArray.TexDepthMapArray, _sceneCtx.ShadowMapArray.TexDepthMapArray);


			// Local helper method for copying texture contents from locally cached shadow targets to scene's shadow map array:
			void CopyShadowTexture(CommandList _cmdList, Texture _texStaticCached, Texture _texShadowMapArray)
			{
				_cmdList.CopyTexture(
					_texStaticCached,
					0, 0, 0, 0, _cascadeIdx,
					_texShadowMapArray,
					0, 0, 0, 0, ShadowMapIdx + _cascadeIdx,
					shadowResolution,
					shadowResolution,
					1, 1);
			}
		}

		// Determine version number for this pass' resources:
		ushort cameraResourceVersion = (ushort)(cascade.resourceVersion ^ _sceneCtx.SceneResourceVersion);

		// Assemble context object for renderers to reference when issuing draw calls:
		_outCameraPassCtx = new()
		{
			CameraInstance = shadowCameraInstance!,
			CmdList = _cmdList,
			Framebuffer = framebuffer!,
			ResSetCamera = cascade.ShadowResSetCamera!,
			CbCamera = cascade.ShadowCbCamera!,
			LightDataBuffer = _sceneCtx.DummyLightDataBuffer,
			CameraResourceVersion = cameraResourceVersion,
			FrameIdx = 0,
			PassIdx = ShadowMapIdx,
			LightCountShadowMapped = 0,
			MtxWorld2Clip = cascade.mtxShadowWorld2Clip,
			OutputDesc = framebuffer is not null ? framebuffer.OutputDescription : default,
			MirrorY = shadowCameraInstance!.ProjectionSettings.mirrorY,
		};

		return true;
	}

	public bool EndDrawShadowCascade()
	{
		if (IsDisposed) return false;

		bool success = true;

		if (!isStaticLight || staticLightDirtyFlags.HasFlag(StaticLightDirtyFlags.Frame))
		{
			success &= shadowCameraInstance is not null && shadowCameraInstance.EndDrawing();
		}
		return success;
	}

	public bool EndDrawShadowMap()
	{
		// Drop render flags for static lighting mode:
		staticLightDirtyFlags &= ~StaticLightDirtyFlags.Frame;

		return true;
	}

	protected abstract bool UpdateShadowMapCameraInstance(float _shadingFocalPointRadius);

	protected abstract Matrix4x4 RecalculateShadowProjectionMatrix(Vector3 _shadingFocalPoint, uint _cascadeIdx);

	public abstract bool CheckVisibilityByCamera(in CameraComponent _camera);
	public abstract bool CheckIsRendererInRange(in IPhysicalRenderer _renderer);

	public abstract bool LoadFromData(in LightData _lightData);
	public abstract bool SaveToData(out LightData _outLightData);

	private bool UpdateStaticLightingTargets()
	{
		if (staticShadowMapArray is null || staticShadowMapArray.IsDisposed)
		{
			uint shadowMapCount = ShadowCascades + 1;
			staticShadowMapArray = new(GraphicsCore, shadowMapCount);
		}

		return !staticShadowMapArray.IsDisposed;
	}

	#endregion
}
