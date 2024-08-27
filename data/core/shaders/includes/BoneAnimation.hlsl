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

#ifdef VARIANT_ANIMATION
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
	_transformed.position += mul(mtxBone, float4(_input.position, 1)) * _boneWeight;
	_transformed.normal += mul(mtxBone, float4(_input.normal, 0)) * _boneWeight;
}

void CalculateBoneTransformation_Ext(
	const in VertexInput_Extended _input,
	const in float4x4 _mtxBone,
	const float _boneWeight,
	const inout VertexInput_Extended _transformed)
{
	_transformed.tangent += mul(mtxBone, float4(_input.tangent, 0)) * _boneWeight;
}

//</FEA>
/***************** FUNCTIONS: ******************/
//<FNC>

void ApplyBoneAnimation(
	const in VertexInput_Basic _inputBasic,
	const in VertexInput_BoneWeights _inputAnim,
	out VertexInput_Basic _transformedBasic)
{
	float staticWeight = 1.0;
	for (uint i = 0; i < FEATURE_ANIMATED_BONECOUNT; ++i)
	{
		const uint boneIndex = _inputAnim.indices[i];
		const float boneWeight = _inputAnim.weights[i];
		const float4x4 mtxBone = BufBoneMatrices[i];

		CalculateBoneTransformation_Basic(_inputBasic, mtxBone, boneWeight, _transformedBasic);
		staticWeight -= boneWeight;
	}

	// Basic:
	_transformedBasic.position += _inputBasic.position * _boneWeight;
	_transformedBasic.normal += _inputBasic.normal * _boneWeight;
}

#ifdef VARIANT_EXTENDED
void ApplyBoneAnimation_Ext(
	const in VertexInput_Basic _inputBasic,
	const in VertexInput_Extended _inputExt,
	const in VertexInput_BoneWeights _inputAnim,
	out VertexInput_Basic _transformedBasic,
	out VertexInput_Extended _transformedExt)
{
	float staticWeight = 1.0;
	for (uint i = 0; i < FEATURE_ANIMATED_BONECOUNT; ++i)
	{
		const uint boneIndex = _inputAnim.indices[i];
		const float boneWeight = _inputAnim.weights[i];
		const float4x4 mtxBone = BufBoneMatrices[i];

		CalculateBoneTransformation_Basic(_inputBasic, mtxBone, boneWeight, _transformedBasic);
		CalculateBoneTransformation_Ext(_inputExt, mtxBone, boneWeight, _transformedExt);
		staticWeight -= boneWeight;
	}

	// Basic:
	_transformedBasic.position += _inputBasic.position * _boneWeight;
	_transformedBasic.normal += _inputBasic.normal * _boneWeight;
	// Extended:
	_transformedExt.tangent += _inputExt.tangent * _boneWeight;
}
#endif //VARIANT_EXTENDED

//</FNC>
#endif //VARIANT_ANIMATION

#endif //__HAS_BONE_ANIMATION__
