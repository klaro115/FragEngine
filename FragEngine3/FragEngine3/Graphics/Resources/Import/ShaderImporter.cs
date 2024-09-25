using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Graphics.Resources.Import.ShaderFormats;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Utility class for importing and creating shader resources.
/// </summary>
public static class ShaderImporter
{
	#region Methods

	/// <summary>
	/// Imports shader data from a resource file.
	/// </summary>
	/// <param name="_resourceManager">The engine's resource manager, through which we want to load the resource data file.</param>
	/// <param name="_handle">A resource handle identifying the shader resource we wish to import.</param>
	/// <param name="_outShaderData">Outputs a shader data instance describing the shader resource, and containing source code and
	/// pre-compiled shader programs.</param>
	/// <returns>True if shader data could be generated or read for the given resource handle, or false, if the import failed.</returns>
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

	/// <summary>
	/// Creates a shader resource from shader data for a given resource handle.
	/// </summary>
	/// <param name="_resourceKey">The identifier key of the resource we're creating this shader for.</param>
	/// <param name="_graphicsCore">The graphics core using which we want to upload and bind the resulting shader programs.</param>
	/// <param name="_shaderData">A shader data object describing the shader resource, and providing source code or pre-compiled shader
	/// programs for it.</param>
	/// <param name="_outShaderRes">Outputs a new shader resource that is ready for use by the rendering pipeline.</param>
	/// <returns>True if a shader resource could be created successfully, false if creating has failed.</returns>
	public static bool CreateShader(string _resourceKey, GraphicsCore _graphicsCore, ShaderData _shaderData, out ShaderResource? _outShaderRes)
	{
		// Check input parameters:
		if (_graphicsCore is null || _graphicsCore.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot create shader resource using null or disposed graphics core!");
			_outShaderRes = null;
			return false;
		}
		Logger? logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance;

		if (string.IsNullOrEmpty(_resourceKey))
		{
			logger?.LogError("Cannot create shader resource for null or empty resource key!");
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

		// Prepare lookup table for all supported variants:
		bool hasPrecompiledVariants = GatherPrecompiledVariants(
			_shaderData,
			variantBytesDict,
			_graphicsCore.CompiledShaderDataType);

		ShaderStages stage = _shaderData.Description.ShaderStage;
		Dictionary<MeshVertexDataFlags, Shader> variants = [];
		uint maxVariantIndex = 0;

		// Compile or upload any pre-compiled shader programs immediately:
		if (hasPrecompiledVariants)
		{
			foreach (var kvp in variantBytesDict)
			{
				try
				{
					ShaderDescription desc = new(stage, kvp.Value, string.Empty);
					Shader variant = _graphicsCore.MainFactory.CreateShader(ref desc);

					variants.Add(kvp.Key, variant);

					uint variantIndex = kvp.Key.GetVariantIndex();
					maxVariantIndex = Math.Max(variantIndex, maxVariantIndex);
				}
				catch (Exception ex)
				{
					logger?.LogException($"Failed to load pre-compiled shader variant '{kvp.Key}' for shader resource '{_resourceKey}'!", ex);
					continue;
				}
			}
		}

		byte[]? sanitizedSourceCodeBytes = null;

		if (_shaderData.HasSourceCode())
		{
			ShaderConfig shaderConfig = ShaderConfig.ConfigWhiteLit;

			if (ShaderConfig.TryParseDescriptionTxt(_shaderData.Description.SourceCode!.MaximumCompiledFeaturesTxt, out shaderConfig))
			{
				uint supportedVariantIndex = shaderConfig.GetVertexDataForVariantFlags().GetVariantIndex();
				maxVariantIndex = Math.Max(supportedVariantIndex, maxVariantIndex);
			}

			// Remove all vertex variants flags and feature defines from source code, so we can quickly prepend them later:
			if (_shaderData.SourceCode!.TryGetValue(_graphicsCore.DefaultShaderLanguage, out sanitizedSourceCodeBytes))
			{
				if (ShaderSourceCodeDefiner.SetVariantDefines(sanitizedSourceCodeBytes!, 0, true, out var buffer))
				{
					sanitizedSourceCodeBytes = new byte[buffer!.Length];
					Array.Copy(buffer.Utf8ByteBuffer, 0, sanitizedSourceCodeBytes, 0, buffer.Length);
					buffer!.ReleaseBuffer();
				}
				if (ShaderSourceCodeDefiner.SetFeatureDefines(sanitizedSourceCodeBytes!, shaderConfig.GetFeatureDefineStrings(false), true, out buffer))
				{
					sanitizedSourceCodeBytes = new byte[buffer!.Length];
					Array.Copy(buffer.Utf8ByteBuffer, 0, sanitizedSourceCodeBytes, 0, buffer.Length);
					buffer!.ReleaseBuffer();
				}
			}
		}

		uint maxVariantCount = Math.Max(maxVariantIndex + 1, 1);
		Shader?[] variantLookupTable = new Shader?[maxVariantCount];
		foreach (var kvp in variants)
		{
			uint variantIndex = kvp.Key.GetVariantIndex();
			variantLookupTable[variantIndex] = kvp.Value;
		}

		// Try creating the shader resource:
		_outShaderRes = new(
			_resourceKey,
			_graphicsCore,
			stage,
			variantLookupTable,
			_shaderData.Description.SourceCode,
			sanitizedSourceCodeBytes);
		return true;
	}

	private static bool GatherPrecompiledVariants(
		ShaderData _shaderData,
		Dictionary<MeshVertexDataFlags, byte[]> _dstVariantBytesDict,
		CompiledShaderDataType _compiledDataType)
	{
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

			// Gather variants' pre-compiled byte data:
			byte[] variantBytes = new byte[compiledVariant.ByteSize];
			Array.Copy(byteCode, compiledVariant.ByteOffset, variantBytes, 0, variantBytes.Length);
			_dstVariantBytesDict.Add(compiledVariant.VariantFlags, variantBytes);
		}
		
		return true;
	}

#endregion
}
