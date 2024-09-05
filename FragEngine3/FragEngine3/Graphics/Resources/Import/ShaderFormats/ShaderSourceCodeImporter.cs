using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats;

internal static class ShaderSourceCodeImporter
{
	#region Methods

	public static bool ImportShaderData(Stream _stream, ResourceHandle _resHandle, string _fileExtension, out ShaderData? _outShaderData)
	{
		//TODO [important]: Wrap source code in ShaderData descriptor, while assuming all standard entry points and such. Use import flags for feature defines.

		throw new NotImplementedException("Shader import from source code file is not supported at this time.");
	}

	#endregion
}
