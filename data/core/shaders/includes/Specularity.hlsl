#if defined(FEATURE_LIGHT) && defined(FEATURE_LIGHT_SOURCES)

/******************* MODELS: *******************/

half CalculateSpecularIntensity_Phong(const in float3 _surfaceNormal, const in float3 _lightDir, const in float3 _viewDir)
{
	float3 reflectedDir = reflect(_lightDir, _surfaceNormal);
	half d = dot(reflectedDir, _viewDir);
	half k = pow(d, roughness);
	return k;
}

half CalculateSpecularIntensity_BlinnPhong(const in float3 _surfaceNormal, const in float3 _lightDir, const in float3 _viewDir)
{
	half3 halfAngleDir = (half3)normalize(0.5 * (_lightDir + _viewDir));
	half d = dot(_surfaceNormal, halfAngleDir);
	half k = pow(d, roughness);
	return k;
}

half CalculateSpecularIntensity_Beckmann(const in float3 _surfaceNormal, const in float3 _lightDir, const in float3 _viewDir)
{
	static const half PI = 3.1415926;

	// Reference link: https://en.wikipedia.org/wiki/Specular_highlight#Beckmann_distribution
	half3 halfAngleDir = (half3)normalize(0.5 * (_lightDir + _viewDir));// H
	half c = max(dot(_surfaceNormal, halfAngleDir), 0);					// cos(a)
	half c2 = c * c;													// c^2
	half m2 = (half)roughness * (half)roughness;						// m^2
	half t2 = (1 - c2) / (c2 * m2);										// tan^2(a) / m^2 = (1 - c^2) / (c^2 * m^2)
	half k = exp(-t2) / (PI * m2 * c2 * c2);
	return k;
}

/***************** FUNCTIONS: ******************/

half CalculateSpecularIntensity(const in float3 _surfaceNormal, const in float3 _lightDir, const in float3 _viewDir)
{
#ifdef FEATURE_LIGHT_MODEL
	#if FEATURE_LIGHT_MODEL == Phong
		return CalculateSpecularIntensity_Phong(_surfaceNormal, _lightDir, _viewDir);
	#elif FEATURE_LIGHT_MODEL == BlinnPhong
		return CalculateSpecularIntensity_BlinnPhong(_surfaceNormal, _lightDir, _viewDir);
	#elif FEATURE_LIGHT_MODEL == Beckmann
		return CalculateSpecularIntensity_Beckmann(_surfaceNormal, _lightDir, _viewDir);
	#else
		return 0.0;
	#endif
#else
	return 0.0;
#endif //FEATURE_LIGHT_MODEL
}

#endif //FEATURE_LIGHT && FEATURE_LIGHT_SOURCES
