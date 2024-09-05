using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Graphics.Resources.Import.ShaderFormats;
using FragEngine3.Resources;
using Veldrid;

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
					if (!ShaderFshaImporter.ImportShaderData(stream, _handle, fileHandle, out _outShaderData))
					{
						_outShaderData = null;
						return false;
					}
					break;
				// B) Full source code file:
				case ".hlsl":
				case ".glsl":
				case ".metal":
					if (!ShaderSourceCodeImporter.ImportShaderData(stream, _handle, formatExt, out _outShaderData))
					{
						_outShaderData = null;
						return false;
					}
					break;
				// C) Compressed or batched resource data file:
				default:
					if (!ShaderBackupImporter.ImportShaderData(stream, _handle, fileHandle, out _outShaderData))
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

		EnginePlatformFlag platformFlags = _handle.resourceManager.engine.PlatformSystem.PlatformFlags;
		CompiledShaderDataType compiledDataType = ShaderDataUtility.GetCompiledDataTypeFlagsForPlatform(platformFlags);

		int maxVariantIndex = -1;
		MeshVertexDataFlags precompiledVertexFlags = 0;
		MeshVertexDataFlags ignoredVariantFlags = ~(_shaderData.Description.RequiredVariants | MeshVertexDataFlags.BasicSurfaceData);

		Dictionary<MeshVertexDataFlags, byte[]> variantBytesDict = [];

		// Prepare lookup array for all supported variants:
		byte[]? byteCode = _shaderData.GetByteCodeOfType(compiledDataType);

		if (byteCode is not null && _shaderData.Description.CompiledVariants.Length != 0)
		{
			foreach (ShaderDescriptionVariantData compiledVariant in _shaderData.Description.CompiledVariants)
			{
				// Skip unsupported types and unnecessary variants:
				if (compiledVariant.Type != compiledDataType) continue;
				if ((compiledVariant.VariantFlags & ignoredVariantFlags) != 0) continue;

				// Update variant flags:
				precompiledVertexFlags |= compiledVariant.VariantFlags;
				maxVariantIndex = Math.Max(maxVariantIndex, (int)compiledVariant.VariantFlags.GetVariantIndex());

				// Gather variants' pre-compiled byte data:
				byte[] variantBytes = new byte[compiledVariant.ByteSize];
				Array.Copy(byteCode, compiledVariant.ByteOffset, variantBytes, 0, variantBytes.Length);
				variantBytesDict.Add(compiledVariant.VariantFlags, variantBytes);
			}
		}

		// Compile remaining required variants from source code, if available:
		if (_shaderData.SourceCode is not null && _shaderData.Description.SourceCode is not null)
		{
			// Fetch source code for current platform and graphics API:
			ShaderLanguage sourceCodeLanguage = ShaderDataUtility.GetShaderLanguageForPlatform(platformFlags);
			byte[]? sourceCodeBytes = _shaderData.SourceCode?.GetValueOrDefault(sourceCodeLanguage);


		}

		//ShaderDescription shaderDesc = 

		//TODO [critical]: Compile program or extract pre-compiled variants from shader data, then create a shader resource around that. Basically rewrite shader factory.

		_outShaderRes = null;   //TEMP
		return true;
	}

#endregion
}
