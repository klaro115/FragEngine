using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public sealed class ShaderResource : Resource
	{
		#region Constructors

		private ShaderResource(ResourceHandle _handle, GraphicsCore _graphicsCore) : base(_handle)
		{
			graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Material's graphics core may not be null!");
		}

		#endregion
		#region Properties

		public readonly GraphicsCore graphicsCore;

		public Shader Shader { get; private set; } = null!;
		public ShaderStages Stage { get; private set; } = ShaderStages.None;

		public override ResourceType ResourceType => ResourceType.Shader;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsDisposed = false;

			Shader?.Dispose();
			if (_disposing)
			{
				Shader = null!;
				Stage = ShaderStages.None;
			}
		}

		public override IEnumerator<ResourceHandle> GetResourceDependencies()
		{
			if (!IsDisposed && GetResourceHandle(out ResourceHandle handle))
			{
				yield return handle;
			}
		}

		public static bool CreateShader(
			ResourceHandle _handle,
			GraphicsCore _graphicsCore,
			out ShaderResource? _outShaderRes)
		{
			// Verify parameters and system states:
			if (_handle == null || !_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot create shader resource from null or invalid resource handle!");
				_outShaderRes = null;
				return false;
			}

			// Try to extrapolate the shader stage from the resource key's suffix: (ex.: Vertex = '_VS')
			ShaderStages stage = ShaderStages.None;
			foreach (var kvp in GraphicsContants.shaderResourceSuffixes)
			{
				if (_handle.Key.EndsWith(kvp.Value))
				{
					stage = kvp.Key;
					break;
				}
			}

			// Create the shader using the standard method:
			return CreateShader(_handle, _graphicsCore, stage, out _outShaderRes);
		}

		public static bool CreateShader(
			ResourceHandle _handle,
			GraphicsCore _graphicsCore,
			ShaderStages _stage,
			out ShaderResource? _outShaderRes)
		{
			// Determine standard entry point function name based on shader stage:
			if (!GraphicsContants.defaultShaderStageEntryPoints.TryGetValue(_stage, out string? entryPoint))
			{
				Console.WriteLine($"Error! Could not determine entry point name for shader stage '{_stage}'!");
				_outShaderRes = null;
				return false;
			}

			// Create the shader using the standard method:
			return CreateShader(_handle, _graphicsCore, _stage, entryPoint, out _outShaderRes);
		}

		public static bool CreateShader(
			ResourceHandle _handle,
			GraphicsCore _graphicsCore,
			ShaderStages _stage,
			string _entryPoint,
			out ShaderResource? _outShaderRes)
		{
			// Verify parameters and system states:
			if (_handle == null || !_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot create shader resource from null or invalid resource handle!");
				_outShaderRes = null;
				return false;
			}
			if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
			{
				Console.WriteLine("Error! Cannot create shader resource using null or disposed resource manager!");
				_outShaderRes = null;
				return false;
			}
			if (_graphicsCore == null || !_graphicsCore.IsInitialized)
			{
				Console.WriteLine("Error! Cannot create shader resource using null or uninitialized graphics core!");
				_outShaderRes = null;
				return false;
			}
			if (_stage == ShaderStages.None)
			{
				Console.WriteLine("Error! Cannot creste shader resource for unknown stage!");
				_outShaderRes = null;
				return false;
			}

			// Don't do anything if the resource has already been loaded:
			if (_handle.IsLoaded)
			{
				_outShaderRes = _handle.GetResource(false, false) as ShaderResource;
				return true;
			}

			// Retrieve the file that this resource is loaded from:
			if (!_handle.resourceManager.GetFileWithResource(_handle.Key, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				Console.WriteLine($"Error! Could not find source file for resource handle '{_handle}'!");
				_outShaderRes = null;
				return false;
			}

			// Try reading raw byte data from file:
			if (!fileHandle.TryReadResourceBytes(_handle, out byte[] bytes))
			{
				Console.WriteLine($"Error! Failed to read shader code for resource '{_handle}'!");
				_outShaderRes = null;
				return false;
			}

			// Try compiling shader:
			Shader shader;
			try
			{
				ShaderDescription shaderDesc = new(_stage, bytes, _entryPoint);

				shader = _graphicsCore.MainFactory.CreateShader(shaderDesc);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to compile shader '{_handle.Key}' ({_stage})!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outShaderRes = null;
				return false;
			}

			// Output finished shader resource:
			_outShaderRes = new(_handle, _graphicsCore)
			{
				Shader = shader,
				Stage = _stage,
			};
			return _outShaderRes.IsLoaded;
		}

		#endregion
	}
}
