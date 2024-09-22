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
					if (!ShaderSourceCodeImporter.ImportShaderData(stream, _handle, fileHandle, formatExt, out _outShaderData))
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

		return true;
	}

	public static bool CreateShader(ResourceHandle _handle, GraphicsCore _graphicsCore, ShaderData _shaderData, out ShaderResource? _outShaderRes)
	{
		// Check input parameters:
		if (_graphicsCore is null || _graphicsCore.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot create shader resource using null or disposed graphics core!");
			_outShaderRes = null;
			return false;
		}
		Logger? logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance;

		if (_handle is null || !_handle.IsValid)
		{
			logger?.LogError("Cannot create shader resource for null or invalid resource handle!");
			_outShaderRes = null;
			return false;
		}
		if (_shaderData is null || !_shaderData.IsValid())
		{
			logger?.LogError("Cannot create shader resource data using null or invalid shader data!");
			_outShaderRes = null;
			return false;
		}

		Dictionary<MeshVertexDataFlags, byte[]> variantBytesDict = [];

		// Prepare lookup array for all supported variants:
		bool hasPrecompiledVariants = GatherPrecompiledVariants(
			_shaderData,
			variantBytesDict,
			_graphicsCore.CompiledShaderDataType,
			out _,
			out _);

		ShaderStages stage = _shaderData.Description.ShaderStage;
		Dictionary<MeshVertexDataFlags, Shader> variants = [];

		if (hasPrecompiledVariants)
		{
			foreach (var kvp in variantBytesDict)
			{
				try
				{
					ShaderDescription desc = new(stage, kvp.Value, string.Empty);
					Shader variant = _graphicsCore.MainFactory.CreateShader(ref desc);

					variants.Add(kvp.Key, variant);
				}
				catch (Exception ex)
				{
					logger?.LogException($"Failed to load pre-compiled shader variant '{kvp.Key}' for shader resource '{_handle.resourceKey}'!", ex);
					continue;
				}
			}
		}

		_outShaderRes = new ShaderResource(
			_handle.resourceKey,
			_graphicsCore,
			variants,
			_shaderData,
			stage);
		return true;
	}

	private static bool GatherPrecompiledVariants(
		ShaderData _shaderData,
		Dictionary<MeshVertexDataFlags, byte[]> _dstVariantBytesDict,
		CompiledShaderDataType _compiledDataType,
		out MeshVertexDataFlags _outPrecompiledVertexFlags,
		out int _outMaxVariantIndex)
	{
		_outPrecompiledVertexFlags = 0;
		_outMaxVariantIndex = -1;

		if (_shaderData.Description.CompiledVariants is null ||
			_shaderData.Description.CompiledVariants.Length == 0)
		{
			return false;
		}

		byte[]? byteCode = _shaderData.GetByteCodeOfType(_compiledDataType);
		if (byteCode is null)
		{
			return false;
		}

		MeshVertexDataFlags ignoredVariantFlags = ~(_shaderData.Description.RequiredVariants | MeshVertexDataFlags.BasicSurfaceData);

		foreach (ShaderDescriptionVariantData compiledVariant in _shaderData.Description.CompiledVariants)
		{
			// Skip unsupported types and unnecessary variants:
			if (!_compiledDataType.HasFlag(compiledVariant.Type)) continue;
			if ((compiledVariant.VariantFlags & ignoredVariantFlags) != 0) continue;

			// Update variant flags:
			_outPrecompiledVertexFlags |= compiledVariant.VariantFlags;
			_outMaxVariantIndex = Math.Max(_outMaxVariantIndex, (int)compiledVariant.VariantFlags.GetVariantIndex());

			// Gather variants' pre-compiled byte data:
			byte[] variantBytes = new byte[compiledVariant.ByteSize];
			Array.Copy(byteCode, compiledVariant.ByteOffset, variantBytes, 0, variantBytes.Length);
			_dstVariantBytesDict.Add(compiledVariant.VariantFlags, variantBytes);
		}
		
		return true;
	}

#endregion
}
