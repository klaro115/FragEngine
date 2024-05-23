using FragEngine3.Graphics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Veldrid;

namespace FragEngine3.Utility.Serialization.CustomSerializers;

/// <summary>
/// A custom converter for nicely formatting colors of type '<see cref="RgbaFloat"/>' for JSON serialization.
/// </summary>
public sealed class RgbaFloatJsonConverter : JsonConverter<RgbaFloat>
{
	#region Methods

	public override RgbaFloat Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? txt = reader.GetString();
		if (string.IsNullOrEmpty(txt))
		{
			return new(0, 0, 0, 0);
		}

		// Check if the color is written as hexadecimal code: (ex: '#FF12CAFF' or 'ffa0b7')
		char c = txt[0];
		if (char.IsAsciiHexDigit(c) || c == '#')
		{
			if (c == '#')
			{
				return Color32.ParseHexString(txt.AsSpan(1)).ToRgbaFloat();
			}
			else
			{
				return Color32.ParseHexString(txt).ToRgbaFloat();
			}
		}

		// Read RGB float components from string:
		int index = 0;
		if (!txt.ReadNextSingle(index, out float r, out index) ||
			!txt.ReadNextSingle(index, out float g, out index) ||
			!txt.ReadNextSingle(index, out float b, out index))
		{
			return new(0, 0, 0, 0);
		}

		// Alpha is optional, set it to 1 if missing:
		if (!txt.ReadNextSingle(index, out float a, out _))
		{
			a = 1.0f;
		}

		return new(r, g, b, a);
	}

	public override void Write(Utf8JsonWriter writer, RgbaFloat value, JsonSerializerOptions options)
	{
		string txt = $"({value.R}; {value.G}; {value.B}; {value.A})";
		writer.WriteStringValue(txt);
	}
	
	#endregion
}
