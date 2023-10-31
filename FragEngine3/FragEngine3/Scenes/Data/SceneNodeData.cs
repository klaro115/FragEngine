using FragEngine3.Scenes.Utility;
using System.Numerics;

namespace FragEngine3.Scenes.Data
{
	[Serializable]
	public sealed class SceneNodeData : ISceneElementData
	{
		#region Properties

		public string Name { get; set; } = string.Empty;
		public SceneElementType ElementType => SceneElementType.SceneNode;
		public int ID { get; set; } = -1;
		public int ParentID { get; set; } = -1;

		public bool IsEnabled { get; set; } = true;
		public Pose LocalPose { get; set; } = Pose.Identity;

		public int ComponentCount { get; set; } = 0;
		public ComponentData[]? ComponentData { get; set; } = null;

		#endregion
	}
}
