using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Internal;
using Veldrid;
using Veldrid.Sdl2;

namespace FragEngine3.Graphics
{
	public abstract class GraphicsCore : IDisposable
	{
		#region Constructors

		protected GraphicsCore(GraphicsSystem _graphicsSystem, EngineConfig _config)
		{
			graphicsSystem = _graphicsSystem ?? throw new ArgumentNullException(nameof(_graphicsSystem), "Graphics system may not be null!");
			config = _config ?? graphicsSystem.engine.GetEngineConfig();
		}
		~GraphicsCore()
		{
			if (!IsDisposed) Dispose(false);
		}

		#endregion
		#region Fields

		protected bool isInitialized = false;
		protected bool quitMessageReceived = false;

		public readonly GraphicsSystem graphicsSystem;
		protected readonly EngineConfig config;

		public PixelFormat DefaultColorTargetPixelFormat { get; protected set; } = PixelFormat.R8_G8_B8_A8_UNorm;
		public PixelFormat DefaultDepthTargetPixelFormat { get; protected set; } = PixelFormat.D24_UNorm_S8_UInt;
		public PixelFormat DefaultShadowMapDepthTargetFormat { get; protected set; } = PixelFormat.R16_UNorm;

		protected readonly List<CommandList> cmdListQueue = new(1);

		protected CommandList? blittingCmdList = null;
		protected readonly List<AsyncGeometryDownloadRequest> asyncDownloadsRequests = [];

		protected readonly object cmdLockObj = new();
		protected readonly object downloadLockObj = new();

		#endregion
		#region Properties

		public bool IsDisposed { get; protected set; } = false;
		public bool IsInitialized => !IsDisposed && isInitialized;

		public GraphicsDevice Device { get; protected set; } = null!;
		public Sdl2Window Window { get; protected set; } = null!;

		public CommandList MainCommandList { get; protected set; } = null!;
		public ResourceFactory MainFactory { get; protected set; } = null!;

		public abstract EnginePlatformFlag ApiPlatformFlag { get; }
		public abstract bool DefaultMirrorY { get; }

		private Logger Logger => graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		protected virtual void Dispose(bool _disposing)
		{
			IsDisposed = true;

			if (_disposing)
			{
				Shutdown();
			}

			blittingCmdList?.Dispose();
			MainCommandList?.Dispose();
			Device?.Dispose();
			Window?.Close();
		}

		public abstract bool Initialize();

		public virtual void Shutdown()
		{
			isInitialized = false;
			quitMessageReceived = false;

			asyncDownloadsRequests.Clear();

			Window?.Close();
			Window = null!;

			Device?.Dispose();
			Device = null!;
		}

		/// <summary>
		/// Update the window and device's message loop to intercept user input and OS signals.
		/// </summary>
		/// <param name="_outRequestExit">Outputs whether a quit signal was received. (Ex.: WM_QUIT on windows)</param>
		/// <returns>True if the message loop was worked off successfully, false if an error occurred.</returns>
		public virtual bool UpdateMessageLoop(out bool _outRequestExit)
		{
			if (!IsInitialized || Window == null)
			{
				_outRequestExit = true;
				return false;
			}

			// Process event backlog:
			InputSnapshot snapshot;
			try
			{
				snapshot = Window.PumpEvents();
			}
			catch (Exception ex)
			{
				Logger.LogException("An exception was caught while updating message loop!", ex);
				_outRequestExit = true;
				return false;
			}

			// Send window input events to input manager:
			if (graphicsSystem.engine?.InputManager != null)
			{
				graphicsSystem.engine.InputManager.UpdateInputStates(snapshot);
			}

			// Output exit request if the window was closed recently:
			_outRequestExit = quitMessageReceived;
			return true;
		}

		/// <summary>
		/// Gets a descriptor detailing very broad capabilities of this graphics framework instance.
		/// </summary>
		/// <returns>A read-only descriptor of device/framework capabilities.</returns>
		public abstract GraphicsCapabilities GetCapabilities();

		public bool CreateCommandList(out CommandList? _outCmdList, CommandListDescription? _cmdListDesc = null)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot create new command list using uninitialized graphics devices!");
				_outCmdList = null;
				return false;
			}

			try
			{
				_outCmdList = _cmdListDesc != null
					? Device.ResourceFactory.CreateCommandList(_cmdListDesc.Value)
					: Device.ResourceFactory.CreateCommandList();
				return _outCmdList != null && !_outCmdList.IsDisposed;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create graphics command list!", ex);
				_outCmdList = null;
				return false;
			}
		}

		public bool CommitCommandList(CommandList _cmdList)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot commit command list to uninitialized graphics core!");
				return false;
			}
			if (_cmdList == null || _cmdList.IsDisposed)
			{
				Logger.LogError("Cannot commit null or disposed command list!");
				return false;
			}

			lock(cmdLockObj)
			{
				cmdListQueue.Add(_cmdList);
			}
			return true;
		}

		public bool BeginFrame()
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot begin frame on uninitialized graphics core!");
				return false;
			}

			// Ensure committed command lists have been reset:
			cmdListQueue.Clear();

			// Start a new frame by opening main command list and assigning main render target:
			MainCommandList.Begin();
			MainCommandList.SetFramebuffer(Device.SwapchainFramebuffer);

			return true;
		}

		public bool EndFrame()
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot execute draw calls using uninitialized graphics core!");
				return false;
			}

			// Finalize main command list:
			MainCommandList.End();
			CommitCommandList(MainCommandList);

			// Wait for previous frame to end:
			Device.WaitForIdle();

			// If there are async download requests, issue these now:
			if (asyncDownloadsRequests.Count != 0 && blittingCmdList != null)
			{
				lock (downloadLockObj)
				{
					blittingCmdList.Begin();

					foreach (AsyncGeometryDownloadRequest request in asyncDownloadsRequests)
					{
						request.Dispatch(blittingCmdList);
					}

					blittingCmdList.End();
				}
				// Assume we want the current state rather than the next frame's, so enqueue downloads first:
				lock (cmdLockObj)
				{
					cmdListQueue.Insert(0, blittingCmdList);
				}
			}

			lock(cmdLockObj)
			{
				// Submit all additional committed command lists:
				foreach (CommandList cmdList in cmdListQueue)
				{
					Device.SubmitCommands(cmdList);
				}

				// Reset/Empty queue for next frame:
				cmdListQueue.Clear();
			}

			// Present buffer to screen:
			Device.SwapBuffers();

			// Finish up download requests:
			lock (downloadLockObj)
			{
				if (asyncDownloadsRequests.Count != 0)
				{
					//Device.WaitForIdle();
					foreach (AsyncGeometryDownloadRequest request in asyncDownloadsRequests)
					{
						if (request != null && request.IsValid)
						{
							request.Finish();
							request.Dispose();
						}
					}
				}
				asyncDownloadsRequests.Clear();
			}

			return true;
		}

		internal bool ScheduleAsyncGeometryDownload(AsyncGeometryDownloadRequest _request)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot schedule async geometry download on uninitialized graphics core!");
				return false;
			}
			if (_request == null || _request.IsValid)
			{
				Logger.LogError("Cannot schedule null or invalid async geometry download request!");
				return false;
			}
			if (_request.callbackReceiveDownloadedData == null)
			{
				Logger.LogError("Cannot schedule async geometry download request with null callback!");
				return false;
			}

			// Prepare a command list specifically for blitting/copying resources:
			if (blittingCmdList == null || blittingCmdList.IsDisposed)
			{
				blittingCmdList = MainFactory.CreateCommandList();
			}

			// Queue up download request:
			lock(downloadLockObj)
			{
				asyncDownloadsRequests.Add(_request);
			}
			return true;
		}

		/// <summary>
		/// Creates a set of render targets according to standard/default parameters for this platform and device.
		/// </summary>
		/// <param name="_width">Horizontal resolution of output image, in pixels. Depending on platform, this should be a multiple of 8.</param>
		/// <param name="_height">Vertical resolution of output image, in pixels.</param>
		/// <param name="_createDepth">Whether to create a depth and stencil buffer for Z-testing while drawing to the render targets.</param>
		/// <param name="_outTexColorTarget">Outputs an RGBA render target for lit and colored render results. This is the stuff you want to display on screen.</param>
		/// <param name="_outTexDepthTarget">Outputs a depth texture, with an optional stencil channel. Null if no depth buffer was requested.</param>
		/// <param name="_outFramebuffer">Outputs a framebuffer using the created color and depth textures as render targets.</param>
		/// <returns>True if frame buffer and texture creation succeeded, false otherwise. All outputs will be disposed on failure.</returns>
		public virtual bool CreateStandardRenderTargets(
			uint _width,
			uint _height,
			bool _createDepth,
			out Texture _outTexColorTarget,
			out Texture? _outTexDepthTarget,
			out Framebuffer _outFramebuffer)
		{
			return CreateRenderTargets(
				DefaultColorTargetPixelFormat,
				_width,
				_height,
				_createDepth,
				out _outTexColorTarget,
				out _outTexDepthTarget,
				out _outFramebuffer);
		}

		/// <summary>
		/// Creates a set of render targets.
		/// </summary>
		/// <param name="_colorFormat">The pixel format for the color texture of the framebuffer. Must be a packed uncompressed format.</param>
		/// <param name="_width">Horizontal resolution of output image, in pixels. Depending on platform, this should be a multiple of 8.</param>
		/// <param name="_height">Vertical resolution of output image, in pixels.</param>
		/// <param name="_createDepth">Whether to create a depth and stencil buffer for Z-testing while drawing to the render targets.</param>
		/// <param name="_outTexColorTarget">Outputs an RGBA render target for lit and colored render results. This is the stuff you want to display on screen.</param>
		/// <param name="_outTexDepthTarget">Outputs a depth texture, with an optional stencil channel. Null if no depth buffer was requested.</param>
		/// <param name="_outFramebuffer">Outputs a framebuffer using the created color and depth textures as render targets.</param>
		/// <returns>True if frame buffer and texture creation succeeded, false otherwise. All outputs will be disposed on failure.</returns>
		public virtual bool CreateRenderTargets(
			PixelFormat _colorFormat,
			uint _width,
			uint _height,
			bool _createDepth,
			out Texture _outTexColorTarget,
			out Texture? _outTexDepthTarget,
			out Framebuffer _outFramebuffer)
		{
			if (!IsInitialized)
			{
				_outTexColorTarget = null!;
				_outTexDepthTarget = null;
				_outFramebuffer = null!;
				Logger.LogError("Cannot create render targets using uninitialized graphics core!");
				return false;
			}

			TextureSampleCount msaaCount = graphicsSystem.Settings.GetTextureSampleCount();

			// Try creating main color target:
			TextureDescription texColorTargetDesc = new(
				_width,
				_height,
				1, 1, 1,
				_colorFormat,
				TextureUsage.RenderTarget | TextureUsage.Sampled,
				TextureType.Texture2D,
				msaaCount);

			try
			{
				_outTexColorTarget = MainFactory.CreateTexture(ref texColorTargetDesc);
				_outTexColorTarget.Name = $"TexColorTarget_{_width}x{_height}_{_colorFormat}";
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create color render targets!", ex);
				_outTexColorTarget = null!;
				_outTexDepthTarget = null;
				_outFramebuffer = null!;
				return false;
			}

			// Try creating depth/stencil target:
			if (_createDepth)
			{
				TextureDescription texDepthTargetDesc = new(
					_width,
					_height,
					1, 1, 1,
					DefaultDepthTargetPixelFormat,
					TextureUsage.DepthStencil | TextureUsage.Sampled,
					TextureType.Texture2D,
					msaaCount);

				try
				{
					_outTexDepthTarget = MainFactory.CreateTexture(ref texDepthTargetDesc);
					_outTexDepthTarget.Name = $"TexDepthTarget_{_width}x{_height}_{DefaultDepthTargetPixelFormat}";
				}
				catch (Exception ex)
				{
					Logger.LogException("Failed to create depth render targets!", ex);
					_outTexColorTarget?.Dispose();
					_outTexColorTarget = null!;
					_outTexDepthTarget = null;
					_outFramebuffer = null!;
					return false;
				}
			}
			else
			{
				_outTexDepthTarget = null;
			}

			try
			{
				FramebufferDescription frameBufferDesc = new(_outTexDepthTarget, _outTexColorTarget);

				_outFramebuffer = MainFactory.CreateFramebuffer(ref frameBufferDesc);
				_outFramebuffer.Name = $"Framebuffer_{_width}x{_height}_{_colorFormat}_HasDepth={_createDepth}";
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException("Failed to create framebuffer from render targets!", ex);
				_outTexColorTarget?.Dispose();
				_outTexDepthTarget?.Dispose();
				_outTexColorTarget = null!;
				_outTexDepthTarget = null;
				_outFramebuffer = null!;
				return false;
			}
		}

		/// <summary>
		/// Creates a tiny blank texture where all pixels are filled with the same color.<para/>
		/// NOTE: This is used internally to create single-color placeholder textures held by the graphics system.
		/// </summary>
		/// <param name="_fillColor">The color to fill all pixels of the texture with.</param>
		/// <param name="_outTexture">Outputs a new texture with a size of 8x8 pixels, that are all of the same color.
		/// This may be null if texture creation fails.</param>
		/// <returns>True if a texture was created and colored successfully, false otherwise. All outputs will be disposed
		/// on failure.</returns>
		public bool CreateBlankTexture(RgbaByte _fillColor, out Texture _outTexture)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot create blank texture using initialized graphics core!");
				_outTexture = null!;
				return false;
			}

			// Create a small 2D texture:
			const int width = 8;
			const int height = 8;
			const int depth = 1;

			TextureDescription textureDesc = new(
				width,
				height,
				depth,
				1, 1,
				DefaultColorTargetPixelFormat,
				TextureUsage.Sampled,
				TextureType.Texture2D);

			try
			{
				_outTexture = MainFactory.CreateTexture(ref textureDesc);
				_outTexture.Name = $"TexBlank_{width}x{height}_{_fillColor}";
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create blank texture! (Fill color: {_fillColor})", ex);
				_outTexture = null!;
				return false;
			}

			// Initialize the texture to a solid color:
			if (!FillTexture(_outTexture, _fillColor, width, height, depth, 0, 0))    // TODO [Bug]: This will be incorrect, if the default pixel format is anything other than 32-bit packed RGBA!
			{
				Logger.LogError($"Failed to fill blank texture with solid color! (Fill color: {_fillColor})");
				_outTexture.Dispose();
				_outTexture = null!;
				return false;
			}
			return true;
		}

		public bool CreateShadowMapArray(uint _resolutionX, uint _resolutionY, uint _arraySize, out Texture _outTexShadowMapArray)
		{
			if (!IsInitialized)
			{
				Logger.LogError("Cannot create shadow map texture array using initialized graphics core!");
				_outTexShadowMapArray = null!;
				return false;
			}

			_resolutionX = Math.Max(_resolutionX, 8);
			_resolutionY = Math.Max(_resolutionY, 8);
			_arraySize = Math.Max(_arraySize, 1);

			const int depth = 1;

			// Create a 2D texture array in single-channel 16-bit float format:
			TextureDescription textureArrayDesc = new(
				_resolutionX,
				_resolutionY,
				depth,
				1,
				_arraySize,
				DefaultShadowMapDepthTargetFormat,
				TextureUsage.Sampled | TextureUsage.DepthStencil,
				TextureType.Texture2D);

			try
			{
				_outTexShadowMapArray = MainFactory.CreateTexture(ref textureArrayDesc);
				_outTexShadowMapArray.Name = $"TexShadowMapArray_{_resolutionX}x{_resolutionY}_count={_arraySize}";
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to create shadow map texture array! (Resolution: {_resolutionX}x{_resolutionY}, count={_arraySize})", ex);
				_outTexShadowMapArray = null!;
				return false;
			}

			// First element is always the default/blank texture, with depth set to maximum:
			bool wasCleared = DefaultShadowMapDepthTargetFormat switch
			{
				PixelFormat.R16_UNorm =>			FillTexture(_outTexShadowMapArray, ushort.MaxValue,	_resolutionX, _resolutionY, depth, 0, 0),
				PixelFormat.R32_Float =>			FillTexture(_outTexShadowMapArray, 1.0f,			_resolutionX, _resolutionY, depth, 0, 0),
				PixelFormat.D24_UNorm_S8_UInt =>	FillTexture(_outTexShadowMapArray, 0xFFFFFF00u,		_resolutionX, _resolutionY, depth, 0, 0),
				PixelFormat.D32_Float_S8_UInt =>	FillTexture(_outTexShadowMapArray, 1.0f,			_resolutionX, _resolutionY, depth, 0, 0),
				_ => false,
			};
			if (!wasCleared)
			{
				Logger.LogError("Failed to clear first layer of shadow map texture array to maximum depth!");
			}
			return wasCleared;
		}

		private bool FillTexture<T>(Texture _texture, T _fillValue, uint _resolutionX, uint _resolutionY, uint _depth = 1, uint _mipLevel = 0, uint _arrayLayer = 0) where T : unmanaged
		{
			uint pixelCount = _resolutionX * _resolutionY * _depth;
			T[] pixelData = new T[pixelCount];
			Array.Fill(pixelData, _fillValue);

			try
			{
				Device.UpdateTexture(_texture, pixelData, 0, 0, 0, _resolutionY, _resolutionY, _depth, _mipLevel, _arrayLayer);
				return true;
			}
			catch (Exception ex)
			{
				Logger.LogException($"Failed to fill texture with solid color value!", ex);
				return false;
			}
		}

		#endregion
	}
}

