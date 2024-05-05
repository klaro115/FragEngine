using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FragEngine3.EngineCore;
using FragEngine3.Utility.Serialization.CustomSerializers;

namespace FragEngine3.Utility.Serialization;

public static class Serializer
{
	#region Fields

	private static readonly JsonSerializerOptions defaultOptions = new()
	{
		AllowTrailingCommas = true,
		WriteIndented = true,
		IncludeFields = true,
		Converters =
		{
			new Matrix4x4JsonConverter(),
			new PoseJsonConverter(),
		},
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

	public static bool RegisterCustomConverter<TConverter, TDataType>(TConverter _customConverter)
		where TConverter : JsonConverter<TDataType>
	{
		if (Options.Converters.Any(o => o is TConverter))
		{
			Logger.Instance?.LogError($"A custom converter of type '{typeof(TConverter).Name}' is already registered!");
			return false;
		}

		Options.Converters.Add(_customConverter);
		return true;
	}

	public static bool UnregisterCustomConverter<TConverter, TDataType>()
		where TConverter : JsonConverter<TDataType>
	{
		foreach (JsonConverter converter in Options.Converters)
		{
			if (converter is TConverter)
			{
				Options.Converters.Remove(converter);
				return true;
			}
		}

		Logger.Instance?.LogError($"No custom converter of type '{typeof(TConverter).Name}' was found!");
		return false;
	}

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

	public static bool SerializeToJson<T>(T _data, out MemoryStream? _outUtf8Stream)
	{
		_outUtf8Stream = null;
		if (_data == null)
		{
			Logger.Instance?.LogError("Cannot serialize null data to JSON!");
			return false;
		}

		try
		{
			_outUtf8Stream = new(512);

			JsonSerializer.Serialize(_outUtf8Stream, _data, Options);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to serialize data '{_data}' to JSON!", ex);
			_outUtf8Stream?.Dispose();
			_outUtf8Stream = null;
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

	public static bool SerializeJsonToCompressedFile<T>(T _data, string _filePath)
	{
		if (string.IsNullOrEmpty(_filePath))
		{
			Logger.Instance?.LogError("Cannot write serialized JSON to file at null or blank file path!");
			return false;
		}
		if (!SerializeToJson(_data, out MemoryStream? jsonUtf8Stream))
		{
			return false;
		}

		try
		{
			jsonUtf8Stream!.Position = 0;

			using FileStream fileStream = new(_filePath, FileMode.Create, FileAccess.Write);
			using DeflateStream zipStream = new(fileStream, CompressionLevel.Optimal);

			jsonUtf8Stream!.CopyTo(zipStream);

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to compress JSON and write result to file at path '{_filePath}'!", ex);
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

		if (!File.Exists(_filePath))
		{
			Logger.Instance?.LogError($"Error! JSON file at path '{_filePath}' could not be found!");
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

		return DeserializeFromJson(jsonTxt, out _outData);
	}

	public static bool DeserializeJsonFromCompressedFile<T>(string _filePath, out T? _outData)
	{
		if (string.IsNullOrEmpty(_filePath))
		{
			Logger.Instance?.LogError("Cannot deserialize JSON from null or blank file path!");
			_outData = default;
			return false;
		}

		if (!File.Exists(_filePath))
		{
			Logger.Instance?.LogError($"Error! JSON file at path '{_filePath}' could not be found!");
			_outData = default;
			return false;
		}

		string jsonTxt;
		try
		{
			using FileStream fileStream = new(_filePath, FileMode.Open, FileAccess.Read);
			using DeflateStream zipStream = new(fileStream, CompressionMode.Decompress);
			using StreamReader reader = new(zipStream, Encoding.UTF8);

			jsonTxt = reader.ReadToEnd();
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Error! Failed to read JSON from file at path '{_filePath}'!", ex);
			_outData = default;
			return false;
		}

		return DeserializeFromJson(jsonTxt, out _outData);
	}

	#endregion
}
