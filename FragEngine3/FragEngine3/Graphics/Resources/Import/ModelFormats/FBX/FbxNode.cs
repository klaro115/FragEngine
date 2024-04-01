namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

public sealed class FbxNode
{
	#region Fields

	public readonly string name = string.Empty;

	private List<FbxNode>? children = null;
	private List<FbxProperty>? properties = null;

	#endregion
}
