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

		public PixelFormat DefaultColorTargetPixelFormat { get; protected set; }= PixelFormat.R8_G8_B8_A8_UNorm;
		public PixelFormat DefaultDepthTargetPixelFormat { get; protected set; } = PixelFormat.D24_UNorm_S8_UInt;

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
		/// Create the render targets according to standard/default parameters for this platform and device.
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

		#endregion
	}
}

