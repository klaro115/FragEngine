using FragEngine3.Utility.Unicode;

namespace FragEngine3.Graphics.Resources.Import.ShaderFormats;

/// <summary>
/// Helper class for setting or removing preprocessor '#define' macros in shader source code prior to compilation.
/// </summary>
public static class ShaderSourceCodeDefiner
{
	#region Types

	public sealed class SourceCodeBuffer(Action<SourceCodeBuffer> _funcReturnBufferToPool, int _minBufferCapacity)
	{
		private readonly Action<SourceCodeBuffer> funcReturnBufferToPool = _funcReturnBufferToPool;
		public byte[] Utf8ByteBuffer { get; private set; } = new byte[_minBufferCapacity];

		public int Length = 0;
		public int Capacity => Utf8ByteBuffer.Length;

		public void Resize(int _minCapacity, bool _copyExistingContents = true)
		{
			_minCapacity = Math.Max(_minCapacity, Utf8ByteBuffer.Length);
			byte[] prevBuffer = Utf8ByteBuffer;
			Utf8ByteBuffer = new byte[_minCapacity];
			prevBuffer.CopyTo(Utf8ByteBuffer, 0);
		}
		public void ReleaseBuffer()
		{
			Length = 0;
			funcReturnBufferToPool(this);
		}
	}

	#endregion
	#region Fields

	private static Stack<SourceCodeBuffer> bufferPool = [];
	private static readonly object bufferPoolLockObj = new();

	#endregion
	#region Constants

	private const string variantDefinePrefix = "#define VARIANT_";

	private const string variantDefineExt = variantDefinePrefix + "EXTENDED";
	private const string variantDefineBlend = variantDefinePrefix + "BLENDSHAPES";
	private const string variantDefineAnim = variantDefinePrefix + "ANIMATED";

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

	public static bool SetVariantDefines(byte[] _sourceCodeUtf8Bytes, MeshVertexDataFlags _variantFlags, bool _removeExistingDefines, out SourceCodeBuffer? _outResultBuffer)
	{
		if (_sourceCodeUtf8Bytes is null || _sourceCodeUtf8Bytes.Length == 0)
		{
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
		if (_variantFlags.HasFlag(MeshVertexDataFlags.ExtendedSurfaceData))
		{
			WriteString(_outResultBuffer, variantDefineExt);
		}
		if (_variantFlags.HasFlag(MeshVertexDataFlags.BlendShapes))
		{
			WriteString(_outResultBuffer, variantDefineBlend);
		}
		if (_variantFlags.HasFlag(MeshVertexDataFlags.Animations))
		{
			WriteString(_outResultBuffer, variantDefineAnim);
		}

		// Copy source code to buffer as-is:
		Array.Copy(_sourceCodeUtf8Bytes, 0, _outResultBuffer.Utf8ByteBuffer, _outResultBuffer.Length, sourceCodeLength);
		_outResultBuffer.Length += sourceCodeLength;

		// Return optional intermediate buffer to pool:
		intermediateBuffer?.ReleaseBuffer();

		return true;

		static void WriteString(SourceCodeBuffer _targetBuffer, string _txt)
		{
			int prevLength = _targetBuffer.Length;
			for (int i = 0; i < _txt.Length; i++)
			{
				_targetBuffer.Utf8ByteBuffer[prevLength + i] = (byte)_txt[i];
			}
			_targetBuffer.Length += _txt.Length;
		}
	}

	#endregion
}
