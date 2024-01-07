using FragEngine3.EngineCore;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Scenes.Data
{
    [Serializable]
	public sealed class SceneData
	{
		#region Types

		public sealed class BehaviourData
		{
			public int BehaviourCount { get; set; } = 0;

			public SceneBehaviourData[]? BehavioursData { get; set; } = null;
		}

		public sealed class HierarchyData
		{
			public int TotalNodeCount { get; set; } = 0;
			public int HierarchyDepth { get; set; } = 0;
			public int TotalComponentCount { get; set; } = 0;
			public int MaxComponentCount { get; set; } = 0;

			public SceneNodeData[]? NodeData { get; set; } = null;
		}

		#endregion
		#region Properties

		public string Name { get; set; } = string.Empty;
		public EngineState UpdatedInEngineStates { get; set; } = EngineState.Running;

		public SceneSettingsData Settings { get; set; } = new();
		public BehaviourData Behaviours { get; set; } = new();
		public HierarchyData Hierarchy { get; set; } = new();

		#endregion
		#region Methods

		public int GetTotalSceneElementCount()
		{
			int count = 0;

			if (Behaviours != null)
			{
				count += Math.Max(Behaviours.BehaviourCount, 0);
			}
			if (Hierarchy != null)
			{
				count += Math.Max(Hierarchy.TotalNodeCount, 0);
				count += Math.Max(Hierarchy.TotalComponentCount, 0);
			}

			return count;
		}

		public static bool Serialize(SceneData _data, out string _outJsonTxt) => Serializer.SerializeToJson(_data, out _outJsonTxt);
		public static bool SerializeToFile(SceneData _data, string _filePath) => Serializer.SerializeJsonToFile(_data, _filePath);

		public static bool Deserialize(string _jsonTxt, out SceneData _outData)
		{
			if (_jsonTxt == null)
			{
				Logger.Instance?.LogError("Cannot deserialize scene data from null JSON string!");
				_outData = new();
				return false;
			}

			bool success = Serializer.DeserializeFromJson(_jsonTxt, out _outData!);
			_outData ??= new();
			return success;
		}
		public static bool DeserializeFromFile(string _filePath, out SceneData _outData)
		{
			if (string.IsNullOrEmpty(_filePath))
			{
				Logger.Instance?.LogError("Cannot deserialize scene data from null or blank file path!");
				_outData = new();
				return false;
			}

			bool success = Serializer.DeserializeJsonFromFile(_filePath, out _outData!);
			_outData ??= new();
			return success;
		}

		#endregion
	}
}
