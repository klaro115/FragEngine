using FragEngine3.EngineCore;
using FragEngine3.Graphics.Resources.ShaderGen;
using FragEngine3.Resources;
using FragEngine3.Utility.Unicode;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources;

/// <summary>
/// Factory class for importing and compiling GPU shader programs.
/// </summary>
public static class ShaderResourceFactory
{
	#region Constants

	private const string shaderGenFlagsStart = $"{DefaultShaderConstants.shaderGenImportFlag}='";

	#endregion
	#region Methods

	public static bool CreateShader(
		ResourceHandle _handle,
		GraphicsCore _graphicsCore,
		out ShaderResource? _outShaderRes)
	{
		// Verify parameters and system states:
		if (_handle == null || !_handle.IsValid)
		{
			_graphicsCore.graphicsSystem.engine.Logger.LogError("Cannot create shader resource from null or invalid resource handle!");
			_outShaderRes = null;
			return false;
		}

		// Try to extrapolate the shader stage from the resource key's suffix: (ex.: Vertex = '_VS')
		ShaderStages stage = ShaderStages.None;
		foreach (var kvp in GraphicsConstants.shaderResourceSuffixes)
		{
			if (_handle.resourceKey.EndsWith(kvp.Value))
			{
				stage = kvp.Key;
				break;
			}
		}

		// Create the shader using the standard method:
		return CreateShader(_handle, _graphicsCore, stage, out _outShaderRes);
	}

	public static bool CreateShader(
		ResourceHandle _handle,
		GraphicsCore _graphicsCore,
		ShaderStages _stage,
		out ShaderResource? _outShaderRes)
	{
		// Determine standard entry point function name based on shader stage:
		if (!GraphicsConstants.defaultShaderStageEntryPoints.TryGetValue(_stage, out string? entryPoint))
		{
			Logger logger = _graphicsCore?.graphicsSystem.engine.Logger ?? Logger.Instance!;
			logger.LogError($"Could not determine entry point name for shader stage '{_stage}'!");
			_outShaderRes = null;
			return false;
		}

		// Create the shader using the standard method:
		return CreateShader(_handle, _graphicsCore, _stage, entryPoint, out _outShaderRes);
	}

	/// <summary>
	/// Import and compile a new shader program from file. This method will load shader code from
	/// its resource data file, compile it, and then upload it to the GPU for rendering or compute.
	/// </summary>
	/// <param name="_handle">A resource handle pointing to the shader's source code file, may not
	/// be null.</param>
	/// <param name="_graphicsCore">The graphics core that wraps the graphics device for which the
	/// shader is created. May not be null, must have been initialized.</param>
	/// <param name="_stage">The shader stage that this shader program will be used for. A shader
	/// program cannot be used for any other stage than the one it was compiled for. Depending on
	/// graphics API, the code for multiple different stages may be contained within a same code
	/// file, differentiated by their respective entry point functions.</param>
	/// <param name="_entryPoint">The name of the shader program's entry point function. This name
	/// is treated as a name stem, where suffixes are used to identify variants of a same shader.
	/// Multiple suffixes may be added to each variant entry point name, separated by underscores.<para/>
	/// For example, the "_Ext" suffix indicates that a vertex shader expects extended surface data
	/// contained within an additional vertex buffer.</param>
	/// <param name="_outShaderRes">Outputs a shader resource created from compiling the shader code.
	/// A shader resource is specific to one shader stage, and may contain multiple variants. Null
	/// if import or compilation fail.</param>
	/// <returns>True if the shader resource was created successfully, false if import or compilation
	/// failed.</returns>
	public static bool CreateShader(
		ResourceHandle _handle,
		GraphicsCore _graphicsCore,
		ShaderStages _stage,
		string _entryPoint,
		out ShaderResource? _outShaderRes)
	{
		// Verify parameters and system states:
		if (_handle == null || !_handle.IsValid)
		{
			Logger.Instance?.LogError("Cannot create shader resource from null or invalid resource handle!");
			_outShaderRes = null;
			return false;
		}
		if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
		{
			Logger.Instance?.LogError("Cannot create shader resource using null or disposed resource manager!");
			_outShaderRes = null;
			return false;
		}
		if (_graphicsCore == null || !_graphicsCore.IsInitialized)
		{
			Logger.Instance?.LogError("Cannot create shader resource using null or uninitialized graphics core!");
			_outShaderRes = null;
			return false;
		}

		Logger logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

		if (_stage == ShaderStages.None)
		{
			logger.LogError("Cannot creste shader resource for unknown stage!");
			_outShaderRes = null;
			return false;
		}

		// Don't do anything if the resource has already been loaded:
		if (_handle.IsLoaded)
		{
			_outShaderRes = _handle.GetResource(false, false) as ShaderResource;
			return true;
		}

		// Check if import flags contain a ShaderGen flag:
		int shaderGenFlagsIdx = !string.IsNullOrEmpty(_handle.importFlags)
			? _handle.importFlags.IndexOf(shaderGenFlagsStart)
			: -1;

		// ShaderGen flag present? Create shader code procedurally:
		if (shaderGenFlagsIdx >= 0)
		{
			return CreateShaderFromShaderGen(
				_graphicsCore,
				_handle.resourceKey,
				_stage,
				_entryPoint,
				_handle.importFlags!,
				shaderGenFlagsIdx,
				out _outShaderRes);
		}
		// Normal imported shader? Load shader code from resource file:
		else
		{
			return CreateShaderFromFile(
				_handle,
				_graphicsCore,
				_stage,
				_entryPoint,
				out _outShaderRes);
		}
	}

	private static bool CreateShaderFromFile(
		ResourceHandle _handle,
		GraphicsCore _graphicsCore,
		ShaderStages _stage,
		string _entryPoint,
		out ShaderResource? _outShaderRes)
	{
		Logger logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

		// Retrieve the file that this resource is loaded from:
		if (!_handle.resourceManager.GetFile(_handle.fileKey, out ResourceFileHandle fileHandle))
		{
			if (!_handle.resourceManager.GetFileWithResource(_handle.resourceKey, out fileHandle) || fileHandle == null)
			{
				logger.LogError($"Could not find source file for resource handle '{_handle}'!");
				_outShaderRes = null;
				return false;
			}
		}

		// Try reading raw byte data from file:
		if (!fileHandle.TryReadResourceBytes(_graphicsCore.graphicsSystem, _handle, out byte[] bytes, out int byteCount))
		{
			logger.LogError($"Failed to read shader code for resource '{_handle}'!");
			_outShaderRes = null;
			return false;
		}

		// Compile shader from UTF-8 code bytes:
		return CreateShaderFromCodeBytes(
			_graphicsCore,
			_handle.resourceKey,
			bytes,
			byteCount,
			_stage,
			_entryPoint,
			out _outShaderRes);
	}

	private static bool CreateShaderFromShaderGen(
		GraphicsCore _graphicsCore,
		string _resourceKey,
		ShaderStages _stage,
		string _entryPoint,
		string _importFlags,
		int _importStartIdx,
		out ShaderResource? _outShaderRes)
	{
		Logger logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

		// Extract configuration description text from import flags string:
		if (_importFlags[0] == DefaultShaderConstants.shaderGenImportFlag[0])
		{
			_importStartIdx += shaderGenFlagsStart.Length;
		}
		int shaderGenEndIdx = _importFlags.IndexOf('\'', _importStartIdx + shaderGenFlagsStart.Length);
		if (shaderGenEndIdx <= _importStartIdx + 1)
		{
			logger.LogError($"Failed to locate ShaderGen configuration description in import flags of resource '{_resourceKey}'!");
			_outShaderRes = null;
			return false;
		}

		int configTxtLength = shaderGenEndIdx - _importStartIdx;
		string configDescTxt = _importFlags.Substring(_importStartIdx, configTxtLength);

		// Try to parse the configuration string describing the required shader features:
		if (!DefaultShaderConfig.TryParseDescriptionTxt(configDescTxt, out DefaultShaderConfig config))
		{
			logger.LogError($"Failed to parse ShaderGen configuration description for resource '{_resourceKey}'!");
			_outShaderRes = null;
			return false;
		}

		// Try to generate shader code from parsed configuration:
		EnginePlatformFlag platformFlags = _graphicsCore.graphicsSystem.engine.PlatformSystem.PlatformFlags;
		if (!DefaultShaderBuilder.CreatePixelShaderVariation(in config, platformFlags, out string shaderCode))
		{
			logger.LogError($"Failed to generate default shader code resource '{_resourceKey}' using config '{configDescTxt}'!");
			_outShaderRes = null;
			return false;
		}

		byte[] shaderCodeBytes = new byte[shaderCode.Length + 1];
		for (int i = 0; i < shaderCode.Length; i++)
		{
			shaderCodeBytes[i] = (byte)shaderCode[i];
		}
		shaderCodeBytes[^1] = (byte)'\0';

		// Compile shader from UTF-8 code bytes:
		return CreateShaderFromCodeBytes(
			_graphicsCore,
			_resourceKey,
			shaderCodeBytes,
			shaderCodeBytes.Length,
			_stage,
			_entryPoint,
			out _outShaderRes);
	}

	public static bool CreateShaderFromCodeBytes(
		GraphicsCore _graphicsCore,
		string _resourceKey,
		byte[] _shaderCodeBytes,
		int _shaderCodeLength,
		ShaderStages _stage,
		string _entryPoint,
		out ShaderResource? _outShaderRes)
	{
		if (_graphicsCore == null || !_graphicsCore.IsInitialized)
		{
			Logger.Instance?.LogError("Cannot create shader resource using null or uninitialized graphics core!");
			_outShaderRes = null;
			return false;
		}
		Logger logger = _graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance!;

		if (_shaderCodeBytes == null || _shaderCodeBytes.Length == 0 || _shaderCodeBytes[0] == '\0')
		{
			logger.LogError("Cannot create shader resource from null or empty shader code!");
			_outShaderRes = null;
			return false;
		}

		// Find all variant entry points:
		Dictionary<MeshVertexDataFlags, string> variantEntryPoints = new((int)MeshVertexDataFlags.ALL);
		int maxVariantIndex = -1;
		try
		{
			StringBuilder variantBuilder = new(256);
			StringBuilder suffixBuilder = new(128);
			Utf8Iterator e = new(_shaderCodeBytes, _shaderCodeLength);
			Utf8Iterator.Position pos;

			e.MoveNext();

			// Find next entry point:
			while ((pos = e.FindNext(_entryPoint)).IsValid)
			{
				variantBuilder.Clear();
				variantBuilder.Append(_entryPoint);
				MeshVertexDataFlags variantFlags = MeshVertexDataFlags.BasicSurfaceData;

				// Iterate over suffixes: (separated by underscores)
				while (e.Current == '_')
				{
					variantBuilder.Append('_');
					suffixBuilder.Clear();
					char c;
					string txt;
					while (e.MoveNext() && (c = e.Current) != '_' && c != '(' && !char.IsWhiteSpace(c) && !char.IsControl(c))
					{
						variantBuilder.Append(c);
						suffixBuilder.Append(c);
					}
					if (suffixBuilder.Length != 0 && GraphicsConstants.shaderEntryPointSuffixesForVariants.TryGetValue((txt = suffixBuilder.ToString()), out MeshVertexDataFlags flag))
					{
						variantFlags |= flag;
					}
				}

				// Add the variant entry point to out lookup table:
				if (!variantEntryPoints.ContainsKey(variantFlags))
				{
					variantEntryPoints.Add(variantFlags, variantBuilder.ToString());
					maxVariantIndex = Math.Max(maxVariantIndex, (int)variantFlags.GetVariantIndex());
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogException($"Failed to read variant entry points for shader '{_resourceKey}' ({_stage})!", ex);
			_outShaderRes = null;
			return false;
		}

		if (maxVariantIndex < 0)
		{
			logger.LogError($"Could not find any entry points for shader '{_resourceKey}' ({_stage})!");
			_outShaderRes = null;
			return false;
		}

		// Try compiling shader:
		Shader?[] shaderVariants = new Shader[maxVariantIndex + 1];
		int shadersCompiledCount = 0;
		for (uint i = 0; i < shaderVariants.Length; ++i)
		{
			MeshVertexDataFlags variantFlags = MeshVertexDataFlagsExt.GetFlagsFromVariantIndex(i);
			if (variantEntryPoints.TryGetValue(variantFlags, out string? variantEntryPoint))
			{
				// Try compiling shader for each variant:
				Shader? shader = null;
				try
				{
					ShaderDescription shaderDesc = new(_stage, _shaderCodeBytes, variantEntryPoint);

					shader = _graphicsCore.MainFactory.CreateShader(ref shaderDesc);
					shader.Name = $"Shader_{_resourceKey}_{_stage}_{variantFlags}";
				}
				catch (Exception ex)
				{
					logger.LogException($"Failed to compile variant '{variantFlags}' for shader '{_resourceKey}' ({_stage})!", ex);
					shader?.Dispose();
					continue;
				}

				shaderVariants[i] = shader;
				shadersCompiledCount++;
			}
		}
		if (shadersCompiledCount == 0)
		{
			logger.LogError($"All variants of shader '{_resourceKey}' ({_stage}) have failed to compile! Shader resource may be broken or incomplete!");
			_outShaderRes = null;
			return false;
		}

		// Output finished shader resource:
		_outShaderRes = new(_resourceKey, _graphicsCore, shaderVariants, _stage);

		return _outShaderRes.IsLoaded;
	}

	#endregion
}
