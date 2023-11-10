using FragEngine3.EngineCore;
using FragEngine3.Utility.Serialization;

namespace FragEngine3.Scenes.Data
{
	[Serializable]
	public sealed class SceneBranchData : ISceneElementData
	{
		#region Properties

		public string PrefabName { get; set; } = string.Empty;
		public int ID { get; set; } = -1;
		public SceneElementType ElementType => SceneElementType.SceneBranch;

		public SceneData.HierarchyData Hierarchy { get; set; } = new();

		#endregion
		#region Methods

		public int GetTotalSceneElementCount()
		{
			int count = 0;

			if (Hierarchy != null)
			{
				count += Math.Max(Hierarchy.TotalNodeCount, 0);
				count += Math.Max(Hierarchy.TotalComponentCount, 0);
			}

			return count;
		}

		public static bool Serialize(SceneBranchData _data, out string _outJsonTxt) => Serializer.SerializeToJson(_data, out _outJsonTxt);
		public static bool SerializeToFile(SceneBranchData _data, string _filePath) => Serializer.SerializeJsonToFile(_data, _filePath);

		public static bool Deserialize(string _jsonTxt, out SceneBranchData _outData)
		{
			if (_jsonTxt == null)
			{
				Logger.Instance?.LogError("Cannot deserialize scene branch data from null JSON string!");
				_outData = new();
				return false;
			}

			bool success = Serializer.DeserializeFromJson(_jsonTxt, out _outData!);
			_outData ??= new();
			return success;
		}
		public static bool DeserializeFromFile(string _filePath, out SceneBranchData _outData)
		{
			if (string.IsNullOrEmpty(_filePath))
			{
				Logger.Instance?.LogError("Cannot deserialize scene branch data from null or blank file path!");
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
