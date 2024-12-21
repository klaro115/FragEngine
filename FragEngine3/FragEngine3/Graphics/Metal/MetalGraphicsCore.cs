using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Config;
using FragEngine3.Graphics.Internal;
using FragEngine3.Graphics.Resources;
using System.Diagnostics;
using Veldrid;
using Veldrid.MetalBindings;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace FragEngine3.Graphics.Metal;

internal sealed class MetalGraphicsCore(GraphicsSystem _graphicsSystem, EngineConfig _config) : GraphicsCore(_graphicsSystem, _config)
{
	#region Fields

	private static readonly GraphicsCapabilities capabilities = new(
		[ new GraphicsCapabilities.DepthStencilFormat(32, 8) ]);

	#endregion
	#region Properties

	public override EnginePlatformFlag ApiPlatformFlag => EnginePlatformFlag.GraphicsAPI_Metal;
	public override bool DefaultMirrorY => true;
	public override ShaderLanguage DefaultShaderLanguage => ShaderLanguage.Metal;
	public override CompiledShaderDataType CompiledShaderDataType => CompiledShaderDataType.MetalArchive;

	#endregion
	#region Methods

	public override bool Initialize()
	{
		if (IsDisposed)
		{
			Logger.LogError("Cannot initialize disposed metal graphics devices!");
			return false;
		}
		if (IsInitialized)
		{
			Logger.LogError("Metal graphics devices are already initialized!");
			return true;
		}

		Stopwatch stopwatch = new();
		stopwatch.Start();

		Console.Write("# Initializing Metal graphics device... ");

		try
		{
			GraphicsSettings settings = graphicsSystem.Settings;

			// CREATE WINDOW:

			Rectangle displayRect = new(0, 0, 1920, 1080);
			unsafe
			{
				Sdl2Native.SDL_GetDisplayBounds(config.Graphics.DisplayIndex, &displayRect);
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
				0, 0,
				width, height,
				windowStyle.GetVeldridWindowState(),
				windowTitle);

			Window = VeldridStartup.CreateWindow(ref windowCreateInfo);
			Window.Closing += () => quitMessageReceived = true;

			// CREATE GRAPHICS DEVICE:

			capabilities.GetBestOutputBitDepth(config.Graphics.OutputBitDepth, out int outputBitDepth);
			bool vsync = settings.Vsync;
			bool useSrgb = config.Graphics.OutputIsSRGB;
			DefaultColorTargetPixelFormat = GetOutputPixelFormat(outputBitDepth, useSrgb);
			DefaultDepthTargetPixelFormat = GetOutputDepthFormat(outputBitDepth, true);
			DefaultShadowMapDepthTargetFormat = PixelFormat.R32_Float;

			GraphicsDeviceOptions deviceOptions = new(
				false,
				DefaultDepthTargetPixelFormat,
				vsync,
				ResourceBindingModel.Default,
				true,
				true,
				useSrgb);

			Device = VeldridStartup.CreateGraphicsDevice(Window, deviceOptions, GraphicsBackend.Metal);
			Device.WaitForIdle();
			Device.SwapBuffers();

			// MAIN RESOURCES:

			MainFactory = Device.ResourceFactory;
			MainCommandList = Device.ResourceFactory.CreateCommandList();
			MainCommandList.Name = "CmdList_Main";
			Device.MainSwapchain.Name = "Swapchain_Main";

			Console.WriteLine("done.");
			Logger.LogMessage("# Initializing D3D graphics device... done.", true);
		}
		catch (Exception ex)
		{
			Console.WriteLine("FAIL.");
			Logger.LogMessage("# Initializing D3D graphics device... FAIL.", true);
			Logger.LogException("Failed to create system default metal graphics device!", ex);
			Shutdown();
			stopwatch.Stop();
			return false;
		}

		if (Device != null)
		{
			// Log general GPU information:
			Logger.LogMessage("+ Graphics device details:");
			Logger.LogMessage($"  - Name: {Device.DeviceName}");
			Logger.LogMessage($"  - Vendor: {Device.VendorName}");
			Logger.LogMessage($"  - Backend: {Device.BackendType}");
			Logger.LogMessage($"  - API version: {Device.ApiVersion}");

			// Log GPU features:
			GraphicsDeviceFeatures features = Device.Features;
			Logger.LogMessage("+ Graphics device features:");
			Logger.LogMessage($"  - Compute shader: {features.ComputeShader}");
			Logger.LogMessage($"  - Geometry shader: {features.GeometryShader}");
			Logger.LogMessage($"  - Tesselation: {features.TessellationShaders}");
			Logger.LogMessage($"  - Structured buffers: {features.StructuredBuffer}");
			Logger.LogMessage($"  - 1D textures: {features.Texture1D}");
			Logger.LogMessage($"  - Float64 in shaders: {features.ShaderFloat64}");
			{
				capabilities.computeShaders = features.ComputeShader;
				capabilities.geometryShaders = features.GeometryShader;
				capabilities.tesselationShaders = features.TessellationShaders;
				capabilities.textures1D = features.Texture1D;
			}

			// Log Metal specific information:
			if (Device.GetMetalInfo(out BackendInfoMetal mtlInfo))
			{
				Logger.LogMessage("+ Metal feature sets:");
				foreach (MTLFeatureSet featureSet in mtlInfo.FeatureSet)
				{
					Logger.LogMessage($"  - {featureSet}");
				}
			}
			else
			{
				Logger.LogError("Could not query Metal feature sets!");
			}
		}

		SamplerManager = new(this);

		stopwatch.Stop();

		isInitialized = Device != null && Window != null;
		if (isInitialized)
		{
			Logger.LogMessage($"# Finished initializing Metal graphics device. ({stopwatch.ElapsedMilliseconds} ms)\n");
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

