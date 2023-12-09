using System.Numerics;
using FragEngine3.Graphics.Components.ConstantBuffers;
using FragEngine3.Graphics.Components.Data;
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
    public sealed class Camera : Component
	{
		#region Types

		public enum ProjectionType
		{
			Orthographic	= 0,
			Perpective,
		}

		[Flags]
		private enum DirtyFlags
		{
			Resolution		= 1,
			RenderTarget	= 2,
			Projection		= 4,

			All				= Resolution | RenderTarget | Projection
		}

		#endregion
		#region Constructors

		public Camera(SceneNode _node) : base(_node)
		{
			core = node.scene.engine.GraphicsSystem.graphicsCore;

			if (core.CreateStandardRenderTargets(
				resolutionX,
				resolutionY,
				true,
				out Texture texColor,
				out Texture? texDepth,
				out Framebuffer framebuffer))
			{
				TexColorTarget = texColor;
				TexDepthStencilTarget = texDepth;
				renderTargets = framebuffer;
			}

			MarkDirty(DirtyFlags.Resolution | DirtyFlags.Projection);

			UpdateProjection();
			UpdateStatesFromActiveRenderTarget();

			GetGlobalConstantBuffer(0, false, out _);

			node.scene.drawManager.RegisterCamera(this);
		}

		#endregion
		#region Fields

		private readonly GraphicsCore core;

		private DirtyFlags dirtyFlags = 0;
		private bool isDrawing = false;
		private uint cameraVersion = 1;

		// Resolution:
		private uint resolutionX = 640;
		private uint resolutionY = 480;
		private uint prevResolutionX = 0;
		private uint prevResolutionY = 0;
		private PixelFormat prevColorPixelFormat = PixelFormat.R8_UNorm;

		// Projection:
		private ProjectionType projectionType = ProjectionType.Perpective;
		private float nearClipPlane = 0.01f;
		private float farClipPlane = 1000.0f;
		private float fieldOfViewRad = 60.0f * DEG2RAD;
		private float orthographicSize = 1.0f;
		private Matrix4x4 mtxProjection = Matrix4x4.Identity;	// Local space => Clip space
		private Matrix4x4 mtxViewport = Matrix4x4.Identity;		// Clip space => Pixel space
		private Matrix4x4 mtxCamera = Matrix4x4.Identity;		// World space => Pixel space
		private Matrix4x4 mtxInvCamera = Matrix4x4.Identity;	// Pixel space => World space

		// Content:
		public uint cameraPriority = 1000;
		public uint layerMask = 0xFFFFFFFFu;

		// Clearing:
		public bool clearBackground = true;
		private bool allowClearDepth = true;
		private bool allowClearStencil = false;
		public Color32 clearColor = Color32.Cornflower;
		public float clearDepth = 1.0f;
		public byte clearStencil = 0x00;

		// Lighting & Scene:
		private DeviceBuffer? lightDataBuffer = null;
		private uint lightDataBufferCapacity = 0;
		private DeviceBuffer? globalConstantBuffer = null;

		//TODO: Consider adding a "useSimplifiedRendering" flag, which would prompt usage of simplified material overrides, if available.

		// Output:
		private Framebuffer renderTargets = null!;
		private Framebuffer? overrideRenderTargets = null;

		private static Camera? mainCamera = null;

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

		public bool IsDirty => dirtyFlags != 0;

		public override SceneEventType[] GetSceneEventList() => sceneEventTypes;

		public bool IsDrawing => !IsDisposed && isDrawing;

		/// <summary>
		/// Gets or sets the width of the camera's output images in pixels. Must be a value between 1 and 8192.<para/>
		/// NOTE: Some GPU architectures may support only a limited set of output resolutions, though most shouldn't
		/// complain so long as the with is divisible by 8.
		/// </summary>
		public uint ResolutionX
		{
			get => resolutionX;
			set
			{
				resolutionX = Math.Clamp(value, 1, 8192);
				AspectRatio = resolutionX / resolutionY;
				MarkDirty(DirtyFlags.Resolution);
			}
		}
		/// <summary>
		/// Gets or sets the height of the camera's output images in pixels. Must be a value between 1 and 8192.
		/// </summary>
		public uint ResolutionY
		{
			get => resolutionY;
			set
			{
				resolutionY = Math.Clamp(value, 1, 8192);
				AspectRatio = resolutionX / resolutionY;
				MarkDirty(DirtyFlags.Resolution);
			}
		}

		public float AspectRatio { get; private set; } = 1.33333f;

		public ProjectionType Projection
		{
			get => projectionType;
			set { projectionType = value; MarkDirty(DirtyFlags.Projection); }
		}

		/// <summary>
		/// Gets or sets the nearest clipping distance of the camera; geometry closer than this can't be rendered
		/// and will be clipped. Must be larger than 0 and less than the value of <see cref="FarClipPlane"/>.
		/// </summary>
		public float NearClipPlane
		{
			get => nearClipPlane;
			set { nearClipPlane = Math.Clamp(value, 0.001f, Math.Min(farClipPlane, 99999.9f)); MarkDirty(DirtyFlags.Projection); }
		}
		/// <summary>
		/// Gets or sets the far clipping distance of the camera; geometry further away than this can't be rendered
		/// and will be clipped. Must be larger <see cref="NearClipPlane"/> and less than 100K.
		/// </summary>
		public float FarClipPlane
		{
			get => farClipPlane;
			set { farClipPlane = Math.Clamp(value, Math.Min(nearClipPlane, 0.002f), 100000.0f); MarkDirty(DirtyFlags.Projection); }
		}

		/// <summary>
		/// Gets or sets the field of view angle in degrees.
		/// </summary>
		public float FieldOfViewDegrees
		{
			get => fieldOfViewRad * RAD2DEG;
			set { fieldOfViewRad = Math.Clamp(value, 0.001f, 179.0f) * DEG2RAD; MarkDirty(DirtyFlags.Projection); }
		}
		/// <summary>
		/// Gets or sets the field of view angle in radians.
		/// </summary>
		public float FieldOfViewRadians
		{
			get => fieldOfViewRad;
			set { fieldOfViewRad = Math.Clamp(value, 0.001f * DEG2RAD, 179.0f * DEG2RAD); MarkDirty(DirtyFlags.Projection); }
		}

		public float OrthographicSize
		{
			get => orthographicSize;
			set { orthographicSize = Math.Clamp(value, 0.001f, 1000.0f); MarkDirty(DirtyFlags.Projection); }
		}

		public ResourceHandle? DefaultShadowMaterialHandle { get; private set; } = null;
		public Material? DefaultShadowMaterial { get; private set; } = null;

		public Texture TexColorTarget { get; private set; } = null!;
		public Texture? TexDepthStencilTarget { get; private set; } = null!;

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
				if (dirtyFlags.HasFlag(DirtyFlags.Projection))
				{
					UpdateProjection();
				}
				return mtxProjection;
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
				if (dirtyFlags.HasFlag(DirtyFlags.Projection) || dirtyFlags.HasFlag(DirtyFlags.Resolution))
				{
					UpdateProjection();
				}
				return mtxViewport;
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
				if (dirtyFlags.HasFlag(DirtyFlags.Projection))
				{
					UpdateProjection();
				}
				else
				{
					UpdateFinalCameraMatrix();
				}
				return mtxCamera;
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
				if (dirtyFlags.HasFlag(DirtyFlags.Projection))
				{
					UpdateProjection();
				}
				else
				{
					UpdateFinalCameraMatrix();
				}
				return mtxInvCamera;
			}
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
		public static Camera? MainCamera
		{
			get { lock (mainCameraLockObj) { return mainCamera != null && mainCamera.IsMainCamera ? mainCamera : null; }; }
		}

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsMainCamera = false;
			dirtyFlags = 0;

			lightDataBuffer?.Dispose();
			if (_disposing)
			{
				lightDataBuffer = null;
				lightDataBufferCapacity = 0;
			}

			renderTargets?.Dispose();
			TexColorTarget?.Dispose();
			TexDepthStencilTarget?.Dispose();

			if (_disposing)
			{
				renderTargets = null!;
				overrideRenderTargets = null;
				TexColorTarget = null!;
				TexDepthStencilTarget = null;
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
				renderTargets?.Dispose();
				TexColorTarget?.Dispose();
				TexDepthStencilTarget?.Dispose();

				renderTargets = null!;
				overrideRenderTargets = null;
				TexColorTarget = null!;
				TexDepthStencilTarget = null;

				// Recreate render targets and framebuffer:
				if (core.CreateStandardRenderTargets(
					resolutionX,
					resolutionY,
					true,
					out Texture texColor,
					out Texture? texDepth,
					out Framebuffer framebuffer))
				{
					TexColorTarget = texColor;
					TexDepthStencilTarget = texDepth;
					renderTargets = framebuffer;
				}

				// Mark dirty and rebuild matrices:
				MarkDirty(DirtyFlags.Resolution | DirtyFlags.Projection);

				UpdateProjection();
				UpdateStatesFromActiveRenderTarget();

				// Reregister camera:
				node.scene.drawManager.UnregisterCamera(this);
				node.scene.drawManager.RegisterCamera(this);

				cameraVersion++;
			}

			IsMainCamera = wasMainCamera;
		}

		public void MarkDirty()
		{
			dirtyFlags |= DirtyFlags.All;
		}
		private void MarkDirty(DirtyFlags _changeFlags)
		{
			dirtyFlags |= _changeFlags;
		}

		public override void ReceiveSceneEvent(SceneEventType _eventType, object? _eventData)
		{
			if (_eventType == SceneEventType.OnNodeDestroyed ||
				_eventType == SceneEventType.OnDestroyComponent)
			{
				node.scene.drawManager.UnregisterCamera(this);
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

		public bool SetOverrideRenderTargets(Framebuffer? _newOverrideRenderTargets, bool _disposedPreviousOverride = false)
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

			// Validate new overrides, if non-null:
			if (_newOverrideRenderTargets != null)
			{
				// Only allow correctly dimensioned render targets to be assigned to a camers:
				if (_newOverrideRenderTargets.Width != resolutionX || _newOverrideRenderTargets.Height != resolutionY)
				{
					Logger.LogError("Resolution mismatch between override render targets and camera!");
					return false;
				}
			}

			// If requested, purge any previously assigned overrides:
			if (_disposedPreviousOverride && overrideRenderTargets != null)
			{
				overrideRenderTargets.Dispose();
			}

			// Assign (or clear) overrides:
			overrideRenderTargets = _newOverrideRenderTargets;

			UpdateStatesFromActiveRenderTarget();

			MarkDirty(DirtyFlags.RenderTarget);
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
				if (dirtyFlags.HasFlag(DirtyFlags.Resolution) || dirtyFlags.HasFlag(DirtyFlags.Projection))
				{
					UpdateProjection();
				}
				else
				{
					UpdateFinalCameraMatrix();
				}
			}
			
			// Assemble global constant buffer contents:
			GlobalConstantBuffer globalConstantBufferData = new()
			{
				// Camera vectors & matrices:
				mtxCamera = mtxCamera,
				cameraPosition = node.WorldPosition,
				cameraDirection = node.WorldForward,

				// Camera parameters:
				resolutionX = resolutionX,
				resolutionY = resolutionY,
				nearClipPlane = nearClipPlane,
				farClipPlane = farClipPlane,

				// Lighting:
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
			if (projectionType == ProjectionType.Orthographic)
			{
				mtxProjection = Matrix4x4.CreateOrthographicLeftHanded(orthographicSize * AspectRatio, orthographicSize, nearClipPlane, farClipPlane);
			}
			else
			{
				mtxProjection = Matrix4x4.CreatePerspectiveFieldOfViewLeftHanded(fieldOfViewRad, AspectRatio, nearClipPlane, farClipPlane);
			}

			// Calculate viewport matrix:
			RecalculateViewportMatrix();

			// Recalculate the final camera matrix by combining the above matrices with the camera node's inverse world matrix:
			UpdateFinalCameraMatrix();

			// Reset the projection's dirty flag:
			dirtyFlags &= ~DirtyFlags.Projection;
		}
		private void RecalculateViewportMatrix()
		{
			mtxViewport = Matrix4x4.CreateViewportLeftHanded(0, 0, resolutionX, resolutionY, 0, 1);
		}
		private void UpdateFinalCameraMatrix()
		{
			// Use the inverted world matrix of the camera node to transform from world space to the camera's local space:
			if (Matrix4x4.Invert(MtxWorld, out Matrix4x4 mtxWorld2Camera))
			{
				// World space => Camera's local space => Clip space => Viewport/pixel space

				// Calculate combined matrix or transforming from world space to clip space:
				mtxCamera = mtxWorld2Camera * mtxProjection;

				Matrix4x4.Invert(mtxCamera, out mtxInvCamera);
			}
			// NOTE: If your projection is doing weird stuff on the GPU, add "#pragma pack_matrix( column_major )" to the top of it.
			// Matrix packing order in System.Numerics is column-major, not row-major! This was messy and annoying to figure out, so
			// just try to deal with it without loosing your mind too much.
		}

		private void UpdateStatesFromActiveRenderTarget()
		{
			if (!GetActiveRenderTargets(out Framebuffer? activeRenderTargets) || activeRenderTargets == null)
			{
				return;
			}

			// Check whether depth and stencil buffers are present and require clearing:
			allowClearDepth = activeRenderTargets.OutputDescription.DepthAttachment.HasValue;
			if (allowClearDepth)
			{
				PixelFormat depthFormat = activeRenderTargets.OutputDescription.DepthAttachment!.Value.Format;
				allowClearStencil = depthFormat == PixelFormat.D24_UNorm_S8_UInt || depthFormat == PixelFormat.D32_Float_S8_UInt;
			}
			else
			{
				allowClearStencil = false;
			}
		}

		/// <summary>
		/// Gets this camera's currently active render targets (aka the framebuffer it will be rendering to).
		/// </summary>
		/// <param name="_outActiveRenderTargets">Outputs a currently framebuffer that the camera will be rendering to.
		/// If an override render target was assigned, that will be prioritized. In all other cases, the camera's own
		/// render target will be used instead.</param>
		/// <returns>True if any valid render target exists and is assigned to this camera, false otherwise.</returns>
		public bool GetActiveRenderTargets(out Framebuffer? _outActiveRenderTargets)
		{
			if (IsDisposed)
			{
				_outActiveRenderTargets = null;
				return false;
			}

			if (overrideRenderTargets != null && !overrideRenderTargets.IsDisposed)
			{
				_outActiveRenderTargets = overrideRenderTargets;
				return true;
			}
			else
			{
				_outActiveRenderTargets = renderTargets;
				return !renderTargets.IsDisposed;
			}
		}

		public bool BeginFrame(CommandList _cmdList, uint _activeLightCount, bool _clearRenderTargets, out GraphicsDrawContext _outDrawCtx, out CameraContext _outCameraCtx)
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

			_cmdList.Begin();

			//bool outputHasChanged = dirtyFlags.HasFlag(DirtyFlags.Resolution) || dirtyFlags.HasFlag(DirtyFlags.RenderTarget);

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

				// Respond to changed resolution:
				if (dirtyFlags.HasFlag(DirtyFlags.Resolution))
				{
					renderTargets?.Dispose();
					TexColorTarget?.Dispose();
					TexDepthStencilTarget?.Dispose();

					if (core.CreateStandardRenderTargets(
						resolutionX,
						resolutionY,
						true,
						out Texture texColor,
						out Texture? texDepth,
						out Framebuffer framebuffer))
					{
						TexColorTarget = texColor;
						TexDepthStencilTarget = texDepth;
						renderTargets = framebuffer;
					}

					UpdateStatesFromActiveRenderTarget();

					// Check if any render target override that is currently assigned matches the camera's resolution:
					if (overrideRenderTargets != null && !overrideRenderTargets.IsDisposed)
					{
						if (overrideRenderTargets.Width != resolutionX || overrideRenderTargets.Height != resolutionY)
						{
							Logger.LogError("Resolution mismatch between camera output and override render targets!");
							_outDrawCtx = null!;
							_outCameraCtx = null!;
							return false;
						}
					}

					// Update viewport matrix now that output resolution has changed:
					if (!dirtyFlags.HasFlag(DirtyFlags.Projection))
					{
						RecalculateViewportMatrix();
					}
				}

				// Respond to changed projection or viewport:
				if (dirtyFlags.HasFlag(DirtyFlags.Projection))
				{
					UpdateProjection();
				}
				else
				{
					UpdateFinalCameraMatrix();
				}


				//TODO: Update system constant buffer with camera and projection data.


				// Fetch the currently active render targets that shall be drawn to:
				if (!GetActiveRenderTargets(out Framebuffer? activeRenderTargets) || activeRenderTargets == null)
				{
					_outDrawCtx = null!;
					_outCameraCtx = null!;
					return false;
				}

				// Check if the output resolution and format has changed, which may necessitate a pipeline rebuild:
				PixelFormat colorPixelFormat = activeRenderTargets.OutputDescription.ColorAttachments[0].Format;
				bool outputChanged =
					prevResolutionX != resolutionX ||
					prevResolutionY != resolutionY ||
					prevColorPixelFormat != colorPixelFormat;
				if (outputChanged)
				{
					cameraVersion++;
				}
				prevResolutionX = resolutionX;
				prevResolutionY = resolutionY;
				prevColorPixelFormat = colorPixelFormat;

				// Initialize constant buffers and light buffers for upcoming draw call:
				if (!GetGlobalConstantBuffer(_activeLightCount, false, out _) ||
					!GetLightDataBuffer(_activeLightCount, out _))
				{
					_outDrawCtx = null!;
					_outCameraCtx = null!;
					return false;
				}

				_outDrawCtx = new(core, _cmdList);
				_outCameraCtx = new(this, globalConstantBuffer!, lightDataBuffer!, activeRenderTargets.OutputDescription);

				// Bind current render targets as output to command list:
				_cmdList.SetFramebuffer(activeRenderTargets);

				// Clear the framebuffer before any new content is drawn to it:
				if (_clearRenderTargets && clearBackground)
				{
					_cmdList.ClearColorTarget(0, clearColor.ToRgbaFloat());

					// Clear depth and stencil buffer, as required:
					if (allowClearDepth)
					{
						if (allowClearStencil)
						{
							_cmdList.ClearDepthStencil(clearDepth, clearStencil);
						}
						else
						{
							_cmdList.ClearDepthStencil(clearDepth);
						}
					}
				}

				dirtyFlags = 0;
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

			_cmdList.End();
			return core.CommitCommandList(_cmdList);
		}

		public Vector3 TransformWorldPointToPixelCoord(Vector3 _worldPoint, bool _allowUpdateProjection = true)
		{
			// If requested, rebuild all projection matrices, or at least all those that have changed:
			if (_allowUpdateProjection)
			{
				if (dirtyFlags.HasFlag(DirtyFlags.Projection))
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
			Vector4 result = Vector4.Transform(_worldPoint, mtxCamera);
			return new Vector3(result.X, result.Y, result.Z) / result.W;
		}

		public Vector3 TransformPixelCoordToWorldPoint(Vector3 _pixelCoord, bool _allowUpdateProjection = true)
		{
			// If requested, rebuild all projection matrices, or at least all those that have changed:
			if (_allowUpdateProjection)
			{
				if (dirtyFlags.HasFlag(DirtyFlags.Projection))
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
			return Vector3.Transform(_pixelCoord, mtxInvCamera);
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

			lock (cameraStateLockObj)
			{
				resolutionX = data.ResolutionX;
				resolutionY = data.ResolutionY;

				nearClipPlane = data.NearClipPlane;
				farClipPlane = data.FarClipPlane;
				FieldOfViewDegrees = data.FieldOfViewDegrees;

				cameraPriority = data.CameraPriority;
				layerMask = data.LayerMask;

				clearBackground = data.ClearBackground;
				clearColor = Color32.ParseHexString(data.ClearColor);
				clearDepth = data.ClearDepth;
				clearStencil = data.ClearStencil;
			}

			// Re-register camera with the scene:
			node.scene.drawManager.UnregisterCamera(this);
			return node.scene.drawManager.RegisterCamera(this);
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			CameraData data;

			lock (cameraStateLockObj)
			{
				data = new()
				{
					ResolutionX = resolutionX,
					ResolutionY = resolutionY,

					NearClipPlane = nearClipPlane,
					FarClipPlane = farClipPlane,
					FieldOfViewDegrees = FieldOfViewDegrees,

					CameraPriority = cameraPriority,
					LayerMask = layerMask,

					ClearBackground = clearBackground,
					ClearColor = clearColor.ToHexString(),
					ClearDepth = clearDepth,
					ClearStencil = clearStencil,
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
