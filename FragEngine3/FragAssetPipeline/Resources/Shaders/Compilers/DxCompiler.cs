using System.Text;
using FragAssetPipeline.Resources.Shaders.FSHA;
using FragEngine3.Utility.Unicode;
using Veldrid;
using Vortice.Dxc;

namespace FragAssetPipeline.Resources.Shaders.Compilers;

public static class DxCompiler
{
	#region Types

	public sealed class DxcResult(bool _isSuccess, byte[]? _compiledShader = null)
	{
		public readonly bool isSuccess = _isSuccess;
		public readonly byte[] compiledShader = _compiledShader ?? [];

		public static DxcResult Failure => new(false, []);
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

	private static readonly byte[] magicNumbersDXBC = [ (byte)'D', (byte)'X', (byte)'B', (byte)'C' ];
	private static readonly byte[] magicNumbersDXIL = [ (byte)'D', (byte)'X', (byte)'I', (byte)'L' ];

	#endregion
	#region Constants

	public const int SHADER_MODEL_MAJOR_VERSION = 6;
	public const int SHADER_MODEL_MINOR_VERSION = 3;

	#endregion
	#region Methods

	/// <summary>
	/// Checks if D3D shader compilation is supported and implemented on the current executing platform.
	/// </summary>
	/// <remarks>The compiler uses dxcompiler.dll, which is a Windows/Direct3D dependency, and thus not available on MacOS or Linux.</remarks>
	/// <returns>True if the exporter will run, false if it is not supported or not implemented.</returns>
	public static bool IsAvailableOnCurrentPlatform()
	{
		return OperatingSystem.IsWindows();
	}

	public static bool CompileShaderToDXBCAndDXIL(string _hlslFilePath, ShaderStages _shaderStage, string? _entryPoint, out DxcResult _outDxbc, out DxcResult _outDxil)
	{
		_outDxbc = DxcResult.Failure;
		_outDxil = DxcResult.Failure;

		DxcResult combinedResult = CompileShader(_hlslFilePath, _shaderStage, _entryPoint, compilerOptionsDXBC);
		if (!combinedResult.isSuccess)
		{
			return false;
		}

		bool hasDxbc = TryFindAsciiStringInByteArray(combinedResult.compiledShader, "DXBC", 0, out int startIdxDxbc);
		bool hasDxil = TryFindAsciiStringInByteArray(combinedResult.compiledShader, "DXIL", Math.Max(startIdxDxbc, 0), out int startIdxDxil);

		if (hasDxbc)
		{
			int length = hasDxil
				? startIdxDxil - startIdxDxbc
				: combinedResult.compiledShader.Length;

			byte[] compiledShaderDxbc = new byte[length];
			Array.Copy(combinedResult.compiledShader, startIdxDxbc, compiledShaderDxbc, 0, length);

			_outDxbc = new(true, compiledShaderDxbc);
		}
		if (hasDxil)
		{
			int length = combinedResult.compiledShader.Length - startIdxDxil;

			byte[] compiledShaderDxil = new byte[length];
			Array.Copy(combinedResult.compiledShader, startIdxDxil, compiledShaderDxil, 0, length);

			_outDxil = new(true, compiledShaderDxil);
		}

		return _outDxbc.isSuccess || _outDxil.isSuccess;
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
			return DxcResult.Failure;
		}
		if (_options is null)
		{
			Console.WriteLine("Error! Cannot compile shader from HLSL code using null compiler options!");
			return DxcResult.Failure;
		}

		// Paramneter value and fallbacks:
		if (_shaderStage == ShaderStages.None && !FshaExportUtility.GetShaderStageFromFileNameSuffix(_hlslFilePath, out _shaderStage))
		{
			Console.WriteLine($"Error! Could not determine shader stage of HLSL shader! File path: '{_hlslFilePath}'");
			return DxcResult.Failure;
		}
		if (!File.Exists(_hlslFilePath))
		{
			string hlslFileAbsPath = Path.GetFullPath(_hlslFilePath);
			if (!File.Exists(hlslFileAbsPath))
			{
				Console.WriteLine($"Error! HLSL shader file at path '{_hlslFilePath}' does not exist!");
				return DxcResult.Failure;
			}
			_hlslFilePath = hlslFileAbsPath;
		}
		if (!FshaExportUtility.GetDefaultEntryPoint(ref _entryPoint, _shaderStage) || _entryPoint is null)
		{
			Console.WriteLine($"Error! Could not find entry point parameter for shader stage '{_shaderStage}'!");
			return DxcResult.Failure;
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
			return DxcResult.Failure;
		}

		DxcShaderStage dxStage = GetDxShaderStage(_shaderStage);

		try
		{
			using var results = DxcCompiler.Compile(dxStage, hlslCode, _entryPoint, _options);

			// Check for errors:
			using var errorBlob = results.GetOutput(DxcOutKind.Errors);
			if (errorBlob is not null && errorBlob.AsSpan().Length > 0)
			{
				string errorTxt = Encoding.UTF8.GetString(errorBlob.AsSpan());
				Console.WriteLine($"Error! Failed to compile HLSL shader variant!\nFile path: '{_hlslFilePath}'\nError output: '{errorTxt}'");
				return DxcResult.Failure;
			}

			// Get compiled shader:
			using var shaderBlob = results.GetOutput(DxcOutKind.Object);
			if (shaderBlob is null || shaderBlob.AsSpan().Length == 0)
			{
				Console.WriteLine($"Error! Failed to compile HLSL shader variant; output was empty!\nFile path: '{_hlslFilePath}'");
				return DxcResult.Failure;
			}

			// Return success:
			byte[] shaderBytes = shaderBlob.AsBytes();
			return new(true, shaderBytes);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error! Failed to compile HLSL shader variant!\nFile path: '{_hlslFilePath}'\nException: {ex}");
			return DxcResult.Failure;
		}
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

	private static bool TryFindAsciiStringInByteArray(byte[] _bytes, string _query, int _startIdx, out int _outResultIdx)
	{
		for (int i = _startIdx; i < _bytes.Length; i++)
		{
			int j;
			for (j = 0; j < _query.Length; ++j)
			{
				byte a = _bytes[i + j];
				byte b = (byte)_query[j];
				if (a != b)
				{
					break;
				}
			}
			if (j >= _query.Length)
			{
				_outResultIdx = i;
				return true;
			}
		}

		_outResultIdx = -1;
		return false;
	}

	#endregion
}
