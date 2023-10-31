
namespace FragEngine3.Scenes.EventSystem
{
	public delegate void FuncReceiveComponentEvent(SceneEventType _eventType, object? _eventData = null);

	public sealed record ComponentEventListener
	{
		#region Constructors

		public ComponentEventListener(Component _target, FuncReceiveComponentEvent _funcReceiver)
		{
			target = _target;
			funcReceiver = _funcReceiver;
		}

		#endregion
		#region Fields

		public readonly Component target;
		public readonly FuncReceiveComponentEvent funcReceiver;

		#endregion
		#region Properties

		public bool IsValid => target != null && !target.IsDisposed && funcReceiver != null;

		#endregion
	}
}
