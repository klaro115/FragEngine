
namespace FragEngine3.Scenes.Data
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class ComponentDataTypeAttribute(Type _sceneElementType) : Attribute
	{
		#region Fields

		public readonly Type sceneElementType = _sceneElementType ?? typeof(object);

		#endregion
	}
}
