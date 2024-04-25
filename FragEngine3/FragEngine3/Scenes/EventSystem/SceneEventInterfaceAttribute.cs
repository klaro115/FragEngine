namespace FragEngine3.Scenes.EventSystem;

/// <summary>
/// Attribute identifying an interface that responds to a specific scene event.
/// </summary>
/// <param name="_eventType">The type of event that this type listens to.</param>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class SceneEventInterfaceAttribute(SceneEventType _eventType) : Attribute
{
	public readonly SceneEventType eventType = _eventType;
}
