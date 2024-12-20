using FragEngine3.EngineCore;
using FragEngine3.UI.Bindings.Internal;
using System.Reflection;

namespace FragEngine3.UI.Bindings;

public sealed class UiPathBinding<TRoot, TValue>(TRoot _rootObject, string _memberPath, bool _allowPrivateMembers = false) : UiBinding<TValue> where TRoot : class
{
	#region Fields

	private readonly UiBindingPathPart[] pathParts = CreatePathParts(_memberPath, _allowPrivateMembers);

	#endregion
	#region Properties

	public TRoot Root { get; set; } = _rootObject;

	#endregion
	#region Methods

	private static UiBindingPathPart[] CreatePathParts(string _memberPath, bool _allowPrivateMembers)   //TODO: Add flags to allow private members if parameter is set!
	{
		string[] nameParts = _memberPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
		UiBindingPathPart[] pathParts = new UiBindingPathPart[nameParts.Length];

		Type targetType = typeof(TRoot);
		try
		{
			for (int i = 0; i < nameParts.Length; ++i)
			{
				string namePart = nameParts[i];
				FieldInfo? field = targetType.GetField(namePart);
				if (field is not null)
				{
					pathParts[i] = new UiBindingPathFieldPart(field);
				}
				else
				{
					PropertyInfo? property = targetType.GetProperty(namePart);
					if (property is not null)
					{
						pathParts[i] = new UiBindingPathPropertyPart(property);
					}
					else
					{
						Logger.Instance?.LogError($"Invalid part name '{namePart}' in path '{_memberPath}'!");
						return [];
					}
				}
			}
			return pathParts;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException($"Failed to parse binding member path '{targetType.Name}:{_memberPath}'", ex);
			return [];
		}
	}

	public override TValue GetValue()
	{
		try
		{
			object? value = Root;
			foreach (UiBindingPathPart part in pathParts)
			{
				value = part.GetValue(value);
			}
			return (TValue)value!;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to get value from path binding!", ex);
			return default!;
		}
	}

	public override bool SetValue(TValue _newValue, UiBindingValueSource _source)
	{
		if (!AllowedSourceMask.HasFlag(_source)) return false;

		try
		{
			// Navigate down the path:
			object? partObject = Root;
			UiBindingPathPart part = pathParts[0];
			for (int i = 0; i < pathParts.Length - 1; ++i)
			{
				part = pathParts[i];
				partObject = part.GetValue(partObject);
			}
			// Set value on last path part:
			part.SetValue(partObject, _newValue);

			SourceOfLastChange = _source;
			NotifyValueChanged(_newValue, _source);
			return true;
		}
		catch (Exception ex)
		{
			Logger.Instance?.LogException("Failed to set value on path binding!", ex);
			return false;
		}
	}

	public string GetPath()
	{
		string path = string.Empty;
		for (int i = 0; i < pathParts.Length; i++)
		{
			if (i != 0)
			{
				path += '.';
			}
			path += pathParts[i].PartName;
		}
		return path;
	}

	public override string ToString()
	{
		return $"PathBinding<{typeof(TValue).Name}> | Path='{GetPath()}'";
	}

	#endregion
}
