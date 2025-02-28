namespace FragEngine3.UI.Bindings;

/// <summary>
/// Enumeration of different sources that the value of a binding can come from.
/// </summary>
[Flags]
public enum UiBindingValueSource
{
	/// <summary>
	/// Value is set during initialization.
	/// </summary>
	Init        = 1,
	/// <summary>
	/// Value is set from the underlying model or data object.
	/// </summary>
	Model       = 2,
	/// <summary>
	/// Value is set from a view or control in the user interface.
	/// </summary>
	View        = 4,

	/// <summary>
	/// Mask with all supported value source flags.
	/// </summary>
	ALL         = Init | Model | View,
}
