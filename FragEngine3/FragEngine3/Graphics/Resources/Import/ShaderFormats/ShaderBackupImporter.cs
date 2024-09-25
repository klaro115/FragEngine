using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats;

internal static class ShaderBackupImporter
{
	#region Methods

	public static bool ImportShaderData(Stream _stream, ResourceHandle _resHandle, ResourceFileHandle _fileHandle, out ShaderData? _outShaderData)
	{
		Logger logger = _resHandle.resourceManager.engine.Logger ?? Logger.Instance!;

		// Check magic numbers to see if it's an FSHA asset:
		byte[] fourCCBuffer = new byte[4];
		int bytesRead = _stream.Read(fourCCBuffer, 0, 4);

		if (bytesRead < 4)
		{
			logger?.LogError($"Cannot import shader data from stream that is empty or EOF! Resource handle: '{_resHandle}'!");
			_outShaderData = null;
			return false;
		}
		if (fourCCBuffer[0] == 'F' && fourCCBuffer[1] == 'S' && fourCCBuffer[2] == 'H' && fourCCBuffer[3] == 'A')
		{
			_stream.Position -= bytesRead;
			return ShaderFshaImporter.ImportShaderData(_stream, _resHandle, _fileHandle, out _outShaderData);
		}

		//TODO 1 [later]: Check for other markers that might help to identify the shader.
		//TODO 2 [later]: If no markers found, assume platform/API-specific source code file and parse that way.

		logger?.LogError($"Cannot import shader data for unsupported resource format! Resource handle: '{_resHandle}'!");
		_outShaderData = null;
		return false;
	}

	#endregion
}
