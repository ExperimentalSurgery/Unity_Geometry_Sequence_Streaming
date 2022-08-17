//This is a modified version of a shader published by smb02dunnal on the Unity forums:
//https://forum.unity3d.com/threads/billboard-geometry-shader.169415/

Shader "Custom/GS Billboard"
{
	Properties
	{
		_PointSize("PointSize", Range(0, 0.02)) = 0.01
		_DistanceScale("DistanceScale", Range(0, 2.0)) = 1.3
		_MinPointSize("MinPointSize", Range(0, 0.1)) = 0.002
		_MaxX("MaxX", Range(-10, 10)) = 10
		_MinX("MinX", Range(-10, 10)) = -10
		_MaxZ("MaxZ", Range(-10, 10)) = 10
		_MinZ("MinZ", Range(-10, 10)) = -10
	}

		SubShader
	{
		Pass
	{
		Tags{ "RenderType" = "Opaque" }
		LOD 200

		CGPROGRAM
#pragma target 5.0
#pragma vertex VS_Main
#pragma fragment FS_Main
#pragma geometry GS_Main
#include "UnityCG.cginc" 
#include "AutoLight.cginc"

	// **************************************************************
	// Data structures												*
	// **************************************************************
	struct GS_INPUT
	{
		float4	pos		: POSITION;
		float4	col		: COLOR;
		LIGHTING_COORDS(0, 1)
	};

	struct FS_INPUT
	{
		float4	pos		: POSITION;
		float4  col		: COLOR;
	};

	// **************************************************************
	// Vars															*
	// **************************************************************
	float _PointSize;
	float _DistanceScale;
	float _MinPointSize;
	float _MaxX;
	float _MinX;
	float _MaxZ;
	float _MinZ;

	// **************************************************************
	// Shader Programs												*
	// **************************************************************

	// Vertex Shader ------------------------------------------------
	GS_INPUT VS_Main(appdata_full v)
	{
		TRANSFER_VERTEX_TO_FRAGMENT(o);
		GS_INPUT output = (GS_INPUT)0;

		//output.pos = mul(unity_ObjectToWorld, v.vertex);
		output.pos=v.vertex;
		output.col = v.color;
		
		return output;
	}

	// Geometry Shader -----------------------------------------------------
	[maxvertexcount(4)]
	void GS_Main(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
	{

		float3 up = UNITY_MATRIX_IT_MV[1].xyz;
		float3 right = -UNITY_MATRIX_IT_MV[0].xyz;
		float dist = length(ObjSpaceViewDir(p[0].pos));
		float halfS = dist*_DistanceScale*_PointSize + _MinPointSize;

		float4 v[4];
		v[0] = float4(p[0].pos + halfS * right - halfS * up, 1.0f);
		v[1] = float4(p[0].pos + halfS * right + halfS * up, 1.0f);
		v[2] = float4(p[0].pos - halfS * right - halfS * up, 1.0f);
		v[3] = float4(p[0].pos - halfS * right + halfS * up, 1.0f);

		float tempX= ( mul(unity_ObjectToWorld, p[0].pos)).x;
		float tempY= ( mul(unity_ObjectToWorld, p[0].pos)).y;
		float tempZ= ( mul(unity_ObjectToWorld, p[0].pos)).z;

		if( _MaxX < tempX || _MinX > tempX || _MaxZ < tempZ || _MinZ > tempZ ){

			p[0].col.r=0;
			p[0].col.g=0;
			p[0].col.b=0;

		} else {
		
			FS_INPUT pIn;
		
			pIn.pos = UnityObjectToClipPos(v[0]);
			pIn.col = p[0].col;
			triStream.Append(pIn);

			pIn.pos = UnityObjectToClipPos(v[1]);
			pIn.col = p[0].col;
			triStream.Append(pIn);

			pIn.pos = UnityObjectToClipPos(v[2]);
			pIn.col = p[0].col;
			triStream.Append(pIn);

			pIn.pos = UnityObjectToClipPos(v[3]);
			pIn.col = p[0].col;
			triStream.Append(pIn);
		}
	}

	// Fragment Shader -----------------------------------------------
	float4 FS_Main(FS_INPUT input) : COLOR
	{
		float atten = LIGHT_ATTENUATION(i);
		return input.col;
	}

		ENDCG
	}
	}
}




