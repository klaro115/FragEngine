
namespace FragEngine3.Scenes.Data
{
	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
	public sealed class ComponentDataTypeAttribute : Attribute
	{
		#region Constructors

		public ComponentDataTypeAttribute(Type _sceneElementType)
		{
			sceneElementType = _sceneElementType ?? typeof(object);
		}

		#endregion
		#region Fields

		public readonly Type sceneElementType;

		#endregion
	}
}
