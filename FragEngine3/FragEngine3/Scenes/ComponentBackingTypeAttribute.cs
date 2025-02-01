namespace FragEngine3.Scenes;

/// <summary>
/// Attribute for types inheriting from <see cref="Component"/> that are backed by, or wrappers around, another non-component instance type.
/// The backing type contains the actual logic, whilst the component type marked with this attribute only exposes the functionality to the
/// node-component system.
/// </summary>
/// <param name="_backingType">The underlying type that implements the actual logic of the component's functionality.</param>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ComponentBackingTypeAttribute(Type _backingType) : Attribute
{
	#region Fields

	public readonly Type backingType = _backingType ?? throw new ArgumentNullException(nameof(_backingType), "Backing type of component may not be null!");

	#endregion
}
