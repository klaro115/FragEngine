using System.Numerics;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components;

public sealed class Camera : Component
{
	#region Constructors

	public Camera(SceneNode _node) : base(_node)
	{
		instance = new(node.scene.engine.GraphicsSystem.graphicsCore);

		node.scene.drawManager.RegisterCamera(this);
	}

	#endregion
	#region Fields

	private readonly CameraInstance instance;

	// Content:
	public uint cameraPriority = 1000;
	public uint layerMask = 0xFFFFu;

	// Scene & Lighting:
	private DeviceBuffer? globalConstantBuffer = null;
	private DeviceBuffer? lightDataBuffer = null;
	private uint lightDataBufferCapacity = 0;

	// Render targets:
	private readonly Dictionary<RenderMode, CameraTarget> targetDict = new(4);
	private CameraTarget? overrideTarget = null;

	private static Camera? mainCamera = null;
	private static readonly object mainCameraLockObj = new();

	#endregion
	#region Constants

	private static readonly SceneEventType[] sceneEventTypes =
	[
		SceneEventType.OnNodeDestroyed,
		SceneEventType.OnDestroyComponent,
	];

	#endregion
	#region Properties

	public override SceneEventType[] GetSceneEventList() => sceneEventTypes;

	public bool IsDrawing => !IsDisposed && instance.IsDrawing;
	public bool HasOverrideFramebuffer => overrideTarget != null && !overrideTarget.IsDisposed;

	public CameraSettings Settings
	{
		get => instance.Settings;
		set => instance.Settings = value;
	}
	public CameraOutput OutputSettings
	{
		get => instance.OutputSettings;
		set => instance.OutputSettings = value;
	}
	public CameraProjection ProjectionSettings
	{
		get => instance.ProjectionSettings;
		set => instance.ProjectionSettings = value;
	}
	public CameraClearing ClearingSettings
	{
		get => instance.ClearingSettings;
		set => instance.ClearingSettings = value;
	}

	/// <summary>
	/// Gets or sets whether this camera is the engine's main camera.
	/// </summary>
	public bool IsMainCamera
	{
		get => MainCamera == this;
		set
		{
			if (value && !IsDisposed) MainCamera = this;
			else if (!value && MainCamera == this) MainCamera = null;
		}
	}
	/// <summary>
	/// Gets or sets the engine's main camera instance. Null if no main camera is assigned, may not be disposed.
	/// </summary>
	public static Camera? MainCamera
	{
		get { lock(mainCameraLockObj) { return mainCamera != null && !mainCamera.IsDisposed ? mainCamera : null; } }
		set { lock(mainCameraLockObj) { if (value == null || !value.IsDisposed) mainCamera = value; } }
	}

	#endregion
	#region Methods

	protected override void Dispose(bool _disposing)
	{
		base.Dispose(_disposing);

		if (_disposing && mainCamera == this)
		{
			mainCamera = null;
		}

		foreach (var kvp in targetDict)
		{
			kvp.Value.Dispose();
		}

		instance.Dispose();

		globalConstantBuffer?.Dispose();
		lightDataBuffer?.Dispose();

		if (_disposing)
		{
			targetDict.Clear();
			overrideTarget = null;

			globalConstantBuffer = null;
			lightDataBuffer = null;
			lightDataBufferCapacity = 0;
		}
	}

	public void MarkDirty() => instance.MarkDirty();

	public override void Refresh()
	{
		// Do not allow resetting after disposal or while actively drawing:
		if (IsDisposed || IsDrawing) return;

		// Temporarily unset camera as main camera, to reduce potential access during reset:
		bool wasMainCamera = IsMainCamera;
		IsMainCamera = false;

		// Purge dynamically allocated resources:
		lightDataBuffer?.Dispose();
		lightDataBufferCapacity = 0;

		overrideTarget = null;
		foreach (var kvp in targetDict)
		{
			kvp.Value.Dispose();
		}
		targetDict.Clear();

		// Reset camera instance and its external references:
		instance.SetOverrideFramebuffer(null, false);

		// Reregister camera component:
		node.scene.drawManager.UnregisterCamera(this);
		node.scene.drawManager.RegisterCamera(this);

		// Reassign camera as main camera:
		if (wasMainCamera)
		{
			IsMainCamera = wasMainCamera;
		}
	}

	public override void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData)
	{
		if (_eventType == SceneEventType.OnNodeDestroyed ||
			_eventType == SceneEventType.OnDestroyComponent)
		{
			node.scene.drawManager.UnregisterCamera(this);
		}
	}

	internal bool GetOrCreateCameraTarget(RenderMode _renderMode, out CameraTarget _outTarget)
	{
		// If an appropriately sized and formatted target exists, use that:
		if (targetDict.TryGetValue(_renderMode, out CameraTarget? target) && target != null && !target.IsDisposed)
		{
			if (instance.IsFramebufferCompatible(in target.framebuffer))
			{
				_outTarget = target;
				return true;
			}

			target.Dispose();
			targetDict.Remove(_renderMode);
		}

		CameraOutput outputSettings = OutputSettings;

		// No fitting target, create a new one:
		_outTarget = new(
			instance.graphicsCore,
			_renderMode,
			outputSettings.resolutionX,
			outputSettings.resolutionY,
			outputSettings.colorFormat,
			outputSettings.hasDepth);
		
		targetDict.Add(_renderMode, _outTarget);
		return true;
	}

	public bool SetOverrideCameraTarget(Framebuffer? _newOverrideTarget, bool _hasOwnershipOfFramebuffer = false)
	{
		// If null, unassign override slot:
		if (_newOverrideTarget == null)
		{
			overrideTarget = null;
			return true;
		}
		if (_newOverrideTarget.IsDisposed)
		{
			Logger.LogError("Cannot set disposed framebuffer as override target on camera!");
			overrideTarget = null;
			return true;
		}

		// Create a new camera target around the given framebuffer:
		Texture texColor = _newOverrideTarget.ColorTargets[0].Target;
		Texture? texDepth = _newOverrideTarget.DepthTarget?.Target;

		overrideTarget = new(
			instance.graphicsCore,
			RenderMode.Custom,
			texColor,
			texDepth!,
			_newOverrideTarget,
			_hasOwnershipOfFramebuffer);
		return true;
	}

	public bool BeginFrame(
		CommandList _cmdList,
		Texture _shadowMapArray,
		RenderMode _renderMode,
		bool _clearRenderTargets,
		uint _activeLightCount,
		out CameraContext _outCameraCtx)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			_outCameraCtx = null!;
			return false;
		}
		if (IsDrawing)
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			_outCameraCtx = null!;
			return false;
		}
		if (_cmdList == null || _cmdList.IsDisposed)
		{
			Logger.LogError("Cannot begin frame on camera using null or disposed command list!");
			_outCameraCtx = null!;
			return false;
		}

		// Create or resize light data buffer:
		if (!UpdateLightDataBuffer(_activeLightCount))
		{
			_outCameraCtx = null!;
			return false;
		}

		// Get or create the active camera target:
		CameraTarget activeTarget;
		if (HasOverrideFramebuffer)
		{
			activeTarget = overrideTarget!;
		}
		else if (!GetOrCreateCameraTarget(_renderMode, out activeTarget))
		{
			Logger.LogError($"Failed to get or create camera target for render mode '{_renderMode}'!");
			_outCameraCtx = null!;
			return false;
		}

		// Assign target's framebuffer as override to the camera instance:
		if (!instance.SetOverrideFramebuffer(activeTarget.framebuffer, true))
		{
			_outCameraCtx = null!;
			return false;
		}

		// Finalize projection and camera parameters, and start drawing:
		instance.MtxWorld = node.WorldTransformation.Matrix;
		if (!instance.BeginDrawing(_cmdList, _clearRenderTargets, out Matrix4x4 mtxWorld2Clip))
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			_outCameraCtx = null!;
			return false;
		}

		if (!UpdateGlobalConstantBuffer(in mtxWorld2Clip, _activeLightCount))
		{
			Logger.LogError("Failed to allocate or update camera's global constant buffer!");
			_outCameraCtx = null!;
			return false;
		}

		_outCameraCtx = new(instance, _cmdList, globalConstantBuffer!, lightDataBuffer!, _shadowMapArray!, activeTarget.framebuffer.OutputDescription);;
		return true;
	}

	public bool EndFrame(CommandList _)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot end frame on disposed camera!");
			return false;
		}
		if (!IsDrawing)
		{
			Logger.LogError("Cannot end frame on a camera that is not currently drawing!");
			return false;
		}

		return instance.EndDrawing();
	}

	public bool GetLightDataBuffer(uint _maxActiveLightCount, out DeviceBuffer? _outLightDataBuffer)
	{
		if (IsDisposed)
		{
			_outLightDataBuffer = null;
			return false;
		}

		if (!UpdateLightDataBuffer(_maxActiveLightCount))
		{
			_outLightDataBuffer = null;
			return false;
		}
		_outLightDataBuffer = lightDataBuffer;
		return true;
	}

	private bool UpdateLightDataBuffer(uint _maxActiveLightCount)
	{
		_maxActiveLightCount = Math.Max(_maxActiveLightCount, 1);
		uint byteSize = Light.LightSourceData.byteSize * _maxActiveLightCount;

		// Create a new buffer if there is none or if the previous one was too small:
		if (lightDataBuffer == null || lightDataBuffer.IsDisposed || lightDataBufferCapacity < byteSize)
		{
			// Purge any previously allocated buffer:
			lightDataBuffer?.Dispose();
			lightDataBuffer = null;

			try
			{
				BufferDescription bufferDesc = new(
					byteSize,
					BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic,
					Light.LightSourceData.byteSize);

				lightDataBuffer = instance.graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
				lightDataBuffer.Name = $"BufLights_Capacity={_maxActiveLightCount}";
				lightDataBufferCapacity = byteSize;
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create camera's light data buffer!", ex);
				lightDataBuffer?.Dispose();
				lightDataBufferCapacity = 0;
				return false;
			}
		}
		return true;
	}

	private bool UpdateGlobalConstantBuffer(in Matrix4x4 _mtxWorld2Clip, uint _activeLightCount)
	{
		// Ensure the buffer is allocated:
		if (globalConstantBuffer == null || globalConstantBuffer.IsDisposed)
		{
			try
			{
				BufferDescription bufferDesc = new(GlobalConstantBuffer.packedByteSize, BufferUsage.UniformBuffer | BufferUsage.Dynamic);

				globalConstantBuffer = instance.graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
				globalConstantBuffer.Name = "CBGlobal";
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create camera's global constant buffer!", ex);
				return false;
			}
		}

		Pose worldPose = node.WorldTransformation;
		Vector3 ambientLightLow = node.scene.settings.AmbientLightIntensityLow;
		Vector3 ambientLightMid = node.scene.settings.AmbientLightIntensityMid;
		Vector3 ambientLightHigh = node.scene.settings.AmbientLightIntensityHigh;

		GlobalConstantBuffer cbData = new()
		{
			// Camera vectors & matrices:
			mtxWorld2Clip = _mtxWorld2Clip,
			cameraPosition = new Vector4(worldPose.position, 0),
			cameraDirection = new Vector4(worldPose.Forward, 0),
			
			// Camera parameters:
			resolutionX = instance.OutputSettings.resolutionX,
			resolutionY = instance.OutputSettings.resolutionY,
			nearClipPlane = instance.ProjectionSettings.nearClipPlane,
			farClipPlane = instance.ProjectionSettings.farClipPlane,

			// Lighting:
			ambientLightLow = new RgbaFloat(new(ambientLightLow, 0)),
			ambientLightMid = new RgbaFloat(new(ambientLightMid, 0)),
			ambientLightHigh = new RgbaFloat(new(ambientLightHigh, 0)),
			lightCount = _activeLightCount,
		};
		
		instance.graphicsCore.Device.UpdateBuffer(globalConstantBuffer, 0, ref cbData, GlobalConstantBuffer.byteSize);

		return true;
	}

	public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElement> _idDataMap)
	{
		if (string.IsNullOrEmpty(_componentData.SerializedData))
		{
			Logger.LogError("Cannot load camera from null or blank serialized data!");
			return false;
		}

		// Deserialize camera data from component data's serialized data string:
		if (!Serializer.DeserializeFromJson(_componentData.SerializedData, out CameraData? data) || data == null)
		{
			Logger.LogError("Failed to deserialize camera component data from JSON!");
			return false;
		}
		if (!data.IsValid())
		{
			Logger.LogError("Deserialized camera component data is invalid!");
			return false;
		}

		CameraSettings settings = Settings;

		settings.ResolutionX = data.ResolutionX;
		settings.ResolutionY = data.ResolutionY;

		settings.NearClipPlane = data.NearClipPlane;
		settings.FarClipPlane = data.FarClipPlane;
		settings.FieldOfViewDegrees = data.FieldOfViewDegrees;

		cameraPriority = data.CameraPriority;
		layerMask = data.LayerMask;

		settings.ClearColor = data.ClearBackground;
		settings.ClearColorValue = Color32.ParseHexString(data.ClearColor).ToRgbaFloat();
		settings.ClearDepthValue = data.ClearDepth;
		settings.ClearStencilValue = data.ClearStencil;

		Settings = settings;

		// Re-register camera with the scene:
		node.scene.drawManager.UnregisterCamera(this);
		return node.scene.drawManager.RegisterCamera(this);
	}

	public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
	{
		CameraData data;

		CameraSettings settings = Settings;

		data = new()
		{
			ResolutionX = settings.ResolutionX,
			ResolutionY = settings.ResolutionY,

			NearClipPlane = settings.NearClipPlane,
			FarClipPlane = settings.FarClipPlane,
			FieldOfViewDegrees = settings.FieldOfViewDegrees,

			CameraPriority = cameraPriority,
			LayerMask = layerMask,

			ClearBackground = settings.ClearColor,
			ClearColor = new Color32(settings.ClearColorValue).ToHexString(),
			ClearDepth = settings.ClearDepthValue,
			ClearStencil = settings.ClearStencilValue,
		};

		if (!Serializer.SerializeToJson(data, out string dataJson))
		{
			Logger.LogError("Failed to serialize camera component data to JSON!");
			_componentData = ComponentData.Empty;
			return false;
		}

		_componentData = new ComponentData()
		{
			SerializedData = dataJson,
		};
		return true;
	}

	#endregion
}
