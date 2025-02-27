﻿using FragAssetPipeline.Resources.Shaders.Compilers;
using FragAssetPipeline.Resources.Shaders.FSHA;
using FragEngine3.Graphics;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Shaders;
using FragEngine3.Graphics.Resources.Shaders.Internal;
using System.Text;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders;

/// <summary>
/// Helper class for creating shader data from source code files.
/// </summary>
public static class ShaderDataLoader
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
		ShaderConfig minConfig = _options.supportedFeatures;
		ShaderConfig maxConfig = _options.supportedFeatures;
		minConfig.SetVariantFlagsFromMeshVertexData(minVariantFlags);
		maxConfig.SetVariantFlagsFromMeshVertexData(maxVariantFlags);

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
			out Dictionary<ShaderData.CompiledDataKey, byte[]>? compiledDataDict))
		{
			Console.WriteLine($"Error! Cannot create shader data, failed to bundle pre-compiled shader data! File path: '{_sourceCodeFilePath}'");
			_outFshaShaderData = null;
			return false;
		}

		bool bundleSourceCode =
			!_options.bundleOnlySourceIfCompilationFails ||
			(compiledDataBlocks is not null && compiledDataDict is not null && compiledDataBlocks.Length != 0);

		_outFshaShaderData = new()
		{
			FileHeader = null,
			Description = new()
			{
				Stage = _options.shaderStage,
				MinCapabilities = minConfig.CreateDescriptionTxt(),
				MaxCapabilities = maxConfig.CreateDescriptionTxt(),
				SourceCode = bundleSourceCode ? sourceCodeBlocks : null,
				CompiledBlocks = compiledDataBlocks,
			},
			SourceCode = bundleSourceCode ? sourceCodeDict : null,
			CompiledData = compiledDataDict,
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
		out Dictionary<ShaderData.CompiledDataKey, byte[]>? _outCompiledDataDict)
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

		_outCompiledDataDict = new(4);
		List<ShaderDataCompiledBlockDesc> compiledDataBlocks = new(4);
		uint dataOffset = 0u;

		if (requiresDxCompiler && ((_options.compiledDataTypeFlags & (CompiledShaderDataType.DXBC | CompiledShaderDataType.DXIL)) != 0))
		{
			CompileUsingDxCompiler(_sourceCodeFilePath, _options, ref dataOffset, compiledDataBlocks, _outCompiledDataDict, false);
		}
		if (requiresDxCompiler && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.SPIRV))
		{
			CompileUsingDxCompiler(_sourceCodeFilePath, _options, ref dataOffset, compiledDataBlocks, _outCompiledDataDict, true);
		}
		if (requiresMacOsCompiler && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.MetalArchive))
		{
			//TODO [later]: Add support for Metal shader compilation once I start caring for MacOS again.
		}

		_outCompiledDataBlocks = compiledDataBlocks.ToArray();
		return true;
	}

	private static bool CompileUsingDxCompiler(
		string _sourceCodeFilePath,
		ShaderExportOptions _options,
		ref uint _dataOffset,
		List<ShaderDataCompiledBlockDesc> _compiledDataBlocks,
		Dictionary<ShaderData.CompiledDataKey, byte[]> _compiledDataDict,
		bool _targetIsSpirv)
	{
		bool success = false;

		foreach (var kvp in _options.entryPoints!)
		{
			MeshVertexDataFlags variantFlags = kvp.Key;
			string entryPoint = kvp.Value;

			if (_targetIsSpirv)
			{
				success |= CompileSpirv(_sourceCodeFilePath, _options, variantFlags, entryPoint, ref _dataOffset, _compiledDataBlocks, _compiledDataDict);
			}
			else
			{
				success |= CompileDxbcAndDxil(_sourceCodeFilePath, _options, variantFlags, entryPoint, ref _dataOffset, _compiledDataBlocks, _compiledDataDict);
			}
		}

		return success;
	}

	private static bool CompileSpirv(
		string _sourceCodeFilePath,
		ShaderExportOptions _options,
		MeshVertexDataFlags _variantFlags,
		string _entryPoint,
		ref uint _dataOffset,
		List<ShaderDataCompiledBlockDesc> _compiledDataBlocks,
		Dictionary<ShaderData.CompiledDataKey, byte[]> _compiledDataDict)
	{
		DxCompiler.DxcResult result = DxCompiler.CompileShaderToSPIRV(
			_sourceCodeFilePath,
			_options.shaderStage,
			_entryPoint);
		if (!result.isSuccess)
		{
			return false;
		}

		ShaderConfig config = _options.supportedFeatures;
		config.SetVariantFlagsFromMeshVertexData(_variantFlags);

		ShaderDataCompiledBlockDesc compiledDataBlock = new(
			CompiledShaderDataType.SPIRV,
			_variantFlags,
			config.CreateDescriptionTxt(),
			_dataOffset,
			(uint)result.compiledShader.Length,
			_entryPoint);

		_dataOffset += (uint)result.compiledShader.Length;
		_compiledDataBlocks.Add(compiledDataBlock);
		_compiledDataDict.Add(new(CompiledShaderDataType.SPIRV, _variantFlags), result.compiledShader);

		return true;
	}

	private static bool CompileDxbcAndDxil(
		string _sourceCodeFilePath,
		ShaderExportOptions _options,
		MeshVertexDataFlags _variantFlags,
		string _entryPoint,
		ref uint _dataOffset,
		List<ShaderDataCompiledBlockDesc> _compiledDataBlocks,
		Dictionary<ShaderData.CompiledDataKey, byte[]> _compiledDataDict)
	{
		if (!DxCompiler.CompileShaderToDXBCAndDXIL(
			_sourceCodeFilePath,
			_options.shaderStage,
			_entryPoint,
			out DxCompiler.DxcResult resultDxbc,
			out DxCompiler.DxcResult resultDxil))
		{
			return false;
		}

		ShaderConfig config = _options.supportedFeatures;
		config.SetVariantFlagsFromMeshVertexData(_variantFlags);

		if (resultDxbc.isSuccess && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXBC))
		{
			ShaderDataCompiledBlockDesc compiledDataBlock = new(
			CompiledShaderDataType.DXBC,
			_variantFlags,
			config.CreateDescriptionTxt(),
			_dataOffset,
			(uint)resultDxbc.compiledShader.Length,
			_entryPoint);

			_dataOffset += (uint)resultDxbc.compiledShader.Length;
			_compiledDataBlocks.Add(compiledDataBlock);
			_compiledDataDict.Add(new(CompiledShaderDataType.DXBC, _variantFlags), resultDxbc.compiledShader);
		}

		if (resultDxil.isSuccess && _options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXIL))
		{
			ShaderDataCompiledBlockDesc compiledDataBlock = new(
			CompiledShaderDataType.DXIL,
			_variantFlags,
			config.CreateDescriptionTxt(),
			_dataOffset,
			(uint)resultDxil.compiledShader.Length,
			_entryPoint);

			_dataOffset += (uint)resultDxil.compiledShader.Length;
			_compiledDataBlocks.Add(compiledDataBlock);
			_compiledDataDict.Add(new(CompiledShaderDataType.DXIL, _variantFlags), resultDxil.compiledShader);
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
