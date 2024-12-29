using FragAssetFormats.Shaders.Import.Internal;
using FragAssetFormats.Shaders.ShaderTypes;
using FragEngine3.EngineCore;
using FragEngine3.Resources;
using Veldrid;

namespace FragEngine3.Graphics.Resources;

/// <summary>
/// A graphics resource representing all vertex variants of a same GPU shader program.<para/>
/// IMPORT: Each shader resource depicts a single pipeline stage. Multiple stages may be defined in a same shader source file,
/// but all variants of a stage must be contained within one contiguous file. If the shader resource was created with source
/// code available, additional variants may be compiled at run-time and on-demand.<para/>
/// LIFECYCLE: Disposing a shader resource will dispose all variant programs created from it. The shader resource holds no
/// additional external resource dependencies and can always be disposed safely once all materials referencing it have been
/// disposed.
/// </summary>
public sealed class ShaderResource : Resource
{
	#region Constructors

	internal ShaderResource(
		string _resourceKey,
		GraphicsCore _graphicsCore,
		ShaderStages _stage,
		MeshVertexDataFlags _supportedVariantFlags,
		Shader?[] _variants,
		ShaderLanguage _sourceCodeLanguage,
		ShaderDescriptionSourceCodeData? _sourceCodeData = null,
		byte[]? _sanitizedSourceCodeBytes = null)
		: base(_resourceKey, _graphicsCore.graphicsSystem.engine)
	{
		graphicsCore = _graphicsCore ?? throw new ArgumentNullException(nameof(_graphicsCore), "Material's graphics core may not be null!");
		Stage = _stage;

		supportedVariantFlags = _supportedVariantFlags;
		unsupportedVariantFlags = ~(supportedVariantFlags | MeshVertexDataFlags.BasicSurfaceData);
		shaderVariants = _variants ?? throw new ArgumentNullException(nameof(_variants), "Shader variants array may not be null!");

		compiledVariantCount = shaderVariants.Count(o => o is not null && !o.IsDisposed);
		totalVariantCount = shaderVariants.Length;

		if (_sourceCodeData is not null && _sanitizedSourceCodeBytes is not null && compiledVariantCount < totalVariantCount)
		{
			canCompileFromSourceCode = true;
			sourceCodeLanguage = _sourceCodeLanguage;
			sourceCodeBytes = _sanitizedSourceCodeBytes;
			sourceCodeData = _sourceCodeData;
		}
	}

	#endregion
	#region Fields

	public readonly GraphicsCore graphicsCore;

	// Compiled variants:

	private readonly MeshVertexDataFlags supportedVariantFlags = 0;
	private readonly MeshVertexDataFlags unsupportedVariantFlags = 0;
	private Shader?[] shaderVariants = [];      // array of variant shader programs, indexed via 'MeshVertexDataFlags.GetVariantIndex()'.

	// Source code:

	public readonly bool canCompileFromSourceCode = false;
	private readonly ShaderLanguage sourceCodeLanguage = 0;
	private byte[]? sourceCodeBytes = null;
	ShaderDescriptionSourceCodeData? sourceCodeData = null;
	private int compiledVariantCount = 0;
	private readonly int totalVariantCount = 0;

	#endregion
	#region Properties

	/// <summary>
	/// Gets the pipeline stage that this shader's programs can be bound to.
	/// </summary>
	public ShaderStages Stage { get; private init; } = ShaderStages.None;

	/// <summary>
	/// Gets the number of supported vertex data variants.
	/// </summary>
	public int VertexVariantCount => shaderVariants is not null ? shaderVariants.Length : 0;

	public override ResourceType ResourceType => ResourceType.Shader;

	private Logger? Logger => graphicsCore.graphicsSystem.engine.Logger ?? Logger.Instance;

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
			shaderVariants = [];
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
		if ((unsupportedVariantFlags & _variantFlags) != 0)
		{
			return false;
		}
		return true;
	}

	/// <summary>
	/// Get the shader program corresponding to a specific vertex definition.
	/// </summary>
	/// <param name="_variantFlags">Flags describing the vertex definitions of a mesh.<para/>
	/// <param name="_outShader">Outputs the corresponding shader program, or null, if no such variant exists.</param>
	/// <returns>True if the shader variant exists, false otherwise.</returns>
	public bool GetShaderProgram(MeshVertexDataFlags _variantFlags, out Shader? _outShader)
	{
		uint variantIdx = _variantFlags.GetVariantIndex();
		if (variantIdx < shaderVariants.Length)
		{
			_outShader = shaderVariants[variantIdx];

			// If no compiled program is ready, but source code is available:
			if (_outShader is null && canCompileFromSourceCode && (unsupportedVariantFlags & _variantFlags) == 0)
			{
				CompileVariantFromSourceCode(_variantFlags);
				_outShader = shaderVariants[variantIdx];
			}
			return _outShader != null;
		}
		_outShader = null;
		return false;
	}

	/// <summary>
	/// Try to compile a missing shader variant from source code.
	/// </summary>
	/// <param name="_variantFlags">Vertex data flags for the shader variant you wish to compile.</param>
	/// <returns>True if the variant was compiled successfully, false otherwise.</returns>
	public bool CompileVariantFromSourceCode(MeshVertexDataFlags _variantFlags)
	{
		if (IsDisposed)
		{
			Logger?.LogError("Cannot compile shader variants from source for disposed shader resource!");
			return false;
		}
		if (_variantFlags == 0 || (_variantFlags & unsupportedVariantFlags) != 0 || !canCompileFromSourceCode)
		{
			Logger?.LogError($"Cannot compile shader variants from source; missing source code, or unsupported variant flags! Flags: '{_variantFlags}'");
			return false;
		}

		// If the variant has already been compiled, exit here and report success:
		uint variantIdx = _variantFlags.GetVariantIndex();
		if (variantIdx >= shaderVariants.Length)
		{
			return false;
		}
		Shader? shader = shaderVariants[variantIdx];
		if (shader is not null && !shader.IsDisposed)
		{
			return true;
		}

		// Fetch the name of the variant's entry point function:
		var entryPoint = sourceCodeData!.EntryPoints?.FirstOrDefault(o => o.VariantFlags == _variantFlags);
		if (entryPoint is null)
		{
			Logger?.LogError($"Cannot compile shader variants from source; missing entry point function name! Flags: '{_variantFlags}'");
			return false;
		}

		// Set variant defines on source code:
		ShaderSourceCodeDefiner.SourceCodeBuffer? sourceCodeBuffer;
		if (sourceCodeLanguage == ShaderLanguage.SPIRV)
		{
			sourceCodeBuffer = new(sourceCodeBytes!, sourceCodeBytes!.Length);
		}
		else if (!ShaderSourceCodeDefiner.SetVariantDefines(sourceCodeBytes!, _variantFlags, false, out sourceCodeBuffer))
		{
			Logger?.LogError($"Cannot compile shader variants from source; failed to set '#define' macros fro vertex flags! Flags: '{_variantFlags}'");
			return false;
		}

		// Trim source code buffer to size, if needed:
		byte[] variantSourceCodeBytes;
		if (sourceCodeBuffer!.Length == sourceCodeBuffer.Capacity)
		{
			variantSourceCodeBytes = sourceCodeBuffer.Utf8ByteBuffer;
		}
		else
		{
			variantSourceCodeBytes = new byte[sourceCodeBuffer.Length];
			Array.Copy(sourceCodeBuffer.Utf8ByteBuffer, variantSourceCodeBytes, sourceCodeBuffer.Length);
		}

		try
		{
			// Compile variant from modified source code:
			ShaderDescription shaderDesc = new(Stage, variantSourceCodeBytes, entryPoint.EntryPoint);

			shader = graphicsCore.MainFactory.CreateShader(ref shaderDesc);
			shader.Name = $"{resourceKey}_{Stage}_V{(int)_variantFlags}";

			shaderVariants[variantIdx] = shader;
			compiledVariantCount++;
		}
		catch (Exception ex)
		{
			Logger?.LogException($"Failed to compile shader variant '{_variantFlags}' for shader resource '{resourceKey}'!", ex);
			return false;
		}
		finally
		{
			sourceCodeBuffer.ReleaseBuffer();
		}

		// Drop source code once all supported variants have been compiled:
		if (compiledVariantCount >= totalVariantCount)
		{
			sourceCodeBytes = null;
			sourceCodeData = null;
		}
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
