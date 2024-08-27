using FragAssetPipeline.Resources.Shaders.Compilers;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Graphics.Resources.ShaderGen;
using System.Text;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders;

public static class FshaExporter
{
	#region Types

	private sealed class CompiledVariant
	{
		public CompiledShaderDataType shaderType = CompiledShaderDataType.Other;
		public MeshVertexDataFlags vertexDataFlags = 0;
		public string entryPoint = string.Empty;
		public byte[] compiledData = [];
		public uint byteOffset = 0u;
	}

	#endregion
	#region Fields

	private static readonly MeshVertexDataFlags[] allOptionalVertexFlags =
	[
		MeshVertexDataFlags.ExtendedSurfaceData,
		MeshVertexDataFlags.BlendShapes,
		MeshVertexDataFlags.Animations
	];
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

	public static bool ExportShaderFromHlslFile(string _filePath, FshaExportOptions _options, out ShaderData? _outFshaShaderData)
	{
		if (_options is null)
		{
			Console.WriteLine("Error! Cannot export FSHA without export options!");
			_outFshaShaderData = null;
			return false;
		}
		if (!CheckIfFileExists(_filePath))
		{
			Console.WriteLine($"Error! Cannot export FSHA, HLSL source file path is null or incorrect! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}
		if (_options.shaderStage == ShaderStages.None && !GetShaderStageFromFileNameSuffix(_filePath, out _options.shaderStage))
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
		List<CompiledVariant> compiledVariants = new(_options.entryPoints!.Count);
		ushort variantCount = 0;
		uint currentByteOffsetDxbc = 0;
		uint currentByteOffsetDxil = 0;
		uint currentByteOffsetSpirv = 0;

		// Group compiled and bundled variants by shader data type:
		bool success = true;
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXBC))
		{
			success &= CompileVariants_DXBC(_filePath, _options, compiledVariants, ref currentByteOffsetDxbc, ref variantCount);
		}
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXIL))
		{
			success &= CompileVariants_DXIL(_filePath, _options, compiledVariants, ref currentByteOffsetDxil, ref variantCount);
		}
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.SPIRV))
		{
			success &= CompileVariants_SPIRV(_filePath, _options, compiledVariants, ref currentByteOffsetSpirv, ref variantCount);
		}
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.MetalArchive))
		{
			//TODO: Add Metal shader library/archive compiler.
		}

		if (!success)
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
		var compiledVariantData = new ShaderDescriptionVariantData[variantCount];
		var entryPointData = new ShaderDescriptionSourceCodeData.VariantEntryPoint[variantCount];

		var allByteCodeDxbc = new byte[currentByteOffsetDxbc];
		var allByteCodeDxil = new byte[currentByteOffsetDxil];
		var allByteCodeSpirv = new byte[currentByteOffsetSpirv];
		uint totalShaderDataByteSize = currentByteOffsetDxbc + currentByteOffsetDxil + currentByteOffsetSpirv;

		for (int i = 0; i < compiledVariants.Count; ++i)
		{
			CompiledVariant compiledVariant = compiledVariants[i];

			// Assemble mappings and entry points for compiled variants:
			compiledVariantData[i] = new()
			{
				Type = compiledVariant.shaderType,
				VariantFlags = compiledVariant.vertexDataFlags,
				VariantDescriptionTxt = "At_Nyn_Ly101p0_V100",      //TODO
				EntryPoint = compiledVariant.entryPoint,
				ByteOffset = compiledVariant.byteOffset,
				ByteSize = (uint)compiledVariant.compiledData.Length,
			};
			entryPointData[i] = new()
			{
				VariantFlags = compiledVariant.vertexDataFlags,
				EntryPoint = compiledVariant.entryPoint,
			};

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
				allByteCode, compiledVariant.byteOffset,
				compiledVariant.compiledData.Length);
		}

		// If source code should be included:
		ShaderDescriptionSourceCodeData? sourceCodeData = null;
		Dictionary<ShaderLanguage, byte[]>? sourceCodeUtf8Blocks = null;
		if (isSourceCodeIncluded)
		{
			// Try to determine entry point functions' base name: (generally the most basic variant)
			string entryPointBase;
			CompiledVariant? baseVariant = compiledVariants.FirstOrDefault(o => o.vertexDataFlags == MeshVertexDataFlags.BasicSurfaceData);
			if (baseVariant is not null)
			{
				entryPointBase = baseVariant.entryPoint;
			}
			else
			{
				entryPointBase = compiledVariants[0].entryPoint;
			}

			// Read source code from files:
			sourceCodeUtf8Blocks = [];
			List<ShaderDescriptionSourceCodeData.SourceCodeBlock> sourceCodeBlocks = [];
			uint sourceCodeCurrentOffset = 0u;
			foreach (ShaderLanguage language in sourceCodeLanguages)
			{
				// Check if a source file of the same name, but using different extension exists:
				if (!ShaderGenConstants.shaderLanguageFileExtensions.TryGetValue(language, out string? languageExt)) continue;

				string sourceCodeFilePath = Path.ChangeExtension(_filePath, languageExt);
				if (CheckIfFileExists(sourceCodeFilePath))
				{
					// Read and add source code byte data for this shader language:
					byte[] sourceCodeUtf8Bytes = File.ReadAllBytes(sourceCodeFilePath);

					ShaderDescriptionSourceCodeData.SourceCodeBlock block = new()
					{
						Language = language,
						ByteOffset = sourceCodeCurrentOffset,
						ByteSize = (uint)sourceCodeUtf8Bytes.Length,
					};
					sourceCodeUtf8Blocks.Add(language, sourceCodeUtf8Bytes);
					sourceCodeBlocks.Add(block);
					sourceCodeCurrentOffset += block.ByteSize;
				}
			}

			// Assemble source code data:
			sourceCodeData = new()
			{
				EntryPointNameBase = entryPointBase,
				EntryPoints = entryPointData,
				SupportedFeaturesTxt = "At_Nyy_Ly111pF_V110",       //TODO
				MaximumCompiledFeaturesTxt = "At_Nyn_Ly101p0_V110", //TODO
				SourceCodeBlocks = sourceCodeBlocks.ToArray(),
			};
		}

		// Assemble shader data:
		_outFshaShaderData = new()
		{
			FileHeader = new ShaderDataFileHeader()
			{
				formatSpecifier = "FSHA",
				formatVersion = new()
				{
					major = 1,
					minor = 0
				},
				fileHeaderSize = ShaderDataFileHeader.minFileHeaderSize,
				shaderDataBlockCount = variantCount,
				shaderData = new()
				{
					byteOffset = 0,							//TODO: Calculate actual offset!
					byteSize = totalShaderDataByteSize,
				}
			},
			Description = new ShaderDescriptionData()
			{
				ShaderStage = _options.shaderStage,
				SourceCode = sourceCodeData,
				CompiledVariants = compiledVariantData,
			},
			SourceCode = sourceCodeUtf8Blocks,
			ByteCodeDxbc = allByteCodeDxbc,
			ByteCodeDxil = null,
			ByteCodeSpirv = null,
		};

		// Check validity and complete-ness before returning success:
		bool isValid = _outFshaShaderData.IsValid();
		if (!isValid)
		{
			Console.WriteLine($"Error! Exported FSHA shader data was incomplete or invalid! File path: '{_filePath}'");
		}
		return isValid;
	}

	private static bool CheckIfFileExists(string _filePath)
	{
		return !string.IsNullOrEmpty(_filePath) && File.Exists(_filePath);
	}

	public static bool GetShaderStageFromFileNameSuffix(string _filePath, out ShaderStages _outShaderStage)
	{
		if (string.IsNullOrEmpty(_filePath))
		{
			_outShaderStage = ShaderStages.None;
			return false;
		}

		string fileName = Path.GetFileNameWithoutExtension(_filePath);

		foreach (var kvp in GraphicsConstants.shaderResourceSuffixes)
		{
			if (fileName.EndsWith(kvp.Value, StringComparison.OrdinalIgnoreCase))
			{
				_outShaderStage = kvp.Key;
				return true;
			}
		}

		_outShaderStage = ShaderStages.None;
		return false;
	}

	private static bool TryFindEntryPoints(string _filePath, FshaExportOptions _options)
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

	private static bool CompileVariants_DXBC(
		string _filePath,
		FshaExportOptions _options,
		List<CompiledVariant> _compiledVariants,
		ref uint _currentByteOffset,
		ref ushort _variantCount)
	{
		if (!DxCompiler.IsAvailableOnCurrentPlatform())
		{
			Console.WriteLine("Warning: DXBC compilation using DxCompiler is not supported on this platform.");
			return false;
		}

		bool success = true;

		foreach (var kvp in _options.entryPoints!)
		{
			var dxcResult = DxCompiler.CompileShaderToDXBC(_filePath, _options.shaderStage, kvp.Value);
			success &= dxcResult.isSuccess;
			if (!dxcResult.isSuccess)
			{
				Console.WriteLine($"Warning! Failed to compile DXBC shader variant for entry point '{kvp.Value}' ({kvp.Key})! File path: '{_filePath}'");
				continue;
			}

			CompiledVariant compiledVariant = new()
			{
				shaderType = CompiledShaderDataType.DXBC,
				vertexDataFlags = kvp.Key,
				entryPoint = kvp.Value,
				compiledData = dxcResult.compiledShader,
				byteOffset = _currentByteOffset,
			};
			_compiledVariants.Add(compiledVariant);
			_currentByteOffset += (uint)dxcResult.compiledShader.Length;
			_variantCount++;
		}

		return success;
	}

	private static bool CompileVariants_DXIL(
		string _filePath,
		FshaExportOptions _options,
		List<CompiledVariant> _compiledVariants,
		ref uint _currentByteOffset,
		ref ushort _variantCount)
	{
		Console.WriteLine($"Warning! DXIL compilation is not fully implemented yet! File path: '{_filePath}'");
		return false;
	}

	private static bool CompileVariants_SPIRV(
		string _filePath,
		FshaExportOptions _options,
		List<CompiledVariant> _compiledVariants,
		ref uint _currentByteOffset,
		ref ushort _variantCount)
	{
		if (!DxCompiler.IsAvailableOnCurrentPlatform())
		{
			Console.WriteLine("Warning: SPIR-V compilation using DxCompiler is not supported on this platform.");
			return false;
		}

		bool success = true;

		foreach (var kvp in _options.entryPoints!)
		{
			var dxcResult = DxCompiler.CompileShaderToSPIRV(_filePath, _options.shaderStage, kvp.Value);
			success &= dxcResult.isSuccess;
			if (!dxcResult.isSuccess)
			{
				Console.WriteLine($"Warning! Failed to compile SPIR-V shader variant for entry point '{kvp.Value}' ({kvp.Key})! File path: '{_filePath}'");
				continue;
			}

			CompiledVariant compiledVariant = new()
			{
				shaderType = CompiledShaderDataType.SPIRV,
				vertexDataFlags = kvp.Key,
				entryPoint = kvp.Value,
				compiledData = dxcResult.compiledShader,
				byteOffset = _currentByteOffset,
			};
			_compiledVariants.Add(compiledVariant);
			_currentByteOffset += (uint)dxcResult.compiledShader.Length;
			_variantCount++;
		}

		return success;
	}

	#endregion
}
