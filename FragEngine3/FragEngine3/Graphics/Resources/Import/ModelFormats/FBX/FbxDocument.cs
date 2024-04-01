using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.Import.ModelFormats.FBX;

// FBX Document Import:
// This is a C# port of a C++ FBX importer by Jakub Skořepa (MIT license, Copyright 2017).
// Link: TODO

public sealed class FbxDocument(uint _version)
{
	#region Fields

	private readonly List<FbxNode> nodes = [];
	public readonly uint version = _version;

	#endregion
	#region Properties

	public uint NodeCount => (uint)nodes.Count;

	#endregion
	#region Methods

	public bool GetNode(uint _nodeIdx, out FbxNode _outNode)
	{
		if (_nodeIdx < NodeCount)
		{
			_outNode = nodes[(int)_nodeIdx];
			return true;
		}
		_outNode = null!;
		return false;
	}

	public void PrintNodeHierarchy()
	{
		foreach (FbxNode node in nodes)
		{
			PrintNodeRecursively(node, 0);
		}


		static void PrintNodeRecursively(FbxNode _node, uint _depth)
		{
			for (uint i = 0; i < _depth; ++i)
			{
				Console.Write('\t');
			}
			Console.WriteLine($"- {_node}");

			for (uint i = 0; i < _node.ChildCount; ++i)
			{
				if (_node.GetChildNode(i, out FbxNode childNode))
				{
					PrintNodeRecursively(childNode, _depth + 1);
				}
			}
		}
	}

	public static bool ReadFbxDocument(BinaryReader _reader, out FbxDocument? _outDocument)
	{
		if (_reader is null)
		{
			Logger.Instance?.LogError("Cannot read FBX document from null stream reader!");
			_outDocument = null;
			return false;
		}

		uint fileStartOffset = (uint)_reader.BaseStream.Position;

		// Check magic numbers at file start:
		if (!CheckMagicNumbers(_reader))
		{
			Logger.Instance?.LogError("Magic numbers leading the FBX stream were invalid!");
			_outDocument = null;
			return false;
		}

		// Check file format version:
		if (!CheckDocumentVersion(_reader, out uint version))
		{
			_outDocument = null;
			return false;
		}

		// Create empty document:
		_outDocument = new(version);

		// Advance to starting position:
		if (!MoveReaderToStartPosition(_reader, out uint startOffset))
		{
			return false;
		}

		// Read nodes:
		bool success = true;

		while ((success &= FbxNode.ReadNode(_reader, fileStartOffset, startOffset, out FbxNode? node)) && !node!.IsNull())
		{
			_outDocument.nodes.Add(node!);

			startOffset = (uint)_reader.BaseStream.Position;
		}

		//TEST
		//_outDocument.PrintNodeHierarchy();

		return success;
	}

	private static bool CheckMagicNumbers(BinaryReader _reader)
	{
		const string magicTxt = "Kaydara FBX Binary  ";

		try
		{
			foreach (char c in magicTxt)
			{
				if (c != _reader.ReadByte()) return false;
			}

			return
				_reader.ReadByte() == 0x00 &&
				_reader.ReadByte() == 0x1A &&
				_reader.ReadByte() == 0x00;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to check version of FBX file stream!", ex);
			return false;
		}
	}

	private static bool CheckDocumentVersion(BinaryReader _reader, out uint _outVersion)
	{
		const uint maxVersion = 7400u;

		try
		{
			_outVersion = _reader.ReadUInt32();

			if (_outVersion > maxVersion)
			{
				Logger.Instance?.LogError($"Unsupported FBX version {_outVersion}; latest supported verison is {maxVersion}!");
				return false;
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to check version of FBX file stream!", ex);
			_outVersion = 0;
			return false;
		}
	}

	private static bool MoveReaderToStartPosition(BinaryReader _reader, out uint _startOffset)
	{
		const uint dataStartOffset = 27;    // magic: 21+2, version: 4

		if (!MoveReaderToPosition(_reader, dataStartOffset))
		{
			Logger.Instance?.LogError("Failed to advance FBX file stream to starting position!");
			_startOffset = 0;
			return false;
		}

		_startOffset = (uint)_reader.BaseStream.Position;
		return true;
	}

	internal static bool MoveReaderToPosition(BinaryReader _reader, uint _targetPosition)
	{
		try
		{
			if (_reader.BaseStream.Position != _targetPosition)
			{
				if (_reader.BaseStream.CanSeek)
				{
					_reader.BaseStream.Position = _targetPosition;
				}
				else
				{
					for (uint i = (uint)_reader.BaseStream.Position; i < _targetPosition; ++i)
					{
						_reader.ReadByte();
					}
				}
			}
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to advance FBX stream reader to target position! (Pos: {_targetPosition})", ex);
			return false;
		}
	}

	#endregion
}
