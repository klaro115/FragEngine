using System.Text.Json;
using FragEngine3.EngineCore;

namespace FragEngine3.Utility.Serialization
{
	public static class Serializer
	{
		#region Fields

		private static readonly JsonSerializerOptions defaultOptions = new()
		{
			AllowTrailingCommas = true,
			WriteIndented = true,
			IncludeFields = true,
		};
		private static JsonSerializerOptions? customOptions = null;

		#endregion
		#region Properties

		public static JsonSerializerOptions Options
		{
			get => customOptions ?? defaultOptions;
			set => customOptions = value;
		}

		#endregion
		#region Methods

		public static bool SerializeToJson<T>(T _data, out string _outJsonTxt)
		{
			if (_data == null)
			{
				Logger.Instance?.LogError("Cannot serialize null data to JSON!");
				_outJsonTxt = string.Empty;
				return false;
			}

			try
			{
				_outJsonTxt = JsonSerializer.Serialize(_data, Options);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to serialize data '{_data}' to JSON!", ex);
				_outJsonTxt = string.Empty;
				return false;
			}
		}

		public static bool SerializeJsonToFile<T>(T _data, string _filePath)
		{
			if (string.IsNullOrEmpty(_filePath))
			{
				Logger.Instance?.LogError("Cannot write serialized JSON to file at null or blank file path!");
				return false;
			}
			if (!SerializeToJson(_data, out string jsonTxt))
			{
				return false;
			}

			try
			{
				File.WriteAllText(_filePath, jsonTxt);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to write JSON to file at path '{_filePath}'!", ex);
				return false;
			}
		}

		public static bool DeserializeFromJson<T>(string _jsonTxt, out T? _outData)
		{
			if (string.IsNullOrEmpty(_jsonTxt))
			{
				Logger.Instance?.LogError("Cannot deserialized data from null or blank JSON!");
				_outData = default;
				return false;
			}

			try
			{
				_outData = JsonSerializer.Deserialize<T>(_jsonTxt, Options);
				return true;
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Failed to deserialize data of type '{typeof(T)}' from JSON!", ex);
				_outData = default;
				return false;
			}
		}

		public static bool DeserializeJsonFromFile<T>(string _filePath, out T? _outData)
		{
			if (string.IsNullOrEmpty(_filePath))
			{
				Logger.Instance?.LogError("Cannot deserialize JSON from null or blank file path!");
				_outData = default;
				return false;
			}

			string jsonTxt;
			try
			{
				jsonTxt = File.ReadAllText(_filePath);
			}
			catch (Exception ex)
			{
				Logger.Instance?.LogException($"Error! Failed to read JSON from file at path '{_filePath}'!", ex);
				_outData = default;
				return false;
			}

			return DeserializeFromJson(_filePath, out _outData);
		}

		#endregion
	}
}
