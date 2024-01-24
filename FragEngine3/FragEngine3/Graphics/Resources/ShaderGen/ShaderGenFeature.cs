using FragEngine3.EngineCore;

namespace FragEngine3.Graphics.Resources.ShaderGen;

[Serializable]
public sealed class ShaderGenFeature
{
	#region Properties

	// IDENTIFIERS:

	public string Name { get; set; } = string.Empty;

	// DATA FLOW:

	public ShaderGenVariable[]? Inputs { get; set; } = null;
	public ShaderGenVariable[]? Outputs { get; set; } = null;
	public ShaderGenVariable[]? InternalVariables { get; set; } = null;

	// CODE TEMPLATES:

	/// <summary>
	/// HLSL code block of additional types that need to be added before this feature's declaring features.
	/// </summary>
	public ShaderGenCodeDeclaration[]? TypesCode { get; set; } = null;
	/// <summary>
	/// HLSL code block of additional functions that need to be added before this feature's declaring feature.
	/// </summary>
	public ShaderGenCodeDeclaration[]? FunctionsCode { get; set; } = null;
	/// <summary>
	/// HLSL code block that needs to inserted where the feature's usage is declared.
	/// </summary>
	public ShaderGenCodeDeclaration? InsertCode { get; set; } = null;

	// REQUIREMENTS:

	/// <summary>
	/// Flags of the different vertex buffers that need to be bound and declared, because this feature relies on those declarations.<para/>
	/// NOTE: Basic surface data is always available, and extended data should be available for most static renderers. Blend shapes and
	/// bone animations are supported only by a small selection of animated renderers.
	/// </summary>
	public MeshVertexDataFlags RequiredVertexFlags { get; set; } = MeshVertexDataFlags.BasicSurfaceData;
	/// <summary>
	/// Other features that this feature is dependent on, and whose functions must have been declared before any code emitted for this feature.
	/// </summary>
	public string[]? Dependencies { get; set; } = null;

	#endregion
	#region Methods

	/// <summary>
	/// Creates an identical copy of this feature. Deep copies are made for all variable declarations, as these may be mutable per instance.
	/// Dependencies do not change even across modifications to the feature declaration and are assigned directly.
	/// </summary>
	public ShaderGenFeature Clone() => new()
	{
		// Identifiers:
		Name = Name ?? string.Empty,

		// Data flow:
		Inputs = (ShaderGenVariable[]?)Inputs?.Clone(),
		Outputs = (ShaderGenVariable[]?)Outputs?.Clone(),
		InternalVariables = (ShaderGenVariable[]?)InternalVariables?.Clone(),

		// Code templates:
		TypesCode = TypesCode,
		FunctionsCode = FunctionsCode,
		InsertCode = InsertCode,

		// Requirements:
		RequiredVertexFlags = RequiredVertexFlags,
		Dependencies = Dependencies,
	};

	public int GetInputCount() => Inputs != null ? Inputs.Length : 0;
	public int GetOutputCount() => Inputs != null ? Inputs.Length : 0;

	public bool HasInput(string _variableName)
	{
		if (string.IsNullOrEmpty(_variableName)) return false;
		if (GetInputCount() == 0) return false;

		return Inputs!.Any(o => string.CompareOrdinal(o.Name, _variableName) == 0);
	}

	public bool HasOutput(string _variableName)
	{
		if (string.IsNullOrEmpty(_variableName)) return false;
		if (GetInputCount() == 0) return false;

		return Outputs!.Any(o => string.CompareOrdinal(o.Name, _variableName) == 0);
	}

	public bool HasVariable(string _variableName)
	{
		if (string.IsNullOrEmpty(_variableName)) return false;

		return
			(Inputs != null && Inputs.Any(o => string.CompareOrdinal(o.Name, _variableName) == 0)) ||
			(Outputs != null && Outputs.Any(o => string.CompareOrdinal(o.Name, _variableName) == 0)) ||
			(InternalVariables != null && InternalVariables.Any(o => string.CompareOrdinal(o.Name, _variableName) == 0));
	}

	/// <summary>
	/// Try to retrieve a specific variable by name.
	/// </summary>
	/// <param name="_variableName">The name of the variable you're looking for, as it would appear in HLSL code. May not be null or blank. Spaces and special symbols are not allowed.</param>
	/// <param name="_outVariable">Outputs the first matching variable that was found. If no matching variable exists, an invalid variable is returned instead.</param>
	/// <param name="_outVariableType">Outputs the usage/type of the variable if it was found.</param>
	/// <returns>True if a variable of that name is declared or used by this feature, false otherwise.</returns>
	public bool GetVariable(string _variableName, out ShaderGenVariable _outVariable, out ShaderGenVariableType _outVariableType)
	{
		if (string.IsNullOrEmpty(_variableName))
		{
			_outVariable = ShaderGenVariable.None;
			_outVariableType = ShaderGenVariableType.None;
			return false;
		}

		if (TryGetVariable(Inputs, out _outVariable))
		{
			_outVariableType = ShaderGenVariableType.Input;
			return true;
		}
		if (TryGetVariable(Outputs, out _outVariable))
		{
			_outVariableType = ShaderGenVariableType.Output;
			return true;
		}
		if (TryGetVariable(InternalVariables, out _outVariable))
		{
			_outVariableType = ShaderGenVariableType.Internal;
			return true;
		}
		return false;


		// Local helper function for finding a variable in a given array:
		bool TryGetVariable(ShaderGenVariable[]? _variableArray, out ShaderGenVariable _outMatch)
		{
			if (_variableArray != null)
			{
				foreach (ShaderGenVariable variable in _variableArray)
				{
					if (string.CompareOrdinal(variable.Name, _variableName) == 0)
					{
						_outMatch = variable;
						return true;
					}
				}
			}
			_outMatch = ShaderGenVariable.None;
			return false;
		}
	}

	public List<string> GetTemplatedVariableNames()
	{
		List<string> templateName = [];

		AddTemplatedNames(Inputs);
		AddTemplatedNames(Outputs);
		AddTemplatedNames(InternalVariables);

		return templateName;


		void AddTemplatedNames(ShaderGenVariable[]? _variableArray)
		{
			if (_variableArray != null)
			{
				foreach (ShaderGenVariable variable in _variableArray)
				{
					if (!string.IsNullOrEmpty(variable.TemplatedName) &&
						!templateName.Contains(variable.TemplatedName))
					{
						templateName.Add(variable.TemplatedName);
					}
				}
			}
		}
	}

	public bool CreateTypesCode(ShaderGenContext _ctx, List<string>? _featureTemplateNames = null)
	{
		if (_ctx == null) return false;
		if (TypesCode == null || TypesCode.Length == 0) return true;

		// If no template were pre-fetched, do so now:
		_featureTemplateNames ??= GetTemplatedVariableNames();

		// Iterate over and generate code for types in global scope:
		foreach (ShaderGenCodeDeclaration globalType in TypesCode)
		{
			// No need to redeclare an existing type:
			if (_ctx.HasTypeDeclaration(globalType)) continue;

			// Register the type:
			if (!_ctx.RegisterTypeDeclaration(globalType))
			{
				Logger.Instance?.LogError($"Failed to register type declaration '{globalType?.Name ?? "NULL"}' for shader feature '{this}'!");
				return false;
			}

			// Generate code:
			if (!globalType.CreateCode(_ctx.typesBuilder, _ctx.templateBuilder, _featureTemplateNames))
			{
				Logger.Instance?.LogError($"Failed to generate code of type declaration '{globalType?.Name ?? "NULL"}' for shader feature '{this}'!");
				return false;
			}
		}

		return true;
	}

	public bool CreateFunctionsCode(ShaderGenContext _ctx, List<string>? _featureTemplateNames = null)
	{
		if (_ctx == null) return false;
		if (FunctionsCode == null || FunctionsCode.Length == 0) return true;

		// If no template were pre-fetched, do so now:
		_featureTemplateNames ??= GetTemplatedVariableNames();

		// Iterate over and generate code for functions in global scope:
		foreach (ShaderGenCodeDeclaration globalFunction in FunctionsCode)
		{
			// No need to redeclare an existing function:
			if (_ctx.HasFunctionDeclaration(globalFunction)) continue;

			// Register the function:
			if (!_ctx.RegisterFunctionDeclaration(globalFunction))
			{
				Logger.Instance?.LogError($"Failed to register function declaration '{globalFunction?.Name ?? "NULL"}' for shader feature '{this}'!");
				return false;
			}

			// Generate code:
			if (!globalFunction.CreateCode(_ctx.functionsBuilder, _ctx.templateBuilder, _featureTemplateNames))
			{
				Logger.Instance?.LogError($"Failed to generate code of function declaration '{globalFunction?.Name ?? "NULL"}' for shader feature '{this}'!");
				return false;
			}
		}

		return true;
	}

	public bool CreateInsertCode(ShaderGenContext _ctx, List<string>? _featureTemplateNames = null)
	{
		if (_ctx == null) return false;
		if (InsertCode == null) return true;

		// Register all variables with context:
		bool success = true;
		if (Inputs != null)
		{
			foreach (ShaderGenVariable variable in Inputs)
			{
				if (!_ctx.HasLocalVariable(variable))
				{
					success &= _ctx.RegisterLocalVariable(variable);
				}
			}
		}
		if (Outputs != null)
		{
			foreach (ShaderGenVariable variable in Outputs)
			{
				if (!_ctx.HasLocalVariable(variable))
				{
					success &= _ctx.RegisterLocalVariable(variable);
				}
			}
		}
		if (InternalVariables != null)
		{
			foreach (ShaderGenVariable variable in InternalVariables)
			{
				if (!_ctx.HasLocalVariable(variable))
				{
					success &= _ctx.RegisterLocalVariable(variable);
				}
			}
		}
		if (!success)
		{
			Logger.Instance?.LogError($"Failed to register local variables of shader feature '{this}'! Check for naming collisions!");
			return false;
		}

		// Register feature in local scope:
		if (!_ctx.RegisterLocalFeature(this))
		{
			Logger.Instance?.LogError($"Failed to register local feature '{this}'!");
			return false;
		}

		// If no template were pre-fetched, do so now:
		if (_featureTemplateNames == null && InsertCode.IsCodeTemplated())
		{
			_featureTemplateNames = GetTemplatedVariableNames();
		}

		// Generate code:
		if (!InsertCode.CreateCode(_ctx.localCodeBuilder, _ctx.templateBuilder, _featureTemplateNames!))
		{
			Logger.Instance?.LogError($"Failed to generate locally inserted code for shader feature '{this}'!");
			return false;
		}

		return true;
	}

	public bool CreateAllCode(ShaderGenContext _ctx)
	{
		List<string> featureTemplateNames = GetTemplatedVariableNames();

		return
			CreateTypesCode(_ctx, featureTemplateNames) &&
			CreateFunctionsCode(_ctx, featureTemplateNames) &&
			CreateInsertCode(_ctx, featureTemplateNames);
	}

	#endregion
}
