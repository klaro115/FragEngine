namespace FragEngine3.UI.Bindings;

[Flags]
public enum UiBindingValueSource
{
	Init        = 1,
	Model       = 2,
	View        = 4,

	ALL         = Init | Model | View,
}
