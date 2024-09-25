using FragEngine3.EngineCore;
using FragEngine3.Utility.Unicode;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats;

/// <summary>
/// Utility class for setting or removing preprocessor '#define' macros in shader source code prior to compilation.
/// </summary>
public static class ShaderSourceCodeDefiner
{
	#region Types

	/// <summary>
	/// Buffer object containing the results of source code modifications. The byte size of this buffer's contents is given by
	/// the <see cref="Length"/> property; any data beyond this size should be ignored.<para/>
	/// OWNERSHIP: A reference to this object should only be retained until the contained shader code data has been extracted.
	/// After usage, the buffer object must be released via <see cref="ReleaseBuffer"/>, to ensure that it is returned to the
	/// pool for later re-use. The ownership of this objects remains with <see cref="ShaderSourceCodeDefiner"/>.
	/// </summary>
	/// <param name="_funcReturnBufferToPool">Function delegate which is called upon release of the buffer.</param>
	/// <param name="_minBufferCapacity">The minimum capacity that must be allocated from the start, in bytes.</param>
	public sealed class SourceCodeBuffer(Action<SourceCodeBuffer> _funcReturnBufferToPool, int _minBufferCapacity)
	{
		private readonly Action<SourceCodeBuffer> funcReturnBufferToPool = _funcReturnBufferToPool;

		/// <summary>
		/// A byte buffer for storing the results of modifying shader source code. Contents are encoded as UTF-8 or ASCII.
		/// </summary>
		public byte[] Utf8ByteBuffer { get; private set; } = new byte[_minBufferCapacity];
		/// <summary>
		/// The current size of contents in the buffer, in bytes.
		/// </summary>
		public int Length = 0;
		/// <summary>
		/// The total current capacity of the buffer, in bytes. If this capacity is too small, the buffer size may be increased
		/// by calling <see cref="Resize(int, bool)"/>.
		/// </summary>
		public int Capacity => Utf8ByteBuffer.Length;

		/// <summary>
		/// Changes the total capacity of the buffer.
		/// </summary>
		/// <param name="_minCapacity">The new minimum capacity that the buffer should be re-allocated to, in bytes. This value
		/// cannot be less than the current value of <see cref="Length"/>.</param>
		/// <param name="_copyExistingContents">Whether to retain existing buffer contents after resizing.</param>
		public void Resize(int _minCapacity, bool _copyExistingContents = true)
		{
			_minCapacity = Math.Max(_minCapacity, Utf8ByteBuffer.Length);
			byte[] prevBuffer = Utf8ByteBuffer;
			Utf8ByteBuffer = new byte[_minCapacity];
			if (_copyExistingContents)
			{
				prevBuffer.CopyTo(Utf8ByteBuffer, 0);
			}
		}
		/// <summary>
		/// Releases the buffer and returns it to a pool for later re-use by its owner. This should be called immediately after
		/// you've extracted all contents you need from <see cref="Utf8ByteBuffer"/>.
		/// </summary>
		public void ReleaseBuffer()
		{
			Length = 0;
			funcReturnBufferToPool(this);
		}
	}

	#endregion
	#region Fields

	private static readonly Stack<SourceCodeBuffer> bufferPool = [];
	private static readonly object bufferPoolLockObj = new();

	#endregion
	#region Constants

	private const string variantDefinePrefix = "#define VARIANT_";

	private const string variantDefineExt = variantDefinePrefix + "EXTENDED";
	private const string variantDefineBlend = variantDefinePrefix + "BLENDSHAPES";
	private const string variantDefineAnim = variantDefinePrefix + "ANIMATED";

	private const string featureDefinePrefix = "#define FEATURE_";

	#endregion
	#region Methods

	private static void ReturnBufferToPool(SourceCodeBuffer _releasedBuffer)
	{
		lock (bufferPoolLockObj)
		{
			bufferPool.Push(_releasedBuffer);
		}
	}

	private static SourceCodeBuffer GetBufferFromPool(int _minBufferCapacity)
	{
		SourceCodeBuffer? buffer;

		// Try re-using an exising source code buffer from our pool:
		lock (bufferPoolLockObj)
		{
			bufferPool.TryPop(out buffer);
		}

		// Create new buffer or resize existing one:
		_minBufferCapacity = Math.Max(_minBufferCapacity, 2048);
		if (buffer is null)
		{
			buffer = new(ReturnBufferToPool, _minBufferCapacity);
		}
		else if (buffer.Capacity < _minBufferCapacity)
		{
			buffer.Resize(_minBufferCapacity);
		}

		return buffer;
	}

	/// <summary>
	/// Adds or removes pre-processor '#define' statements for shader variant flags.
	/// </summary>
	/// <param name="_sourceCodeUtf8Bytes">Byte array containing shader source code in UTF-8 or ASCII encoding. May not be null or empty.</param>
	/// <param name="_variantFlags">Mesh vertex data flags that should be added for the desired shader variant.</param>
	/// <param name="_removeExistingDefines">Whether to remove all existing variant defines before adding the requested new ones.
	/// It is recommended to set this to true, unless you know that the source code has been sanitized of variant defines beforehand.</param>
	/// <param name="_outResultBuffer">Outputs a buffer containing the resulting source code. After the contained data has been used,
	/// this object must be released via '<see cref="SourceCodeBuffer.ReleaseBuffer"/>' to return it to the pool for later re-use.</param>
	/// <returns>True if removing and adding variant defines succeeded, false otherwise.</returns>
	public static bool SetVariantDefines(byte[] _sourceCodeUtf8Bytes, MeshVertexDataFlags _variantFlags, bool _removeExistingDefines, out SourceCodeBuffer? _outResultBuffer)
	{
		if (_sourceCodeUtf8Bytes is null || _sourceCodeUtf8Bytes.Length == 0)
		{
			Logger.Instance?.LogError("Cannot set variant defines on null or empty source code!");
			_outResultBuffer = null;
			return false;
		}

		int sourceCodeLength = _sourceCodeUtf8Bytes.Length;

		// Get a UTF-8 byte buffer for doing our string editing on:
		_outResultBuffer = GetBufferFromPool(sourceCodeLength + 512);
		SourceCodeBuffer? intermediateBuffer = null;

		if (_removeExistingDefines)
		{
			intermediateBuffer = GetBufferFromPool(_outResultBuffer.Capacity);

			Utf8Iterator e = new(_sourceCodeUtf8Bytes, _sourceCodeUtf8Bytes.Length);
			Utf8Iterator.Position lastEndPos = new(0, 0, 0);
			Utf8Iterator.Position curMatchPos;

			// Find each unneeded vertex variant '#define' in source code:
			while ((curMatchPos = e.FindNext(variantDefinePrefix)).IsValid)
			{
				// Copy source code to intermediate buffer, while skipping lines we wish to remove:
				int copyByteSize = curMatchPos.utf8Position - lastEndPos.utf8Position;
				Array.Copy(_sourceCodeUtf8Bytes, lastEndPos.utf8Position, intermediateBuffer.Utf8ByteBuffer, intermediateBuffer.Length, copyByteSize);
				intermediateBuffer.Length += copyByteSize;

				lastEndPos = e.AdvanceToEndOfWord();
			}
			if (lastEndPos.utf8Position < _sourceCodeUtf8Bytes.Length)
			{
				// Copy remaining source code:
				int remainingByteSize = _sourceCodeUtf8Bytes.Length - lastEndPos.utf8Position;
				Array.Copy(_sourceCodeUtf8Bytes, lastEndPos.utf8Position, intermediateBuffer.Utf8ByteBuffer, intermediateBuffer.Length, remainingByteSize);
				intermediateBuffer.Length += remainingByteSize;
			}

			// Perform all subsequent steps on intermediate buffer instead of original source code:
			_sourceCodeUtf8Bytes = intermediateBuffer.Utf8ByteBuffer;
			sourceCodeLength = intermediateBuffer.Length;
		}
		
		// Insert variant defines first:
		if (_variantFlags > MeshVertexDataFlags.BasicSurfaceData)
		{
			if (_variantFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData))
			{
				WriteString(_outResultBuffer, variantDefineExt);
				WriteString(_outResultBuffer, Environment.NewLine);
			}
			if (_variantFlags.HasFlag(MeshVertexDataFlags.BlendShapes))
			{
				WriteString(_outResultBuffer, variantDefineBlend);
				WriteString(_outResultBuffer, Environment.NewLine);
			}
			if (_variantFlags.HasFlag(MeshVertexDataFlags.Animations))
			{
				WriteString(_outResultBuffer, variantDefineAnim);
				WriteString(_outResultBuffer, Environment.NewLine);
			}
		}

		// Copy source code to buffer as-is:
		Array.Copy(_sourceCodeUtf8Bytes, 0, _outResultBuffer.Utf8ByteBuffer, _outResultBuffer.Length, sourceCodeLength);
		_outResultBuffer.Length += sourceCodeLength;

		// Return optional intermediate buffer to pool:
		intermediateBuffer?.ReleaseBuffer();

		return true;
	}

	public static bool SetFeatureDefines(byte[] _sourceCodeUtf8Bytes, IList<string> _featureDefines, bool _removeExistingDefines, out SourceCodeBuffer? _outResultBuffer)
	{
		int sourceCodeLength = _sourceCodeUtf8Bytes.Length;
		SourceCodeBuffer? intermediateBuffer = null;

		if (_removeExistingDefines)
		{
			if (!RemoveAllFeatureDefines(_sourceCodeUtf8Bytes, out intermediateBuffer))
			{
				intermediateBuffer?.ReleaseBuffer();
				_outResultBuffer = null;
				return false;
			}

			_sourceCodeUtf8Bytes = intermediateBuffer!.Utf8ByteBuffer;
			sourceCodeLength = intermediateBuffer.Length;
		}

		int totalDefineLength = CalculateDefinesLength(_featureDefines);
		int maximumOutputLength = sourceCodeLength + totalDefineLength;

		_outResultBuffer = GetBufferFromPool(maximumOutputLength);

		WriteDefines(_outResultBuffer, _featureDefines);

		// Copy source code to buffer as-is:
		Array.Copy(_sourceCodeUtf8Bytes, 0, _outResultBuffer.Utf8ByteBuffer, _outResultBuffer.Length, sourceCodeLength);
		_outResultBuffer.Length += sourceCodeLength;

		// Return optional intermediate buffer to pool:
		intermediateBuffer?.ReleaseBuffer();

		return true;
	}

	public static bool RemoveAllFeatureDefines(byte[] _sourceCodeUtf8Bytes, out SourceCodeBuffer? _outResultBuffer)
	{
		if (_sourceCodeUtf8Bytes is null || _sourceCodeUtf8Bytes.Length == 0)
		{
			Logger.Instance?.LogError("Cannot remove feature defines from null or empty source code!");
			_outResultBuffer = null;
			return false;
		}

		int sourceCodeLength = _sourceCodeUtf8Bytes.Length;

		// Get a UTF-8 byte buffer for doing our string editing on:
		_outResultBuffer = GetBufferFromPool(sourceCodeLength);

		Utf8Iterator e = new(_sourceCodeUtf8Bytes, _sourceCodeUtf8Bytes.Length);
		Utf8Iterator.Position lastEndPos = new(0, 0, 0);
		Utf8Iterator.Position curMatchPos;

		// Find each feature '#define' in source code:
		while ((curMatchPos = e.FindNext(featureDefinePrefix)).IsValid)
		{
			// Skip ahead to the end of the current line:
			Utf8Iterator.Position newLineStartPos = e.AdvanceToEndOfLine();

			// Copy code section between previous and current match:
			int copyLength = curMatchPos.utf8Position - lastEndPos.utf8Position;
			Array.Copy(_sourceCodeUtf8Bytes, lastEndPos.utf8Position, _outResultBuffer.Utf8ByteBuffer, _outResultBuffer.Length, copyLength);
			_outResultBuffer.Length += copyLength;

			lastEndPos = newLineStartPos;
		}
		if (lastEndPos.utf8Position < _sourceCodeUtf8Bytes.Length)
		{
			// Copy remaining source code:
			int remainingByteSize = _sourceCodeUtf8Bytes.Length - lastEndPos.utf8Position;
			Array.Copy(_sourceCodeUtf8Bytes, lastEndPos.utf8Position, _outResultBuffer.Utf8ByteBuffer, _outResultBuffer.Length, remainingByteSize);
			_outResultBuffer.Length += remainingByteSize;
		}

		return true;
	}

	public static bool PrependFeatureDefines(byte[] _sourceCodeUtf8Bytes, IList<string> _featureDefines, out SourceCodeBuffer? _outResultBuffer)
	{
		if (_sourceCodeUtf8Bytes is null || _sourceCodeUtf8Bytes.Length == 0)
		{
			Logger.Instance?.LogError("Cannot remove feature defines from null or empty source code!");
			_outResultBuffer = null;
			return false;
		}

		int sourceCodeLength = _sourceCodeUtf8Bytes.Length;
		int totalDefineLength = CalculateDefinesLength(_featureDefines);
		int maximumOutputLength = sourceCodeLength + totalDefineLength;

		// Get a UTF-8 byte buffer for doing our string editing on:
		_outResultBuffer = GetBufferFromPool(maximumOutputLength);

		WriteDefines(_outResultBuffer, _featureDefines);

		// Copy all source code directly after:
		_sourceCodeUtf8Bytes.CopyTo(_outResultBuffer.Utf8ByteBuffer, _outResultBuffer.Length);
		_outResultBuffer.Length += sourceCodeLength;

		return true;
	}

	/// <summary>
	/// Calculates the total byte size of writing several defines as lines in a single string.
	/// </summary>
	/// <param name="_featureDefines">A list of define strings that should be written as code lines.</param>
	/// <returns>The number of ASCII bytes that would be written.</returns>
	private static int CalculateDefinesLength(IList<string> _featureDefines)
	{
		if (_featureDefines is null || _featureDefines.Count == 0) return 0;

		int lineSeparatorLength = Environment.NewLine.Length;
		int totalDefineLength = 0;

		// Measure the total byte size that needs to be reserved for feature defines:
		foreach (string featureDefine in _featureDefines)
		{
			int length = featureDefine.Length;
			if (!featureDefine.EndsWith('\n'))
			{
				length += lineSeparatorLength;
			}
			totalDefineLength += length;
		}

		return totalDefineLength;
	}

	/// <summary>
	/// Writes a series of '#define' string as lines to the given buffer, and ensures there are line breaks in-between them.
	/// </summary>
	/// <param name="_resultBuffer">The output buffer that defines should be written to.</param>
	/// <param name="_featureDefines">A list of defines that need to be written.</param>
	private static void WriteDefines(SourceCodeBuffer _resultBuffer, IList<string> _featureDefines)
	{
		if (_featureDefines is null || _featureDefines.Count == 0) return;

		// Write all defines first, ending each on a line break:
		foreach (string featureDefine in _featureDefines)
		{
			WriteString(_resultBuffer, featureDefine);
			if (!featureDefine.EndsWith('\n'))
			{
				WriteString(_resultBuffer, Environment.NewLine);
			}
		}
	}

	/// <summary>
	/// Local helper function for writing a string to UTF-8 byte buffer. 
	/// </summary>
	/// <param name="_targetBuffer">A source code buffer to write the string to.</param>
	/// <param name="_txt">The string we wish to write. All characters must be in the ASCII value range.</param>
	private static void WriteString(SourceCodeBuffer _targetBuffer, string _txt)
	{
		int prevLength = _targetBuffer.Length;
		for (int i = 0; i < _txt.Length; i++)
		{
			_targetBuffer.Utf8ByteBuffer[prevLength + i] = (byte)_txt[i];
		}
		_targetBuffer.Length += _txt.Length;
	}

	#endregion
}
