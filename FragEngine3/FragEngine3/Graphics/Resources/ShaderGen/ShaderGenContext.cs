using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public sealed class ShaderGenContext(ShaderGenLanguage _language)
{
	#region Fields

	public readonly StringBuilder constants = new(2300);
	public readonly StringBuilder resources = new(512);
	public readonly StringBuilder vertexOutputs = new(300);
	public readonly StringBuilder functions = new(4096);
	public readonly List<ShaderGenVariant> variants =
	[
		new ShaderGenVariant(MeshVertexDataFlags.BasicSurfaceData),
	];
	//^Note: Starting capacities set for a pixel shader with all features enabled.

	public readonly ShaderGenLanguage language = _language;
	public readonly HashSet<string> globalDeclarations = new(10);

	public uint boundUniformsIdx = 3;
	public uint boundTextureIdx = 2;
	public uint boundBufferIdx = 0;
	public uint boundSamplerIdx = 1;

	#endregion
	#region Methods

	public void Clear()
	{
		constants.Clear();
		resources.Clear();
		vertexOutputs.Clear();
		functions.Clear();

		foreach (ShaderGenVariant variant in variants)
		{
			variant.isEnabled = false;
			variant.Clear();
		}
	}

	public bool HasGlobalDeclaration(string _name) => !string.IsNullOrEmpty(_name) && globalDeclarations.Contains(_name);

	public bool WriteFunction_MainPixel(StringBuilder _finalBuilder)
	{
		if (_finalBuilder == null) return false;

		bool success = true;

		foreach (ShaderGenVariant variant in variants)
		{
			success &= variant.WriteFunction_MainPixel(this, _finalBuilder);
		}

		return success;
	}

	#endregion
}
