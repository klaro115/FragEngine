namespace FragEngine3.EngineCore.Input;

internal struct InputButtonState()
{
	#region Properties

	public bool IsDown { get; private set; } = false;
	public bool WasDown { get; private set; } = false;

	public readonly bool IsClicked => IsDown && !WasDown;
	public readonly bool IsReleased => !IsDown && WasDown;

	#endregion
	#region Methods

	public void Update(bool _newIsDown)
	{
		WasDown = IsDown;
		IsDown = _newIsDown;
	}

	#endregion
}
