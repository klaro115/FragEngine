using FragEngine3.Scenes;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FragEngine3.Utility.Serialization.CustomSerializers;

public sealed class PoseJsonConverter : JsonConverter<Pose>
{
	#region Methods

	public override Pose Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)	// Untested
	{
		string? txt = reader.GetString();
		if (string.IsNullOrEmpty(txt))
		{
			return Pose.Identity;
		}

		Pose pose = Pose.Identity;
		int index = 0;

		// Position:
		for (int i = 0; i < 3; ++i)
		{
			if (!txt.ReadNextSingle(index, out float value, out index)) return pose;

			pose.position[i] = value;
		}
		// Rotation:
		for (int i = 0; i < 4; ++i)
		{
			if (!txt.ReadNextSingle(index, out float value, out index)) return pose;

			pose.rotation[i] = value;
		}
		// Scale:
		for (int i = 0; i < 3; ++i)
		{
			if (!txt.ReadNextSingle(index, out float value, out index)) return pose;

			pose.scale[i] = value;
		}

		return pose;
	}

	public override void Write(Utf8JsonWriter writer, Pose value, JsonSerializerOptions options)
	{
		StringBuilder builder = new(128);

		builder
			.Append("{{ ")
			// Position:
			.Append(value.position.X).Append(';').Append(' ')
			.Append(value.position.Y).Append(';').Append(' ')
			.Append(value.position.Z)
			.Append(" }; { ")
			// Rotation:
			.Append(value.rotation.X).Append(';').Append(' ')
			.Append(value.rotation.Y).Append(';').Append(' ')
			.Append(value.rotation.Z).Append(';').Append(' ')
			.Append(value.rotation.W)
			.Append(" }; { ")
			// Scale:
			.Append(value.scale.X).Append(';').Append(' ')
			.Append(value.scale.Y).Append(';').Append(' ')
			.Append(value.scale.Z)
			.Append(" }}");

		writer.WriteStringValue(builder.ToString());
	}

	#endregion
}
