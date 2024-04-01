using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

public sealed class FbxNode(string _name)
{
	#region Types

	private struct NodeHeader
	{
		public uint endOffset;
		public uint numProperties;
		public uint propertyListLength;
		public byte nameLength;
	}

	#endregion
	#region Fields

	public readonly string name = _name ?? string.Empty;

	private List<FbxProperty>? properties = null;
	private List<FbxNode>? children = null;

	#endregion
	#region Properties

	public uint ChildCount => children != null ? (uint)children.Count : 0u;
	public uint PropertyCount => properties != null ? (uint)properties.Count : 0u;

	#endregion
	#region Methods

	public bool IsNull() => ChildCount == 0 && PropertyCount == 0 && string.IsNullOrEmpty(name);

	public bool GetChildNode(uint _childIdx, out FbxNode _outNode)
	{
		if (_childIdx < ChildCount)
		{
			_outNode = children![(int)_childIdx];
			return true;
		}
		_outNode = null!;
		return false;
	}

	public bool GetProperty(uint _propertyIdx, out FbxProperty _outProperty)
	{
		if (_propertyIdx < PropertyCount)
		{
			_outProperty = properties![(int)_propertyIdx];
			return true;
		}
		_outProperty = null!;
		return false;
	}

	public override string ToString()
	{
		return $"Node, Name: '{name}', Children: {ChildCount}, Properties: {PropertyCount}";
	}

	public static bool ReadNode(BinaryReader _reader, uint _fileStartOffset, uint _nodeStartOffset, int _depth, out FbxNode? _outNode)
	{
		if (_reader is null)
		{
			_outNode = null;
			return false;
		}
		if (_reader.BaseStream.Position >= _reader.BaseStream.Length)
		{
			_outNode = null;
			return false;
		}

		if (!ReadNodeHeader(_reader, out NodeHeader header, out string name))
		{
			_outNode = null;
			return false;
		}

		_outNode = new(name);

		if (!ReadProperties(_reader, _outNode, _nodeStartOffset, in header))
		{
			return false;
		}

		//TEST TEST TEST TEST
		for (int i = 0; i < _depth; ++i)
		{
			Console.Write("  ");
		}
		Console.WriteLine($"- {_outNode}");
		//TEST TEST TEST TEST

		if (!ReadChildren(_reader, _outNode, _fileStartOffset, _nodeStartOffset, _depth + 1, in header))
		{
			return false;
		}

		return true;
	}

	private static bool ReadNodeHeader(BinaryReader _reader, out NodeHeader _outHeader, out string _outName)
	{
		try
		{
			_outHeader = new()
			{
				endOffset = _reader.ReadUInt32(),
				numProperties = _reader.ReadUInt32(),
				propertyListLength = _reader.ReadUInt32(),
				nameLength = _reader.ReadByte(),
			};

			byte[] nameUtf8 = _reader.ReadBytes(_outHeader.nameLength);
			_outName = System.Text.Encoding.UTF8.GetString(nameUtf8);

			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to read FBX node header!", ex);
			_outHeader = default;
			_outName = string.Empty;
			return false;
		}
	}

	private static bool ReadProperties(BinaryReader _reader, FbxNode _node, uint _nodeStartOffset, in NodeHeader _header)
	{
		if (_header.numProperties == 0)
		{
			return true;
		}

		uint propertiesStartOffset = _nodeStartOffset + 13 + _header.nameLength;
		if (!FbxDocument.MoveReaderToPosition(_reader, propertiesStartOffset))
		{
			return false;
		}

		try
		{
			bool success = true;

			_node.properties ??= new((int)_header.numProperties);

			for (uint i = 0; i < _header.numProperties; i++)
			{
				if (success &= FbxPropertyReader.ReadProperty(_reader, out FbxProperty property))
				{
					_node.properties.Add(property);
				}
			}

			return success;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to read properties of FBX node '{_node.name}'!", ex);
			return false;
		}
	}

	private static bool ReadChildren(BinaryReader _reader, FbxNode _parentNode, uint _fileStartOffset, uint _nodeStartOffset, int _childDepth, in NodeHeader _header)
	{
		uint childrenEndOffset = _fileStartOffset + _header.endOffset;
		if (_reader.BaseStream.Position >= childrenEndOffset)
		{
			return true;
		}

		uint childrenStartOffset = _nodeStartOffset + 13 + _header.nameLength + _header.propertyListLength;
		if (!FbxDocument.MoveReaderToPosition(_reader, childrenStartOffset))
		{
			return false;
		}

		try
		{
			bool success = true;

			_parentNode.children ??= [];

			while (_reader.BaseStream.Position < childrenEndOffset)
			{
				// Recursively read child nodes:
				if ((success &= ReadNode(_reader, _fileStartOffset, (uint)_reader.BaseStream.Position, _childDepth, out FbxNode? node)) && !node!.IsNull())
				{
					_parentNode.children.Add(node!);
				}
			}

			return success;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to read child nodes of FBX node '{_parentNode.name}'!", ex);
			return false;
		}
	}

	#endregion
}
