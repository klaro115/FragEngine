using FragEngine3.EngineCore;
using FragEngine3.Resources;
using FragEngine3.Utility.Unicode;
using System.Text;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public static class ShaderResourceFactory
	{
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
			foreach (var kvp in GraphicsContants.shaderResourceSuffixes)
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
			if (!GraphicsContants.defaultShaderStageEntryPoints.TryGetValue(_stage, out string? entryPoint))
			{
				Logger logger = _graphicsCore?.graphicsSystem.engine.Logger ?? Logger.Instance!;
				logger.LogError($"Could not determine entry point name for shader stage '{_stage}'!");
				_outShaderRes = null;
				return false;
			}

			// Create the shader using the standard method:
			return CreateShader(_handle, _graphicsCore, _stage, entryPoint, out _outShaderRes);
		}

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
			if (!fileHandle.TryReadResourceBytes(_handle, out byte[] bytes, out int byteCount))
			{
				logger.LogError($"Failed to read shader code for resource '{_handle}'!");
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
				Utf16Iterator e = new(bytes, byteCount);
				Utf16Iterator.Position pos;

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
						if (suffixBuilder.Length != 0 && GraphicsContants.shaderEntryPointSuffixesForVariants.TryGetValue((txt = suffixBuilder.ToString()), out MeshVertexDataFlags flag))
						{
							variantFlags |= flag;
						}
					}

					// Add the variant entry point to out lookup table:
					if (!variantEntryPoints.ContainsKey(variantFlags))
					{
						variantEntryPoints.Add(variantFlags, variantBuilder.ToString());
						maxVariantIndex = Math.Max(maxVariantIndex, (int)variantFlags - 1);
					}
				}
			}
			catch (Exception ex)
			{
				logger.LogException($"Failed to read variant entry points for shader '{_handle.resourceKey}' ({_stage})!", ex);
				_outShaderRes = null;
				return false;
			}

			if (maxVariantIndex < 0)
			{
				logger.LogError($"Could not find any entry points for shader '{_handle.resourceKey}' ({_stage})!");
				_outShaderRes = null;
				return false;
			}

			// Try compiling shader:
			Shader?[] shaderVariants = new Shader[maxVariantIndex + 1];
			int shadersCompiledCount = 0;
			for (int i = 0; i < shaderVariants.Length; ++i)
			{
				MeshVertexDataFlags variantFlags = (MeshVertexDataFlags)(i + 1);
				if (variantEntryPoints.TryGetValue(variantFlags, out string? variantEntryPoint))
				{
					// Try compiling shader for each variant:
					Shader? shader = null;
					try
					{
						ShaderDescription shaderDesc = new(_stage, bytes, variantEntryPoint);

						shader = _graphicsCore.MainFactory.CreateShader(ref shaderDesc);
					}
					catch (Exception ex)
					{
						logger.LogException($"Failed to compile variant '{variantFlags}' for shader '{_handle.resourceKey}' ({_stage})!", ex);
						shader?.Dispose();
						continue;
					}

					shaderVariants[i] = shader;
					shadersCompiledCount++;
				}
			}
			if (shadersCompiledCount == 0)
			{
				logger.LogError($"All variants of shader '{_handle.resourceKey}' ({_stage}) have failed to compile! Shader resource may be broken or incomplete!");
				_outShaderRes = null;
				return false;
			}

			// Output finished shader resource:
			_outShaderRes = new(_handle, _graphicsCore, shaderVariants, _stage);

			return _outShaderRes.IsLoaded;
		}

		#endregion
	}
}
