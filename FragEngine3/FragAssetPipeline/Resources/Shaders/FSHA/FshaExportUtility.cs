using FragEngine3.Graphics;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders.FSHA;

/// <summary>
/// Helper class for FSHA shader export.
/// </summary>
internal static class FshaExportUtility
{
	#region Methods
	
	public static bool CheckIfFileExists(string _filePath)
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

		string? fileName = Path.GetFileNameWithoutExtension(_filePath);
		if (string.IsNullOrEmpty(fileName))
		{
			_outShaderStage = ShaderStages.None;
			return false;
		}

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

	public static bool GetDefaultEntryPoint(ref string? _entryPoint, ShaderStages _shaderStage)
	{
		if (!string.IsNullOrEmpty(_entryPoint))
		{
			return true;
		}

		return GraphicsConstants.defaultShaderStageEntryPoints.TryGetValue(_shaderStage, out _entryPoint);
	}

	#endregion
}
