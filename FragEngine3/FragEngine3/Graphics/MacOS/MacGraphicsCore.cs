using FragEngine3.EngineCore.Config;
using FragEngine3.Graphics.Internal;
using Veldrid;
using Veldrid.MetalBindings;
using Veldrid.StartupUtilities;

namespace FragEngine3.Graphics.MacOS
{
	internal sealed class MacGraphicsCore : GraphicsCore
	{
		#region Constructors

		public MacGraphicsCore(GraphicsSystem _graphicsSystem, EngineConfig _config) : base(_graphicsSystem, _config) { }

		#endregion
		#region Fields

		private static readonly GraphicsCapabilities capabilities = new();

		#endregion
		#region Methods

		public override bool Initialize()
		{
			if (IsDisposed)
			{
				Console.WriteLine("Error! Cannot initialize disposed metal graphics devices!");
				return false;
			}
			if (IsInitialized)
			{
				Console.WriteLine("Error! Metal graphics devices are already initialized!");
				return true;
			}

			Console.Write("# Initializing Metal graphics device... ");

			try
			{
				// CREATE WINDOW:

				WindowStyle windowStyle = config.Graphics.WindowStyle;
				int width = 640;
				int height = 480;
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
				bool vsync = graphicsSystem.Settings.Vsync;
				bool useSrgb = config.Graphics.OutputIsSRGB;
				PixelFormat outputPixelFormat = GetOutputPixelFormat(outputBitDepth, useSrgb);

				GraphicsDeviceOptions deviceOptions = new(
					false,
					outputPixelFormat,
					vsync,
					ResourceBindingModel.Improved,
					false,
					true,
					useSrgb);

				Device = VeldridStartup.CreateGraphicsDevice(Window, deviceOptions, GraphicsBackend.Metal);
				Device.WaitForIdle();
				Device.SwapBuffers();

				// MAIN RESOURCES:

				MainFactory = Device.ResourceFactory;
				MainCommandList = Device.ResourceFactory.CreateCommandList();

				Console.WriteLine("done.");
			}
			catch (Exception ex)
			{
				Console.WriteLine("FAIL.");
				Console.WriteLine($"Error! Failed to create system default metal graphics device!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				Shutdown();
				return false;
			}

			if (Device != null)
			{
				// Log general GPU information:
				Console.WriteLine("+ Graphics device details:");
				Console.WriteLine($"  - Name: {Device.DeviceName}");
				Console.WriteLine($"  - Vendor: {Device.VendorName}");
				Console.WriteLine($"  - Backend: {Device.BackendType}");
				Console.WriteLine($"  - API version: {Device.ApiVersion}");

				// Log GPU features:
				GraphicsDeviceFeatures features = Device.Features;
				Console.WriteLine("+ Graphics device features:");
				Console.WriteLine($"  - Compute shader: {features.ComputeShader}");
				Console.WriteLine($"  - Geometry shader: {features.GeometryShader}");
				Console.WriteLine($"  - Tesselation: {features.TessellationShaders}");
				Console.WriteLine($"  - Structured buffers: {features.StructuredBuffer}");
				Console.WriteLine($"  - 1D textures: {features.Texture1D}");
				Console.WriteLine($"  - Float64 in shaders: {features.ShaderFloat64}");
				{
					capabilities.computeShaders = features.ComputeShader;
					capabilities.geometryShaders = features.GeometryShader;
					capabilities.tesselationShaders = features.TessellationShaders;
					capabilities.textures1D = features.Texture1D;
				}

				// Log Metal specific information:
				if (Device.GetMetalInfo(out BackendInfoMetal mtlInfo))
				{
					Console.WriteLine("+ Metal feature sets:");
					foreach (MTLFeatureSet featureSet in mtlInfo.FeatureSet)
					{
						Console.WriteLine($"  - {featureSet}");
					}
				}
				else
				{
					Console.WriteLine("Error! Could not query Metal feature sets!");
				}
			}

			isInitialized = Device != null;
			quitMessageReceived = false;
			return isInitialized;
		}

		private static PixelFormat GetOutputPixelFormat(int _bitDepth, bool _useSrgb)
		{
			if (_useSrgb)
			{
				return _bitDepth switch
				{
					8 => PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
					_ => PixelFormat.B8_G8_R8_A8_UNorm,
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

