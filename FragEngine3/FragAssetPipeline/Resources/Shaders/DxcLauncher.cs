using FragEngine3.Graphics;
using System.Text;
using Veldrid;
using Vortice.Dxc;

namespace FragAssetPipeline.Resources.Shaders;

public static class DxcLauncher
{
	#region Types

	public sealed class DxcResult(bool _isSuccess, byte[]? _compiledShader = null)
	{
		public readonly bool isSuccess = _isSuccess;
		public readonly byte[] compiledShader = _compiledShader ?? [];
	}

	#endregion
	#region Fields

	private static readonly DxcCompilerOptions compilerOptionsDXBC = new()
	{
		// General:
		ShaderModel = new DxcShaderModel(SHADER_MODEL_MAJOR_VERSION, SHADER_MODEL_MINOR_VERSION),

		// SPIR-V:
		GenerateSpirv = false,
	};
	private static readonly DxcCompilerOptions compilerOptionsSPIRV = new()
	{
		// General:
		ShaderModel = new DxcShaderModel(SHADER_MODEL_MAJOR_VERSION, SHADER_MODEL_MINOR_VERSION),

		// SPIR-V:
		GenerateSpirv = true,
		VkUseDXLayout = true,
		VkUseDXPositionW = true,
	};

	#endregion
	#region Constants

	public const int SHADER_MODEL_MAJOR_VERSION = 6;
	public const int SHADER_MODEL_MINOR_VERSION = 3;

	#endregion
	#region Methods

	public static DxcResult CompileShaderToDXBC(string _hlslFilePath, ShaderStages _shaderStage, string? _entryPoint)
	{
		return CompileShader(_hlslFilePath, _shaderStage, _entryPoint, compilerOptionsDXBC);
	}

	public static DxcResult CompileShaderToSPIRV(string _hlslFilePath, ShaderStages _shaderStage, string? _entryPoint)
	{
		return CompileShader(_hlslFilePath, _shaderStage, _entryPoint, compilerOptionsSPIRV);
	}

	public static DxcResult CompileShader(string _hlslFilePath, ShaderStages _shaderStage, string? _entryPoint, DxcCompilerOptions _options)
	{
		// Input null checks:
		if (string.IsNullOrEmpty(_hlslFilePath))
		{
			Console.WriteLine("Error! File path to HLSL shader code may not be null or empty!");
			return new(false);
		}
		if (_options is null)
		{
			Console.WriteLine("Error! Cannot compile shader from HLSL code using null compiler options!");
			return new(false);
		}

		// Paramneter value and fallbacks:
		if (_shaderStage == ShaderStages.None && !TryGetShaderStageFromFileName(_hlslFilePath, out _shaderStage))
		{
			Console.WriteLine($"Error! Could not determine shader stage of HLSL shader! File path: '{_hlslFilePath}'");
			return new(false);
		}
		if (!File.Exists(_hlslFilePath))
		{
			string hlslFileAbsPath = Path.GetFullPath(_hlslFilePath);
			if (!File.Exists(hlslFileAbsPath))
			{
				Console.WriteLine($"Error! HLSL shader file at path '{_hlslFilePath}' does not exist!");
				return new(false);
			}
			_hlslFilePath = hlslFileAbsPath;
		}
		if (!GetEntryPointParameter(ref _entryPoint, _shaderStage) || _entryPoint is null)
		{
			Console.WriteLine($"Error! Could not find entry point parameter for shader stage '{_shaderStage}'!");
			return new(false);
		}

		// Read HLSL shader code from file:
		string hlslCode;
		try
		{
			hlslCode = File.ReadAllText(_hlslFilePath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error! Failed to read HLSL shader file at path '{_hlslFilePath}'!\nException: {ex}");
			return new(false);
		}

		DxcShaderStage dxStage = GetDxShaderStage(_shaderStage);

		try
		{
			using var results = DxcCompiler.Compile(dxStage, hlslCode, _entryPoint, compilerOptionsDXBC);

			// Check for errors:
			using var errorBlob = results.GetOutput(DxcOutKind.Errors);
			if (errorBlob is not null && errorBlob.BufferSize > 0)
			{
				string errorTxt = Encoding.UTF8.GetString(errorBlob.AsSpan());
				Console.WriteLine($"Error! Failed to compile HLSL shader code to DXBC!\nFile path: '{_hlslFilePath}'\nError output: '{errorTxt}'");
				return new(false);
			}

			// Get compiled shader:
			using var shaderBlob = results.GetOutput(DxcOutKind.Object);
			if (shaderBlob is null || shaderBlob.BufferSize == 0)
			{
				Console.WriteLine($"Error! Failed to compile HLSL shader code to DXBC; output was empty!\nFile path: '{_hlslFilePath}'");
				return new(false);
			}

			// Return success:
			byte[] shaderBytes = shaderBlob.AsBytes();
			return new(true, shaderBytes);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error! Failed to compile HLSL shader to DXBC!\nFile path: '{_hlslFilePath}'\nException: {ex}");
			return new(false);
		}
	}

	private static bool TryGetShaderStageFromFileName(string _hlslFilePath, out ShaderStages _outShaderStage)
	{
		string fileName = Path.GetFileNameWithoutExtension(_hlslFilePath);
		if (string.IsNullOrEmpty(fileName))
		{
			_outShaderStage = ShaderStages.None;
			return false;
		}

		foreach (var kvp in GraphicsConstants.shaderResourceSuffixes)
		{
			if (fileName.EndsWith(kvp.Value))
			{
				_outShaderStage = kvp.Key;
				return true;
			}
		}
		_outShaderStage = ShaderStages.None;
		return false;
	}

	private static bool GetEntryPointParameter(ref string? _entryPoint, ShaderStages _shaderStage)
	{
		if (!string.IsNullOrEmpty(_entryPoint))
		{
			return true;
		}

		return GraphicsConstants.defaultShaderStageEntryPoints.TryGetValue(_shaderStage, out _entryPoint);
	}

	private static DxcShaderStage GetDxShaderStage(ShaderStages _shaderStage)
	{
		return _shaderStage switch
		{
			ShaderStages.Vertex => DxcShaderStage.Vertex,
			ShaderStages.Geometry => DxcShaderStage.Geometry,
			ShaderStages.TessellationControl => DxcShaderStage.Domain,	// untested
			ShaderStages.TessellationEvaluation => DxcShaderStage.Hull,	// untested
			ShaderStages.Fragment => DxcShaderStage.Pixel,
			ShaderStages.Compute => DxcShaderStage.Compute,
			_ => 0,
		};
	}

	#endregion
}
