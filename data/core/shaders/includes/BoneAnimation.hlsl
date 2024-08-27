#ifndef __HAS_BONE_ANIMATION__
#define __HAS_BONE_ANIMATION__

/******************* DEFINES: ******************/
//<DEF>

#define FEATURE_ANIMATED_BONECOUNT 4

//</DEF>
/****************** INCLUDES: ******************/
//<INC>

#include "./VertexData/VertexInput.hlsl"

//</INC>
/****************** RESOURCES: *****************/

#ifdef VARIANT_ANIMATED
//<RES>

StructuredBuffer<float4x4> BufBoneMatrices : register(t4, vs);	// Buffer of all bone transformation matrices.

//^TODO: Consider using a vertex buffer instead for bone transformation matrices!

//</RES>
/***************** ANIMATION: ******************/
//<FEA>

void CalculateBoneTransformation_Basic(
	const in VertexInput_Basic _input,
	const in float4x4 _mtxBone,
	const float _boneWeight,
	const inout VertexInput_Basic _transformed)
{
	_transformed.position += mul(_mtxBone, float4(_input.position, 1)) * _boneWeight;
	_transformed.normal += mul(_mtxBone, float4(_input.normal, 0)) * _boneWeight;
}

void CalculateBoneTransformation_Ext(
	const in VertexInput_Extended _input,
	const in float4x4 _mtxBone,
	const float _boneWeight,
	const inout VertexInput_Extended _transformed)
{
	_transformed.tangent += mul(_mtxBone, float4(_input.tangent, 0)) * _boneWeight;
}

//</FEA>
/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyBoneAnimation(
	inout VertexInput_Basic _inputBasic,
	const in VertexInput_BoneWeights _inputAnim)
{
	VertexInput_Basic transformedBasic;
	float staticWeight = 1.0;

	// Add weighted per-bone transformations:
	for (uint i = 0; i < FEATURE_ANIMATED_BONECOUNT; ++i)
	{
		const uint boneIndex = _inputAnim.indices[i];
		const float boneWeight = _inputAnim.weights[i];
		const float4x4 mtxBone = BufBoneMatrices[i];

		CalculateBoneTransformation_Basic(_inputBasic, mtxBone, boneWeight, transformedBasic);
		staticWeight -= boneWeight;
	}

	// Add static geometry with remaining weight:
	transformedBasic.position += _inputBasic.position * staticWeight;
	transformedBasic.normal += _inputBasic.normal * staticWeight;
	_inputBasic = transformedBasic;
}

#ifdef VARIANT_EXTENDED
void ApplyBoneAnimation_Ext(
	inout VertexInput_Basic _inputBasic,
	inout VertexInput_Extended _inputExt,
	const in VertexInput_BoneWeights _inputAnim)
{
	VertexInput_Basic transformedBasic;
	VertexInput_Extended transformedExt;
	float staticWeight = 1.0;

	// Add weighted per-bone transformations:
	for (uint i = 0; i < FEATURE_ANIMATED_BONECOUNT; ++i)
	{
		const uint boneIndex = _inputAnim.indices[i];
		const float boneWeight = _inputAnim.weights[i];
		const float4x4 mtxBone = BufBoneMatrices[i];

		CalculateBoneTransformation_Basic(_inputBasic, mtxBone, boneWeight, transformedBasic);
		CalculateBoneTransformation_Ext(_inputExt, mtxBone, boneWeight, transformedExt);
		staticWeight -= boneWeight;
	}

	// Add static geometry with remaining weight:
	transformedBasic.position += _inputBasic.position * staticWeight;
	transformedBasic.normal += _inputBasic.normal * staticWeight;
	transformedExt.tangent += _inputExt.tangent * staticWeight;
	_inputBasic = transformedBasic;
	_inputExt = transformedExt;
}
#endif //VARIANT_EXTENDED

//</FNC>
#endif //VARIANT_ANIMATED

#endif //__HAS_BONE_ANIMATION__
