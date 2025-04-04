using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Config;
using FragEngine3.Graphics.Internal;
using System.Diagnostics;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using Veldrid;
using FragEngine3.Graphics.Resources.Shaders;

namespace FragEngine3.Graphics.Vulkan;

public sealed class VulkanGraphicsCore(GraphicsSystem _graphicsSystem, EngineConfig _config) : GraphicsCore(_graphicsSystem, _config)
{
	#region Constructors

	#endregion
	#region Fields

	private static readonly GraphicsCapabilities capabilities = new();

	#endregion
	#region Properties

	public override EnginePlatformFlag ApiPlatformFlag => EnginePlatformFlag.GraphicsAPI_Vulkan;
	public override bool DefaultMirrorY => false;
	public override ShaderLanguage DefaultShaderLanguage => ShaderLanguage.SPIRV;
	public override CompiledShaderDataType CompiledShaderDataType => CompiledShaderDataType.SPIRV;

	#endregion
	#region Methods

	public override bool Initialize()
	{
		if (IsDisposed)
		{
			logger.LogError("Cannot initialize disposed Vulkan graphics devices!");
			return false;
		}
		if (IsInitialized)
		{
			logger.LogError("Vulkan graphics devices are already initialized!");
			return true;
		}

		Stopwatch stopwatch = new();
		stopwatch.Start();

		Console.Write("# Initializing Vulkan graphics device... ");

		try
		{
			GraphicsSettings settings = graphicsSystem.Settings;

			// DEFINE WINDOW:

			int errorCode = Sdl2Native.SDL_Init(SDLInitFlags.Video);
			if (errorCode != 0)
			{
				logger.LogSdl2Error(errorCode, EngineCore.Logging.LogEntrySeverity.Major);
			}

			Rectangle displayRect = new(0, 0, 1920, 1080);
			unsafe
			{
				errorCode = Sdl2Native.SDL_GetDisplayBounds(config.Graphics.DisplayIndex, &displayRect);
				if (errorCode != 0)
				{
					logger.LogSdl2Error(errorCode);
				}
			}

			int posX = 0;
			int posY = 0;
			int width = Math.Min((int)settings.Resolution.X, displayRect.Width);
			int height = Math.Min((int)settings.Resolution.Y, displayRect.Height);
			if (config.Graphics.CenterWindowOnScreen)
			{
				posX = displayRect.Width / 2 - width / 2;
				posY = displayRect.Height / 2 - height / 2;
			}
			posX += displayRect.X;
			posY += displayRect.Y;

			WindowStyle windowStyle = config.Graphics.WindowStyle;
			string windowTitle = config.MainWindowTitle ?? config.ApplicationName ?? string.Empty;

			WindowCreateInfo windowCreateInfo = new(
				posX, posY,
				width, height,
				windowStyle.GetVeldridWindowState(),
				windowTitle);

			// DEFINE GRAPHICS DEVICE:

			capabilities.GetBestOutputBitDepth(config.Graphics.OutputBitDepth, out int outputBitDepth);
			bool vsync = settings.Vsync;
			bool useSrgb = config.Graphics.OutputIsSRGB;
			DefaultColorTargetPixelFormat = GetOutputPixelFormat(outputBitDepth, useSrgb);
			DefaultDepthTargetPixelFormat = GetOutputDepthFormat(outputBitDepth, false);

			GraphicsDeviceOptions deviceOptions = new(
				false,
				DefaultDepthTargetPixelFormat,
				vsync,
				ResourceBindingModel.Default,
				true,
				true,
				useSrgb);

			// CREATE GRAPHICS DEVICE & WINDOW:

			VeldridStartup.CreateWindowAndGraphicsDevice(windowCreateInfo, deviceOptions, GraphicsBackend.Vulkan, out Sdl2Window window, out GraphicsDevice device);
			Device = device;
			Window = window;
			Window.Closing += () => quitMessageReceived = true;

			Device.WaitForIdle();
			Device.SwapBuffers();

			// MAIN RESOURCES:

			MainFactory = Device.ResourceFactory;
			MainCommandList = Device.ResourceFactory.CreateCommandList();
			MainCommandList.Name = "CmdList_Main";
			Device.MainSwapchain.Name = "Swapchain_Main";

			Console.WriteLine("done.");
			logger.LogMessage("# Initializing Vulkan graphics device... done.", true);
		}
		catch (Exception ex)
		{
			Console.WriteLine("FAIL.");
			logger.LogMessage("# Initializing Vulkan graphics device... FAIL.", true);
			logger.LogException("Failed to create system default Vulkan graphics device!", ex);
			Shutdown();
			stopwatch.Stop();
			return false;
		}

		if (Device != null)
		{
			// Log general GPU information:
			logger.LogMessage("+ Graphics device details:");
			logger.LogMessage($"  - Name: {Device.DeviceName}");
			logger.LogMessage($"  - Vendor: {Device.VendorName}");
			logger.LogMessage($"  - Backend: {Device.BackendType}");
			logger.LogMessage($"  - API version: {Device.ApiVersion}");

			// Log GPU features:
			GraphicsDeviceFeatures features = Device.Features;
			logger.LogMessage("+ Graphics device features:");
			logger.LogMessage($"  - Compute shader: {features.ComputeShader}");
			logger.LogMessage($"  - Geometry shader: {features.GeometryShader}");
			logger.LogMessage($"  - Tesselation: {features.TessellationShaders}");
			logger.LogMessage($"  - Structured buffers: {features.StructuredBuffer}");
			logger.LogMessage($"  - 1D textures: {features.Texture1D}");
			logger.LogMessage($"  - Float64 in shaders: {features.ShaderFloat64}");
			{
				capabilities.computeShaders = features.ComputeShader;
				capabilities.geometryShaders = features.GeometryShader;
				capabilities.tesselationShaders = features.TessellationShaders;
				capabilities.textures1D = features.Texture1D;
			}

			// Log Vulkan specific information:
			if (Device.GetVulkanInfo(out BackendInfoVulkan vkInfo))
			{
				logger.LogMessage("+ Vulkan device details:");
				logger.LogMessage($"  - Driver name: {vkInfo.DriverName}");
				logger.LogMessage($"  - Driver info: {vkInfo.DriverInfo}");
				logger.LogMessage($"  - Queue index: {vkInfo.GraphicsQueueFamilyIndex}");

				/* // Commented out because this spams too much stuff into the console and logs.
				var deviceExts = vkInfo.AvailableDeviceExtensions;
				if (deviceExts is not null && deviceExts.Count != 0)
				{
					logger.LogMessage("  - Device extensions:");
					foreach (BackendInfoVulkan.ExtensionProperties extension in deviceExts)
					{
						logger.LogMessage($"    * {extension.Name}: v{extension.SpecVersion}");
					}
				}
				*/
				var instanceExts = vkInfo.AvailableInstanceExtensions;
				if (instanceExts is not null && instanceExts.Count != 0)
				{
					logger.LogMessage("  - Instance extensions:");
					foreach (string extension in instanceExts)
					{
						logger.LogMessage($"    * {extension}");
					}
				}
				var instanceLayers = vkInfo.AvailableInstanceLayers;
				if (instanceLayers is not null && instanceLayers.Count != 0)
				{
					logger.LogMessage("  - Instance layers:");
					foreach (string layer in instanceLayers)
					{
						logger.LogMessage($"    * {layer}");
					}
				}
			}
			else
			{
				logger.LogError("Could not query Vulkan device details!");
			}
		}

		SamplerManager = new(this);

		stopwatch.Stop();

		isInitialized = Device is not null && Window is not null;
		if (isInitialized)
		{
			logger.LogMessage($"# Finished initializing Vulkan graphics device. ({stopwatch.ElapsedMilliseconds} ms)\n");

			Window!.Closing += OnWindowClosing;
			Window.Resized += OnWindowResized;
		}

		quitMessageReceived = false;
		return isInitialized;
	}

	private static PixelFormat GetOutputDepthFormat(int _bitDepth, bool _addStencil)
	{
		GraphicsCapabilities.DepthStencilFormat format = capabilities.depthStencilFormats.MinBy(o => Math.Abs(o.depthMapDepth - _bitDepth));

		return format.depthMapDepth switch
		{
			16 => PixelFormat.R16_UNorm,
			24 => PixelFormat.D24_UNorm_S8_UInt,
			32 => _addStencil
				? PixelFormat.D32_Float_S8_UInt
				: PixelFormat.R32_Float,
			_ => PixelFormat.D24_UNorm_S8_UInt,
		};
	}

	private static PixelFormat GetOutputPixelFormat(int _bitDepth, bool _useSrgb)
	{
		if (_useSrgb)
		{
			return _bitDepth switch
			{
				8 => PixelFormat.B8_G8_R8_A8_UNorm_SRgb,
				_ => PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
			};
		}
		else
		{
			return _bitDepth switch
			{
				8 => PixelFormat.B8_G8_R8_A8_UNorm,
				10 => PixelFormat.R10_G10_B10_A2_UNorm,
				16 => PixelFormat.R16_G16_B16_A16_UNorm,
				32 => PixelFormat.R32_G32_B32_A32_Float,
				_ => PixelFormat.R8_G8_B8_A8_UNorm,
			};
		}
	}

	public override GraphicsCapabilities GetCapabilities() => capabilities;

	#endregion
}
