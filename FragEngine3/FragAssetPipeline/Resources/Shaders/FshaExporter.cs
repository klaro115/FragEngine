using FragEngine3.Graphics;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
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

	public static bool ExportShaderFromHlslFile(string _filePath, string _entryPointBase, ShaderStages _shaderStage, out ShaderData? _outFshaShaderData)
	{
		if (string.IsNullOrEmpty(_entryPointBase))
		{
			Console.WriteLine("Error! Cannot export FSHA using null entry point function base name!");
			_outFshaShaderData = null;
			return false;
		}
		if (!CheckIfFileExists(_filePath))
		{
			Console.WriteLine($"Error! Cannot export FSHA, HLSL source file path is null or incorrect! File path: '{_filePath}'");
			_outFshaShaderData = null;
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
			_outFshaShaderData = null;
			return false;
		}

		// Try finding variant entry points within the source code:
		StringBuilder entryPointBuilder = new(_entryPointBase.Length + 128);
		Dictionary<MeshVertexDataFlags, string> entryPoints = [];

		foreach (MeshVertexDataFlags variantFlags in validVertexDataVariantFlags)
		{
			// Assemble variant entry point name:
			entryPointBuilder.Append(_entryPointBase);
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
				entryPoints.Add(variantFlags, entryPoint);
			}
		}

		if (entryPoints.Count == 0)
		{
			Console.WriteLine($"Error! Cannot export FSHA, could not find entry point functions in HLSL source code! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}

		return ExportShaderFromHlslFile(_filePath, entryPoints, _shaderStage, out _outFshaShaderData);
	}

	public static bool ExportShaderFromHlslFile(string _filePath, Dictionary<MeshVertexDataFlags, string> _entryPoints, ShaderStages _shaderStage, out ShaderData? _outFshaShaderData)
	{
		if (!CheckIfFileExists(_filePath))
		{
			Console.WriteLine($"Error! Cannot export FSHA, HLSL source file path is null or incorrect! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}
		if (_shaderStage == ShaderStages.None && !GetShaderStageFromFileNameSuffix(_filePath, out _shaderStage))
		{
			Console.WriteLine($"Error! Cannot export FSHA, unable to determine shader stage from HLSL source file path! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}
		if (_entryPoints is null || _entryPoints.Count == 0)
		{
			Console.WriteLine($"Error! Cannot export FSHA without entry point function names! File path: '{_filePath}'");
			_outFshaShaderData = null;
			return false;
		}

		// Try compiling all variants from the provided entry points:
		List<CompiledVariant> compiledVariants = new(_entryPoints.Count);
		ushort variantCount = 0;
		uint currentByteOffset = 0;

		foreach (var kvp in _entryPoints)
		{
			var dxcResult = DxcLauncher.CompileShaderToDXBC(_filePath, _shaderStage, kvp.Value);
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
				ShaderStage = _shaderStage,
				SourceCode = new ShaderDescriptionSourceCodeData()
				{
					EntryPointNameBase = entryPointBase,
					EntryPoints = entryPointData,
					SupportedFeaturesTxt = "At_Nyy_Ly111pF_V110",       //TODO
					MaximumCompiledFeaturesTxt = "At_Nyn_Ly101p0_V110",	//TODO
				},
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

	#endregion
}
