namespace FragEngine3.Utility;

/// <summary>
/// Extension and helper methods for dealing with floating-point types.
/// </summary>
public static class FloatExt
{
	#region Methods

	/// <summary>
	/// Checks if two intervals of numbers overlap.
	/// </summary>
	/// <param name="_minA">The lower bound of the 1st interval.</param>
	/// <param name="_maxA">The upper bound of the 1st interval.</param>
	/// <param name="_minB">The lower bound of the 2nd interval.</param>
	/// <param name="_maxB">The upper bound of the 2nd interval.</param>
	/// <returns>True if the intervals overlap, false if they don't.</returns>
	public static bool Overlaps(float _minA, float _maxA, float _minB, float _maxB)
	{
		return !(_minA > _maxB || _maxA < _minB);
	}

	/// <summary>
	/// Checks if two intervals of numbers overlap, and measures the bounds of the overlapping region.
	/// </summary>
	/// <param name="_minA">The lower bound of the 1st interval.</param>
	/// <param name="_maxA">The upper bound of the 1st interval.</param>
	/// <param name="_minB">The lower bound of the 2nd interval.</param>
	/// <param name="_maxB">The upper bound of the 2nd interval.</param>
	/// <param name="_outOverlapMin">Outputs the lower bound of the overlapping region, or 0, if there is no overlap.</param>
	/// <param name="_outOverlapMax">Outputs the upper bound of the overlapping region, or 0, if there is no overlap.</param>
	/// <returns>True if the intervals overlap, false if they don't.</returns>
	public static bool GetOverlap(float _minA, float _maxA, float _minB, float _maxB, out float _outOverlapMin, out float _outOverlapMax)
	{
		if (Overlaps(_minA, _maxA, _minB, _maxB))
		{
			_outOverlapMin = Math.Max(_minA, _minB);
			_outOverlapMax = Math.Min(_maxA, _maxB);
			return true;
		}
		_outOverlapMin = 0;
		_outOverlapMax = 0;
		return false;
	}

	/// <summary>
	/// Checks if one interval of numbers contains another interval.
	/// </summary>
	/// <param name="_minA">The lower bound of the 1st interval.</param>
	/// <param name="_maxA">The upper bound of the 1st interval.</param>
	/// <param name="_minB">The lower bound of the 2nd interval.</param>
	/// <param name="_maxB">The upper bound of the 2nd interval.</param>
	/// <returns>True if the 2nd intervals lies entirely within the bounds of the 1st, false otherwise.</returns>
	public static bool Contains(float _minA, float _maxA, float _minB, float _maxB)
	{
		return _minB >= _minA && _maxB <= _maxA;
	}

	#endregion
}
