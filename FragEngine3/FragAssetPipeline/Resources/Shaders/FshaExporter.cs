using FragAssetPipeline.Resources.Shaders.Compilers;
using FragAssetPipeline.Resources.Shaders.FSHA;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Graphics.Resources.Shaders.Internal;
using FragEngine3.Utility.Unicode;
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

	// Array of all shader languages that are supported for bundling as source code:
	private static readonly ShaderLanguage[] allSourceCodeShaderLanguages =
	[
		ShaderLanguage.HLSL,
		ShaderLanguage.Metal,
		ShaderLanguage.GLSL,
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
			maxVariantFlags,
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
		MeshVertexDataFlags _maxVariantFlags,
		out ShaderDataSourceCodeDesc[]? _outSourceCodeBlocks,
		out Dictionary<ShaderLanguage, byte[]>? _outSourceCodeDict)
	{
		if (_options.bundledSourceCodeLanguages == 0)
		{
			_outSourceCodeBlocks = null;
			_outSourceCodeDict = null;
			return true;
		}

		// Check if different language versions of the source code file exist in same directory:
		Dictionary<ShaderLanguage, string> sourceCodeFilePaths = new(4);
		foreach (ShaderLanguage sourceCodeLanguage in allSourceCodeShaderLanguages)
		{
			if (!_options.bundledSourceCodeLanguages.HasFlag(sourceCodeLanguage))
			{
				continue;
			}
			if (!ShaderConstants.shaderLanguageFileExtensions.TryGetValue(sourceCodeLanguage, out string? fileExt))
			{
				continue;
			}
			string filePath = Path.ChangeExtension(_sourceCodeFilePath, fileExt);
			if (FshaExportUtility.CheckIfFileExists(filePath))
			{
				sourceCodeFilePaths.Add(sourceCodeLanguage, filePath);
			}
		}

		// No bundle-able source code? Exit here:
		if (sourceCodeFilePaths.Count == 0)
		{
			_outSourceCodeBlocks = null;
			_outSourceCodeDict = null;
			return true;
		}

		// Ensure the entry point function name is set:
		if (string.IsNullOrEmpty(_options.entryPointBase))
		{
			_options.entryPointBase = _options.entryPoints![0];
		}

		_outSourceCodeBlocks = new ShaderDataSourceCodeDesc[sourceCodeFilePaths.Count];
		_outSourceCodeDict = new(sourceCodeFilePaths.Count);
		int i = 0;

		foreach (var kvp in sourceCodeFilePaths)
		{
			// Read source code files into byte buffers:
			byte[] sourceCodeBytes;
			try
			{
				sourceCodeBytes = File.ReadAllBytes(kvp.Value);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to read source code file contents! File path: '{kvp.Value}'\nException: {ex}");
				continue;
			}
			_outSourceCodeDict.Add(kvp.Key, sourceCodeBytes);

			// Prepare block description:
			_outSourceCodeBlocks[i++] = new(
				kvp.Key,
				_maxVariantFlags,
				0,
				(ushort)sourceCodeBytes.Length,
				_options.entryPointBase!);
		}

		if (_outSourceCodeDict.Count == 0)
		{
			_outSourceCodeBlocks = null;
			_outSourceCodeDict = null;
			return false;
		}
		return true;
	}

	private static bool BundleCompiledDataBlocks(
		string _sourceCodeFilePath,
		ShaderExportOptions _options,
		out ShaderDataCompiledBlockDesc[]? _outCompiledDataBlocks,
		out Dictionary<CompiledShaderDataType, byte[]>? _outCompiledDataDict)
	{
		const CompiledShaderDataType dxCompilerDataTypes = CompiledShaderDataType.DXBC | CompiledShaderDataType.DXIL | CompiledShaderDataType.SPIRV;
		const CompiledShaderDataType macOsCompilerDataTypes = CompiledShaderDataType.MetalArchive;

		if (_options.compiledDataTypeFlags == 0)
		{
			_outCompiledDataBlocks = null;
			_outCompiledDataDict = null;
			return true;
		}

		bool requiresDxCompiler = (_options.compiledDataTypeFlags & dxCompilerDataTypes) != 0;
		bool requiresMacOsCompiler = (_options.compiledDataTypeFlags & macOsCompilerDataTypes) != 0;

		if (requiresDxCompiler && !DxCompiler.IsAvailableOnCurrentPlatform())
		{
			Console.WriteLine("Error! Failed to pre-compile shader data; Shader compiler is not available on current platform!");
			_outCompiledDataBlocks = null;
			_outCompiledDataDict = null;
			return false;
		}

		List<ShaderDataCompiledBlockDesc> compiledDataBlocks = [];
		List<byte> compiledDataBuffer = new(4096);
		_outCompiledDataDict = new(4);

		if (requiresDxCompiler && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXBC))
		{
			if (CompileUsingDxCompiler(_sourceCodeFilePath, _options, compiledDataBlocks, compiledDataBuffer, false))
			{
				_outCompiledDataDict.Add(CompiledShaderDataType.DXBC, compiledDataBuffer.ToArray());
			}
		}
		if (requiresDxCompiler && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXIL))
		{
			//TODO
		}
		if (requiresDxCompiler && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.SPIRV))
		{
			if (CompileUsingDxCompiler(_sourceCodeFilePath, _options, compiledDataBlocks, compiledDataBuffer, true))
			{
				_outCompiledDataDict.Add(CompiledShaderDataType.SPIRV, compiledDataBuffer.ToArray());
			}
		}
		if (requiresMacOsCompiler && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.MetalArchive))
		{
			//TODO
		}

		_outCompiledDataBlocks = compiledDataBlocks.ToArray();
		return true;
	}

	private static bool CompileUsingDxCompiler(string _sourceCodeFilePath, ShaderExportOptions _options, List<ShaderDataCompiledBlockDesc> _compiledDataBlocks, List<byte> _compiledDataBuffer, bool _targetIsSpirv)
	{
		_compiledDataBuffer.Clear();

		CompiledShaderDataType dataType = _targetIsSpirv
			? CompiledShaderDataType.SPIRV
			: CompiledShaderDataType.DXBC;

		foreach (var kvp in _options.entryPoints!)
		{
			DxCompiler.DxcResult result = _targetIsSpirv
				? DxCompiler.CompileShaderToSPIRV(_sourceCodeFilePath, _options.shaderStage, kvp.Value)
				: DxCompiler.CompileShaderToDXBC(_sourceCodeFilePath, _options.shaderStage, kvp.Value);
			if (!result.isSuccess)
			{
				continue;
			}

			ShaderConfig config = _options.supportedFeatures;
			config.SetVariantFlagsFromMeshVertexData(kvp.Key);

			ShaderDataCompiledBlockDesc compiledDataBlock = new(
				dataType,
				kvp.Key,
				config.CreateDescriptionTxt(),
				(uint)_compiledDataBuffer.Count,
				(uint)result.compiledShader.Length,
				kvp.Value);

			_compiledDataBuffer.AddRange(result.compiledShader);
			_compiledDataBlocks.Add(compiledDataBlock);
		}

		return true;
	}

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
