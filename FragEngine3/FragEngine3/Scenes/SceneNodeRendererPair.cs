using FragEngine3.Graphics;

namespace FragEngine3.Scenes
{
	public sealed record SceneNodeRendererPair
	{
		#region Constructors

		public SceneNodeRendererPair(SceneNode _node, IRenderer _renderer)
		{
			node = _node;
			renderer = _renderer;
		}

		#endregion
		#region Fields

		public readonly SceneNode node;
		public readonly IRenderer renderer;

		#endregion
	}
}
