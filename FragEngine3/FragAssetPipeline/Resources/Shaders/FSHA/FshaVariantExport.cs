using FragAssetPipeline.Resources.Shaders.Compilers;
using FragEngine3.Graphics.Resources.Shaders;

namespace FragAssetPipeline.Resources.Shaders.FSHA;

/// <summary>
/// Helper class for compiling shader variants for FSHA format export.
/// </summary>
internal static class FshaVariantExport
{
	#region Types

	public sealed class OutputDetails
	{
		public uint variantCount = 0u;
		public uint totalByteSize = 0u;

		public uint dxbcByteSize = 0u;
		public uint dxilByteSize = 0u;
		public uint spirvByteSize = 0u;
		//... (metal stuff)
	}

	#endregion
	#region Methods

	public static bool CompileVariants(
		string _filePath,
		ShaderExportOptions _options,
		List<FshaCompiledVariant> _compiledVariants,
		out OutputDetails _outOutputDetails)
	{
		_outOutputDetails = new();
		if (string.IsNullOrEmpty(_filePath) || _options is null) return false;

		bool success = true;

		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXBC))
		{
			success &= CompileVariants_DXBC(_filePath, _options, _compiledVariants, _outOutputDetails);
		}
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.DXIL))
		{
			success &= CompileVariants_DXIL(_filePath, _options, _compiledVariants, _outOutputDetails);
		}
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.SPIRV))
		{
			success &= CompileVariants_SPIRV(_filePath, _options, _compiledVariants, _outOutputDetails);
		}
		if (_options.compiledDataTypeFlags.HasFlag(CompiledShaderDataType.MetalArchive))
		{
			//TODO: Add Metal shader library/archive compiler.
		}

		return success;
	}

	public static bool CompileVariants_DXBC(
		string _filePath,
		ShaderExportOptions _options,
		List<FshaCompiledVariant> _compiledVariants,
		OutputDetails _outputDetails)
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

			FshaCompiledVariant compiledVariant = new()
			{
				shaderType = CompiledShaderDataType.DXBC,
				vertexDataFlags = kvp.Key,
				entryPoint = kvp.Value,
				compiledData = dxcResult.compiledShader,
				relativeByteOffset = _outputDetails.dxbcByteSize,
				totalByteOffset = _outputDetails.totalByteSize,
			};
			_compiledVariants.Add(compiledVariant);

			uint variantSize = (uint)dxcResult.compiledShader.Length;
			_outputDetails.totalByteSize += variantSize;
			_outputDetails.dxbcByteSize += variantSize;
			_outputDetails.variantCount++;
		}

		return success;
	}

	public static bool CompileVariants_DXIL(
		string _filePath,
		ShaderExportOptions _options,
		List<FshaCompiledVariant> _compiledVariants,
		OutputDetails _outputDetails)
	{
		Console.WriteLine("Warning! DXIL compilation is not fully implemented yet!");
		return true;
	}

	public static bool CompileVariants_SPIRV(
		string _filePath,
		ShaderExportOptions _options,
		List<FshaCompiledVariant> _compiledVariants,
		OutputDetails _outputDetails)
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

			FshaCompiledVariant compiledVariant = new()
			{
				shaderType = CompiledShaderDataType.SPIRV,
				vertexDataFlags = kvp.Key,
				entryPoint = kvp.Value,
				compiledData = dxcResult.compiledShader,
				relativeByteOffset = _outputDetails.spirvByteSize,
				totalByteOffset = _outputDetails.totalByteSize,
			};
			_compiledVariants.Add(compiledVariant);

			uint variantSize = (uint)dxcResult.compiledShader.Length;
			_outputDetails.totalByteSize += variantSize;
			_outputDetails.spirvByteSize += variantSize;
			_outputDetails.variantCount++;
		}

		return success;
	}

	#endregion
}
