﻿using System.Numerics;
using System.Runtime.InteropServices;
using Veldrid;

namespace FragEngine3.Graphics;

/// <summary>
/// A 32-bit color structure using 8 bits per channel, arranged in R8_G8_B8_A8 word order.
/// On a little-endian system, the byte order of components should be A8_B8_G8_R8.
/// Whether the color represented by the structure is in sRGB or in linear color space depends entirely on its source.
/// </summary>
[Serializable]
[StructLayout(LayoutKind.Sequential, Pack = 4, Size = byteSize)]
public struct Color32 : IEquatable<Color32>
{
	#region Constructors

	/// <summary>
	/// Creates a new color structure.
	/// </summary>
	/// <param name="_packedRgba">A packed 32-bit integer value where color channels are arranged in R8_G8_B8_A8 word order.</param>
	public Color32(uint _packedRgba)
	{
		packedValue = _packedRgba;
	}

	/// <summary>
	/// Creates a new color structure.
	/// </summary>
	/// <param name="_r">Red color component.</param>
	/// <param name="_g">Green color component.</param>
	/// <param name="_b">Blue color component.</param>
	/// <param name="_a">Alpha color component, this typically represents opacity.</param>
	public Color32(byte _r, byte _g, byte _b, byte _a)
	{
		packedValue = ((uint)_r << 24) | ((uint)_g << 16) | ((uint)_b << 8) | ((uint)_a << 0);
	}

	/// <summary>
	/// Creates a new color structure.
	/// </summary>
	/// <param name="_vectorRgba">A 4-dimensional vector where components XYZW map to RGBA respectively.
	/// All values are expected to be in the range of [0, 1], component values exceeding this will be clamped to the permissive range.</param>
	public Color32(Vector4 _vectorRgba)
	{
		uint r = (uint)Math.Clamp(_vectorRgba.X * 255, 0, 0xFF);
		uint g = (uint)Math.Clamp(_vectorRgba.Y * 255, 0, 0xFF);
		uint b = (uint)Math.Clamp(_vectorRgba.Z * 255, 0, 0xFF);
		uint a = (uint)Math.Clamp(_vectorRgba.W * 255, 0, 0xFF);
		packedValue = (r << 24) | (g << 16) | (b << 8) | (a << 0);
	}

	/// <summary>
	/// Creates a new color structure.
	/// </summary>
	/// <param name="_vectorRgba">A 4-dimensional vector where components XYZ map to RGB respectively.
	/// All values are expected to be in the range of [0, 1], component values exceeding this will be clamped to the permissive range.</param>
	public Color32(Vector3 _vectorRgb)
	{
		uint r = (uint)Math.Clamp(_vectorRgb.X * 255, 0, 0xFF);
		uint g = (uint)Math.Clamp(_vectorRgb.Y * 255, 0, 0xFF);
		uint b = (uint)Math.Clamp(_vectorRgb.Z * 255, 0, 0xFF);
		packedValue = (r << 24) | (g << 16) | (b << 8) | 0xFF;
	}

	/// <summary>
	/// Creates a new color structure.
	/// </summary>
	/// <param name="_color">A color in Veldrid's byte color type.</param>
	public Color32(RgbaByte _color)
	{
		packedValue = ((uint)_color.R << 24) | ((uint)_color.G << 16) | ((uint)_color.B << 8) | ((uint)_color.A << 0);
	}

	/// <summary>
	/// Creates a new color structure.
	/// </summary>
	/// <param name="_color">A color in Veldrid's float color type.</param>
	public Color32(RgbaFloat _color)
	{
		uint r = (uint)Math.Clamp(_color.R * 255, 0, 0xFF);
		uint g = (uint)Math.Clamp(_color.G * 255, 0, 0xFF);
		uint b = (uint)Math.Clamp(_color.B * 255, 0, 0xFF);
		uint a = (uint)Math.Clamp(_color.A * 255, 0, 0xFF);
		packedValue = (r << 24) | (g << 16) | (b << 8) | (a << 0);
	}

	#endregion
	#region Fields

	/// <summary>
	/// A packed 32-bit integer encoding a 4-component color value.
	/// </summary>
	public uint packedValue;

	#endregion
	#region Properties

	/// <summary>
	/// Gets or sets the red color component.
	/// </summary>
	public byte R
	{
		readonly get => (byte)((packedValue >> 24) & 0xFF);
		set => packedValue = (packedValue & 0x00FFFFFFu) | ((uint)value << 24);
	}
	/// <summary>
	/// Gets or sets the green color component.
	/// </summary>
	public byte G
	{
		readonly get => (byte)((packedValue >> 16) & 0xFF);
		set => packedValue = (packedValue & 0xFF00FFFFu) | ((uint)value << 16);
	}
	/// <summary>
	/// Gets or sets the blue color component.
	/// </summary>
	public byte B
	{
		readonly get => (byte)((packedValue >> 8) & 0xFF);
		set => packedValue = (packedValue & 0xFFFF00FFu) | ((uint)value << 8);
	}
	/// <summary>
	/// Gets or sets the alpha color component. This typically represents opacity.
	/// </summary>
	public byte A
	{
		readonly get => (byte)((packedValue >> 0) & 0xFF);
		set => packedValue = (packedValue & 0xFFFFFF00u) | ((uint)value << 0);
	}

	/// <summary>
	/// Luminance (Y) value of the color in a YCbCr color space.
	/// </summary>
	/// <remarks>RGB to YCbCr conversion implemented according to: https://sistenix.com/rgb2ycbcr.html</remarks>
	public readonly byte Y => (byte)(16 + (((R << 6) + (R << 1) + (G << 7) + G + (B << 4) + (B << 3) + B) >> 8));
	/// <summary>
	/// First chroma (Cb) value of the color in a YCbCr color space.
	/// </summary>
	public readonly byte Cb => (byte)(128 + ((-((R << 5) + (R << 2) + (R << 1)) - ((G << 6) + (G << 3) + (G << 1)) + (B << 7) - (B << 4)) >> 8));
	/// <summary>
	/// Second chroma (Cr) value of the color in a YCbCr color space.
	/// </summary>
	public readonly byte Cr => (byte)(128 + (((R << 7) - (R << 4) - ((G << 6) + (G << 5) - (G << 1)) - ((B << 4) + (B << 1))) >> 8));

	#endregion
	#region Constants

	/// <summary>
	/// The size of this color structure, in bytes.
	/// </summary>
	public const int byteSize = sizeof(uint);

	private const float inv255 = 1.0f / 255.0f;

	#endregion
	#region Methods

	/// <summary>
	/// Unpacks the 32-bit integer <see cref="packedValue"/> field into 4 color components.
	/// </summary>
	/// <param name="_outR">Outputs the color's red component</param>
	/// <param name="_outG">Outputs the color's green component</param>
	/// <param name="_outB">Outputs the color's blue component</param>
	/// <param name="_outA">Outputs the color's alpha component</param>
	public readonly void Unpack(out byte _outR, out byte _outG, out byte _outB, out byte _outA)
	{
		_outR = (byte)((packedValue >> 24) & 0xFF);
		_outG = (byte)((packedValue >> 16) & 0xFF);
		_outB = (byte)((packedValue >> 8) & 0xFF);
		_outA = (byte)((packedValue >> 0) & 0xFF);
	}
	/// <summary>
	/// Unpacks the 32-bit integer <see cref="packedValue"/> field into 4 bytes, in the order as bytes appear in memory.<para/>
	/// Note: This method assumes a little-endian CPU/OS architecture.
	/// </summary>
	/// <param name="_outByte0">Outputs the 1st byte of the packed color value.</param>
	/// <param name="_outByte1">Outputs the 2nd byte of the packed color value.</param>
	/// <param name="_outByte2">Outputs the 3rd byte of the packed color value.</param>
	/// <param name="_outByte3">Outputs the 4th byte of the packed color value.</param>
	public readonly void GetByteOrder(out byte _outByte0, out byte _outByte1, out byte _outByte2, out byte _outByte3)
	{
		_outByte0 = (byte)((packedValue >> 0) & 0xFF);
		_outByte1 = (byte)((packedValue >> 8) & 0xFF);
		_outByte2 = (byte)((packedValue >> 16) & 0xFF);
		_outByte3 = (byte)((packedValue >> 24) & 0xFF);
	}

	/// <summary>
	/// Gets the lowest value across all 4 color channels.
	/// </summary>
	public readonly byte Min() => Math.Min(Math.Min(R, G), Math.Min(B, A));
	/// <summary>
	/// Gets the highest value across all 4 color channels.
	/// </summary>
	public readonly byte Max() => Math.Max(Math.Max(R, G), Math.Max(B, A));

	/// <summary>
	/// Gets an inverted version of this color. For each color channel, the original value is subtracted from the maximum value, and assembled into a new color.<para/>
	/// NOTE: In floating-point arithmetic, this would be implemented as 1.0f minus the valued of the channel in a [0, 1] range, hence the name.
	/// </summary>
	public readonly Color32 OneMinus()
	{
		return new(uint.MaxValue - packedValue);
	}
	/// <summary>
	/// Gets a squared version of this color. For each color channel, the original value is multiplied by itself, and assembled into a new color.
	/// </summary>
	public readonly Color32 Squared()
	{
		return new Color32(
			(byte)((R * R) >> 8),
			(byte)((G * G) >> 8),
			(byte)((B * B) >> 8),
			(byte)((A * A) >> 8));
	}

	/// <summary>
	/// Converts the color's packed value to a word order of A8_R8_G8_B8.
	/// </summary>
	public readonly uint ToARGB()
	{
		// Move alpha byte to the front of the word:
		return ((packedValue >> 8) & 0x00FFFFFFu) | ((packedValue << 24) & 0xFF000000u);
	}

	/// <summary>
	/// Converts the color to Veldrid's floating-point color format.
	/// </summary>
	public readonly RgbaFloat ToRgbaFloat()
	{
		return new(R * inv255, G * inv255, B * inv255, A * inv255);
	}

	/// <summary>
	/// Converts the color to Veldrid's integer color format.
	/// </summary>
	public readonly RgbaByte ToRgbaByte()
	{
		return new(R, G, B, A);
	}

	/// <summary>
	/// Linear interpolation from one color to another.
	/// </summary>
	/// <param name="_a">The first color, at k=0.</param>
	/// <param name="_b">The second color, at k=1.</param>
	/// <param name="_k">Interpolation factor, in the range from 0.0f to 1.0f.</param>
	/// <returns></returns>
	public static Color32 Lerp(Color32 _a, Color32 _b, float _k)
	{
		uint t = (uint)Math.Clamp(_k * 255, 0, 255);
		uint it = 255 - t;
		return new Color32(
			(byte)((_a.R * it + _b.R * t) >> 8),
			(byte)((_a.G * it + _b.G * t) >> 8),
			(byte)((_a.B * it + _b.B * t) >> 8),
			(byte)((_a.A * it + _b.A * t) >> 8));
	}
	/// <summary>
	/// Linear interpolation from one color to another.
	/// </summary>
	/// <param name="_a">The first color, at k=0.</param>
	/// <param name="_b">The second color, at k=255.</param>
	/// <param name="_k">Interpolation factor, in the range from 0 (0x00) to 255 (0xFF).</param>
	/// <returns></returns>
	public static Color32 Lerp(Color32 _a, Color32 _b, byte _k)
	{
		uint it = 255 - (uint)_k;
		return new Color32(
			(byte)((_a.R * it + _b.R * _k) >> 8),
			(byte)((_a.G * it + _b.G * _k) >> 8),
			(byte)((_a.B * it + _b.B * _k) >> 8),
			(byte)((_a.A * it + _b.A * _k) >> 8));
	}

	/// <summary>
	/// Component-wise addition of one color's values onto another.
	/// </summary>
	/// <param name="left">The first color.</param>
	/// <param name="right">The second color.</param>
	/// <returns>A new color with added component values; values cannot exceed 255.</returns>
	public static Color32 operator +(Color32 left, Color32 right)
	{
		return new Color32(
			(byte)Math.Min(left.R + right.R, 0xFF),
			(byte)Math.Min(left.G + right.G, 0xFF),
			(byte)Math.Min(left.B + right.B, 0xFF),
			(byte)Math.Min(left.A + right.A, 0xFF));
	}
	/// <summary>
	/// Component-wise subtraction of one color's values from another.
	/// </summary>
	/// <param name="left">The first color.</param>
	/// <param name="right">The second color.</param>
	/// <returns>A new color with subtracted component values; values cannot drop below 0.</returns>
	public static Color32 operator -(Color32 left, Color32 right)
	{
		return new Color32(
			(byte)Math.Max(left.R - right.R, 0),
			(byte)Math.Max(left.G - right.G, 0),
			(byte)Math.Max(left.B - right.B, 0),
			(byte)Math.Max(left.A - right.A, 0));
	}

	/// <summary>
	/// Component-wise multiplication of one color's saturation with another.<para/>
	/// This multiplication treats color component values as percentages, normalized to a 0..255 value range.
	/// </summary>
	/// <param name="left">The first color.</param>
	/// <param name="right">The second color.</param>
	/// <returns>A new color with multiplied component values.</returns>
	public static Color32 operator *(Color32 left, Color32 right)
	{
		return new Color32(
			(byte)((left.R * right.R) >> 8),
			(byte)((left.G * right.G) >> 8),
			(byte)((left.B * right.B) >> 8),
			(byte)((left.A * right.A) >> 8));
	}
	/// <summary>
	/// Multiplies all component value with a scalar value.
	/// </summary>
	/// <param name="_color32">A color.</param>
	/// <param name="_multiplier">The factor that all components are multiplied with.</param>
	/// <returns>A new color with multiplied component value; values are clamped to the 0..255 range.</returns>
	public static Color32 operator *(Color32 _color32, float _multiplier)
	{
		return new Color32(
			(byte)Math.Clamp(_color32.R * _multiplier, 0, 0xFF),
			(byte)Math.Clamp(_color32.G * _multiplier, 0, 0xFF),
			(byte)Math.Clamp(_color32.B * _multiplier, 0, 0xFF),
			(byte)Math.Clamp(_color32.A * _multiplier, 0, 0xFF));
	}

	public static bool operator ==(Color32 left, Color32 right) => left.packedValue == right.packedValue;
	public static bool operator !=(Color32 left, Color32 right) => left.packedValue != right.packedValue;

	public static explicit operator Vector3(Color32 _color32) => new Vector3(_color32.R, _color32.G, _color32.B) * inv255;
	public static explicit operator Vector4(Color32 _color32) => new Vector4(_color32.R, _color32.G, _color32.B, _color32.A) * inv255;
	public static explicit operator Color32(Vector4 _vectorRgba) => new(_vectorRgba);
	public static explicit operator Color32(Vector3 _vectorRgb) => new(_vectorRgb);

	public override readonly bool Equals(object? obj) => obj is Color32 other && packedValue == other.packedValue;
	public readonly bool Equals(Color32 other) => packedValue == other.packedValue;
	public override readonly int GetHashCode() => packedValue.GetHashCode();

	public override readonly string ToString()
	{
		return $"(R: {R}, G: {G}, B: {B}, A: {A})";
	}

	/// <summary>
	/// Create a formatted hexadecimal representation of the color value. Components are ordered in RGBA order.
	/// </summary>
	public readonly string ToHexString()
	{
		return $"{R:X2}{G:X2}{B:X2}{A:X2}";
	}
	/// <summary>
	/// Create a formatted lower-case hexadecimal representation of the color value. Components are ordered in RGBA order.
	/// </summary>
	public readonly string ToHexStringLower()
	{
		return $"{R:x2}{G:x2}{B:x2}{A:x2}";
	}

	/// <summary>
	/// Try parsing a string into a 32-bit color.
	/// </summary>
	/// <param name="_hexString">A string with 6 or 8 hexadecimal characters. Components must be in RGBA order.</param>
	/// <returns>A color parsed from the string, or transparent black, if parsing fails.</returns>
	public static Color32 ParseHexString(string _hexString)
	{
		if (string.IsNullOrEmpty(_hexString) || _hexString.Length < 6)
		{
			return TransparentBlack;
		}
		uint r = (ParseDigit(0) << 4) | ParseDigit(1);
		uint g = (ParseDigit(2) << 4) | ParseDigit(3);
		uint b = (ParseDigit(4) << 4) | ParseDigit(5);
		uint a = 0xFF;
		if (_hexString.Length >= 8)
		{
			a = (ParseDigit(6) << 4) | ParseDigit(7);
		}
		return new((byte)r, (byte)g, (byte)b, (byte)a);

		uint ParseDigit(int _index)
		{
			uint c = _hexString[_index];
			if (c >= 'a')
				return c - 'a' + 10;
			else if (c >= 'A')
				return c - 'A' + 10;
			else
				return c - '0';
		}
	}

	/// <summary>
	/// Try parsing a string into a 32-bit color.
	/// </summary>
	/// <param name="_hexString">A sequence of 6 or 8 hexadecimal characters. Components must be in RGBA order.</param>
	/// <returns>A color parsed from the character sequence, or transparent black, if parsing fails.</returns>
	public static Color32 ParseHexString(ReadOnlySpan<char> _hexString)
	{
		if (_hexString.IsEmpty || _hexString.Length < 6)
		{
			return TransparentBlack;
		}
		uint r = (ParseDigit(_hexString[0]) << 4) | ParseDigit(_hexString[1]);
		uint g = (ParseDigit(_hexString[2]) << 4) | ParseDigit(_hexString[3]);
		uint b = (ParseDigit(_hexString[4]) << 4) | ParseDigit(_hexString[5]);
		uint a = 0xFF;
		if (_hexString.Length >= 8)
		{
			a = (ParseDigit(_hexString[6]) << 4) | ParseDigit(_hexString[7]);
		}
		return new((byte)r, (byte)g, (byte)b, (byte)a);

		static uint ParseDigit(uint c)
		{
			if (c >= 'a')
				return c - 'a' + 10;
			else if (c >= 'A')
				return c - 'A' + 10;
			else
				return c - '0';
		}
	}

	#endregion
	#region Standard Colors

	public static Color32 White =>				new(0xFFFFFFFFu);
	public static Color32 Black =>				new(0x000000FFu);
	public static Color32 TransparentBlack =>	new(0x00000000u);

	public static Color32 Red =>				new(0xFF0000FFu);
	public static Color32 Green =>				new(0x00FF00FFu);
	public static Color32 Blue =>				new(0x0000FFFFu);

	public static Color32 Cornflower =>			new(0x6495EDFFu);	// ARGB: 0xFF6495EDu

	#endregion
}
