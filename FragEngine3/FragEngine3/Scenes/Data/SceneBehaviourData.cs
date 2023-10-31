
namespace FragEngine3.Scenes.Data
{
	[Serializable]
	public sealed class SceneBehaviourData : ISceneElementData
	{
		#region Properties

		public string Type { get; set; } = string.Empty;
		public int ID { get; set; } = -1;
		public SceneElementType ElementType => SceneElementType.SceneNode;

		//...

		public string SerializedData { get; set; } = string.Empty;

		#endregion
	}
}
