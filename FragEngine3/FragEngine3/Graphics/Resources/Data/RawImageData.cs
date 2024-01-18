using FragEngine3.EngineCore;
using System.Collections;
using Veldrid;

namespace FragEngine3.Graphics.Resources.Data
{
	public sealed class RawImageData : IEnumerable<Color32>
	{
		#region Fields

		public PixelFormat pixelFormat = PixelFormat.B8_G8_R8_A8_UNorm_SRgb;
		public uint width = 0;
		public uint height = 0;
		public uint dpi = 0;

		public Color32[] pixelData = [];

		#endregion
		#region Properties

		public bool IsValid => pixelData != null && PixelCount <= pixelData.Length;

		public uint PixelCount => width * height;
		public uint DataCount => Math.Min(pixelData != null ? (uint)pixelData.Length : 0u, PixelCount);

		public uint MaxDataByteSize => width * height * Color32.byteSize;

		public Color32 this[uint _pixelIdx]
		{
			get => _pixelIdx < pixelData.Length ? pixelData[_pixelIdx] : Color32.TransparentBlack;
			set
			{
				if (_pixelIdx < pixelData.Length) pixelData[_pixelIdx] = value;
			}
		}
		
		public Color32 this[uint _x, uint _y]
		{
			get
			{
				uint pixelIdx = _y * width + _x;
				return pixelIdx < pixelData.Length ? pixelData[pixelIdx] : Color32.TransparentBlack;
			}
			set
			{
				uint pixelIdx = _y * width + _x;
				if (pixelIdx < pixelData.Length) pixelData[pixelIdx] = value;
			}
		}

		#endregion
		#region Methods

		/// <summary>
		/// Reverse row order of the image, therefore mirroring its content vertically.<para/>
		/// NOTE: This operation is done as in-place as possible, using a small single-row buffer as intermediate storage.
		/// </summary>
		/// <returns>True if the image was mirrored vertically, false if the image was invalid.</returns>
		public bool MirrorVertically()
		{
			if (!IsValid)
			{
				Logger.Instance?.LogError("Cannot mirror invalid raw image along Y-axis!");
				return false;
			}

			uint halfHeight = height / 2;

			Color32[] rowBuffer = new Color32[width];
			for (uint y = 0; y < halfHeight; ++y)
			{
				long srcLineStartIdx = y * width;
				long dstLineStartIdx = (height - y - 1) * width;

				Array.Copy(pixelData, srcLineStartIdx, rowBuffer, 0, width);				// src => buffer
				Array.Copy(pixelData, dstLineStartIdx, rowBuffer, srcLineStartIdx, width);  // dst => src
				rowBuffer.CopyTo(pixelData, dstLineStartIdx);								// buffer => dst
			}

			return true;
		}

		public IEnumerator<Color32> GetEnumerator()
		{
			for (uint i = 0; i < DataCount; ++i)
			{
				yield return pixelData[i];
			}
		}
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public override string ToString()
		{
			return $"RawImageData (Resolution: {width}x{height}, Format: {pixelData}, Size: {MaxDataByteSize} bytes)";
		}

		#endregion
	}
}
