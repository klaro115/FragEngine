using FragAssetFormats.Geometry;
using FragAssetFormats.Shaders.ShaderTypes;
using FragEngine3.Graphics.Resources;
using FragEngine3.Graphics.Resources.Data.ShaderTypes;
using FragEngine3.Graphics.Resources.Shaders;
using Veldrid;

namespace FragAssetPipeline.Resources.Shaders;

/// <summary>
/// Options for compiling, exporting and bundling shader data by the <see cref="FshaExporter"/>.
/// </summary>
public sealed class FshaExportOptions
{
	/// <summary>
	/// Flags for each type of compiled data you want to generate. This specifies
	/// for which platforms and graphics APIs pre-compiled shader variants should
	/// be built.<para/>
	/// If no flags are provided, only <see cref="CompiledShaderDataType.DXBC"/> will
	/// be targeted by default if D3D is supported on the compiling device. If DXBC is
	/// not supported, no variants shall be pre-compiled.
	/// </summary>
	public CompiledShaderDataType compiledDataTypeFlags = CompiledShaderDataType.ALL;

	/// <summary>
	/// Flags for all language for which the original source code shall be bundled.
	/// If no flags are set, no source code will be included. Any flags for which
	/// no source code is available will be skipped.
	/// </summary>
	public ShaderLanguage bundledSourceCodeLanguages = ShaderLanguage.ALL;

	/// <summary>
	/// Whether to only bundle source code if no variants could be compiled. If
	/// true, the exporter will still generate an FSHA file, but it will contain
	/// no pre-compiled variants. Export will fail if no source code was set to
	/// be bundled.
	/// </summary>
	public bool bundleOnlySourceIfCompilationFails = false;

	/// <summary>
	/// Which shader stage we're compiling. Only one stage flag can be active for
	/// each build, since each FSHA file can only target a single shader stage.
	/// </summary>
	public ShaderStages shaderStage = ShaderStages.None;

	/// <summary>
	/// Flags for all vertex shader flags that may be pre-compiled. All variant
	/// entry points that use flags that are not raised on this option will be
	/// skipped during compilation.<para/>
	/// Example: If only basic and extended vertex data flags are raised, any
	/// variants using blend shapes or bone animation will not be pre-compiled.<para/>
	/// Note: The flag '<see cref="MeshVertexDataFlags.BasicSurfaceData"/>' needs
	/// to be raised in order for any variants to be pre-compiled.
	/// </summary>
	public MeshVertexDataFlags maxVertexVariantFlags = MeshVertexDataFlags.BasicSurfaceData;

	/// <summary>
	/// The base name for all entry point functions. This is only used if
	/// <see cref="entryPoints"/> is unset. If null, the exporter will try to use
	/// default entry point names instead.
	/// </summary>
	public string? entryPointBase = null;

	/// <summary>
	/// The names of all entry point functions of all variants included in this
	/// shader. Even if more variants exist, only these entry point functions will
	/// actually be pre-compiled. If null or empty, <see cref="entryPointBase"/>
	/// is used instead.
	/// </summary>
	public Dictionary<MeshVertexDataFlags, string>? entryPoints = null;

	public ShaderConfig supportedFeatures = ShaderConfig.ConfigMinimal;

	//...
}
