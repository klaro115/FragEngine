using FragEngine3.EngineCore;
using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Internal;
using System.Diagnostics;
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;

namespace FragEngine3.Graphics.D3D12
{
	internal sealed class Dx12GraphicsCore(GraphicsSystem _graphicsSystem, EngineConfig _config) : GraphicsCore(_graphicsSystem, _config)
	{
		#region Fields

		private static readonly GraphicsCapabilities capabilities = new();

		#endregion
		#region Properties

		private Logger Logger => graphicsSystem.engine.Logger ?? Logger.Instance!;

		#endregion
		#region Methods

		public override bool Initialize()
		{
			if (IsDisposed)
			{
				Logger.LogError("Cannot initialize disposed D3D graphics devices!");
				return false;
			}
			if (IsInitialized)
			{
				Logger.LogError("D3D graphics devices are already initialized!");
				return true;
			}

			Stopwatch stopwatch = new();
			stopwatch.Start();

			Console.Write("# Initializing D3D graphics device... ");

			try
			{
				// DEFINE WINDOW:

				WindowStyle windowStyle = config.Graphics.WindowStyle;
				int width = 1280;
				int height = 720;
				string windowTitle = config.MainWindowTitle ?? config.ApplicationName ?? string.Empty;

				WindowCreateInfo windowCreateInfo = new(
					0, 0,
					width, height,
					windowStyle.GetVeldridWindowState(),
					windowTitle);

				// DEFINE GRAPHICS DEVICE:

				capabilities.GetBestOutputBitDepth(config.Graphics.OutputBitDepth, out int outputBitDepth);
				bool vsync = graphicsSystem.Settings.Vsync;
				bool useSrgb = config.Graphics.OutputIsSRGB;
				DefaultColorTargetPixelFormat = GetOutputPixelFormat(outputBitDepth, useSrgb);
				DefaultDepthTargetPixelFormat = GetOutputDepthFormat(outputBitDepth);

				GraphicsDeviceOptions deviceOptions = new(
					false,
					DefaultDepthTargetPixelFormat,
					vsync,
					ResourceBindingModel.Improved,
					true,
					true,
					useSrgb);

				// CREATE GRAPHICS DEVICE & WINDOW:

				VeldridStartup.CreateWindowAndGraphicsDevice(windowCreateInfo, deviceOptions, GraphicsBackend.Direct3D11, out Sdl2Window window, out GraphicsDevice device);
				Device = device;
				Window = window;
				Window.Closing += () => quitMessageReceived = true;

				Device.WaitForIdle();
				Device.SwapBuffers();

				// MAIN RESOURCES:

				MainFactory = Device.ResourceFactory;
				MainCommandList = Device.ResourceFactory.CreateCommandList();

				Console.WriteLine("done.");
				Logger.LogMessage("# Initializing D3D graphics device... done.", true);
			}
			catch (Exception ex)
			{
				Console.WriteLine("FAIL.");
				Logger.LogMessage("# Initializing D3D graphics device... FAIL.", true);
				Logger.LogException("Failed to create system default D3D graphics device!", ex);
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

				// Log D3D specific information:
				if (Device.GetD3D11Info(out BackendInfoD3D11 d3dInfo))
				{
					Logger.LogMessage("+ D3D device details:");
					Logger.LogMessage($"  - PCI ID: {d3dInfo.DeviceId}");
				}
				else
				{
					Logger.LogError("Could not query D3D device details!");
				}
			}

			isInitialized = Device != null && Window != null;
			if (isInitialized)
			{
				Logger.LogMessage($"# Finished initializing D3D graphics device. ({stopwatch.ElapsedMilliseconds} ms)\n");
			}

			stopwatch.Stop();

			quitMessageReceived = false;
			return isInitialized;
		}

		private static PixelFormat GetOutputDepthFormat(int _bitDepth)
		{
			GraphicsCapabilities.DepthStencilFormat format = capabilities.depthStencilFormats.MinBy(o => Math.Abs(o.depthMapDepth - _bitDepth));

			return format.depthMapDepth switch
			{
				24 => PixelFormat.D24_UNorm_S8_UInt,
				32 => PixelFormat.D32_Float_S8_UInt,
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
					8 => PixelFormat.R8_G8_B8_A8_UNorm,
					10 => PixelFormat.R10_G10_B10_A2_UNorm,
					16 => PixelFormat.R16_G16_B16_A16_UNorm,
					32 => PixelFormat.R32_G32_B32_A32_Float,
					_ => PixelFormat.B8_G8_R8_A8_UNorm,
				};
			}
		}

		public override GraphicsCapabilities GetCapabilities() => capabilities;

		#endregion
	}
}
