using System.Text;
using FragEngine3.Resources;
using FragEngine3.Utility;
using FragEngine3.Utility.Unicode;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	public sealed class ShaderResource : Resource
	{
		#region Constructors

		private ShaderResource(ResourceHandle _handle, GraphicsCore _graphicsCore) : base(_handle)
		{
			graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Material's graphics core may not be null!");
		}

		#endregion
		#region Fields

		private MeshVertexDataFlags[] vertexVariants = { MeshVertexDataFlags.BasicSurfaceData };
		private Shader?[] shaderVariants = Array.Empty<Shader?>();

		#endregion
		#region Properties

		public readonly GraphicsCore graphicsCore;

		public ShaderStages Stage { get; private set; } = ShaderStages.None;

		public int VertexVariantCount => vertexVariants != null ? vertexVariants.Length : 0;
		
		public override ResourceType ResourceType => ResourceType.Shader;

		#endregion
		#region Methods

		protected override void Dispose(bool _disposing)
		{
			IsDisposed = false;

			if (shaderVariants != null)
			{
				for (int i = 0; i < shaderVariants.Length; ++i)
				{
					shaderVariants[i]?.Dispose();
					shaderVariants[i] = null;
				}
			}
			if (_disposing)
			{
				vertexVariants = Array.Empty<MeshVertexDataFlags>();
				shaderVariants = Array.Empty<Shader>();
				Stage = ShaderStages.None;
			}
		}

		/// <summary>
		/// Check whether a variant of the shader exists for a specific vertex definition.
		/// </summary>
		/// <param name="_variantFlags">Flags describing the vertex definitions of a mesh.<para/>
		/// NOTE: At least '<see cref="MeshVertexDataFlags.BasicSurfaceData"/>' must be raised for any surface shader.</param>
		/// <returns>True if this shader resource has a program for the given variant, false otherwise.</returns>
		public bool HasVariant(MeshVertexDataFlags _variantFlags)
		{
			for (int i = 0; i < vertexVariants.Length; ++i)
			{
				if (vertexVariants[i].HasFlag(_variantFlags))
				{
					return true;
				}
			}
			return false;
		}

		/// <summary>
		/// Gets the vertex definition flags for a specific variant that is supported by this shader's programs.
		/// </summary>
		/// <param name="_variantIndex">Index of the variant in question. Must be between 0 and '<see cref="VertexVariantCount"/>'.</param>
		/// <param name="_outVariantFlags">Outputs the mesh vertex definition flags for this variant.</param>
		/// <returns>True if the variant index and flags were valid, false otherwise.</returns>
		public bool GetVariantVertexDataFlags(int _variantIndex, out MeshVertexDataFlags _outVariantFlags)
		{
			if (_variantIndex >= 0 && _variantIndex < VertexVariantCount)
			{
				_outVariantFlags = vertexVariants[_variantIndex];
				return _outVariantFlags.HasFlag(MeshVertexDataFlags.BasicSurfaceData);
			}
			_outVariantFlags = 0;
			return false;
		}

		/// <summary>
		/// Get the shader program corresponding to a specific vertex definition.
		/// </summary>
		/// <param name="_variantFlags">Flags describing the vertex definitions of a mesh.<para/>
		/// <param name="_outShader">Outputs the corresponding shader program, or null, if no such variant exists.</param>
		/// <returns>True if the shader variant exists, false otherwise.</returns>
		public bool GetShaderProgram(MeshVertexDataFlags _variantFlags, out Shader? _outShader)
		{
			int variantIdx = (int)_variantFlags - 1;
			if (variantIdx >= 0 && variantIdx < shaderVariants.Length)
			{
				_outShader = shaderVariants[variantIdx];
				return _outShader != null;
			}
			_outShader = null;
			return false;
		}

		public override IEnumerator<ResourceHandle> GetResourceDependencies()
		{
			if (!IsDisposed && GetResourceHandle(out ResourceHandle handle))
			{
				yield return handle;
			}
		}

		public static bool CreateShader(
			ResourceHandle _handle,
			GraphicsCore _graphicsCore,
			out ShaderResource? _outShaderRes)
		{
			// Verify parameters and system states:
			if (_handle == null || !_handle.IsValid)
			{
				Console.WriteLine("Error! Cannot create shader resource from null or invalid resource handle!");
				_outShaderRes = null;
				return false;
			}

			// Try to extrapolate the shader stage from the resource key's suffix: (ex.: Vertex = '_VS')
			ShaderStages stage = ShaderStages.None;
			foreach (var kvp in GraphicsContants.shaderResourceSuffixes)
			{
				if (_handle.Key.EndsWith(kvp.Value))
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
				Console.WriteLine($"Error! Could not determine entry point name for shader stage '{_stage}'!");
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
				Console.WriteLine("Error! Cannot create shader resource from null or invalid resource handle!");
				_outShaderRes = null;
				return false;
			}
			if (_handle.resourceManager == null || _handle.resourceManager.IsDisposed)
			{
				Console.WriteLine("Error! Cannot create shader resource using null or disposed resource manager!");
				_outShaderRes = null;
				return false;
			}
			if (_graphicsCore == null || !_graphicsCore.IsInitialized)
			{
				Console.WriteLine("Error! Cannot create shader resource using null or uninitialized graphics core!");
				_outShaderRes = null;
				return false;
			}
			if (_stage == ShaderStages.None)
			{
				Console.WriteLine("Error! Cannot creste shader resource for unknown stage!");
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
			if (!_handle.resourceManager.GetFileWithResource(_handle.Key, out ResourceFileHandle? fileHandle) || fileHandle == null)
			{
				Console.WriteLine($"Error! Could not find source file for resource handle '{_handle}'!");
				_outShaderRes = null;
				return false;
			}

			// Try reading raw byte data from file:
			if (!fileHandle.TryReadResourceBytes(_handle, out byte[] bytes))
			{
				Console.WriteLine($"Error! Failed to read shader code for resource '{_handle}'!");
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
				Utf16Iterator e = new(bytes, bytes.Length);
				Utf16Iterator.Position pos;

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
				Console.WriteLine($"Error! Failed to read variant entry points for shader '{_handle.Key}' ({_stage})!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outShaderRes = null;
				return false;
			}

			if (maxVariantIndex < 0)
			{
				Console.WriteLine($"Error! Could not find any entry points for shader '{_handle.Key}' ({_stage})!");
				_outShaderRes = null;
				return false;
			}

			// Try compiling shader:
			Shader?[] shaderVariants = new Shader[maxVariantIndex + 1];
			for (int i = 0; i < shaderVariants.Length; ++i)
			{
				MeshVertexDataFlags variantFlags = (MeshVertexDataFlags)(i + 1);
				if (variantEntryPoints.ContainsKey(variantFlags))
				{
					//TODO: Move the shader compilation from below here! We'll be compiling once for each variant, rather than just one program!
				}
			}

			// Try compiling shader:	[OLD]
			Shader shader;
			try
			{
				ShaderDescription shaderDesc = new(_stage, bytes, _entryPoint);

				shader = _graphicsCore.MainFactory.CreateShader(ref shaderDesc);
			}
			catch (Exception ex)
			{
				Console.WriteLine($"Error! Failed to compile shader '{_handle.Key}' ({_stage})!\nException type: '{ex.GetType()}'\nException message: '{ex.Message}'");
				_outShaderRes = null;
				return false;
			}

			// Output finished shader resource:
			_outShaderRes = new(_handle, _graphicsCore)
			{
				shaderVariants = shaderVariants,
				Stage = _stage,
			};
			return _outShaderRes.IsLoaded;
		}

		#endregion
	}
}
