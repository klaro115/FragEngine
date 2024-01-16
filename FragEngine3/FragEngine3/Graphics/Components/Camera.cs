using System.Numerics;
using FragEngine3.Graphics.Cameras;
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
	private DeviceBuffer? lightDataBuffer = null;
	private uint lightDataBufferCapacity = 0;

	// Render targets:
	private readonly Dictionary<RenderMode, CameraTarget> targetDict = new(4);
	private CameraTarget? overrideTarget = null;

	private readonly Stack<CameraPassResources> passResourcePool = new(4);
	private readonly Stack<CameraPassResources> passResourcesInUse = new(4);

	// Main camera:
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

	public uint FrameCounter { get; private set; } = 0;
	public uint PassCounter { get; private set; } = 0;

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

		instance.Dispose();

		foreach (var kvp in targetDict)
		{
			kvp.Value.Dispose();
		}

		foreach (CameraPassResources passRes in passResourcePool)
		{
			passRes.Dispose();
		}
		foreach (CameraPassResources passRes in passResourcesInUse)
		{
			passRes.Dispose();
		}

		lightDataBuffer?.Dispose();

		if (_disposing)
		{
			targetDict.Clear();
			overrideTarget = null;
			passResourcePool.Clear();
			passResourcesInUse.Clear();

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

		foreach (CameraPassResources passRes in passResourcePool)
		{
			passRes.Dispose();
		}
		foreach (CameraPassResources passRes in passResourcesInUse)
		{
			passRes.Dispose();
		}
		passResourcePool.Clear();
		passResourcesInUse.Clear();

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

	private bool GetOrCreateCameraPassResources(out CameraPassResources _outPassResources)
	{
		if (!passResourcePool.TryPop(out _outPassResources!))
		{
			_outPassResources = new();
		}

		if (!CameraUtility.CreateConstantBuffer_CBCamera(in instance.graphicsCore, out _outPassResources.cbCamera))
		{
			return false;
		}

		passResourcesInUse.Push(_outPassResources);
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

	public bool BeginFrame(uint _activeLightCount)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			return false;
		}
		if (IsDrawing)
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			return false;
		}

		// Create or resize light data buffer:
		if (!CameraUtility.VerifyOrCreateLightDataBuffer(
			in instance.graphicsCore,
			_activeLightCount,
			ref lightDataBuffer,
			ref lightDataBufferCapacity))
		{
			return false;
		}

		// Reset pass counter:
		PassCounter = 0;
		return true;
	}

	public bool EndFrame()
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot end frame on camera that has been disposed!");
			return false;
		}
		if (IsDrawing)
		{
			Logger.LogError("Cannot end frame on camera that is still drawing!");
			return false;
		}

		// Return resources to pool for re-use next frame:
		foreach (CameraPassResources passRes in passResourcesInUse)
		{
			passResourcePool.Push(passRes);
		}
		passResourcesInUse.Clear();

		// Update counters:
		PassCounter = 0;
		FrameCounter++;
		return true;
	}

	public bool BeginPass(
		in SceneContext _sceneCtx,
		CommandList _cmdList,
		Texture _texShadowMaps,
		RenderMode _renderMode,
		bool _clearRenderTargets,
		uint _cameraIdx_,
		uint _activeLightCount,
		uint _shadowMappedLightCount,
		out CameraContext _outCameraCtx)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot begin pass on camera that is already drawing!");
			_outCameraCtx = null!;
			return false;
		}
		if (IsDrawing)
		{
			Logger.LogError("Cannot begin pass on camera that is already drawing!");
			_outCameraCtx = null!;
			return false;
		}
		if (_cmdList == null || _cmdList.IsDisposed)
		{
			Logger.LogError("Cannot begin pass on camera using null or disposed command list!");
			_outCameraCtx = null!;
			return false;
		}

		// Fetch or prepare a reusable set of resources to use for this pass:
		if (!GetOrCreateCameraPassResources(out CameraPassResources passResources))
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

		Framebuffer activeFramebuffer = activeTarget.framebuffer;

		// Assign target's framebuffer as override to the camera instance:
		if (!instance.SetOverrideFramebuffer(activeFramebuffer, true))
		{
			_outCameraCtx = null!;
			return false;
		}

		// Finalize projection and camera parameters, and start drawing:
		instance.MtxWorld = node.WorldTransformation.Matrix;
		if (!instance.BeginDrawing(_cmdList, _clearRenderTargets, true, out Matrix4x4 mtxWorld2Clip))
		{
			Logger.LogError("Camera instance failed to begin pass!");
			_outCameraCtx = null!;
			return false;
		}

		Pose worldPose = node.WorldTransformation;

		// Camera data constant buffer:
		if (!CameraUtility.UpdateConstantBuffer_CBCamera(
			in instance,
			in worldPose,
			in mtxWorld2Clip,
			_cameraIdx_,
			_activeLightCount,
			_shadowMappedLightCount,
			ref passResources.cbCamera!))
		{
			Logger.LogError("Failed to allocate or update camera constant buffer!");
			_outCameraCtx = null!;
			return false;
		}

		// Camera's default resource set:
		if (!CameraUtility.UpdateOrCreateDefaultCameraResourceSet(
			in instance.graphicsCore,
			in _sceneCtx.resLayoutCamera,
			in _sceneCtx.cbScene,
			in passResources.cbCamera,
			in lightDataBuffer!,
			in _texShadowMaps,
			in _sceneCtx.samplerShadowMaps,
			ref passResources.defaultCameraResourceSet))
		{
			Logger.LogError("Failed to allocate or update camera's default resource set!");
			_outCameraCtx = null!;
			return false;
		}

		// Assemble camera context for rendering:
		_outCameraCtx = new(
			instance,
			_cmdList,

			passResources.defaultCameraResourceSet!,
			passResources.cbCamera!,
			activeFramebuffer,
			lightDataBuffer!,
			_texShadowMaps,

			activeFramebuffer.OutputDescription);

		return true;
	}

	public bool EndPass()
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

		PassCounter++;

		return instance.EndDrawing();
	}

	public bool GetLightDataBuffer(uint _maxActiveLightCount, out DeviceBuffer? _outLightDataBuffer)
	{
		if (IsDisposed)
		{
			_outLightDataBuffer = null;
			return false;
		}

		if (!CameraUtility.VerifyOrCreateLightDataBuffer(
			in instance.graphicsCore,
			_maxActiveLightCount,
			ref lightDataBuffer,
			ref lightDataBufferCapacity))
		{
			_outLightDataBuffer = null;
			return false;
		}
		_outLightDataBuffer = lightDataBuffer;
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
