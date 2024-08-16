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
		MeshVertexDataFlags.Animations,
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
		MeshVertexDataFlags.BasicSurfaceData | MeshVertexDataFlags.ExtendedSurfaceData | MeshVertexDataFlags.BlendShapes | MeshVertexDataFlags.Animations,
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
		List<ShaderGenLanguage> sourceCodeLanguages = new(3);

		if (isSourceCodeIncluded)
		{
			const int languageFlagsMaxBits = sizeof(ShaderGenLanguage) * 8;
			for (int i = 0; i < languageFlagsMaxBits; ++i)
			{
				ShaderGenLanguage languageFlag = (ShaderGenLanguage)(1 << i);
				if (_options.bundledSourceCodeLanguages.HasFlag(languageFlag))
				{
					sourceCodeLanguages.Add(languageFlag);
				}
			}
		}

		// Extract compiled data type flags:
		if (_options.shaderTypeFlags == 0)
		{
			_options.shaderTypeFlags |= CompiledShaderDataType.DXBC;
		}
		List<CompiledShaderDataType> compiledShaderTypes = new(3);
		{
			const int shaderTypeFlagsMaxBits = sizeof(CompiledShaderDataType) * 8;
			for (int i = 0; i < shaderTypeFlagsMaxBits; ++i)
			{
				CompiledShaderDataType shaderTypeFlag = (CompiledShaderDataType)(1 << i);
				if (_options.shaderTypeFlags.HasFlag(shaderTypeFlag))
				{
					compiledShaderTypes.Add(shaderTypeFlag);
				}
			}
		}

		// Try compiling all variants from the provided entry points:
		List<CompiledVariant> compiledVariants = new(_options.entryPoints!.Count);
		ushort variantCount = 0;
		uint currentByteOffset = 0;

		// Group compiled and bundled variants by shader data type:
		foreach (CompiledShaderDataType compiledType in compiledShaderTypes)
		{
			//TODO
		}






		
		foreach (var kvp in _options.entryPoints)		//TODO: Refactor and move this into above loop.
		{
			var dxcResult = DxCompiler.CompileShaderToDXBC(_filePath, _options.shaderStage, kvp.Value);
			if (!dxcResult.isSuccess)
			{
				Console.WriteLine($"Warning! Failed to compile FSHA shader variant for entry point '{kvp.Value}' ({kvp.Key})! File path: '{_filePath}'");
				continue;
			}

			CompiledVariant compiledVariant = new()
			{
				vertexDataFlags = kvp.Key,
				entryPoint = kvp.Value,
				compiledData = dxcResult.compiledShader,
				byteOffset = currentByteOffset,
			};
			compiledVariants.Add(compiledVariant);
			currentByteOffset += (uint)dxcResult.compiledShader.Length;
			variantCount++;
		}

		if (compiledVariants.Count == 0)
		{
			Console.WriteLine($"Error! Failed to compile any FSHA shader variants! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}

		// Prepare variant data and source code for writing:
		var compiledVariantData = new ShaderDescriptionVariantData[variantCount];
		var entryPointData = new ShaderDescriptionSourceCodeData.VariantEntryPoint[variantCount];
		var allByteCodeDxbc = new byte[currentByteOffset];

		for (int i = 0; i < compiledVariants.Count; ++i)
		{
			CompiledVariant compiledVariant = compiledVariants[i];

			compiledVariantData[i] = new()
			{
				Type = CompiledShaderDataType.DXBC,
				VariantFlags = compiledVariant.vertexDataFlags,
				VariantDescriptionTxt = "At_Nyn_Ly101p0_V100",		//TODO
				EntryPoint = compiledVariant.entryPoint,
				ByteOffset = compiledVariant.byteOffset,
				ByteSize = (uint)compiledVariant.compiledData.Length,
			};
			entryPointData[i] = new()
			{
				VariantFlags = compiledVariant.vertexDataFlags,
				EntryPoint = compiledVariant.entryPoint,
			};
			Array.Copy(
				compiledVariant.compiledData, 0,
				allByteCodeDxbc, compiledVariant.byteOffset,
				compiledVariant.compiledData.Length);
		}

		// If source code should be included:
		ShaderDescriptionSourceCodeData? sourceCodeData = null;
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

			// Assemble source code data:
			sourceCodeData = new()
			{
				EntryPointNameBase = entryPointBase,
				EntryPoints = entryPointData,
				SupportedFeaturesTxt = "At_Nyy_Ly111pF_V110",       //TODO
				MaximumCompiledFeaturesTxt = "At_Nyn_Ly101p0_V110", //TODO
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
					byteOffset = currentByteOffset,
				}
			},
			Description = new ShaderDescriptionData()
			{
				ShaderStage = _options.shaderStage,
				SourceCode = sourceCodeData,
				CompiledVariants = compiledVariantData,
			},
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

	private bool 

	#endregion
}
