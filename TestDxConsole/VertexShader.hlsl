// THI// THIS CODE AND INFORMATION IS PROVIDED "AS IS" WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO
// THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
// PARTICULAR PURPOSE.
//
// Copyright (c) Microsoft Corporation. All rights reserved
//----------------------------------------------------------------------

struct VSIn
{
	float4 position : SV_POSITION;
	float2 texcoord : TEXCOORD;
};

struct VSOut
{
	float4 position : SV_POSITION;
	float2 texcoord : TEXCOORD;
};

VSOut main(float4 position : POSITION, float2 texcoord : TEXCOORD)
{
	VSOut output;
	output.position = position;
	output.texcoord = texcoord;

	return output;
}


/*
struct VS_INPUT
{
	float4 Pos : SV_POSITION;
	float2 Tex : TEXCOORD;
};

struct VS_OUTPUT
{
	float4 Pos : SV_POSITION;
	float2 Tex : TEXCOORD;
};


//--------------------------------------------------------------------------------------
// Vertex Shader
//--------------------------------------------------------------------------------------
VS_OUTPUT main(VS_INPUT input)
{
	return input;
}

*/