namespace FragEngine3.Graphics.Internal
{
	[Serializable]
	public sealed class GraphicsCapabilities
	{
		#region Types

		public readonly struct Range
		{
			public Range(int _min, int _max)
			{
				min = _min;
				max = _max;
			}

			public readonly int min;
			public readonly int max;

			public int Size => max - min;
		}

		public readonly struct DepthStencilFormat
		{
			public DepthStencilFormat(int _depthMapDepth, int _stencilMapDepth)
			{
				depthMapDepth = _depthMapDepth;
				stencilMapDepth = _stencilMapDepth;
			}

			public readonly int depthMapDepth;
			public readonly int stencilMapDepth;

			public bool HasDepthMap => depthMapDepth != 0;
			public bool HasStencilMap => stencilMapDepth != 0;
		}

		#endregion
		#region Constructors

		public GraphicsCapabilities() { }

		public GraphicsCapabilities(
			int _minResX,				int _minResY,
			int _maxResX,				int _maxResY,
			int _minChannelCount,		int _maxChannelCount,
			int _minChannelBitDepth,	int _maxChannelBitDepth,
			int _minPixelByteSize,		int _maxPixelByteSize,
			int _textureAlignmentX,		int _textureAlignmentY,
			int _kernelPixelAlignment,
			DepthStencilFormat[] _depthStencilMaps,
			int[] _outputChannelBitDepths)
		{
			minResolutionX = _minResX;
			minResolutionY = _minResY;
			maxResolutionX = _maxResX;
			maxResolutionY = _maxResY;

			minChannelCount = _minChannelCount;
			maxChannelCount = _maxChannelCount;

			minChannelBitDepth = _minChannelBitDepth;
			maxChannelBitDepth = _maxChannelBitDepth;

			minPixelByteSize = _minPixelByteSize;
			maxPixelByteSize = _maxPixelByteSize;

			textureAlignmentX = _textureAlignmentX;
			textureAlignmentY = _textureAlignmentY;
			kernelPixelAlignment = _kernelPixelAlignment;

			depthStencilFormats = _depthStencilMaps ?? Array.Empty<DepthStencilFormat>();
			outputChannelBitDepths = _outputChannelBitDepths ?? Array.Empty<int>();
		}

		#endregion
		#region Fields

		public readonly int minResolutionX = 640;
		public readonly int minResolutionY = 480;

		public readonly int maxResolutionX = 7680;
		public readonly int maxResolutionY = 4320;

		public readonly int minChannelCount = 1;					// R8_UNORM = 1 channel
		public readonly int maxChannelCount = 4;					// R8G8B8A8_UNORM = 4 channels

		public readonly int minChannelBitDepth = 5;					// RGB565 = 5 bits
		public readonly int maxChannelBitDepth = 32;				// R32_FLOAT = 32 bits

		public readonly int minPixelByteSize = 1 * sizeof(byte);	// R8_UNORM = 1
		public readonly int maxPixelByteSize = 4 * sizeof(float);   // R32B32G32A32_FLOAT = 16

		public readonly int textureAlignmentX = 8;
		public readonly int textureAlignmentY = 8;
		public readonly int kernelPixelAlignment = 8;

		public readonly DepthStencilFormat[] depthStencilFormats = new DepthStencilFormat[]
		{
			// Depth only:
			new DepthStencilFormat(16, 0),
			new DepthStencilFormat(32, 0),

			// Depth & Stencil:
			new DepthStencilFormat(16, 8),
			new DepthStencilFormat(24, 8),
			new DepthStencilFormat(32, 8),

			// Stencil only:
			new DepthStencilFormat(0, 8),
		};
		public readonly int[] outputChannelBitDepths = new int[]
		{
			8, 10, 16
		};

		public bool computeShaders = true;
		public bool geometryShaders = false;
		public bool tesselationShaders = false;
		public bool textures1D = true;

		#endregion
		#region Properties

		public int DepthStencilFormatCount => depthStencilFormats != null ? depthStencilFormats.Length : 0;

		public Range ResolutionX => new(minResolutionX, maxResolutionX);
		public Range ResolutionY => new(minResolutionY, maxResolutionY);

		public Range ChannelCounts => new(minChannelCount, maxChannelCount);
		public Range ChannelBitDepths => new(minChannelBitDepth, maxChannelBitDepth);
		public Range PixelByteSizes => new(minPixelByteSize, maxPixelByteSize);

		#endregion
		#region Methods

		public bool GetBestOutputBitDepth(int _requestedBitDepth, out int _outBestBitDepth)
		{
			if (outputChannelBitDepths.Contains(_requestedBitDepth))
			{
				_outBestBitDepth = _requestedBitDepth;
				return true;
			}

			int bestDiff = 1000;
			_outBestBitDepth = 8;
			foreach (int supportedBitDepth in outputChannelBitDepths)
			{
				int diff = Math.Abs(_requestedBitDepth - supportedBitDepth);
				if (diff < bestDiff)
				{
					bestDiff = diff;
					_outBestBitDepth = supportedBitDepth;
				}
			}
			return bestDiff < 1000;
		}

		#endregion
	}
}
