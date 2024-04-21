using System.Numerics;
using Veldrid;

namespace FragEngine3.EngineCore.Input;

internal struct InputKeyAxes(Key _xNegative, Key _yNegative, Key _zNegative, Key _xPositive, Key _yPositive, Key _zPositive)
{
	#region Fields

	private readonly Key xNegative = _xNegative;
	private readonly Key yNegative = _yNegative;
	private readonly Key zNegative = _zNegative;

	private readonly Key xPositive = _xPositive;
	private readonly Key yPositive = _yPositive;
	private readonly Key zPositive = _zPositive;

	#endregion
	#region Properties

	public Vector3 Value { get; private set; } = Vector3.Zero;
	public Vector3 PrevValue { get; private set; } = Vector3.Zero;
	public Vector3 SmoothedValue { get; private set; } = Vector3.Zero;
	public readonly Vector3 ValueDiff => Value - PrevValue;

	#endregion
	#region Methods

	public void Update(in InputButtonState[] _keyStates, float _smoothingDiff)
	{
		PrevValue = Value;
		Value = new(
			GetAxis(in _keyStates, xNegative, xPositive),
			GetAxis(in _keyStates, yNegative, yPositive),
			GetAxis(in _keyStates, zNegative, zPositive));
		SmoothedValue = VectorExt.MoveTowards(SmoothedValue, Value, _smoothingDiff);
	}

	private static float GetAxis(in InputButtonState[] _keyStates, Key _keyNegative, Key _keyPositive)
	{
		float x = 0.0f;
		if (_keyStates[(int)_keyNegative].IsDown) x -= 1;
		if (_keyStates[(int)_keyPositive].IsDown) x += 1;
		return x;
	}
	
	#endregion
}
