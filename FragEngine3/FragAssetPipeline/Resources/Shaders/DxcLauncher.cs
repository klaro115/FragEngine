using FragEngine3.Graphics;
using System.Diagnostics;
using System.Text;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders;

public static class DxcLauncher
{
	#region Types

	public sealed class DxcResult(bool _isSuccess, string? _compiledCode = null)
	{
		public readonly bool isSuccess = _isSuccess;
		public readonly string compiledCode = _compiledCode ?? string.Empty;
	}

	#endregion
	#region Fields

	private static string? dxcExeAbsolutePath = null;

	#endregion
	#region Constants

	public const string dxcDirRelativePath = ThirdPartyConstants.toolsFolderRelativePath + "DXC/";
	public const string dxcExeRelativePath = dxcDirRelativePath + dxcExeName;
	public const string dxcExeName = "dxc.exe";

	public const int SHADER_MODEL_MAJOR_VERSION = 6;
	public const int SHADER_MODEL_MINOR_VERSION = 3;

	private const int DXC_COMPILER_TIMEOUT = 5000;

	#endregion
	#region Methods

	public static DxcResult CompileShaderToDXIL(string _hlslFilePath, ShaderStages _shaderStage, string? _entryPoint, string? _includeFilePath = null)
	{
		if (string.IsNullOrEmpty(_hlslFilePath))
		{
			Console.WriteLine("Error! File path to HLSL shader code may not be null or empty!");
			return new(false);
		}
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

		dxcExeAbsolutePath ??= Path.GetFullPath(dxcExeRelativePath);

		// Full command format: '<dxc_path> -T <shader_model> -E <entry_point> -I <include_path> <hlsl_path>'
		List<string> dxcArgumentList = new(5);

		// Format parameter values:
		if (!GetShaderModelParameter(_shaderStage, dxcArgumentList))
		{
			Console.WriteLine($"Error! Could not find shader model parameter for shader stage '{_shaderStage}'!");
			return new(false);
		}
		if (!GetEntryPointParameter(_entryPoint, _shaderStage, dxcArgumentList))
		{
			Console.WriteLine($"Error! Could not find entry point parameter for shader stage '{_shaderStage}'!");
			return new(false);
		}
		GetIncludePathParameter(_includeFilePath, dxcArgumentList);
		dxcArgumentList.Add($"\"{_hlslFilePath}\"");

		Process? process = null;
		StringBuilder outputBuilder = new(2048);
		try
		{
			ProcessStartInfo processInfo = new(dxcExeAbsolutePath)
			{
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				//StandardOutputEncoding = Encoding.UTF8,
			};

			// Assemble full command and log it:
			Console.WriteLine("Full command:");
			Console.Write("$    \"");
			Console.Write(dxcExeAbsolutePath);
			Console.Write('\"');
			foreach (string argument in dxcArgumentList)
			{
				processInfo.ArgumentList.Add(argument);
				Console.Write(' ');
				Console.Write(argument);
			}
			Console.WriteLine();

			// Start DXC executable via commandline:
			process = new()
			{
				StartInfo = processInfo,
			};
			process.Exited += (sender, e) => Console.WriteLine("Exited.");
			if (!process.Start())
			{
				Console.WriteLine("Error! Failed to start process for DXIL shader compilation using DXC!");
				return new(false);
			}

			// Async listeners for output and errors: (yes, this is messy, but operating DXC via CMD is needlessly hard and this is the only way to get reliable output)
			ReadErrorsAsync();
			//var task = Task.Run(async () =>
			//{
			//	string outputTxt = await process.StandardOutput.ReadToEndAsync();
			//	if (!string.IsNullOrEmpty(outputTxt))
			//	{
			//		outputBuilder.Append(outputTxt);
			//	}
			//});

			// Wait for compilation to finish: (yes, there is a WaitForExit method, but it causes DXC to block quietly and never return, so ugly thread sleep logic it is)
			while (!process.HasExited)
			{
				Thread.Sleep(10);
			}

			if (process.ExitCode != 0)
			{
				ReadErrorsAsync()?.Wait();
				Thread.Sleep(2000);
				Console.WriteLine($"Error! HLSL to DXIL shader compilation has failed! Shader: '{_hlslFilePath}' ({_shaderStage})! (Exit code: {process.ExitCode})");
				return new(false);
			}

			// Output compiled DXIL shader code:
			string finalOutput = process.StandardOutput.ReadToEnd();
			outputBuilder.Append(finalOutput);
			return new(true, outputBuilder.ToString());
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Error! An exception was caught while compiling HLSL shader to DXIL!\nShader: '{_hlslFilePath}' ({_shaderStage})\nException: {ex}");
			return new(false);
		}
		finally
		{
			process?.Close();
			process?.Dispose();
		}

		Task ReadErrorsAsync()
		{
			return Task.Run(async () =>
			{
				string errorTxt = await process.StandardError.ReadToEndAsync();
				if (!string.IsNullOrEmpty(errorTxt))
				{
					Console.WriteLine($"Error: {errorTxt}");
				}
			});
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

	private static bool GetShaderModelParameter(ShaderStages _shaderStage, List<string> _argumentList)
	{
		const char missingPrefix = '_';

		var stagePrefix = _shaderStage switch
		{
			ShaderStages.Vertex => 'v',
			ShaderStages.Geometry => 'g',
			ShaderStages.Fragment => 'p',
			ShaderStages.Compute => 'c',
			_ => missingPrefix,
		};
		if (stagePrefix == missingPrefix)
		{
			return false;
		}

		// Format:	'-T <shader_model> '
		// Example:	'-T ps_6_3 '
		_argumentList.Add($"-T {stagePrefix}s_{SHADER_MODEL_MAJOR_VERSION}_{SHADER_MODEL_MINOR_VERSION}");
		return true;
	}

	private static bool GetEntryPointParameter(string? _entryPoint, ShaderStages _shaderStage, List<string> _argumentList)
	{
		// Format:	'-E <entry_point>'
		// Example:	'-E Main_Pixel '
		if (!string.IsNullOrEmpty(_entryPoint))
		{
			_argumentList.Add($"-E {_entryPoint}");
			return true;
		}

		if (!GraphicsConstants.defaultShaderStageEntryPoints.TryGetValue(_shaderStage, out _entryPoint))
		{
			_argumentList.Add($"-E {_entryPoint}");
			return true;
		}
		return false;
	}

	private static bool GetIncludePathParameter(string? _includeFilePath, List<string> _argumentList)
	{
		if (string.IsNullOrEmpty(_includeFilePath))
		{
			return false;
		}

		// Format:	'-I <include_path> '
		// Example:	'-I "./shaders/lighting.hlsl" '
		_argumentList.Add($"-I \"{_includeFilePath}\"");
		return true;
	}

	#endregion
}
