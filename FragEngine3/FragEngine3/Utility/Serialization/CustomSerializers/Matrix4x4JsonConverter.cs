using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FragEngine3.Utility.Serialization.CustomSerializers;

public sealed class Matrix4x4JsonConverter : JsonConverter<Matrix4x4>
{
	#region Methods

	public override Matrix4x4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)	// Untested
	{
		string? txt = reader.GetString();
		if (string.IsNullOrEmpty(txt))
		{
			return Matrix4x4.Identity;
		}

		Matrix4x4 mtx = Matrix4x4.Identity;

		int numberIdx = 0;
		for (int i = 0; i < txt.Length; ++i)
		{
			char c = txt[i];
			if (char.IsNumber(c))
			{
				for (int j = i + 1; j < txt.Length; ++j)
				{
					c = txt[j];
					if (c == ';' || c == ']' || char.IsWhiteSpace(c))
					{
						ReadOnlySpan<char> span = txt.AsSpan(i, j - i);

						int x = numberIdx % 4;
						int y = numberIdx / 4;
						mtx[y, x] = float.Parse(span);

						numberIdx++;
						i = j;
						break;
					}
				}
			}
		}
		return mtx;
	}

	public override void Write(Utf8JsonWriter writer, Matrix4x4 value, JsonSerializerOptions options)
	{
		StringBuilder builder = new(128);

		builder.Append('[').Append(' ');
		for (int y = 0; y < 4; y++)
		{
			for (int x = 0; x < 4; x++)
			{
				builder.Append(value[y, x]);
				if (!(y == 3 && x == 3))
				{
					builder.Append(';').Append(' ');
				}
			}
		}
		builder.Append(' ').Append(']');

		writer.WriteStringValue(builder.ToString());
	}

	#endregion
}
