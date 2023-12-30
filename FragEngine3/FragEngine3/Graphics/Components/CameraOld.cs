using System.Numerics;
using FragEngine3.Containers;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Components.Internal;
using FragEngine3.Graphics.Contexts;
using FragEngine3.Graphics.Resources;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Scenes.EventSystem;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
	[Obsolete($"Replaced using {nameof(Cameras.CameraInstance)}")]
    public sealed class CameraOld : Component
	{
		#region Types

		public enum ProjectionType
		{
			Orthographic	= 0,
			Perpective,
		}

		private sealed class OutputData
		{
			public uint resolutionX = 640;
			public uint resolutionY = 480;
			public PixelFormat outputFormat = PixelFormat.B8_G8_R8_A8_UNorm;
		}

		private sealed class ProjectionData
		{
			public ProjectionType projectionType = ProjectionType.Perpective;

			// Projection parameters:
			public float nearClipPlane = 0.01f;
			public float farClipPlane = 1000.0f;
			public float fieldOfViewRad = 60.0f * DEG2RAD;
			public float orthographicSize = 1.0f;

			// Matrices:
			public Matrix4x4 mtxProjection = Matrix4x4.Identity;	// Local space => Clip space
			public Matrix4x4 mtxViewport = Matrix4x4.Identity;		// Clip space => Pixel space
			public Matrix4x4 mtxCamera = Matrix4x4.Identity;		// World space => Clip space
			public Matrix4x4 mtxInvCamera = Matrix4x4.Identity;		// Clip space => World space
			public Matrix4x4 mtxWorld2Pixel = Matrix4x4.Identity;	// World space => Pixel space
			public Matrix4x4 mtxPixel2World = Matrix4x4.Identity;	// Pixel space => World space
		}

		private sealed class ClearingData
		{
			// Clearing behaviour flags:
			public bool clearBackground = true;
			public bool allowClearDepth = true;
			public bool allowClearStencil = false;

			// Clearing values:
			public Color32 clearColor = Color32.Cornflower;
			public float clearDepth = 1.0f;
			public byte clearStencil = 0x00;
		}

		#endregion
		#region Constructors

		public CameraOld(SceneNode _node, RenderMode _mainRenderMode = RenderMode.Opaque) : base(_node)
		{
			core = node.scene.engine.GraphicsSystem.graphicsCore;
			mainRenderMode = _mainRenderMode;

			output.Value.outputFormat = core.DefaultColorTargetPixelFormat;

			CameraTarget mainTarget = new(core, mainRenderMode, ResolutionX, ResolutionY, OutputFormat, true);
			targetDict.Add(mainRenderMode, mainTarget);

			UpdateProjection();
			UpdateStatesFromActiveRenderTarget(mainRenderMode);

			GetGlobalConstantBuffer(0, false, out _);

			//node.scene.drawManager.RegisterCamera(this);
		}

		#endregion
		#region Fields

		private readonly GraphicsCore core;
		private readonly RenderMode mainRenderMode;

		private bool isDrawing = false;
		private uint cameraVersion = 1;

		// Output:
		private VersionedMember<OutputData> output = new(new(), 0);
		private VersionedMember<ProjectionData> projection = new(new(), 0);
		private readonly ClearingData clearing = new();

		// Content:
		public uint cameraPriority = 1000;
		public uint layerMask = 0xFFFFFFFFu;

		// Lighting & Scene:
		private DeviceBuffer? lightDataBuffer = null;
		private uint lightDataBufferCapacity = 0;
		private DeviceBuffer? globalConstantBuffer = null;

		// Render targets:
		private readonly Dictionary<RenderMode, CameraTarget> targetDict = [];
		private CameraTarget? overrideTarget = null;

		//TODO: Consider adding a "useSimplifiedRendering" flag, which would prompt usage of simplified material overrides, if available.


		// Main camera:
		private static CameraOld? mainCamera = null;
		
		private readonly object cameraStateLockObj = new();
		private static readonly object mainCameraLockObj = new();

		#endregion
		#region Constants

		private const float RAD2DEG = 180.0f / MathF.PI;
		private const float DEG2RAD = MathF.PI / 180.0f;

		private static readonly SceneEventType[] sceneEventTypes =
		[
			SceneEventType.OnNodeDestroyed,
			SceneEventType.OnDestroyComponent,
		];

		#endregion
		#region Properties

		public override SceneEventType[] GetSceneEventList() => sceneEventTypes;

		public bool IsDrawing => !IsDisposed && isDrawing;

		/// <summary>
		/// Gets or sets the width of the camera's output images in pixels. Must be a value between 1 and 8192.<para/>
		/// NOTE: Some GPU architectures may support only a limited set of output resolutions, though most shouldn't
		/// complain so long as the with is divisible by 8.
		/// </summary>
		public uint ResolutionX
		{
			get => output.Value.resolutionX;
			set
			{
				output.Value.resolutionX = Math.Clamp(value, 1, 8192);
				AspectRatio = (float)output.Value.resolutionX / output.Value.resolutionY;
				output.UpdateValue(cameraVersion + 1, output.Value);
			}
		}
		/// <summary>
		/// Gets or sets the height of the camera's output images in pixels. Must be a value between 1 and 8192.
		/// </summary>
		public uint ResolutionY
		{
			get => output.Value.resolutionY;
			set
			{
				output.Value.resolutionY = Math.Clamp(value, 1, 8192);
				AspectRatio = (float)output.Value.resolutionX / output.Value.resolutionY;
				output.UpdateValue(cameraVersion + 1, output.Value);
			}
		}

		public PixelFormat OutputFormat
		{
			get => output.Value.outputFormat;
			set
			{
				output.Value.outputFormat = value;
				output.UpdateValue(cameraVersion + 1, output.Value);
			}
		}

		public float AspectRatio { get; private set; } = 1.33333f;

		public ProjectionType Projection
		{
			get => projection.Value.projectionType;
			set
			{
				projection.Value.projectionType = value;
				projection.UpdateValue(cameraVersion + 1, projection.Value);
			}
		}

		/// <summary>
		/// Gets or sets the nearest clipping distance of the camera; geometry closer than this can't be rendered
		/// and will be clipped. Must be larger than 0 and less than the value of <see cref="FarClipPlane"/>.
		/// </summary>
		public float NearClipPlane
		{
			get => projection.Value.nearClipPlane;
			set
			{
				projection.Value.nearClipPlane = Math.Clamp(value, 0.001f, Math.Min(projection.Value.farClipPlane, 99999.9f));
				projection.UpdateValue(cameraVersion + 1, projection.Value);
			}
		}
		/// <summary>
		/// Gets or sets the far clipping distance of the camera; geometry further away than this can't be rendered
		/// and will be clipped. Must be larger <see cref="NearClipPlane"/> and less than 100K.
		/// </summary>
		public float FarClipPlane
		{
			get => projection.Value.farClipPlane;
			set
			{
				projection.Value.farClipPlane = Math.Clamp(value, Math.Min(projection.Value.nearClipPlane, 0.002f), 100000.0f);
				projection.UpdateValue(cameraVersion + 1, projection.Value);
			}
		}

		/// <summary>
		/// Gets or sets the field of view angle in degrees.
		/// </summary>
		public float FieldOfViewDegrees
		{
			get => projection.Value.fieldOfViewRad * RAD2DEG;
			set
			{
				projection.Value.fieldOfViewRad = Math.Clamp(value, 0.001f, 179.0f) * DEG2RAD;
				projection.UpdateValue(cameraVersion + 1, projection.Value);
			}
		}
		/// <summary>
		/// Gets or sets the field of view angle in radians.
		/// </summary>
		public float FieldOfViewRadians
		{
			get => projection.Value.fieldOfViewRad;
			set
			{
				projection.Value.fieldOfViewRad = Math.Clamp(value, 0.001f * DEG2RAD, 179.0f * DEG2RAD);
				projection.UpdateValue(cameraVersion + 1, projection.Value);
			}
		}

		public float OrthographicSize
		{
			get => projection.Value.orthographicSize;
			set
			{
				projection.Value.orthographicSize = Math.Clamp(value, 0.001f, 1000.0f);
				projection.UpdateValue(cameraVersion + 1, projection.Value);
			}
		}

		public ResourceHandle? DefaultShadowMaterialHandle { get; private set; } = null;
		public Material? DefaultShadowMaterial { get; private set; } = null;

		/// <summary>
		/// Gets matrix containing the camera's world space transformation.
		/// </summary>
		public Matrix4x4 MtxWorld => node.WorldTransformation.Matrix;
		/// <summary>
		/// Gets just the projection matrix for transforming a point from the camera's local space into clip space
		/// coordinates. If out-of-date, all projection matrices are recalculated by calling this.
		/// </summary>
		public Matrix4x4 MtxProjection
		{
			get
			{
				if (!projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				return projection.Value.mtxProjection;
			}
		}
		/// <summary>
		/// Gets just the viewport matrix for transforming a point from the clip space into viewport pixel space
		/// coordinates. If out-of-date, all projection matrices are recalculated by calling this.
		/// </summary>
		public Matrix4x4 MtxViewport
		{
			get
			{
				if (!projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				return projection.Value.mtxViewport;
			}
		}
		/// <summary>
		/// Gets the final projection matrix for the current frame. This matrix may be used to transforms a point
		/// from world space into screen pixel coordinates. If out-of-date, all projection matrices are recalculated
		/// by calling this.<para/>
		/// NOTE: This is the result of multiplying the inverse world matrix, the projection matrix, and the viewport
		/// matrix. This matrix is recalculated each frame, as well as each time '<see cref="UpdateProjection"/>'
		/// is called.
		/// </summary>
		public Matrix4x4 MtxCamera
		{
			get
			{
				if (!projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				return projection.Value.mtxCamera;
			}
		}
		/// <summary>
		/// Gets the inverse of the final projection matrix for the current frame. This matrix may be used to transforms
		/// a pixel coordinate from viewport space into a position in world space. If out-of-date, all projection matrices
		/// are recalculated by calling this.<para/>
		/// NOTE: This is the inverse matrix of '<see cref="MtxCamera"/>'.
		/// </summary>
		public Matrix4x4 MtxInvCamera
		{
			get
			{
				if (!projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				return projection.Value.mtxInvCamera;
			}
		}

		public bool ClearBackground
		{
			get => clearing.clearBackground;
			set => clearing.clearBackground = value;
		}

		public Color32 ClearColor
		{
			get => clearing.clearColor;
			set => clearing.clearColor = value;
		}

		public float ClearDepth
		{
			get => clearing.clearDepth;
			set => clearing.clearDepth = Math.Clamp(value, 0.0f, 1.0f);
		}

		/// <summary>
		/// Gets or sets whether this camera is the engine's main camera. This is a global property, so don't set
		/// this if you have multiple scenes that each expect their own main camera.
		/// </summary>
		public bool IsMainCamera
		{
			get => mainCamera == this && !IsDisposed;
			set
			{
				if (IsMainCamera == value) return;
				lock(mainCameraLockObj)
				{
					mainCamera = value ? this : null;
				}
			}
		}

		/// <summary>
		/// Gets the engine's currently assigned main camera. This may be null if no camera has been marked as the
		/// global main camera, or if the main camera has been disposed. Only rely on this property if your game
		/// only ever has one dedicated main camera across all scenes that can concurrently be active and loaded.
		/// Use '<see cref="IsMainCamera"/>' to mark a camera as main.
		/// </summary>
		public static CameraOld? MainCamera
		{
			get { lock (mainCameraLockObj) { return mainCamera != null && mainCamera.IsMainCamera ? mainCamera : null; }; }
		}

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsMainCamera = false;

			lightDataBuffer?.Dispose();
			if (_disposing)
			{
				lightDataBuffer = null;
				lightDataBufferCapacity = 0;
			}

			foreach (var kvp in targetDict)
			{
				kvp.Value.Dispose();
			}
			targetDict.Clear();

			overrideTarget?.Dispose();
			if (_disposing)
			{
				overrideTarget = null;
			}

			base.Dispose(_disposing);
		}

		public override void Refresh()
		{
			// Temporarily remove main camera, to prevent undesirable access during reset:
			bool wasMainCamera = IsMainCamera;
			IsMainCamera = false;

			lock(cameraStateLockObj)
			{
				// Purge light data buffer:
				lightDataBuffer?.Dispose();
				lightDataBuffer = null;
				lightDataBufferCapacity = 0;

				// Purge render targets and framebuffer:
				foreach (var kvp in targetDict)
				{
					kvp.Value.Dispose();
				}
				targetDict.Clear();

				overrideTarget?.Dispose();
				overrideTarget = null;

				// Recreate render targets and framebuffer:
				CameraTarget newTarget = new(core, mainRenderMode, ResolutionX, ResolutionY, OutputFormat, true);
				targetDict.Add(mainRenderMode, newTarget);

				UpdateProjection();
				UpdateStatesFromActiveRenderTarget(RenderMode.Opaque);

				// Reregister camera:
				//node.scene.drawManager.UnregisterCamera(this);
				//node.scene.drawManager.RegisterCamera(this);

				cameraVersion++;
			}

			IsMainCamera = wasMainCamera;
		}

		public void MarkDirty()
		{
			cameraVersion++;
		}

		public override void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData)
		{
			if (_eventType == SceneEventType.OnNodeDestroyed ||
				_eventType == SceneEventType.OnDestroyComponent)
			{
				//node.scene.drawManager.UnregisterCamera(this);
			}
		}

		public bool SetDefaultShadowMaterial(string _resourceKey, bool _loadImmediatelyIfNotReady = false)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot assign default shadow material to disposed camera!");
				return false;
			}
			if (string.IsNullOrEmpty(_resourceKey))
			{
				Logger.LogError("Cannot assign default shadow material from null or blank resource key!");
				return false;
			}

			ResourceManager resourceManager = core.graphicsSystem.engine.ResourceManager;
			if (!resourceManager.GetResource(_resourceKey, out ResourceHandle handle))
			{
				Logger.LogError($"Camera's default shadow material for resource key '{_resourceKey}' could not be found!");
				return false;
			}

			return SetDefaultShadowMaterial(handle, _loadImmediatelyIfNotReady);
		}
		public bool SetDefaultShadowMaterial(ResourceHandle? _materialHandle, bool _loadImmediatelyIfNotReady = false)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot assign default shadow material to disposed camera!");
				return false;
			}
			if (_materialHandle != null && !_materialHandle.IsValid)
			{
				Logger.LogError("Cannot assign invalid material as camera's default shadow material!");
				return false;
			}

			// Assign (or clear) the material's handle:
			DefaultShadowMaterialHandle = _materialHandle;

			// If a shadow material was assigned, make sure it's loaded:
			if (DefaultShadowMaterialHandle != null)
			{
				DefaultShadowMaterial = DefaultShadowMaterialHandle.GetResource(_loadImmediatelyIfNotReady, true) as Material;
			}
			return true;
		}

		public bool SetOverrideRenderTargets(Framebuffer? _newOverrideRenderTargets, bool _transferOwnership)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot assign render target override on disposed camera!");
				return false;
			}
			if (_newOverrideRenderTargets != null && _newOverrideRenderTargets.IsDisposed)
			{
				Logger.LogError("Cannot assign disposed render targets as override to camera!");
				return false;
			}

			CameraTarget? prevTarget = overrideTarget;
			overrideTarget?.Dispose();
			overrideTarget = null;

			// Validate new overrides, if non-null:
			if (_newOverrideRenderTargets != null)
			{
				// Only allow correctly dimensioned render targets to be assigned to a camers:
				if (_newOverrideRenderTargets.Width != ResolutionX || _newOverrideRenderTargets.Height != ResolutionY)
				{
					Logger.LogError("Resolution mismatch between override render targets and camera!");
					return false;
				}

				// Assign (or clear) overrides:
				overrideTarget = new(core, mainRenderMode, null!, null!, _newOverrideRenderTargets, _transferOwnership);
			}

			UpdateStatesFromActiveRenderTarget(0);

			if (prevTarget != overrideTarget)
			{
				cameraVersion++;
			}
			return true;
		}

		/// <summary>
		/// Gets or (re)allocates a GPU-side buffer for all data about that light sources that may influence this geometry visible to this camera.
		/// </summary>
		/// <param name="_expectedCapacity">The expected or projected worst-case number of light sources that may affect geometry rendered by this
		/// camera. If zero or negative, the buffer will still be allocated with capacity for just 1 light source's data.</param>
		/// <param name="_outLightDataBuffer">Outputs a structured buffer with capacity for at least one piece of light source data of type
		/// '<see cref="Light.LightSourceData"/>'. If the previously allocated buffer was large enough, no new allocation will be made. Null if
		/// the camera has been disposed or if buffer creation failed.</param>
		/// <returns>True if the light data buffer was of sufficient size, or if it was reallocated to the requested capacity, false otherwise.</returns>
		internal bool GetLightDataBuffer(uint _expectedCapacity, out DeviceBuffer? _outLightDataBuffer)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot get light data buffer for disposed camera!");
				_outLightDataBuffer = null;
				return false;
			}

			_expectedCapacity = Math.Max(_expectedCapacity, 1);

			// (Re)allocate the light data buffer according to new capacity requirements:
			if (lightDataBuffer == null || lightDataBuffer.IsDisposed || lightDataBufferCapacity < _expectedCapacity)
			{
				// Purge any previously created buffer:
				lightDataBuffer?.Dispose();

				// Allocate a new structured buffer for light source data:
				try
				{
					uint byteSize = Light.LightSourceData.byteSize * (uint)_expectedCapacity;
					const BufferUsage usage = BufferUsage.Dynamic | BufferUsage.StructuredBufferReadOnly;
					const uint byteStride = Light.LightSourceData.byteSize;

					BufferDescription lightDataBufferDesc = new(byteSize, usage, byteStride);

					lightDataBuffer = core.MainFactory.CreateBuffer(ref lightDataBufferDesc);
					lightDataBufferCapacity = _expectedCapacity;
					cameraVersion++;
				}
				catch (Exception ex)
				{
					Logger.LogException("Failed to create or resize camera's buffer for light source data!", ex);
					_outLightDataBuffer = null;
					return false;
				}
			}
			
			// Output buffer and return success:
			_outLightDataBuffer = lightDataBuffer;
			return true;
		}

		/// <summary>
		/// Gets and updates a constant buffer containing all global scene information that the camera might rely on.
		/// </summary>
		/// <param name="_activeLightCount">The number of visible lights that might influence geometry within the camera's viewport frustum.
		/// This must not exceed the number of light source data entries in the camera's light data buffer.</param>
		/// <param name="_updateProjection">Whether to update projection matrices before updating the constant buffer's contents. Usually,
		/// the projection should already have been updated shortly before this method is called, by the scene's graphics stack.</param>
		/// <param name="_outGlobalConstantBuffer">Outputs a constant buffer with up-to-date data about the scene and the camera's projection.
		/// The data layout in this buffer matches the type '<see cref="GlobalConstantBuffer"/>'. Null if the camera has been disposed or if
		/// buffer creation failed.</param>
		/// <returns>True if the constant buffer could be retrieved/created, and updated with fresh data, false otherwise.</returns>
		private bool GetGlobalConstantBuffer(uint _activeLightCount, bool _updateProjection, out DeviceBuffer? _outGlobalConstantBuffer)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot get global constant buffer for disposed camera!");
				_outGlobalConstantBuffer = null;
				return false;
			}

			if (globalConstantBuffer == null || globalConstantBuffer.IsDisposed)
			{
				globalConstantBuffer?.Dispose();

				BufferDescription globalConstantBufferDesc = new(GlobalConstantBuffer.packedByteSize, BufferUsage.Dynamic | BufferUsage.UniformBuffer);

				globalConstantBuffer = core.MainFactory.CreateBuffer(ref globalConstantBufferDesc);
				cameraVersion++;
			}

			// If necessary, rebuilt projection matrices, since their values will be uploaded to the constant buffer:
			if (_updateProjection)
			{
				if (!projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
					projection.UpdateValue(cameraVersion, projection.Value);
				}
				else
				{
					UpdateFinalCameraMatrix();
				}
			}

			Vector3 ambientLight = new(0.2f, 0.2f, 0.2f);	//TEMP

			// Assemble global constant buffer contents:
			GlobalConstantBuffer globalConstantBufferData = new()
			{
				// Camera vectors & matrices:
				mtxCamera = projection.Value.mtxCamera,
				cameraPosition = new(node.WorldPosition, 0),
				cameraDirection = new(node.WorldForward, 0),

				// Camera parameters:
				resolutionX = ResolutionX,
				resolutionY = ResolutionY,
				nearClipPlane = projection.Value.nearClipPlane,
				farClipPlane = projection.Value.farClipPlane,

				// Lighting:
				ambientLight = ambientLight,
				lightCount = _activeLightCount,
			};

			// Upload to GPU:
			core.Device.UpdateBuffer(globalConstantBuffer, 0, ref globalConstantBufferData, GlobalConstantBuffer.byteSize);

			_outGlobalConstantBuffer = globalConstantBuffer;
			return true;
		}

		public void UpdateProjection()
		{
			//NOTE: We are using a left-handed coordinate system, where X=right, Y=up, and Z=forward.

			// Calculate projection from camera's local space to clip space:
			if (projection.Value.projectionType == ProjectionType.Orthographic)
			{
				projection.Value.mtxProjection = Matrix4x4.CreateOrthographicLeftHanded(
					projection.Value.orthographicSize * AspectRatio,
					projection.Value.orthographicSize,
					projection.Value.nearClipPlane,
					projection.Value.farClipPlane);
			}
			else
			{
				projection.Value.mtxProjection = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(
					projection.Value.fieldOfViewRad,
					AspectRatio,
					projection.Value.nearClipPlane,
					projection.Value.farClipPlane);
			}

			// Calculate viewport matrix:
			projection.Value.mtxViewport = Matrix4x4.CreateViewportLeftHanded(0, 0, ResolutionX, ResolutionY, 0, 1);

			// Recalculate the final camera matrix by combining the above matrices with the camera node's inverse world matrix:
			UpdateFinalCameraMatrix();

			// Reset the projection's dirty flag:
			projection.UpdateValue(cameraVersion, projection.Value);
		}
		private void UpdateFinalCameraMatrix()
		{
			// Use the inverted world matrix of the camera node to transform from world space to the camera's local space:
			if (Matrix4x4.Invert(MtxWorld, out Matrix4x4 mtxWorld2Camera))
			{
				// World space => Camera's local space => Clip space => Viewport/pixel space

				// Calculate combined matrix or transforming from world space to clip space:
				projection.Value.mtxCamera = mtxWorld2Camera * projection.Value.mtxProjection;
				projection.Value.mtxWorld2Pixel = projection.Value.mtxCamera * projection.Value.mtxViewport;

				Matrix4x4.Invert(projection.Value.mtxCamera, out projection.Value.mtxInvCamera);
				Matrix4x4.Invert(projection.Value.mtxWorld2Pixel, out projection.Value.mtxPixel2World);
			}
			// NOTE: If your projection is doing weird stuff on the GPU, add "#pragma pack_matrix( column_major )" to the top of it.
			// Matrix packing order in System.Numerics is column-major, not row-major! This was messy and annoying to figure out, so
			// just try to deal with it without loosing your mind too much.
		}

		private void UpdateStatesFromActiveRenderTarget(RenderMode _renderMode)
		{
			if (!GetActiveRenderTargets(_renderMode, out CameraTarget? activeTarget) || activeTarget == null)
			{
				return;
			}

			// Check whether depth and stencil buffers are present and require clearing:
			clearing.allowClearDepth = activeTarget.hasDepth;
			clearing.allowClearStencil = clearing.allowClearDepth && activeTarget.hasStencil;
		}

		/// <summary>
		/// Gets this camera's currently active render targets (aka the framebuffer it will be rendering to).
		/// </summary>
		/// <param name="_outActiveTarget">Outputs a currently framebuffer that the camera will be rendering to.
		/// If an override render target was assigned, that will be prioritized. In all other cases, the camera's own
		/// render target will be used instead.</param>
		/// <returns>True if any valid render target exists and is assigned to this camera, false otherwise.</returns>
		internal bool GetActiveRenderTargets(RenderMode _renderMode, out CameraTarget? _outActiveTarget)
		{
			if (IsDisposed)
			{
				_outActiveTarget = null;
				return false;
			}

			if (overrideTarget != null && !overrideTarget.IsDisposed)
			{
				_outActiveTarget = overrideTarget;
				return true;
			}
			else if (targetDict.TryGetValue(_renderMode, out CameraTarget? target))
			{
				ulong expectedDescriptorID = CameraTarget.CreateDescriptorID(ResolutionX, ResolutionY, OutputFormat, core.DefaultDepthTargetPixelFormat, true, true);

				// If the target is expired or out-of-spec, recreate it now:
				if (target == null || target.IsDisposed || expectedDescriptorID != target.descriptorID)
				{
					target?.Dispose();
					target = new(core, _renderMode, ResolutionX, ResolutionY, OutputFormat, true);
					targetDict.Remove(_renderMode);
					targetDict.Add(_renderMode, target);
				}

				_outActiveTarget = target;
				return true;
			}
			else
			{
				_outActiveTarget = new(core, _renderMode, ResolutionX, ResolutionY, OutputFormat, true);
				targetDict.Add(_renderMode, _outActiveTarget);
				return true;
			}
		}

		internal bool GetOrCreateOwnRenderTargets(RenderMode _renderMode, out CameraTarget? _outTarget)
		{
			if (IsDisposed)
			{
				_outTarget = null;
				return false;
			}

			bool recreateTarget = targetDict.TryGetValue(_renderMode, out _outTarget);
			if (!recreateTarget)
			{
				ulong expectedDescriptorID = CameraTarget.CreateDescriptorID(ResolutionX, ResolutionY, OutputFormat, core.DefaultDepthTargetPixelFormat, true, true);
				recreateTarget =
					_outTarget == null ||
					_outTarget.IsDisposed ||
					expectedDescriptorID != _outTarget.descriptorID;
			}
			
			if (recreateTarget)
			{
				_outTarget?.Dispose();
				_outTarget = new(core, _renderMode, ResolutionX, ResolutionY, OutputFormat, true);
				targetDict.Remove(_renderMode);
				targetDict.Add(_renderMode, _outTarget);
			}
			return true;
		}

		public bool BeginFrame(CommandList _cmdList, RenderMode _renderMode, uint _activeLightCount, bool _clearRenderTargets, out GraphicsDrawContext _outDrawCtx, out CameraContext _outCameraCtx)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot bind disposed camera for rendering!");
				_outDrawCtx = null!;
				_outCameraCtx = null!;
				return false;
			}
			if (_cmdList == null || _cmdList.IsDisposed)
			{
				Logger.LogError("Cannot bind camera using null or disposed command list!");
				_outDrawCtx = null!;
				_outCameraCtx = null!;
				return false;
			}

			if (_cmdList != core.MainCommandList)
			{
				_cmdList.Begin();
			}

			lock (cameraStateLockObj)
			{
				isDrawing = true;

				// If a default shadow material is loaded, make sure it's loaded:
				if (DefaultShadowMaterial == null || DefaultShadowMaterial.IsDisposed)
				{
					if (DefaultShadowMaterialHandle != null)
					{
						DefaultShadowMaterial = DefaultShadowMaterialHandle.GetResource(true, true) as Material;
					}
					else
					{
						DefaultShadowMaterial = null;
					}
				}

				// Update camera version from versioned members:
				{
					uint newestVersion = cameraVersion;

					newestVersion = Math.Max(newestVersion, projection.Version);
					newestVersion = Math.Max(newestVersion, output.Version);

					if (cameraVersion != newestVersion)
					{
						cameraVersion = newestVersion + 1;
					}
				}

				// Respond to changed projection or viewport:
				if (!projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				else
				{
					UpdateFinalCameraMatrix();
				}

				// Fetch the currently active render targets that shall be drawn to:
				if (!GetActiveRenderTargets(_renderMode, out CameraTarget? activeTarget) || activeTarget == null)
				{
					_outDrawCtx = null!;
					_outCameraCtx = null!;
					return false;
				}

				// Initialize constant buffers and light buffers for upcoming draw call:
				if (!GetGlobalConstantBuffer(_activeLightCount, false, out _) ||
					!GetLightDataBuffer(_activeLightCount, out _))
				{
					_outDrawCtx = null!;
					_outCameraCtx = null!;
					return false;
				}

				_outDrawCtx = new(core, _cmdList);
				//_outCameraCtx = new(this, globalConstantBuffer!, lightDataBuffer!, activeTarget.outputDesc);
				_outCameraCtx = new(null!, globalConstantBuffer!, lightDataBuffer!, activeTarget.outputDesc);

				// Bind current render targets as output to command list:
				_cmdList.SetFramebuffer(activeTarget.framebuffer);

				// Clear the framebuffer before any new content is drawn to it:
				if (_clearRenderTargets && clearing.clearBackground)
				{
					_cmdList.ClearColorTarget(0, clearing.clearColor.ToRgbaFloat());

					// Clear depth and stencil buffer, as required:
					if (clearing.allowClearDepth)
					{
						if (clearing.allowClearStencil)
						{
							_cmdList.ClearDepthStencil(clearing.clearDepth, clearing.clearStencil);
						}
						else
						{
							_cmdList.ClearDepthStencil(clearing.clearDepth);
						}
					}
				}
			}

			return true;
		}

		public bool EndFrame(CommandList _cmdList)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot unbind disposed camera from rendering!");
				return false;
			}

			lock(cameraStateLockObj)
			{
				//...

				isDrawing = false;
			}

			if (_cmdList != core.MainCommandList)
			{
				_cmdList.End();
			}
			return core.CommitCommandList(_cmdList);
		}

		/// <summary>
		/// Converts a position from world space to a pixel coordinate where it would appear on the camera's framebuffer.
		/// </summary>
		/// <param name="_worldPoint">A position coordinate in world space.</param>
		/// <param name="_allowUpdateProjection">Whether to allow recalculation of projection matrices if some or all of
		/// them are out-of-date. If the camera's resolution, projection, and pose didn't change, this can be set to false.
		/// Similarly, the matrices will be up-to-date after a first call to this method with the flag enabled, so any
		/// subsequent calls right after will not require matrices to be updated and the parameeter can be set to false.</param>
		/// <returns>A pixel coordinate corresponding to where the camera would display the given position on its render
		/// targets.</returns>
		public Vector3 TransformWorldPointToPixelCoord(Vector3 _worldPoint, bool _allowUpdateProjection = true)
		{
			// If requested, rebuild all projection matrices, or at least all those that have changed:
			if (_allowUpdateProjection)
			{
				if (projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				else
				{
					// Even if the rest are up-to-date, the final camera matrix should be recalculated once a frame, before first use:
					UpdateFinalCameraMatrix();
				}
			}

			// Transform world space position to viewport pixel space:
			Vector4 result = Vector4.Transform(_worldPoint, projection.Value.mtxWorld2Pixel);
			return new Vector3(result.X, result.Y, result.Z) / result.W;
		}

		/// <summary>
		/// Converts a pixel coordinate on the camera's framebuffer to a corresponding position in world space.
		/// </summary>
		/// <param name="_pixelCoord">A coordinate in the camera's framebuffer's pixel space.</param>
		/// <param name="_allowUpdateProjection">Whether to allow recalculation of projection matrices if some or all of
		/// them are out-of-date. If the camera's resolution, projection, and pose didn't change, this can be set to false.
		/// Similarly, the matrices will be up-to-date after a first call to this method with the flag enabled, so any
		/// subsequent calls right after will not require matrices to be updated and the parameeter can be set to false.</param>
		/// <returns>A world space position corresponding to the given pixel coordinate.</returns>
		public Vector3 TransformPixelCoordToWorldPoint(Vector3 _pixelCoord, bool _allowUpdateProjection = true)
		{
			// If requested, rebuild all projection matrices, or at least all those that have changed:
			if (_allowUpdateProjection)
			{
				if (projection.GetValue(cameraVersion, out _))
				{
					UpdateProjection();
				}
				else
				{
					// Even if the rest are up-to-date, the final camera matrix should be recalculated once a frame, before first use:
					UpdateFinalCameraMatrix();
				}
			}

			// Transform world space position to viewport pixel space:
			return Vector3.Transform(_pixelCoord, projection.Value.mtxPixel2World);
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

			// Reset camera version:
			cameraVersion = 0;

			lock (cameraStateLockObj)
			{
				output.UpdateValue(cameraVersion, new()
				{
					resolutionX = data.ResolutionX,
					resolutionY = data.ResolutionY,
				});

				projection.UpdateValue(cameraVersion, new()
				{
					nearClipPlane = data.NearClipPlane,
					farClipPlane = data.FarClipPlane,
					fieldOfViewRad = data.FieldOfViewDegrees * DEG2RAD,
				});

				cameraPriority = data.CameraPriority;
				layerMask = data.LayerMask;

				clearing.clearBackground = data.ClearBackground;
				clearing.clearColor = Color32.ParseHexString(data.ClearColor);
				clearing.clearDepth = data.ClearDepth;
				clearing.clearStencil = data.ClearStencil;
			}

			// Re-register camera with the scene:
			//node.scene.drawManager.UnregisterCamera(this);
			//return node.scene.drawManager.RegisterCamera(this);
			return true;
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			CameraData data;

			lock (cameraStateLockObj)
			{
				data = new()
				{
					ResolutionX = ResolutionX,
					ResolutionY = ResolutionY,

					NearClipPlane = projection.Value.nearClipPlane,
					FarClipPlane = projection.Value.farClipPlane,
					FieldOfViewDegrees = FieldOfViewDegrees,

					CameraPriority = cameraPriority,
					LayerMask = layerMask,

					ClearBackground = clearing.clearBackground,
					ClearColor = clearing.clearColor.ToHexString(),
					ClearDepth = clearing.clearDepth,
					ClearStencil = clearing.clearStencil,
				};
			}

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
}
