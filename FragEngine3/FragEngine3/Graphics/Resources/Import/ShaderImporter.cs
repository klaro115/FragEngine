using FragAssetFormats.Shaders;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Hub instance for managing and delegating the import of shader programs through previously registered importers.
/// </summary>
/// <param name="_graphicsCore">The graphics core through which's graphics device the shaders shall be created and executed.</param>
public sealed class ShaderImporter(ResourceManager _resourceManager, GraphicsCore _graphicsCore) : BaseResourceImporter<IShaderImporter>(_graphicsCore)
{
	#region Constructors

	~ShaderImporter()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Fields

	private readonly ResourceManager resourceManager = _resourceManager;

	#endregion
	#region Methods

	public bool ImportShaderData(ResourceHandle _handle, out ShaderData? _outShaderData)
	{
		//TODO

		_outShaderData = null;  //TEMP
		return false;
	}

	public bool CreateShader(string _resourceKey, ShaderData _shaderData, out ShaderResource? _outShaderResource)
	{
		//TODO

		_outShaderResource = null;	//TEMP
		return false;
	}

	#endregion
}
