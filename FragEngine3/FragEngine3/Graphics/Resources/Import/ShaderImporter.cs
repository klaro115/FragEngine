using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Graphics.Resources.Shaders.Internal;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Import;

/// <summary>
/// Hub instance for managing and delegating the import of shader programs through previously registered importers.
/// </summary>
/// <param name="_graphicsCore">The graphics core through which's graphics device the shaders shall be created and executed.</param>
public sealed class ShaderImporter(ResourceManager _resourceManager, GraphicsCore _graphicsCore) : BaseResourceImporter<IShaderImporter>(_resourceManager, _graphicsCore)
{
	#region Constructors

	~ShaderImporter()
	{
		if (!IsDisposed) Dispose(false);
	}

	#endregion
	#region Methods

	public bool ImportShaderData(ResourceHandle _handle, out ShaderData? _outShaderData)
	{
		if (IsDisposed)
		{
			logger.LogError($"Cannot import shader data using disposed {nameof(ShaderImporter)}!");
			_outShaderData = null;
			return false;
		}

		if (!TryGetResourceFile(_handle, out ResourceFileHandle fileHandle))
		{
			_outShaderData = null;
			return false;
		}

		Stream? stream = null;
		try
		{
			// Open file stream:
			if (!fileHandle.TryOpenDataStream(resourceManager.engine, _handle.dataOffset, _handle.dataSize, out stream, out _))
			{
				logger.LogError($"Failed to open file stream for resource handle '{_handle}'!");
				_outShaderData = null;
				return false;
			}

			// Import from stream, identifying file format from extension:
			string formatExt = Path.GetExtension(fileHandle.dataFilePath);
			if (string.IsNullOrEmpty(formatExt))
			{
				_outShaderData = null;
				return false;
			}
			formatExt = formatExt.ToLowerInvariant();

			if (!importerFormatDict.TryGetValue(formatExt, out IShaderImporter? importer))
			{
				logger.LogError($"Failed to import shader data for resource handle '{_handle}'!");
				_outShaderData = null;
				return false;
			}

			bool success = importer.ImportShaderData(importCtx, stream, out _outShaderData); 
			return success;
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to import model data for resource handle '{_handle}'!", ex);
			_outShaderData = null;
			return false;
		}
		finally
		{
			stream?.Close();
		}
	}

	public bool CreateShader(string _resourceKey, ShaderData _shaderData, out ShaderResource? _outShaderResource)
	{
		// Check input parameters:
		if (string.IsNullOrEmpty(_resourceKey))
		{
			logger.LogError("Cannot create shader resource for null or empty resource key!");
			_outShaderResource = null;
			return false;
		}
		if (_shaderData is null || !_shaderData.IsValid())
		{
			logger.LogError("Cannot create shader resource data using null or invalid shader data!");
			_outShaderResource = null;
			return false;
		}

		ShaderLanguage sourceCodeLanguage = graphicsCore.DefaultShaderLanguage;
		ShaderStages stage = _shaderData.Description.Stage;

		if (!ShaderConfig.TryParseDescriptionTxt(_shaderData.Description.MinCapabilities, out ShaderConfig minConfig) ||
			!ShaderConfig.TryParseDescriptionTxt(_shaderData.Description.MaxCapabilities, out ShaderConfig maxConfig))
		{
			logger.LogError("Failed to parse the minimum and maximum capabilities descriptions from shader data!");
			_outShaderResource = null;
			return false;
		}

		MeshVertexDataFlags maxSupportedVariantFlags = minConfig.GetVertexDataForVariantFlags() | maxConfig.GetVertexDataForVariantFlags();

		// Identify all pre-compiled variants:
		bool hasCompiledVariants = GetCompiledVariants(
			_shaderData,
			ref maxSupportedVariantFlags,
			out byte[]? compiledShaderData,
			out Dictionary<MeshVertexDataFlags, ShaderDataCompiledBlockDesc>? compiledBlocks);

		// Select all source code variants:
		_ = GetSourceCodeVariants(
			_shaderData,
			sourceCodeLanguage,
			ref maxSupportedVariantFlags,
			out List<ShaderDataSourceCodeDesc>? sourceCodeData,
			out byte[]? sanitizedSourceCodeBytes);

		// Allocate array of shader programs that will fit a matrix of all possible variants:
		uint maxVariantIndex = maxSupportedVariantFlags.GetVariantIndex();
		uint maxVariantCount = maxVariantIndex + 1;
		Shader?[] precompiledVariants = new Shader[maxVariantCount];

		// Upload compiled data to GPU as ready variants:
		if (hasCompiledVariants)
		{
			foreach (var kvp in compiledBlocks!)
			{
				uint variantIndex = kvp.Key.GetVariantIndex();

				byte[] variantBytes = new byte[kvp.Value.size];
				Array.Copy(compiledShaderData!, kvp.Value.offset, variantBytes, 0, kvp.Value.size);

				try
				{
					ShaderDescription variantDesc = new(stage, variantBytes, kvp.Value.entryPoint);

					Shader variant = graphicsCore.MainFactory.CreateShader(ref variantDesc);
					variant.Name = $"{stage}Shader_{_resourceKey}_{kvp.Key}";
					precompiledVariants[variantIndex] = variant;
				}
				catch (Exception ex)
				{
					logger.LogException($"Failed to create shader program for pre-compiled variant '{kvp.Key}' of shader resource '{_resourceKey}'!", ex);
					continue;
				}
			}
		}

		// Try creating the actual shader resource object:
		try
		{
			_outShaderResource = new(
				_resourceKey,
				graphicsCore,
				_shaderData.Description.Stage,
				maxSupportedVariantFlags,
				precompiledVariants,
				sourceCodeLanguage,
				sourceCodeData,
				sanitizedSourceCodeBytes);
			return true;
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to create shader resource instance for resource key '{_resourceKey}'!", ex);
			_outShaderResource = null;
			return false;
		}
	}

	private bool GetCompiledVariants(
		ShaderData _shaderData,
		ref MeshVertexDataFlags _maxSupportedVariantFlags,
		out byte[]? _outCompiledShaderData,
		out Dictionary<MeshVertexDataFlags, ShaderDataCompiledBlockDesc>? _outCompiledBlocks)
	{
		_outCompiledShaderData = null;
		if (_shaderData.Description.CompiledBlocks is null)
		{
			_outCompiledBlocks = null;
			return false;
		}

		CompiledShaderDataType compiledDataType = graphicsCore.CompiledShaderDataType;
		for (int i = 1; i <= (int)compiledDataType; ++i)
		{
			CompiledShaderDataType currentDataType = (CompiledShaderDataType)i;
			if (_shaderData.TryGetByteCode(currentDataType, out _outCompiledShaderData))
			{
				compiledDataType = currentDataType;
				break;
			}
		}

		if (_outCompiledShaderData is null || _outCompiledShaderData.Length == 0)
		{
			_outCompiledBlocks = null;
			return false;
		}

		_outCompiledBlocks = [];
		foreach (var block in _shaderData.Description.CompiledBlocks)
		{
			if (block.dataType != compiledDataType)
			{
				continue;
			}
			if (!ShaderConfig.TryParseDescriptionTxt(block.capabilities, out ShaderConfig variantConfig))
			{
				continue;
			}

			MeshVertexDataFlags variantFlags = variantConfig.GetVertexDataForVariantFlags();

			_maxSupportedVariantFlags |= block.variantFlags;
			_outCompiledBlocks.Add(variantFlags, block);
		}

		return _outCompiledBlocks.Count != 0;
	}

	private static bool GetSourceCodeVariants(
		ShaderData _shaderData,
		ShaderLanguage _language,
		ref MeshVertexDataFlags _maxSupportedVariantFlags,
		out List<ShaderDataSourceCodeDesc>? _outSourceCodeData,
		out byte[]? _outSanitizedSourceCodeBytes)
	{
		if (_shaderData.Description.SourceCode is null)
		{
			_outSourceCodeData = null;
			_outSanitizedSourceCodeBytes = null;
			return false;
		}

		if (!_shaderData.TryGetFullSourceCode(_language, out byte[]? fullSourceCodeBytes))
		{
			_outSourceCodeData = null;
			_outSanitizedSourceCodeBytes = null;
			return false;
		}

		_outSourceCodeData = new(_shaderData.Description.SourceCode.Length);
		foreach (ShaderDataSourceCodeDesc blockDesc in _shaderData.Description.SourceCode)
		{
			if (blockDesc.language == _language)
			{
				_maxSupportedVariantFlags |= blockDesc.variantFlags;
				_outSourceCodeData.Add(blockDesc);
			}
		}
		if (_outSourceCodeData.Count == 0)
		{
			_outSanitizedSourceCodeBytes = null;
			return false;
		}

		if (!ShaderSourceCodeDefiner.RemoveAllFeatureDefines(fullSourceCodeBytes!, out var sanitizedCodeBuffer))
		{
			sanitizedCodeBuffer?.ReleaseBuffer();
			_outSourceCodeData = null;
			_outSanitizedSourceCodeBytes = null;
			return false;
		}

		_outSanitizedSourceCodeBytes = new byte[sanitizedCodeBuffer!.Length];
		Array.Copy(sanitizedCodeBuffer.Utf8ByteBuffer, _outSanitizedSourceCodeBytes, sanitizedCodeBuffer.Length);

		sanitizedCodeBuffer.ReleaseBuffer();
		return true;
	}

	#endregion
}
