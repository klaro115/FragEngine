using Veldrid;

namespace FragEngine3.Graphics.Utility;

public static class SceneUtility
{
	#region Methods

	public static bool CreateObjectResourceLayout(
		in GraphicsCore _graphicsCore,
		out ResourceLayout _outResLayoutObject)
	{
		try
		{
			ResourceLayoutDescription resLayoutDesc = new(GraphicsConstants.DEFAULT_OBJECT_RESOURCE_LAYOUT_DESC);

			_outResLayoutObject = _graphicsCore.MainFactory.CreateResourceLayout(ref resLayoutDesc);
			_outResLayoutObject.Name = $"ResLayout_Object";
			return true;
		}
		catch (Exception ex)
		{
			_graphicsCore.graphicsSystem.Engine.Logger.LogException($"Failed to create default object resource layout!", ex);
			_outResLayoutObject = null!;
			return false;
		}
	}

	#endregion
}
