namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Serializable]
public sealed class ShaderDescriptionSourceCodeData
{
	#region Fields

	[Serializable]
	public sealed class VariantEntryPoint
	{
		public MeshVertexDataFlags VariantFlags { get; init; } = MeshVertexDataFlags.BasicSurfaceData;
		public string EntryPoint { get; init; } = string.Empty;

		public override string ToString() => $"Variant Flags: {VariantFlags}, Entry point function: '{EntryPoint ?? string.Empty}'";
	}

	[Serializable]
	public sealed class SourceCodeBlock
	{
		public ShaderLanguage Language { get; init; } = 0;
		public uint ByteOffset { get; set; } = 0;
		public uint ByteSize { get; init; } = 0;

		public override string ToString() => $"Language: {Language}, Offset: {ByteOffset} bytes, Size: {ByteSize} bytes";
	}

	#endregion
	#region Properties

	// ENTRY POINTS:

	/// <summary>
	/// Name base of all entry point functions within the source code for this stage.
	/// Suffixes added to this name may be used to find and identify variants during run-time compilation.
	/// </summary>
	public string EntryPointNameBase { get; init; } = string.Empty;
	/// <summary>
	/// An array of all variant entry points and their respective vertex data flags. If null, entry points will be scanned and identified
	/// based on '<see cref="EntryPointNameBase"/>' and standard variant suffixes instead.
	/// </summary>
	public VariantEntryPoint[]? EntryPoints { get; init; } = null;

	// FEATURES:

	/// <summary>
	/// String-encoded list of all ShaderGen features supported by this shader's source code. (i.e. what is possible)
	/// </summary>
	public string SupportedFeaturesTxt { get; init; } = string.Empty;
	/// <summary>
	/// String-encoded list of all ShaderGen features that were enabled for the most fully-featured variant. (i.e. what has been pre-compiled)
	/// </summary>
	public string MaximumCompiledFeaturesTxt { get; init; } = string.Empty;

	// CODE BLOCKS:

	/// <summary>
	/// An array of all source code blocks bundled with this file, each in a different language.
	/// </summary>
	public SourceCodeBlock[] SourceCodeBlocks { get; init; } = [];

	#endregion
	#region Methods

	public bool IsValid()
	{
		bool result =
			!string.IsNullOrEmpty(EntryPointNameBase) &&
			!string.IsNullOrEmpty(SupportedFeaturesTxt) &&
			!string.IsNullOrEmpty(MaximumCompiledFeaturesTxt) &&
			SourceCodeBlocks is not null &&
			SourceCodeBlocks.Length != 0;
		return result;
	}

	#endregion
}
