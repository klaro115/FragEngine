using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources;

namespace FragEngine3.Graphics.Resources.Import;

public static class ShaderImporter
{
	#region Methods

	public static bool ImportShaderData(ResourceManager _resourceManager, ResourceHandle _handle, out ShaderData? _outShaderData)
	{
		// Check input parameters:
		if (_handle is null || !_handle.IsValid)
		{
			Logger.Instance?.LogError("Cannot import shader data using null or invalid resource handle!");
			_outShaderData = null;
			return false;
		}
		if (_resourceManager is null || _resourceManager.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot import shader data using null or disposed resource manager!");
			_outShaderData = null;
			return false;
		}

		Logger logger = _handle.resourceManager.engine.Logger ?? Logger.Instance!;

		// Retrieve the file that this resource is loaded from:
		ResourceFileHandle fileHandle;
		if (string.IsNullOrEmpty(_handle.fileKey))
		{
			if (!_handle.resourceManager.GetFileWithResource(_handle.fileKey, out fileHandle))
			{
				logger.LogError($"Could not find any resource data file containing resource handle '{_handle}'!");
				_outShaderData = null;
				return false;
			}
		}
		else
		{
			if (!_handle.resourceManager.GetFile(_handle.fileKey, out fileHandle))
			{
				logger.LogError($"Resource data file for resource handle '{_handle}' does not exist!");
				_outShaderData = null;
				return false;
			}
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			if (!fileHandle.TryOpenDataStream(_resourceManager.engine, _handle.dataOffset, _handle.dataSize, out stream, out _))
			{
				logger.LogError($"Failed to open file stream for resource handle '{_handle}'!");
				_outShaderData = null;
				return false;
			}

			// Import from stream, identifying file format from extension:
			string formatExt = Path.GetExtension(fileHandle.dataFilePath).ToLowerInvariant();

			switch (formatExt)
			{
				// A) FSHA shader asset file format:
				case ".fsha":
					if (!ImportShaderDataFromFSHA(stream, _handle, fileHandle, out _outShaderData))
					{
						_outShaderData = null;
						return false;
					}
					break;
				// B) Full source code file:
				case ".hlsl":
				case ".glsl":
				case ".metal":
					if (!ImportShaderDataFromSourceCode(stream, _handle, formatExt, out _outShaderData))
					{
						_outShaderData = null;
						return false;
					}
					break;
				// C) Compressed or batched resource data file:
				default:
					if (!ImportShaderDataFromUnknownFileType(stream, _handle, fileHandle, out _outShaderData))
					{
						_outShaderData = null;
						return false;
					}
					break;
			}
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to import shader data for resource handle '{_handle}'!", ex);
			_outShaderData = null;
			return false;
		}
		finally
		{
			stream?.Close();
		}

		//TODO [later]: Apply import flags, if applicable.

		_outShaderData = null; //TEMP
		return true;
	}

	public static bool CreateShader(ResourceHandle _handle, GraphicsCore _graphicsCore, ShaderData _shaderData, out ShaderResource? _outShaderRes)
	{
		// Check input parameters:
		if (_handle is null || !_handle.IsValid)
		{
			Logger.Instance?.LogError("Cannot create shader resource for null or invalid resource handle!");
			_outShaderRes = null;
			return false;
		}
		if (_graphicsCore is null || _graphicsCore.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot create shader resource using null or disposed graphics core!");
			_outShaderRes = null;
			return false;
		}
		if (_shaderData is null || !_shaderData.IsValid())
		{
			Logger.Instance?.LogError("Cannot create shader resource data using null or invalid shader data!");
			_outShaderRes = null;
			return false;
		}

		//TODO [critical]: Compile program or extract pre-compiled variants from shader data, then create a shader resource around that. Basically rewrite shader factory.

		_outShaderRes = null;   //TEMP
		return true;
	}

	private static bool ImportShaderDataFromFSHA(Stream _stream, ResourceHandle _resHandle, ResourceFileHandle _fileHandle, out ShaderData? _outShaderData)
	{
		const EnginePlatformFlag vulkanPlatforms = EnginePlatformFlag.OS_Windows | EnginePlatformFlag.OS_Linux | EnginePlatformFlag.OS_FreeBSD;
		const EnginePlatformFlag d3dFlags = EnginePlatformFlag.OS_Windows | EnginePlatformFlag.GraphicsAPI_D3D;
		const EnginePlatformFlag metalFlags = EnginePlatformFlag.OS_MacOS | EnginePlatformFlag.GraphicsAPI_Metal;

		// Determine for which platform and graphics API we'll be compiling:
		EnginePlatformFlag platformFlags = _resHandle.resourceManager.engine.PlatformSystem.PlatformFlags;
		CompiledShaderDataType typeFlags = 0;

		if ((platformFlags & vulkanPlatforms) != 0 && platformFlags.HasFlag(EnginePlatformFlag.GraphicsAPI_Vulkan))
		{
			typeFlags |= CompiledShaderDataType.SPIRV;
		}
		else if (platformFlags.HasFlag(d3dFlags))
		{
			typeFlags |= CompiledShaderDataType.DXBC | CompiledShaderDataType.DXIL;
		}
		else if (platformFlags.HasFlag(metalFlags))
		{
			typeFlags |= CompiledShaderDataType.MetalArchive;
		}

		// Read the relevant shader data from stream:
		using BinaryReader reader = new(_stream);

		return ShaderData.Read(reader, out _outShaderData, typeFlags);
	}

	private static bool ImportShaderDataFromSourceCode(Stream _stream, ResourceHandle _resHandle, string _fileExtension, out ShaderData? _outShaderData)
	{
		//TODO [important]: Wrap source code in ShaderData descriptor, while assuming all standard entry points and such. Use import flags for feature defines.

		throw new NotImplementedException("Shader import from source code file is not supported at this time.");
	}

	private static bool ImportShaderDataFromUnknownFileType(Stream _stream, ResourceHandle _resHandle, ResourceFileHandle _fileHandle, out ShaderData? _outShaderData)
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
			return ImportShaderDataFromFSHA(_stream, _resHandle, _fileHandle, out _outShaderData);
		}

		//TODO 1 [later]: Check for other markers that might help to identify the shader.
		//TODO 2 [later]: If no markers found, assume platform/API-specific source code file and parse that way.

		logger?.LogError($"Cannot import shader data for unsupported resource format! Resource handle: '{_resHandle}'!");
		_outShaderData = null;
		return false;
	}

#endregion
}
