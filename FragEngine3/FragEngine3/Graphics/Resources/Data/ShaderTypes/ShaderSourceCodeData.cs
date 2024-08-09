using FragEngine3.EngineCore;
using FragEngine3.Utility;
using System.Text;

namespace FragEngine3.Graphics.Resources.Data.ShaderTypes;

[Serializable]
public sealed class ShaderSourceCodeData
{
	#region Fields

	public static readonly ShaderSourceCodeData none = new()
	{
		HlslByteOffset = 0,
		HlslCode = null,
	};

	#endregion
	#region Properties

	// HEADER:

	public uint HlslByteOffset { get; init; } = 0;
	public short HlslByteLength { get; init; } = -1;

	// DATA PAYLOAD: (elsewhere on the stream)

	public string? HlslCode { get; init; } = string.Empty;

	#endregion
	#region Constants

	public const uint HEADER_BYTE_SIZE = sizeof(uint) + sizeof(short);

	#endregion
	#region Methods

	public bool IsEmpty()
	{
		return
			HlslByteOffset == 0 ||
			HlslByteLength == 0 ||
			string.IsNullOrEmpty(HlslCode);
	}

	public static bool Read(BinaryReader _reader, out ShaderSourceCodeData _outSourceCodeData)
	{
		if (_reader is null)
		{
			Logger.Instance?.LogError("Cannot read shader source code data using null binary reader!");
			_outSourceCodeData = none;
			return false;
		}

		try
		{
			// Read offset and size:
			uint hlslByteOffset = _reader.ReadUInt32();
			short hlslByteLength = _reader.ReadInt16();
			string? hlslCode;

			// If either offset or length are zero, HLSL source code is not included and may be skipped:
			if (hlslByteOffset == 0 ||  hlslByteLength == 0)
			{
				_outSourceCodeData = none;
				return true;
			}

			// Advance stream to read source code, encoded as UTF-8:
			_reader.JumpToPosition(hlslByteOffset);

			if (hlslByteLength > 0)
			{
				// Read bytes en-bloc:
				byte[] utf8Bytes = new byte[hlslByteLength];
				int bytesRead = _reader.Read(utf8Bytes, 0, hlslByteLength);

				// Convert UTF-8 code units to UTF-16 string:
				hlslCode = Encoding.UTF8.GetString(utf8Bytes, 0, bytesRead);
			}
			else
			{
				// Read until a string terminator is found:
				List<byte> utf8Bytes = new(1024);
				byte c = _reader.ReadByte();
				do
				{
					utf8Bytes.Add(c);
				}
				while ((c = _reader.ReadByte()) != 0);

				// Convert UTF-8 code units to UTF-16 string:
				hlslCode = Encoding.UTF8.GetString(utf8Bytes.ToArray());
			}

			// Assemble data object and return success:
			_outSourceCodeData = new()
			{
				HlslByteOffset = hlslByteOffset,
				HlslByteLength = hlslByteLength,
				HlslCode = hlslCode,
			};
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read shader source code data from stream!", ex);
			_outSourceCodeData = none;
			return false;
		}
	}

	public uint GetHeaderActualByteSize() => IsEmpty() ? HEADER_BYTE_SIZE : 0;

	public bool WriteHeader(BinaryWriter _writer)
	{
		if (_writer is null)
		{
			Logger.Instance?.LogError("Cannot write shader source code data using null binary writer!");
			return false;
		}

		try
		{
			_writer.Write(HlslByteOffset);
			_writer.Write(HlslByteLength);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to write header for shader source code data to stream!", ex);
			return false;
		}
	}

	public bool WritePayload(BinaryWriter _writer)
	{
		if (_writer is null)
		{
			Logger.Instance?.LogError("Cannot write shader source code data using null binary writer!");
			return false;
		}
		// Don't write anything if there's no source code:
		if (IsEmpty())
		{
			return true;
		}

		try
		{
			// Advance stream to designated offset:
			_writer.JumpToPosition(HlslByteOffset, false);

			// Write HLSL source code as UTF-8 bytes:
			byte[] utf8Bytes = Encoding.UTF8.GetBytes(HlslCode!);
			_writer.Write(utf8Bytes);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to write payload for shader source code data to stream!", ex);
			return false;
		}
	}

	#endregion
}
