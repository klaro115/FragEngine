using System.Numerics;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Lighting;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components;

public sealed class Camera : Component, IOnNodeDestroyedListener, IOnComponentRemovedListener
{
	#region Constructors

	public Camera(SceneNode _node) : base(_node)
	{
		instance = new(node.scene.engine.GraphicsSystem.graphicsCore);
		LightDataBuffer = new(instance.graphicsCore, 1);

		node.scene.drawManager.RegisterCamera(this);
	}

	#endregion
	#region Fields

	private readonly CameraInstance instance;

	// Content:
	public uint cameraPriority = 1000;
	public uint layerMask = 0xFFFFu;

	// Render targets:
	private readonly Dictionary<RenderMode, CameraTarget> targetDict = new(4);
	private CameraTarget? overrideTarget = null;
	private ushort resourceVersion = 0;

	private readonly Stack<CameraPassResources> passResourcePool = new(4);
	private readonly Stack<CameraPassResources> passResourcesInUse = new(4);

	// Motion:
	private Pose lastFrameWorldPose = Pose.Identity;
	private Matrix4x4 mtxMotionSinceLastFrame = Matrix4x4.Identity;

	// Main camera:
	private static Camera? mainCamera = null;
	private static readonly object mainCameraLockObj = new();

	#endregion
	#region Properties

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

	// Scene & Lighting:
	public LightDataBuffer LightDataBuffer { get; private set; }

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

		LightDataBuffer?.Dispose();

		if (_disposing)
		{
			targetDict.Clear();
			overrideTarget = null;
			passResourcePool.Clear();
			passResourcesInUse.Clear();
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
		LightDataBuffer?.Dispose();

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

	public void OnNodeDestroyed()
	{
		node.scene.drawManager.UnregisterCamera(this);
	}
	public void OnComponentRemoved(Component removedComponent)
	{
		if (removedComponent == this)
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

	private bool GetActiveCameraTarget(RenderMode _renderMode, out CameraTarget _outActiveTarget, out Framebuffer _outActiveFramebuffer)
	{
		if (HasOverrideFramebuffer)
		{
			_outActiveTarget = overrideTarget!;
		}
		else if (!GetOrCreateCameraTarget(_renderMode, out _outActiveTarget))
		{
			Logger.LogError($"Failed to get or create camera target for render mode '{_renderMode}'!");
			_outActiveTarget = null!;
			_outActiveFramebuffer = null!;
			return false;
		}

		_outActiveFramebuffer = _outActiveTarget.framebuffer;
		return true;
	}

	public bool BeginFrame(uint _activeLightCount, out bool _outRebuildResSetCamera)
	{
		_outRebuildResSetCamera = false;
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

		// Create light data buffer:
		if (LightDataBuffer == null || LightDataBuffer.IsDisposed)
		{
			LightDataBuffer = new(instance.graphicsCore, _activeLightCount);
			_outRebuildResSetCamera = true;
		}
		if (!LightDataBuffer.PrepareBufLights(_activeLightCount, out bool recreatedLightDataBuffer))
		{
			return false;
		}
		_outRebuildResSetCamera |= recreatedLightDataBuffer;
		if (_outRebuildResSetCamera)
		{
			resourceVersion++;
		}

		// Calculate camera transformation/movement since last frame:
		{
			Pose curFrameWorldPose = node.WorldTransformation;
			Vector3 posDiff = curFrameWorldPose.position - lastFrameWorldPose.position;
			Quaternion rotDiff = curFrameWorldPose.rotation * Quaternion.Conjugate(lastFrameWorldPose.rotation);
			mtxMotionSinceLastFrame = Matrix4x4.CreateFromQuaternion(rotDiff) * Matrix4x4.CreateTranslation(posDiff);
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

		// Store current world space pose for camera motion checks next frame:
		lastFrameWorldPose = node.WorldTransformation;

		// Update counters:
		PassCounter = 0;
		FrameCounter++;
		return true;
	}

	public bool BeginPass(
		in SceneContext _sceneCtx,
		CommandList _cmdList,
		RenderMode _renderMode,
		bool _clearRenderTargets,
		uint _cameraIdx_,
		uint _activeLightCount,
		uint _shadowMappedLightCount,
		out CameraPassContext _outCameraPassCtx,
		bool _rebuildResSetCamera = false)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot begin pass on camera that is already drawing!");
			_outCameraPassCtx = null!;
			return false;
		}
		if (IsDrawing)
		{
			Logger.LogError("Cannot begin pass on camera that is already drawing!");
			_outCameraPassCtx = null!;
			return false;
		}
		if (_cmdList == null || _cmdList.IsDisposed)
		{
			Logger.LogError("Cannot begin pass on camera using null or disposed command list!");
			_outCameraPassCtx = null!;
			return false;
		}

		if (!GetOrCreateCameraPassResources(out CameraPassResources passResources))
		{
			_outCameraPassCtx = null!;
			return false;
		}

		if (!GetActiveCameraTarget(_renderMode, out _, out Framebuffer activeFramebuffer))
		{
			_outCameraPassCtx = null!;
			return false;
		}
		if (!instance.SetOverrideFramebuffer(activeFramebuffer, true))
		{
			_outCameraPassCtx = null!;
			return false;
		}

		// Finalize projection and camera parameters, and start drawing:
		instance.MtxWorld = node.WorldTransformation.Matrix;
		if (!instance.BeginDrawing(_cmdList, _clearRenderTargets, true, out Matrix4x4 mtxWorld2Clip))
		{
			Logger.LogError("Camera instance failed to begin pass!");
			_outCameraPassCtx = null!;
			return false;
		}

		// Update CBCamera:
		if (!CameraUtility.UpdateConstantBuffer_CBCamera(
			in instance,
			node.WorldTransformation,
			in mtxWorld2Clip,
			in mtxMotionSinceLastFrame,
			_cameraIdx_,
			_activeLightCount,
			_shadowMappedLightCount,
			ref passResources.cbCameraData,
			ref passResources.cbCamera!,
			out bool cbCameraChanged))
		{
			Logger.LogError("Failed to allocate or update camera constant buffer!");
			_outCameraPassCtx = null!;
			return false;
		}

		// Always force a rebuild of the camera's resource set if either the scene resources have changed, or those owned by the camera:
		_rebuildResSetCamera |= cbCameraChanged;

		// Update ResSetCamera:
		if (!CameraUtility.UpdateOrCreateCameraResourceSet(
			in instance.graphicsCore,
			in _sceneCtx,
			in passResources.cbCamera,
			LightDataBuffer,
			ref passResources.resSetCamera,
			out bool _,
			_rebuildResSetCamera))
		{
			Logger.LogError("Failed to allocate or update camera's default resource set!");
			_outCameraPassCtx = null!;
			return false;
		}
		if (_rebuildResSetCamera)
		{
			resourceVersion++;
		}

		// Determine version number for this pass' resources:
		ushort cameraResourceVersion = (ushort)(resourceVersion ^ _sceneCtx.SceneResourceVersion);

		// Assemble context for rendering this pass:
		_outCameraPassCtx = new()
		{
			CameraInstance = instance,
			CmdList = _cmdList,
			Framebuffer = activeFramebuffer,
			ResSetCamera = passResources.resSetCamera!,
			CbCamera = passResources.cbCamera,
			LightDataBuffer = LightDataBuffer!,
			CameraResourceVersion = cameraResourceVersion,
			FrameIdx = FrameCounter,
			PassIdx = PassCounter,
			LightCountShadowMapped = _shadowMappedLightCount,
			MtxWorld2Clip = mtxWorld2Clip,
			OutputDesc = activeFramebuffer.OutputDescription,
			MirrorY = instance.ProjectionSettings.mirrorY,
		};

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
