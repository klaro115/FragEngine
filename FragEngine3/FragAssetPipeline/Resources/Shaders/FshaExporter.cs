using FragAssetPipeline.Resources.Shaders.FSHA;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Graphics.Resources.Shaders.Internal;
using System.Text;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders;

/// <summary>
/// Exporter for the FSHA shader asset format.
/// </summary>
public static class FshaExporter	//TODO: Rename this to something more sensible. This is not really related to the FSHA format.
{
	#region Fields

	// Array of all optional vertex data flags:
	private static readonly MeshVertexDataFlags[] allOptionalVertexFlags =
	[
		MeshVertexDataFlags.ExtendedSurfaceData,
		MeshVertexDataFlags.BlendShapes,
		MeshVertexDataFlags.Animations
	];

	// Array of all valid permutations of vertex data flags for surface shaders:
	private static readonly MeshVertexDataFlags[] validVertexDataVariantFlags =
	[
		MeshVertexDataFlags.BasicSurfaceData,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.BlendShapes,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.Animations,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.BlendShapes | MeshVertexDataFlags.Animations,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData | MeshVertexDataFlags.BlendShapes,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData | MeshVertexDataFlags.Animations,
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData | MeshVertexDataFlags.BlendShapes | MeshVertexDataFlags.Animations
	];

	#endregion
	#region Methods

	public static bool CreateShaderDataFromSourceCode(string _sourceCodeFilePath, ShaderExportOptions _options, out ShaderData? _outFshaShaderData)
	{
		// Check input parameters:
		if (_options is null)
		{
			Console.WriteLine("Error! Cannot create shader data without export options!");
			_outFshaShaderData = null;
			return false;
		}
		if (!FshaExportUtility.CheckIfFileExists(_sourceCodeFilePath))
		{
			Console.WriteLine($"Error! Cannot create shader data, source file path is null or incorrect! File path: '{_sourceCodeFilePath}'");
			_outFshaShaderData = null;
			return false;
		}

		// Check which shader stage we're working with:
		if (_options.shaderStage == ShaderStages.None && !FshaExportUtility.GetShaderStageFromFileNameSuffix(_sourceCodeFilePath, out _options.shaderStage))
		{
			Console.WriteLine($"Error! Cannot create shader data, unable to determine shader stage from source file path! File path: '{_sourceCodeFilePath}'");
			_outFshaShaderData = null;
			return false;
		}

		// Identify entry point functions:
		if (!TryFindEntryPoints(_sourceCodeFilePath, _options))
		{
			Console.WriteLine($"Error! Cannot create shader data, unable to determine entry points! File path: '{_sourceCodeFilePath}'");
			_outFshaShaderData = null;
			return false;
		}

		MeshVertexDataFlags minVariantFlags = MeshVertexDataFlags.ALL;
		MeshVertexDataFlags maxVariantFlags = 0;
		foreach (var kvp in _options.entryPoints!)
		{
			minVariantFlags = (MeshVertexDataFlags)Math.Min((int)minVariantFlags, (int)kvp.Key);
			maxVariantFlags |= kvp.Key;
		}
		ShaderConfig minConfig = new();	// TODO
		ShaderConfig maxConfig = new(); // TODO

		if (!BundleSourceCodeBlocks(
			_sourceCodeFilePath,
			_options,
			out ShaderDataSourceCodeDesc[]? sourceCodeBlocks,
			out Dictionary<ShaderLanguage, byte[]>? sourceCodeDict))
		{
			Console.WriteLine($"Error! Cannot create shader data, failed to bundle source code data! File path: '{_sourceCodeFilePath}'");
			_outFshaShaderData = null;
			return false;
		}

		if (!BundleCompiledDataBlocks(
			_sourceCodeFilePath,
			_options,
			out ShaderDataCompiledBlockDesc[]? compiledDataBlocks,
			out Dictionary<CompiledShaderDataType, byte[]>? compiledDataDict))
		{
			Console.WriteLine($"Error! Cannot create shader data, failed to bundle pre-compiled shader data! File path: '{_sourceCodeFilePath}'");
			_outFshaShaderData = null;
			return false;
		}

		_outFshaShaderData = new()
		{
			FileHeader = null,
			Description = new()
			{
				Stage = _options.shaderStage,
				MinCapabilities = minConfig.CreateDescriptionTxt(),
				MaxCapabilities = maxConfig.CreateDescriptionTxt(),
				SourceCode = sourceCodeBlocks,
				CompiledBlocks = compiledDataBlocks,
			},
			SourceCode = sourceCodeDict,
			//TODO
		};
		return true;
	}

	private static bool BundleSourceCodeBlocks(
		string _sourceCodeFilePath,
		ShaderExportOptions _options,
		out ShaderDataSourceCodeDesc[]? _outSourceCodeBlocks,
		out Dictionary<ShaderLanguage, byte[]>? _outSourceCodeDict)
	{
		throw new NotImplementedException("TODO");
	}

	private static bool BundleCompiledDataBlocks(
		string _sourceCodeFilePath,
		ShaderExportOptions _options,
		out ShaderDataCompiledBlockDesc[]? _outCompiledDataBlocks,
		out Dictionary<CompiledShaderDataType, byte[]>? _outCompiledDataDict)
	{
		throw new NotImplementedException("TODO");
	}




	/*
	[Obsolete("rewritten")]
	public static bool ExportShaderFromHlslFile(string _filePath, ShaderExportOptions _options, out ShaderData? _outFshaShaderData)
	{
		if (_options is null)
		{
			Console.WriteLine("Error! Cannot export FSHA without export options!");
			_outFshaShaderData = null;
			return false;
		}
		if (!FshaExportUtility.CheckIfFileExists(_filePath))
		{
			Console.WriteLine($"Error! Cannot export FSHA, HLSL source file path is null or incorrect! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}
		if (_options.shaderStage == ShaderStages.None && !FshaExportUtility.GetShaderStageFromFileNameSuffix(_filePath, out _options.shaderStage))
		{
			Console.WriteLine($"Error! Cannot export FSHA, unable to determine shader stage from HLSL source file path! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}
		if (!TryFindEntryPoints(_filePath, _options))
		{
			Console.WriteLine($"Error! Cannot export FSHA, unable to determine entry points! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}

		MeshVertexDataFlags supportedVariantFlags = 0;
		MeshVertexDataFlags maxCompiledVariantFlags = 0;
		foreach (var kvp in _options.entryPoints!)
		{
			supportedVariantFlags |= kvp.Key;
		}

		// Extract source code language flags:
		bool isSourceCodeIncluded = _options.bundledSourceCodeLanguages != 0;
		List<ShaderLanguage> sourceCodeLanguages = new(3);

		if (isSourceCodeIncluded)
		{
			const int languageFlagsMaxBits = sizeof(ShaderLanguage) * 8;
			for (int i = 0; i < languageFlagsMaxBits; ++i)
			{
				ShaderLanguage languageFlag = (ShaderLanguage)(1 << i);
				if (_options.bundledSourceCodeLanguages.HasFlag(languageFlag))
				{
					sourceCodeLanguages.Add(languageFlag);
				}
			}
		}

		// Try compiling all variants from the provided entry points:
		List<FshaCompiledVariant> compiledVariants = new(_options.entryPoints!.Count);
		if (!FshaVariantExport.CompileVariants(_filePath, _options, compiledVariants, out FshaVariantExport.OutputDetails outputDetails))
		{
			Console.WriteLine($"Warning: Compilation of one or more shader variants failed! File path: '{_filePath}'");
		}

		// Return error if no variants were compiled, and if source-only export is disabled:
		if (compiledVariants.Count == 0 && (!_options.bundleOnlySourceIfCompilationFails || !isSourceCodeIncluded))
		{
			Console.WriteLine($"Error! Failed to compile any FSHA shader variants! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}
		
		// Prepare variant data and source code for writing:
		var compiledVariantData = new ShaderDescriptionVariantData[outputDetails.variantCount];
		var entryPointData = new ShaderDescriptionSourceCodeData.VariantEntryPoint[outputDetails.variantCount];

		var allByteCodeDxbc = new byte[outputDetails.dxbcByteSize];
		var allByteCodeDxil = new byte[outputDetails.dxilByteSize];
		var allByteCodeSpirv = new byte[outputDetails.spirvByteSize];

		for (int i = 0; i < compiledVariants.Count; ++i)
		{
			FshaCompiledVariant compiledVariant = compiledVariants[i];

			// Create description string for this variant:
			ShaderConfig variantConfig = _options.supportedFeatures;
			variantConfig.SetVariantFlagsFromMeshVertexData(compiledVariant.vertexDataFlags);

			// Assemble mappings and entry points for compiled variants:
			compiledVariantData[i] = new()
			{
				Type = compiledVariant.shaderType,
				VariantFlags = compiledVariant.vertexDataFlags,
				VariantDescriptionTxt = variantConfig.CreateDescriptionTxt(),
				EntryPoint = compiledVariant.entryPoint,
				RelativeByteOffset = compiledVariant.relativeByteOffset,
				TotalByteOffset = compiledVariant.totalByteOffset,
				ByteSize = (uint)compiledVariant.compiledData.Length,
			};
			entryPointData[i] = new()
			{
				VariantFlags = compiledVariant.vertexDataFlags,
				EntryPoint = compiledVariant.entryPoint,
			};
			maxCompiledVariantFlags |= compiledVariant.vertexDataFlags;

			// Copy compiled byte data into the backend-specific byte buffer:
			byte[] allByteCode = compiledVariant.shaderType switch
			{
				CompiledShaderDataType.DXBC => allByteCodeDxbc,
				CompiledShaderDataType.DXIL => allByteCodeDxil,
				CompiledShaderDataType.SPIRV => allByteCodeSpirv,
				_ => [],
			};
			Array.Copy(
				compiledVariant.compiledData, 0,
				allByteCode, compiledVariant.relativeByteOffset,
				compiledVariant.compiledData.Length);
		}

		// If source code should be included:
		List<ShaderDataSourceCodeDesc>? sourceCodeData = null;
		Dictionary<ShaderLanguage, byte[]>? sourceCodeUtf8Blocks = null;
		uint sourceCodeTotalByteSize = 0u;

		if (isSourceCodeIncluded)
		{
			// Try to determine entry point functions' base name: (generally the most basic variant)
			string entryPointBase;
			FshaCompiledVariant? baseVariant = compiledVariants.FirstOrDefault(o => o.vertexDataFlags == MeshVertexDataFlags.BasicSurfaceData);
			if (baseVariant is not null)
			{
				entryPointBase = baseVariant.entryPoint;
			}
			else if (compiledVariants is not null && compiledVariants.Count != 0)
			{
				entryPointBase = compiledVariants[0].entryPoint;
			}
			else
			{
				entryPointBase = string.Empty;
				FshaExportUtility.GetDefaultEntryPoint(ref entryPointBase!, _options.shaderStage);
			}

			// Read source code from files:
			sourceCodeUtf8Blocks = [];
			sourceCodeData = [];

			uint sourceCodeCurrentOffset = 0u;
			foreach (ShaderLanguage language in sourceCodeLanguages)
			{
				// Check if a source file of the same name, but using different extension exists:
				if (!ShaderConstants.shaderLanguageFileExtensions.TryGetValue(language, out string? languageExt)) continue;

				string sourceCodeFilePath = Path.ChangeExtension(_filePath, languageExt);
				if (FshaExportUtility.CheckIfFileExists(sourceCodeFilePath))
				{
					// Read and add source code byte data for this shader language:
					byte[] sourceCodeUtf8Bytes = File.ReadAllBytes(sourceCodeFilePath);

					ShaderDataSourceCodeDesc block = new(
						language,
						0,												//TODO
						(ushort)sourceCodeCurrentOffset,
						(ushort)sourceCodeUtf8Bytes.Length,
						entryPointBase);								//TODO

					sourceCodeUtf8Blocks.Add(language, sourceCodeUtf8Bytes);
					sourceCodeData.Add(block);
					sourceCodeCurrentOffset += block.size;
				}
			}

			// Create description strings for maximum compiled and maximum supported feature sets:
			ShaderConfig maxCompiledConfig = _options.supportedFeatures;
			maxCompiledConfig.SetVariantFlagsFromMeshVertexData(maxCompiledVariantFlags);

			ShaderConfig supportedConfig = _options.supportedFeatures;
			supportedConfig.SetVariantFlagsFromMeshVertexData(supportedVariantFlags);

			// Assemble source code data:
			sourceCodeTotalByteSize = sourceCodeCurrentOffset;
		}

		// Assemble shader data:
		_outFshaShaderData = new()
		{
			FileHeader = null,
			Description = new ShaderDataDescription()
			{
				Stage = _options.shaderStage,
				MinCapabilities = "TODO",
				MaxCapabilities = "TODO",
				SourceCode = sourceCodeData?.ToArray(),
				CompiledBlocks = compiledVariantData.ToArray(),
			},
			SourceCode = sourceCodeUtf8Blocks,
			ByteCodeDxbc = allByteCodeDxbc,
			ByteCodeDxil = allByteCodeDxil,
			ByteCodeSpirv = allByteCodeSpirv,
		};

		// Check validity and complete-ness before returning success:
		bool isValid = _outFshaShaderData.IsValid();
		if (!isValid)
		{
			Console.WriteLine($"Error! Exported FSHA shader data was incomplete or invalid! File path: '{_filePath}'");
		}
		return isValid;
	}
	*/

	private static bool TryFindEntryPoints(string _filePath, ShaderExportOptions _options)
	{
		if (_options is null)
		{
			return false;
		}
		if (_options.entryPoints is not null && _options.entryPoints.Count != 0)
		{
			return true;
		}

		// if no entry point names were provided at all, try using default names:
		if (string.IsNullOrWhiteSpace(_options.entryPointBase) &&
			!GraphicsConstants.defaultShaderStageEntryPoints.TryGetValue(_options.shaderStage, out _options.entryPointBase))
		{
			Console.WriteLine($"Error! Could not find default entry point base name for shader stage '{_options.shaderStage}'!");
			return false;
		}

		// Read source code from file:
		string sourceCode;
		try
		{
			sourceCode = File.ReadAllText(_filePath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error! Failed to read HLSL source code! File path: '{_filePath}'\nException: {ex}");
			return false;
		}

		// Try finding variant entry points within the source code:
		StringBuilder entryPointBuilder = new(_options.entryPointBase.Length + 128);
		_options.entryPoints = [];

		foreach (MeshVertexDataFlags variantFlags in validVertexDataVariantFlags)
		{
			// Assemble variant entry point name:
			entryPointBuilder.Append(_options.entryPointBase);
			foreach (MeshVertexDataFlags optionalVertexFlag in allOptionalVertexFlags)
			{
				if (variantFlags.HasFlag(optionalVertexFlag) && GraphicsConstants.shaderVariantsForEntryPointSuffixes.TryGetValue(optionalVertexFlag, out string? vertexDataSuffix))
				{
					entryPointBuilder.Append('_').Append(vertexDataSuffix);
				}
			}

			string entryPoint = entryPointBuilder.ToString();
			entryPointBuilder.Clear();

			// Check if this variant entrypoint function exists in source code:
			if (sourceCode.Contains($" {entryPoint}("))
			{
				_options.entryPoints.Add(variantFlags, entryPoint);
			}
		}

		if (_options.entryPoints.Count == 0)
		{
			Console.WriteLine($"Error! Cannot export FSHA, could not find entry point functions in HLSL source code! File path: '{_filePath}'");
			_options.entryPoints = null;
			return false;
		}
		return true;
	}

	#endregion
}
