namespace FragEngine3.Graphics.Resources.Materials;

/// <summary>
/// Delegate for the '<see cref="Material.BoundResourcesChanged"/>' event.
/// </summary>
/// <param name="_material">The material whose bound resources have changed,</param>
public delegate void OnMaterialBoundResourcesChangedHandler(Material _material);

/// <summary>
/// Delegate for the '<see cref="Material.ReplacementsChanged"/>' event.
/// </summary>
/// <param name="_material">The material whose replacement materials have changed.</param>
public delegate void OnMaterialReplacementsChangedHandler(Material _material);
