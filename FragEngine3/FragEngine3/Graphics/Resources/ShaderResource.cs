using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources
{
	/// <summary>
	/// A graphics resource representing all vertex variants of a same GPU shader program.<para/>
	/// IMPORT: Each shader resource depicts a single pipeline stage. Multiple stages may be defined in a same shader source file,
	/// but all variants of a stage must be contained within one contiguous file.<para/>
	/// LIFECYCLE: Disposing a shader resource will dispose all variant programs created from it. The shader resource holds no
	/// additional external resource dependencies and can always be disposed safely once all materials referencing it have been
	/// disposed.
	/// </summary>
	public sealed class ShaderResource : Resource
	{
		#region Constructors

		internal ShaderResource(ResourceHandle _handle, GraphicsCore _graphicsCore, Shader?[] _shaderVariants, ShaderStages _stage) : base(_handle)
		{
			graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Material's graphics core may not be null!");

			shaderVariants = _shaderVariants ?? throw new ArgumentNullException(nameof(_shaderVariants), "Shader variants array may not be null!");
			Stage = _stage;
		}

		#endregion
		#region Fields

		private MeshVertexDataFlags[] vertexVariants = [ MeshVertexDataFlags.BasicSurfaceData ];
		private Shader?[] shaderVariants = [];      // array of variant shader programs, indexed via numeric value of MeshVertexDataFlags enum.

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
				vertexVariants = [];
				shaderVariants = [];
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
			if (_variantFlags == 0)
			{
				return false;
			}
			for (int i = 0; i < vertexVariants.Length; ++i)
			{
				if (vertexVariants[i] == _variantFlags)
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

		#endregion
	}
}
