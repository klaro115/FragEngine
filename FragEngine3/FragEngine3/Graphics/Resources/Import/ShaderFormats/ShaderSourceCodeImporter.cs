using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.Data;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Resources;
using FragEngine3.Utility.Unicode;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats;

internal static class ShaderSourceCodeImporter
{
	#region Methods

	public static bool ImportShaderData(Stream _stream, ResourceHandle _resHandle, ResourceFileHandle _fileHandle, string _fileExtension, out ShaderData? _outShaderData)
	{
		Logger? logger = _resHandle.resourceManager.engine.Logger ?? Logger.Instance;

		// Identify shader language from file extension: (ex.: ".hlsl" for HLSL)
		if (!TryIdentifyShaderLanguage(_fileExtension, out ShaderLanguage language))
		{
			logger?.LogError($"Cannot identify shader language from source code file extension! (Resource key: '{_resHandle.resourceKey}')");
			_outShaderData = null;
			return false;
		}

		// Identify the shader stage from file name suffix: (ex.: "_VS" for vertex shaders)
		if (!TryIdentifyShaderStage(_fileHandle, out ShaderStages stage))
		{
			logger?.LogError($"Cannot find shader stage suffix in source code file name! (Resource key: '{_resHandle.resourceKey}')");
			_outShaderData = null;
			return false;
		}

		// Read shader source code to array:
		ulong expectedBytesCount = _resHandle.dataSize != 0 ? _resHandle.dataSize : _fileHandle.dataFileSize;
		byte[] sourceCodeBytes = new byte[expectedBytesCount];
		int actualBytesCount = _stream.Read(sourceCodeBytes, 0, (int)expectedBytesCount);
		if (actualBytesCount < sourceCodeBytes.Length)
		{
			logger?.LogWarning($"Mismatch between between expected byte size and actual byte size of shader source code! (Resource key: '{_resHandle.resourceKey}')");

			byte[] actualSourceCodeBytes = new byte[actualBytesCount];
			Array.Copy(sourceCodeBytes, actualSourceCodeBytes, actualBytesCount);
			sourceCodeBytes = actualSourceCodeBytes;
		}

		// Find entry points and variants in source code:
		string entryPointNameBase = GraphicsConstants.defaultShaderStageEntryPoints[stage];

		if (!TryFindEntryPoints(
			sourceCodeBytes,
			actualBytesCount,
			entryPointNameBase,
			out Dictionary<MeshVertexDataFlags, string> variantEntryPoints,
			out MeshVertexDataFlags allVariantFlags))
		{
			logger?.LogError($"Could not find any entry points for shader '{_resHandle.resourceKey}' ({stage})!");
			_outShaderData = null;
			return false;
		}

		// Create a vague estimate of a shader configuration description: (variant flags are most important)
		ShaderConfig shaderConfig = ShaderConfig.ConfigWhiteLit;
		shaderConfig.SetVariantFlagsFromMeshVertexData(allVariantFlags);
		string descriptionText = shaderConfig.CreateDescriptionTxt();

		// Assemble shader data description object:
		var entryPoints = new ShaderDescriptionSourceCodeData.VariantEntryPoint[variantEntryPoints.Count];
		int i = 0;
		foreach (var kvp in variantEntryPoints)
		{
			entryPoints[i++] = new()
			{
				VariantFlags = kvp.Key,
				EntryPoint = kvp.Value,
			};
		}

		var sourceCodeBlocks = new ShaderDescriptionSourceCodeData.SourceCodeBlock[1]
		{
			new()
			{
				Language = language,
				ByteOffset = 0,
				ByteSize = (uint)actualBytesCount,
			},
		};

		_outShaderData = new ShaderData()
		{
			FileHeader = new ShaderDataFileHeader()
			{
				fileHeaderSize = ShaderDataFileHeader.minFileHeaderSize,
				jsonDescription = new()
				{
					byteOffset = ShaderDataFileHeader.minFileHeaderSize,
					byteSize = 0,
				},
				sourceCode = new()
				{
					byteOffset = ShaderDataFileHeader.minFileHeaderSize,
					byteSize = (uint)actualBytesCount,
				},
				shaderDataBlockCount = 0,
				shaderData = new()
				{
					byteOffset = 0,
					byteSize = 0,
				}
			},
			Description = new ShaderDescriptionData()
			{
				ShaderStage = stage,
				RequiredVariants = allVariantFlags,
				SourceCode = new ShaderDescriptionSourceCodeData()
				{
					EntryPointNameBase = entryPointNameBase,
					EntryPoints = entryPoints,
					SupportedFeaturesTxt = descriptionText,
					MaximumCompiledFeaturesTxt = descriptionText,
					SourceCodeBlocks = sourceCodeBlocks,
				},
				CompiledVariants = [],
			},
			SourceCode = new Dictionary<ShaderLanguage, byte[]>()
			{
				[language] = sourceCodeBytes,
			},
			ByteCodeDxbc = null,
			ByteCodeDxil = null,
			ByteCodeSpirv = null,
			ByteCodeMetal = null,
		};
		return _outShaderData.IsValid();
	}

	private static bool TryIdentifyShaderLanguage(string _fileExtension, out ShaderLanguage _outLanguage)
	{
		foreach (var kvp in ShaderConstants.shaderLanguageFileExtensions)
		{
			if (kvp.Value == _fileExtension)
			{
				_outLanguage = kvp.Key;
				return true;
			}
		}
		_outLanguage = 0;
		return false;
	}

	private static bool TryIdentifyShaderStage(ResourceFileHandle _fileHandle, out ShaderStages _outStage)
	{
		_outStage = ShaderStages.None;

		string? fileName = Path.GetFileNameWithoutExtension(_fileHandle.dataFilePath);
		if (string.IsNullOrEmpty(fileName))
		{
			return false;
		}

		foreach (var kvp in GraphicsConstants.shaderResourceSuffixes)
		{
			if (fileName.EndsWith(kvp.Value))
			{
				_outStage = kvp.Key;
				return true;
			}
		}
		return false;
	}

	private static bool TryFindEntryPoints(
		byte[] _sourceCodeBytes,
		int _sourceCodeBytesCount,
		string _entryPointNameBase,
		out Dictionary<MeshVertexDataFlags, string> _outVariantEntryPoints,
		out MeshVertexDataFlags _outAllVariantFlags)
	{
		_outVariantEntryPoints = new((int)MeshVertexDataFlags.ALL);
		_outAllVariantFlags = 0;

		StringBuilder variantBuilder = new(256);
		StringBuilder suffixBuilder = new(128);

		AsciiIterator e = new(_sourceCodeBytes, _sourceCodeBytesCount);
		while (e.FindNext(_entryPointNameBase).IsValid)
		{
			variantBuilder.Clear();
			variantBuilder.Append(_entryPointNameBase);
			MeshVertexDataFlags variantFlags = MeshVertexDataFlags.BasicSurfaceData;

			// Iterate over suffixes: (separated by underscores)
			while (e.Current == '_')
			{
				variantBuilder.Append('_');
				suffixBuilder.Clear();
				char c;
				while (e.MoveNext() && (c = e.Current) != '_' && c != '(' && !char.IsWhiteSpace(c) && !char.IsControl(c))
				{
					variantBuilder.Append(c);
					suffixBuilder.Append(c);
				}
				if (suffixBuilder.Length != 0 && GraphicsConstants.shaderEntryPointSuffixesForVariants.TryGetValue(suffixBuilder.ToString(), out MeshVertexDataFlags flag))
				{
					variantFlags |= flag;
				}
			}

			// Add the variant entry point to out lookup table:
			if (!_outVariantEntryPoints.ContainsKey(variantFlags))
			{
				_outVariantEntryPoints.Add(variantFlags, variantBuilder.ToString());
				_outAllVariantFlags |= variantFlags;
			}
		}

		return _outVariantEntryPoints.Count != 0;
	}

	#endregion
}
