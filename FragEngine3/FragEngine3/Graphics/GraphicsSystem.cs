using System.Numerics;
using System.Runtime.InteropServices;
using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Config;
using FragEngine3.Graphics.D3D11;
using FragEngine3.Graphics.Metal;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Vulkan;
using FragEngine3.Resources;
using FragEngine3.Utility.Serialization;
using Veldrid;

namespace FragEngine3.Graphics;

/// <summary>
/// The engine's managment service for graphics and rendering. Graphics devices and windows are exposed through the
/// <see cref="GraphicsCore"/> class, all instances of which are owned by this type.
/// </summary>
public class GraphicsSystem : IEngineSystem
{
	#region Constructors

	/// <summary>
	/// Creates a new graphics system for the engine.
	/// </summary>
	/// <param name="_engine"></param>
	/// <exception cref="ArgumentNullException">The engine may not be null.</exception>
	/// <exception cref="ObjectDisposedException">The engine has already been disposed.</exception>
	/// <exception cref="NullReferenceException">Failed to get or create a main <see cref="GraphicsCore"/>.</exception>
	/// <exception cref="Exception">Failed to initialize the main <see cref="GraphicsCore"/>.</exception>
	internal GraphicsSystem(Engine _engine)
	{
		if (_engine is null)
			throw new ArgumentNullException(nameof(_engine), "Engine may not be null!");
		if (_engine.IsDisposed)
			throw new ObjectDisposedException(nameof(_engine), "Engine may not be disposed!");

		Engine = _engine;
		logger = Engine.Logger;
		config = Engine.GetEngineConfig();

		// Try loading graphics settings from file, use default settings on failure:
		if (!LoadGraphicsSettings(out _, true))
		{
			Settings = Engine.GetEngineConfig().Graphics.FallbackGraphicsSettings;
		}

		// Create main window and main graphics device:
		graphicsCore = CreateGraphicsCore();
		if (graphicsCore is null || graphicsCore.IsDisposed)
		{
			throw new NullReferenceException("Main graphics core creation failed!");
		}

		if (!graphicsCore.Initialize())
		{
			throw new Exception("Failed to initialize the main graphics core!");
		}

		// Listen to window events:
		graphicsCore.WindowClosing += OnWindowClosing;
		graphicsCore.WindowResized += OnWindowResized;
	}

	#endregion
	#region Events

	/// <summary>
	/// Event that is triggered whenever a window is closing.
	/// </summary>
	public event FuncWindowClosing? WindowClosing = null;
	/// <summary>
	/// Event that is triggered whenever a window's size has changed.
	/// </summary>
	public event FuncWindowResized? WindowResized = null;

	#endregion
	#region Fields

	private readonly Logger logger;
	private readonly EngineConfig config;

	/// <summary>
	/// The main graphics core of the engine, created automatically on startup.
	/// </summary>
	public readonly GraphicsCore graphicsCore;
	private readonly List<GraphicsCore> allGraphicsCores = new(1);

	#endregion
	#region Properties

	/// <summary>
	/// Gets whether this graphics system has been disposed already.
	/// </summary>
	public bool IsDisposed { get; private set; } = false;

	public Engine Engine { get; }

	public GraphicsSettings Settings { get; set; } = new(); //TODO: Setter needs to notify graphics systems of changed settings.

	public ResourceHandle TexPlaceholderWhite { get; private set; } = ResourceHandle.None; //TODO: These are core-specific resources; move to graphics core!
	public ResourceHandle TexPlaceholderGray { get; private set; } = ResourceHandle.None;
	public ResourceHandle TexPlaceholderBlack { get; private set; } = ResourceHandle.None;
	public ResourceHandle TexPlaceholderTransparent { get; private set; } = ResourceHandle.None;
	public ResourceHandle TexPlaceholderMagenta { get; private set; } = ResourceHandle.None;

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

		if (TexPlaceholderWhite is not null && TexPlaceholderWhite.IsLoaded) TexPlaceholderWhite.Unload();
		if (TexPlaceholderGray is not null && TexPlaceholderGray.IsLoaded) TexPlaceholderGray.Unload();
		if (TexPlaceholderBlack is not null && TexPlaceholderBlack.IsLoaded) TexPlaceholderBlack.Unload();
		if (TexPlaceholderTransparent is not null && TexPlaceholderTransparent.IsLoaded) TexPlaceholderTransparent.Unload();
		if (TexPlaceholderMagenta is not null && TexPlaceholderMagenta.IsLoaded) TexPlaceholderMagenta.Unload();

		if (_disposing)
		{
			TexPlaceholderWhite = ResourceHandle.None;
			TexPlaceholderGray = ResourceHandle.None;
			TexPlaceholderBlack = ResourceHandle.None;
			TexPlaceholderTransparent = ResourceHandle.None;
			TexPlaceholderMagenta = ResourceHandle.None;
		}

		graphicsCore.Dispose();
		for (int i = 0; i < allGraphicsCores.Count; ++i)
		{
			GraphicsCore core = allGraphicsCores[i];
			if (!core.IsDisposed) core.Dispose();
		}
		if (_disposing) allGraphicsCores.Clear();
	}

	/// <summary>
	/// Tries to load graphics settings from a file located in the app's configuration and settings directory.
	/// </summary>
	/// <param name="_outSettings">Outputs the settings that were loaded from file. Null if no settings file exists, or if loading it has failed.</param>
	/// <param name="_silent">Whether to skip logging error messages. If false, any failure to find and load a settings file will log an error.</param>
	/// <returns>True if a settings file exists and was loaded successfully, false otherwise.</returns>
	public bool LoadGraphicsSettings(out GraphicsSettings? _outSettings, bool _silent = false)
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot load settings for disposed graphics system!");
			_outSettings = null;
			return false;
		}
		if (Engine.ResourceManager?.fileGatherer is null ||
			Engine.ResourceManager.IsDisposed)
		{
			logger.LogError("Cannot determine settings path using null or disposed resource manager!");
			_outSettings = null;
			return false;
		}

		string applicationPath = Engine.ResourceManager.fileGatherer.applicationPath;
		string settingsDirPath = Path.Combine(applicationPath, GraphicsConstants.SETTINGS_ROOT_DIR_REL_PATH);
		string filePath = Path.GetFullPath(Path.Combine(settingsDirPath, GraphicsConstants.SETTINGS_FILE_NAME));

		if (!File.Exists(filePath))
		{
			if (!_silent) logger.LogError($"Graphics settings file does not exist at path '{filePath}'!");
			_outSettings = null;
			return false;
		}

		if (!Serializer.DeserializeJsonFromFile(filePath, out _outSettings) || _outSettings == null)
		{
			if (!_silent) logger.LogError("Failed to load graphics settings from file!");
			_outSettings = null;
			return false;
		}

		Settings = _outSettings;
		return true;
	}

	/// <summary>
	/// Saves the current graphics settings to file into the app's configuration and settings directory.
	/// </summary>
	/// <returns>True if settings were successfully serialized to file, false otherwise.</returns>
	public bool SaveGraphicsSettings()
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot save settings of disposed graphics system!");
			return false;
		}
		if (Engine.ResourceManager?.fileGatherer is null ||
			Engine.ResourceManager.IsDisposed)
		{
			logger.LogError("Cannot determine settings path using null or disposed resource manager!");
			return false;
		}

		string applicationPath = Engine.ResourceManager.fileGatherer.applicationPath;
		string settingsDirPath = Path.Combine(applicationPath, GraphicsConstants.SETTINGS_ROOT_DIR_REL_PATH);
		string filePath = Path.GetFullPath(Path.Combine(settingsDirPath, GraphicsConstants.SETTINGS_FILE_NAME));

		return Serializer.SerializeJsonToFile(Settings, filePath);
	}

	private GraphicsCore CreateGraphicsCore()
	{
		if (IsDisposed) throw new ObjectDisposedException("Graphics system", "Cannot create core for disposed graphics system!");

		logger.LogMessage($"+ Creating graphics core for platform: '{Engine.PlatformSystem.PlatformFlags}'");

		GraphicsCore newCore;

		if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
		{
			// On Windows, create Direct3D devices if 'config.PreferNativeFramework' is true, otherwise use Vulkan:
			if (config.Graphics.PreferNativeFramework)
			{
				newCore = new Dx11GraphicsCore(this, config);
			}
			else
			{
				newCore = new VulkanGraphicsCore(this, config);
			}
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
		{
			// On MacOS, Metal is the only real option, unless we want to use something like MoltenVK:
			newCore = new MetalGraphicsCore(this, config);
		}
		else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
		{
			// On Linux, Vulkan is the only supported framework:
			newCore = new VulkanGraphicsCore(this, config);
		}
		else
		{
			throw new PlatformNotSupportedException("Current OS platform is not supported by graphics system!");
		}

		// Register core with this graphics system:
		allGraphicsCores.Add(newCore);
		allGraphicsCores.RemoveAll(o => o is null || o.IsDisposed);
		return newCore;
	}

	internal bool LoadBaseContent()
	{
		if (IsDisposed || !graphicsCore.IsInitialized)
		{
			logger.LogError("Cannot load base content for disposed or uninitialized graphics system!");
			return false;
		}

		bool success = true;

		logger.LogMessage("- Creating placeholder textures.");

		if (success &= CreatePlaceholderTexture("TexWhite", RgbaByte.White, out ResourceHandle texHandle))
		{
			TexPlaceholderWhite = texHandle;
		}
		if (success &= CreatePlaceholderTexture("TexGray", new RgbaByte(127, 127, 127, 255), out texHandle))
		{
			TexPlaceholderGray = texHandle;
		}
		if (success &= CreatePlaceholderTexture("TexBlack", RgbaByte.Black, out texHandle))
		{
			TexPlaceholderBlack = texHandle;
		}
		if (success &= CreatePlaceholderTexture("TexTransparent", new RgbaByte(0, 0, 0, 0), out texHandle))
		{
			TexPlaceholderTransparent = texHandle;
		}
		if (success &= CreatePlaceholderTexture("TexMagenta", new RgbaByte(255, 0, 255, 255), out texHandle))
		{
			TexPlaceholderMagenta = texHandle;
		}

		return success;


		// Local helper method for creating solid-color placeholder textures:
		bool CreatePlaceholderTexture(string _resourceKey, RgbaByte _fillColor, out ResourceHandle _outHandle)
		{
			if (!graphicsCore.CreateBlankTexture(_fillColor, out Texture texBlank))
			{
				_outHandle = ResourceHandle.None;
				return false;
			}
			return new TextureResource(_resourceKey, Engine, texBlank, out _outHandle).IsLoaded;
		}
	}

	/// <summary>
	/// Update the window message loop to intercept user input and OS signals.
	/// </summary>
	/// <param name="_outRequestExit">Outputs whether a quit signal was received. (Ex.: WM_QUIT on windows)</param>
	/// <returns>True if the message loop was worked off successfully, false if an error occurred.</returns>
	internal bool UpdateMessageLoop(out bool _outRequestExit)
	{
		if (!IsDisposed && graphicsCore is not null)
		{
			return graphicsCore.UpdateMessageLoop(out _outRequestExit);
		}
		_outRequestExit = true;
		return false;
	}

	internal bool BeginFrame()
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot begin new frame on disposed graphics system!");
			return false;
		}
		if (graphicsCore is null || !graphicsCore.IsInitialized)
		{
			logger.LogError("Cannot begin new frame with null or uninitialized graphics core!");
			return false;
		}

		bool success = true;

		foreach (GraphicsCore core in allGraphicsCores)
		{
			if (core.IsInitialized)
			{
				success &= core.BeginFrame();
			}
		}

		return success;
	}

	internal bool EndFrame()
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot end frame on disposed graphics system!");
			return false;
		}
		if (graphicsCore is null || !graphicsCore.IsInitialized)
		{
			logger.LogError("Cannot end frame with null or uninitialized graphics core!");
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

	private void OnWindowClosing(GraphicsCore _graphicsCore)
	{
		if (!IsDisposed && WindowClosing is not null)
		{
			WindowClosing.Invoke(_graphicsCore);
		}
	}

	private void OnWindowResized(GraphicsCore _graphicsCore, Vector2 _previousSize, Vector2 _newSize)
	{
		if (!IsDisposed && WindowResized is not null)
		{
			WindowResized.Invoke(_graphicsCore, _previousSize, _newSize);
		}
	}

	#endregion
}
