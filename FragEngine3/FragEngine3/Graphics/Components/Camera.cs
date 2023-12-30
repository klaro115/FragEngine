using System.Numerics;
using FragEngine3.Graphics.Cameras;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components;

public sealed class Camera : Component
{
	#region Constructors

	public Camera(SceneNode _node) : base(_node)
	{
		instance = new(node.scene.engine.GraphicsSystem.graphicsCore);
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
	#region Properties

	public bool IsDrawing => !IsDisposed && instance.IsDrawing;
	public bool HasOverrideFramebuffer => overrideTarget != null && !overrideTarget.IsDisposed;

	public bool IsMainCamera
	{
		get => MainCamera == this;
		set { lock(mainCameraLockObj) { mainCamera = this; } }
	}
	public static Camera? MainCamera
	{
		get { lock(mainCameraLockObj) { return mainCamera; } }
		set { lock(mainCameraLockObj) { if (value == null || !value.IsDisposed) mainCamera = value; } }
	}

    #endregion
    #region Methods

    protected override void Dispose(bool _disposing)
    {
        base.Dispose(_disposing);

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

		// No fitting target, create a new one:
		_outTarget = new(
			instance.graphicsCore,
			_renderMode,
			instance.ResolutionX,
			instance.ResolutionY,
			instance.ColorFormat,
			instance.HasDepth);
		
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
		RenderMode _renderMode,
		bool _clearRenderTargets,
		uint _activeLightCount,
		out GraphicsDrawContext _outDrawCtx,
		out CameraContext _outCameraCtx)
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			_outDrawCtx = null!;
			_outCameraCtx = null!;
			return false;
		}
		if (IsDrawing)
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			_outDrawCtx = null!;
			_outCameraCtx = null!;
			return false;
		}

		// Create or resize light data buffer:
		if (!UpdateLightDataBuffer(_activeLightCount))
		{
			_outDrawCtx = null!;
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
			_outDrawCtx = null!;
			_outCameraCtx = null!;
			return false;
		}

		// Assign target's framebuffer as override to the camera instance:
		if (!instance.SetOverrideFramebuffer(activeTarget.framebuffer, true))
		{
			_outDrawCtx = null!;
			_outCameraCtx = null!;
			return false;
		}

		// Finalize projection and camera parameters, and start drawing:
		if (!instance.BeginDrawing(_cmdList, _clearRenderTargets, out Matrix4x4 mtxWorld2Clip))
		{
			Logger.LogError("Cannot begin frame on camera that is already drawing!");
			_outCameraCtx = null!;
			_outDrawCtx = null!;
			return false;
		}

		if (!UpdateGlobalConstantBuffer(in mtxWorld2Clip, _activeLightCount))
		{
			Logger.LogError("Failed to allocate or update camera's global constant buffer!");
			_outDrawCtx = null!;
			_outCameraCtx = null!;
			return false;
		}

		_outDrawCtx = new(instance.graphicsCore, _cmdList);
		_outCameraCtx = new(this, globalConstantBuffer!, lightDataBuffer!, activeTarget.framebuffer.OutputDescription);
		return true;
	}

	public bool EndFrame(CommandList _cmdList)
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

		// Do nothing if a buffer of adequate size is already ready:
		if (lightDataBuffer != null && !lightDataBuffer.IsDisposed && lightDataBufferCapacity >= byteSize)
		{
			return true;
		}

		// Purge any previously allocated buffer:
		lightDataBuffer?.Dispose();
		lightDataBuffer = null;

		try
		{
			BufferDescription bufferDesc = new(byteSize, BufferUsage.StructuredBufferReadOnly | BufferUsage.Dynamic);

			lightDataBuffer = instance.graphicsCore.MainFactory.CreateBuffer(ref bufferDesc);
			return true;
		}
		catch (Exception ex)
		{
			Logger.LogException("Failed to create camera's light data buffer!", ex);
			return false;
		}
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
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create camera's global constant buffer!", ex);
				return false;
			}
		}

		Pose worldPose = node.WorldTransformation;
		Vector3 ambientLight = new(0.2f, 0.2f, 0.2f);	//TEMP
		
		GlobalConstantBuffer cbData = new()
		{
			// Camera vectors & matrices:
			mtxCamera = _mtxWorld2Clip,
			cameraPosition = new Vector4(worldPose.position, 0),
			cameraDirection = new Vector4(worldPose.Forward, 0),
			
			// Camera parameters:
			resolutionX = instance.ResolutionX,
			resolutionY = instance.ResolutionY,
			nearClipPlane = instance.NearClipPlane,
			farClipPlane = instance.FarClipPlane,

			// Lighting:
			ambientLight = ambientLight,
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

		instance.ResolutionX = data.ResolutionX;
		instance.ResolutionY = data.ResolutionY;

		instance.NearClipPlane = data.NearClipPlane;
		instance.FarClipPlane = data.FarClipPlane;
		instance.FieldOfViewDegrees = data.FieldOfViewDegrees;

		cameraPriority = data.CameraPriority;
		layerMask = data.LayerMask;

		instance.ClearColor = data.ClearBackground;
		instance.ClearColorValue = Color32.ParseHexString(data.ClearColor).ToRgbaFloat();
		instance.ClearDepthValue = data.ClearDepth;
		instance.ClearStencilValue = data.ClearStencil;

		// Re-register camera with the scene:
		node.scene.drawManager.UnregisterCamera(this);
		return node.scene.drawManager.RegisterCamera(this);
    }

    public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
    {
		CameraData data;

		data = new()
		{
			ResolutionX = instance.ResolutionX,
			ResolutionY = instance.ResolutionY,

			NearClipPlane = instance.NearClipPlane,
			FarClipPlane = instance.FarClipPlane,
			FieldOfViewDegrees = instance.FieldOfViewDegrees,

			CameraPriority = cameraPriority,
			LayerMask = layerMask,

			ClearBackground = instance.ClearColor,
			ClearColor = new Color32(instance.ClearColorValue).ToHexString(),
			ClearDepth = instance.ClearDepthValue,
			ClearStencil = instance.ClearStencilValue,
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
