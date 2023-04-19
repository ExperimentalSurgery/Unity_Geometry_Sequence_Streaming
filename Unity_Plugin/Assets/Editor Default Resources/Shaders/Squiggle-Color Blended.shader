// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Squiggle/Color Blended" {
    Properties {
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
            "PreviewType"="Plane"
        }
        Pass {
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct VertexInput
			{
                float4 vertex : POSITION;
                float4 vertexColor : COLOR;
            };

            struct VertexOutput
			{
                float4 pos : SV_POSITION;
                float4 vertexColor : COLOR;
            };

            VertexOutput vert (VertexInput v)
			{
                VertexOutput o = (VertexOutput)0;
                o.vertexColor = v.vertexColor;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(VertexOutput i) : COLOR
			{
                return fixed4(i.vertexColor.rgb,1);
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
