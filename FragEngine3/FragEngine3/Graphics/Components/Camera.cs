using FragEngine3.Graphics.Components.Data;
using FragEngine3.Graphics.Resources;
using FragEngine3.Resources;
using FragEngine3.Scenes;
using FragEngine3.Scenes.Data;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics.Components
{
	public sealed class Camera : Component
	{
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
		}

		#endregion
		#region Fields

		private readonly GraphicsCore core;

		// Resolution:
		private uint resolutionX = 640;
		private uint resolutionY = 480;

		// Projection:
		private float nearClipPlane = 0.01f;
		private float farClipPlane = 1000.0f;
		private float fieldOfViewRad = 60.0f * Deg2Rad;

		// Content:
		public uint cameraPriority = 1000;
		public uint layerMask = 0xFFFFFFFFu;

		// Clearing:
		public bool clearBackground = true;
		private bool allowClearDepth = true;
		private bool allowClearStencil = false;
		public Color32 clearColor = Color32.Cornflower;
		public float clearDepth = 1.0e+8f;
		public byte clearStencil = 0x00;


		//TODO: Resize or recreate output render targets when resolutions are changed.
		//TODO: Register camera components in scene, then draw them automatically (sorted by 'cameraPriority' value) via the scene's graphics stack.
		//TODO: Add material override (for shadow maps, etc.) or add shadow map override to materials.
		//TODO: Consider adding a "useSimplifiedRendering" flag, which would prompt usage of simplified material overrides, if available.

		// Output:
		private Framebuffer renderTargets = null!;
		private Framebuffer? overrideRenderTargets = null;

		private static Camera? mainCamera = null;

		private static readonly object mainCameraLockObj = new();

		#endregion
		#region Constants

		private const float Rad2Deg = 180.0f / MathF.PI;
		private const float Deg2Rad = MathF.PI / 180.0f;
		
		#endregion
		#region Properties

		/// <summary>
		/// Gets or sets the width of the camera's output images in pixels. Must be a value between 1 and 8192.<para/>
		/// NOTE: Some GPU architectures may support only a limited set of output resolutions, though most shouldn't
		/// complain so long as the with is divisible by 8.
		/// </summary>
		public uint ResolutionX
		{
			get => resolutionX;
			set => resolutionX = Math.Clamp(value, 1, 8192);
		}
		/// <summary>
		/// Gets or sets the height of the camera's output images in pixels. Must be a value between 1 and 8192.
		/// </summary>
		public uint ResolutionY
		{
			get => resolutionY;
			set => resolutionY = Math.Clamp(value, 1, 8192);
		}

		/// <summary>
		/// Gets or sets the nearest clipping distance of the camera; geometry closer than this can't be rendered
		/// and will be clipped. Must be larger than 0 and less than the value of <see cref="FarClipPlane"/>.
		/// </summary>
		public float NearClipPlane
		{
			get => nearClipPlane;
			set => nearClipPlane = Math.Clamp(value, 0.001f, Math.Min(farClipPlane, 99999.9f));
		}
		/// <summary>
		/// Gets or sets the far clipping distance of the camera; geometry further away than this can't be rendered
		/// and will be clipped. Must be larger <see cref="NearClipPlane"/> and less than 100K.
		/// </summary>
		public float FarClipPlane
		{
			get => farClipPlane;
			set => farClipPlane = Math.Clamp(value, Math.Min(nearClipPlane, 0.002f), 100000.0f);
		}

		/// <summary>
		/// Gets or sets the field of view angle in degrees.
		/// </summary>
		public float FieldOfViewDegrees
		{
			get => fieldOfViewRad * Rad2Deg;
			set => fieldOfViewRad = Math.Clamp(value, 0.001f, 179.0f) * Deg2Rad;
		}
		/// <summary>
		/// Gets or sets the field of view angle in radians.
		/// </summary>
		public float FieldOfViewRadians
		{
			get => fieldOfViewRad;
			set => fieldOfViewRad = Math.Clamp(value, 0.001f * Deg2Rad, 179.0f * Deg2Rad);
		}

		public ResourceHandle? DefaultShadowMaterialHandle { get; private set; } = null;
		public Material? DefaultShadowMaterial { get; private set; } = null;

		public Texture TexColorTarget { get; private set; } = null!;
		public Texture? TexDepthStencilTarget { get; private set; } = null!;

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
			return true;
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

		public bool BeginFrame(CommandList _cmdList)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot bind disposed camera for rendering!");
				return false;
			}
			if (_cmdList == null || _cmdList.IsDisposed)
			{
				Logger.LogError("Cannot bind camera using null or disposed command list!");
				return false;
			}

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


			//TODO: Update system constant buffer with camera and projection data.


			// Fetch the currently active render targets that shall be drawn to:
			if (!GetActiveRenderTargets(out Framebuffer? activeRenderTargets) || activeRenderTargets == null)
			{
				return false;
			}

			// Bind current render targets as output to command list:
			_cmdList.SetFramebuffer(activeRenderTargets);

			// Clear the framebuffer before any new content is drawn to it:
			if (clearBackground)
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

			return true;
		}

		public bool EndFrame()
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot unbind disposed camera from rendering!");
				return false;
			}

			//...

			return true;
		}

		public override bool LoadFromData(in ComponentData _componentData, in Dictionary<int, ISceneElementData> _idDataMap)
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
				Logger.LogError("Deserialize camera component data is invalid!");
				return false;
			}

			resolutionX = data.ResolutionX;
			resolutionY = data.ResolutionY;

			nearClipPlane = data.NearClipPlane;
			farClipPlane = data.FarClipPlane;
			FieldOfViewDegrees = data.FieldOfViewDegrees;

			return true;
		}

		public override bool SaveToData(out ComponentData _componentData, in Dictionary<ISceneElement, int> _idDataMap)
		{
			CameraData data = new()
			{
				ResolutionX = resolutionX,
				ResolutionY = resolutionY,

				NearClipPlane = nearClipPlane,
				FarClipPlane = farClipPlane,
				FieldOfViewDegrees = FieldOfViewDegrees,
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
}
