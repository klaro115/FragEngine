using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public sealed class DefaultShaderBuilderContext(DefaultShaderLanguage _language)
{
	#region Fields

	public readonly StringBuilder constants = new(2300);
	public readonly StringBuilder resources = new(512);
	public readonly StringBuilder vertexOutputs = new(300);
	public readonly StringBuilder functions = new(4096);
	public readonly StringBuilder mainInputs = new(256);
	public readonly StringBuilder mainCode = new(650);
	public readonly StringBuilder mainHeader = new(512);
	//^Note: Starting capacities set for a pixel shader with all features enabled.

	public readonly DefaultShaderLanguage language = _language;
	public readonly HashSet<string> globalDeclarations = new(10);
	public MeshVertexDataFlags vertexDataFlags = MeshVertexDataFlags.BasicSurfaceData;
	public string varNameNormals = "inputBasic.normal";

	public int boundUniformsIdx = 3;
	public int boundTextureIdx = 2;
	public int boundBufferIdx = 0;
	public int boundSamplerIdx = 1;

	#endregion
}
