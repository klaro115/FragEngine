using FragEngine3.EngineCore;
using System.Text;

namespace FragEngine3.Graphics.Resources.ShaderGen;

public sealed class ShaderGenContext(
	// Scope:
	string _scopeName,

	// Code generation:
	StringBuilder? _typesBuilder = null,
	StringBuilder? _functionsBuilder = null,
	StringBuilder? _localCodeBuilder = null,

	// Declarations:
	List<ShaderGenCodeDeclaration>? _globalTypes = null,
	List<ShaderGenCodeDeclaration>? _globalFunctions = null,
	List<ShaderGenFeature>? _localFeatures = null,
	List<ShaderGenVariable>? _localVariables = null)
{
	#region Fields

	// Scope:
	public readonly string scopeName = _scopeName ?? string.Empty;

	// Code generation:
	public readonly StringBuilder typesBuilder = _typesBuilder ?? new(512);
	public readonly StringBuilder functionsBuilder = _functionsBuilder ?? new(1024);
	public readonly StringBuilder localCodeBuilder = _localCodeBuilder ?? new(1024);

	public readonly StringBuilder templateBuilder = new(256);

	// Declarations:
	public readonly List<ShaderGenCodeDeclaration> globalTypes = _globalTypes ?? [];
	public readonly List<ShaderGenCodeDeclaration> globalFunctions = _globalFunctions ?? [];
	public readonly List<ShaderGenFeature> localFeatures = _localFeatures ?? [];
	public readonly List<ShaderGenVariable> localVariables = _localVariables ?? [];

	#endregion
	#region Properties

	public int LocalFeatureCount => localFeatures.Count;
	public int LocalVariableCount => localVariables.Count;

	#endregion
	#region Methods

	public bool HasTypeDeclaration(ShaderGenCodeDeclaration _globalType)
	{
		return _globalType?.Name != null && globalTypes.Any(o => ReferenceEquals(o, _globalType) || string.CompareOrdinal(o.Name, _globalType.Name) == 0);
	}

	public bool HasFunctionDeclaration(ShaderGenCodeDeclaration _globalFunction)
	{
		return _globalFunction?.Name != null && globalFunctions.Any(o => ReferenceEquals(o, _globalFunction) || string.CompareOrdinal(o.Name, _globalFunction.Name) == 0);
	}

	public bool HasLocalFeature(ShaderGenFeature _localFeature)
	{
		return _localFeature != null && localFeatures.Contains(_localFeature);
	}

	public bool HasLocalVariable(ShaderGenVariable _localVariable)
	{
		return _localVariable?.Name != null && localVariables.Any(o => ReferenceEquals(o, _localVariable) || string.CompareOrdinal(o.Name, _localVariable.Name) == 0);
	}
	public bool HasLocalVariable(string _localVariableName)
	{
		return !string.IsNullOrEmpty(_localVariableName) && localVariables.Any(o => string.CompareOrdinal(o.Name, _localVariableName) == 0);
	}

	public bool RegisterTypeDeclaration(ShaderGenCodeDeclaration _newGlobalType)
	{
		if (_newGlobalType?.Name == null)
		{
			Logger.Instance?.LogError($"Cannot add null or unnamed global type to shader generation context '{_scopeName}'!");
			return false;
		}
		// Already registered? Awesome. Do nothing:
		if (HasTypeDeclaration(_newGlobalType))
		{
			return true;
		}
		globalTypes.Add(_newGlobalType);
		return true;
	}

	public bool RegisterFunctionDeclaration(ShaderGenCodeDeclaration _newGlobalFunction)
	{
		if (_newGlobalFunction?.Name == null)
		{
			Logger.Instance?.LogError($"Cannot add null or unnamed global function to shader generation context '{_scopeName}'!");
			return false;
		}
		// Already registered? Awesome. Do nothing:
		if (HasFunctionDeclaration(_newGlobalFunction))
		{
			return true;
		}
		globalFunctions.Add(_newGlobalFunction);
		return true;
	}

	public bool RegisterLocalFeature(ShaderGenFeature _newFeature)
	{
		if (_newFeature == null)
		{
			Logger.Instance?.LogError($"Cannot add null feature to shader generation context '{_scopeName}'!");
			return false;
		}
		// Already registered? Awesome. Do nothing:
		if (localFeatures.Contains(_newFeature))
		{
			return true;
		}
		localFeatures.Add(_newFeature);
		return true;
	}

	public bool RegisterLocalVariable(ShaderGenVariable _newVariable)
	{
		if (_newVariable?.Name == null)
		{
			Logger.Instance?.LogError($"Cannot add null variable to shader generation context '{_scopeName}'!");
			return false;
		}
		// Already registered? Awesome. Do nothing:
		if (localVariables.Contains(_newVariable))
		{
			return true;
		}
		// Do not allow any redefinitions of variable names. Re-assignments must have been handled manually before calling for registration:
		if (HasLocalVariable(_newVariable))
		{
			Logger.Instance?.LogError($"Redefinition of variable name '{_newVariable.Name}' in generation context '{_scopeName}'!");
			return false;
		}
		localVariables.Add(_newVariable);
		return true;
	}

	public string AssembleAllCode()
	{
		int expectedCapacity =
			typesBuilder.Length +
			functionsBuilder.Length +
			localCodeBuilder.Length +
			200;

		StringBuilder builder = new(expectedCapacity);

		builder.Append("\r\n/******************** TYPES: *******************/\r\n");
		builder.Append(typesBuilder);

		builder.Append("\r\n/****************** FUNCTIONS: *****************/\r\n");
		builder.Append(functionsBuilder);

		builder.Append("\r\n/******************* SHADERS: ******************/\r\n");
		builder.Append(localCodeBuilder);

		return builder.ToString();
	}

	#endregion
}
