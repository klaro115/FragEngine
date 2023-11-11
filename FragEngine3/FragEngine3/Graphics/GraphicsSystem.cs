using System.Runtime.InteropServices;
using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Config;
using FragEngine3.Graphics.D3D12;
using FragEngine3.Graphics.MacOS;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Graphics
{
	public class GraphicsSystem : IDisposable
	{
		#region Types

		[Flags]
		public enum DirtyFlags
		{
			None		= 0,

			Output		= 1,	// Window size, resolution, MSAA, VSync
			Settings	= 2,	// Misc. settings and flags

			All			= Settings | Output,
		}

		#endregion
		#region Constructors

		public GraphicsSystem(Engine _engine)
		{
			engine = _engine ?? throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
			config = engine.GetEngineConfig();

			// Try loading graphics settings from file, use default settings on failure:
			if (!LoadGraphicsSettings(out _, true))
			{
				settings = engine.GetEngineConfig().Graphics.FallbackGraphicsSettings;
			}

			// Create main window and main graphics device:
			graphicsCore = CreateGraphicsCore();
			if (graphicsCore == null || graphicsCore.IsDisposed) throw new NullReferenceException("Main graphics core may not be null!");
			graphicsCore.Initialize();

			// Initialize draw call helper:
			drawCallHelper = new(this);
		}

		#endregion
		#region Fields

		public readonly Engine engine;
		private readonly EngineConfig config;
		private GraphicsSettings settings = new();

		public readonly GraphicsCore graphicsCore;
		private readonly List<GraphicsCore> allGraphicsCores = new(1);
		public readonly GraphicsDrawCallHelper drawCallHelper;

		private DirtyFlags dirtyFlags = 0;

		#endregion
		#region Properties

		public bool IsDisposed { get; private set; } = false;
		public GraphicsSettings Settings
		{
			get => settings;
			set { settings = value; MarkDirty(DirtyFlags.Settings | DirtyFlags.Output); }
		}

		public bool IsDirty => dirtyFlags != 0;

		private Logger Logger => engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public void Dispose()
		{
			GC.SuppressFinalize(this);
			Dispose(true);
		}
		private void Dispose(bool _disposing)
		{
			IsDisposed = true;

			graphicsCore.Dispose();
			for (int i = 0; i < allGraphicsCores.Count; ++i)
			{
				GraphicsCore core = allGraphicsCores[i];
				if (!core.IsDisposed) core.Dispose();
			}
			if (_disposing) allGraphicsCores.Clear();
		}

		public void MarkDirty()
		{
			dirtyFlags = DirtyFlags.All;
		}
		internal void MarkDirty(DirtyFlags _flags)
		{
			dirtyFlags |= _flags;
		}

		public bool LoadGraphicsSettings(out GraphicsSettings? _outSettings, bool _silent = false)
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot load settings for disposed graphics system!");
				_outSettings = null;
				return false;
			}
			if (engine.ResourceManager?.fileLoader == null ||
				engine.ResourceManager.IsDisposed)
			{
				Logger.LogError("Cannot determine settings path using null or disposed resource manager!");
				_outSettings = null;
				return false;
			}

			string applicationPath = engine.ResourceManager.fileLoader.applicationPath;
			string settingsDirPath = Path.Combine(applicationPath, GraphicsContants.SETTINGS_ROOT_DIR_REL_PATH);
			string filePath = Path.GetFullPath(Path.Combine(settingsDirPath, GraphicsContants.SETTINGS_FILE_NAME));

			if (!File.Exists(filePath))
			{
				if (!_silent) Logger.LogError($"Graphics settings file does not exist at path '{filePath}'!");
				_outSettings = null;
				return false;
			}

			if (!Serializer.DeserializeJsonFromFile(filePath, out _outSettings) || _outSettings == null)
			{
				if (!_silent) Logger.LogError("Failed to load graphics settings from file!");
				_outSettings = null;
				return false;
			}

			Settings = _outSettings;
			return true;
		}

		public bool SaveGraphicsSettings()
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot save settings of disposed graphics system!");
				return false;
			}
			if (engine.ResourceManager?.fileLoader == null ||
				engine.ResourceManager.IsDisposed)
			{
				Logger.LogError("Cannot determine settings path using null or disposed resource manager!");
				return false;
			}

			string applicationPath = engine.ResourceManager.fileLoader.applicationPath;
			string settingsDirPath = Path.Combine(applicationPath, GraphicsContants.SETTINGS_ROOT_DIR_REL_PATH);
			string filePath = Path.GetFullPath(Path.Combine(settingsDirPath, GraphicsContants.SETTINGS_FILE_NAME));

			return Serializer.SerializeJsonToFile(settings, filePath);
		}

		private GraphicsCore CreateGraphicsCore()
		{
			if (IsDisposed) throw new ObjectDisposedException("Graphics system", "Cannot create core for disposed graphics system!");

			GraphicsCore newCore;

			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				// On Windows, create Direct3D devices if 'config.PreferNativeFramework' is true, otherwise use Vulkan:
				if (config.Graphics.PreferNativeFramework)
				{
					newCore = new Dx12GraphicsCore(this, config);
				}
				else
				{
					throw new NotImplementedException("Windows Vulkan graphics have not been implemented!");
				}
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
			{
				// On MacOS, Metal is the only real option, unless we want to use something like MoltenVK:
				newCore = new MacGraphicsCore(this, config);
			}
			else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
			{
				// On Linux, Vulkan is the only supported framework:
				throw new NotImplementedException("Linux graphics support has not been implemented!");
			}
			else
			{
				throw new PlatformNotSupportedException("Current OS platform is not supported by graphics system!");
			}

			// Register core with this graphics system:
			allGraphicsCores.Add(newCore);
			allGraphicsCores.RemoveAll(o => o == null || o.IsDisposed);
			return newCore;
		}

		/// <summary>
		/// Update the window message loop to intercept user input and OS signals.
		/// </summary>
		/// <param name="_outRequestExit">Outputs whether a quit signal was received. (Ex.: WM_QUIT on windows)</param>
		/// <returns>True if the message loop was worked off successfully, false if an error occurred.</returns>
		public bool UpdateMessageLoop(out bool _outRequestExit)
		{
			if (!IsDisposed && graphicsCore != null)
			{
				return graphicsCore.UpdateMessageLoop(out _outRequestExit);
			}
			_outRequestExit = true;
			return false;
		}

		public bool BeginFrame()
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot begin new frame on disposed graphics system!");
				return false;
			}
			if (graphicsCore == null || !graphicsCore.IsInitialized)
			{
				Logger.LogError("Cannot begin new frame with null or uninitialized graphics core!");
				return false;
			}

			bool success = true;

			foreach (GraphicsCore core in allGraphicsCores)
			{
				if (core.IsInitialized)
				{
					success &= core.BeginFrame(dirtyFlags);
				}
			}

			dirtyFlags = DirtyFlags.None;
			return success;
		}

		public bool EndFrame()
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot end frame on disposed graphics system!");
				return false;
			}
			if (graphicsCore == null || !graphicsCore.IsInitialized)
			{
				Logger.LogError("Cannot end frame with null or uninitialized graphics core!");
				return false;
			}

			bool success = true;

			foreach (GraphicsCore core in allGraphicsCores)
			{
				if (core.IsInitialized)
				{
					success &= core.EndFrame();
				}
			}

			return success;
		}

		#endregion
	}
}

