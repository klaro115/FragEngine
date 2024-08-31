using FragEngine3.Graphics;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders.FSHA;
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
