Shader "Unlit/Indirect/Position-DoubleBuffer"
{
    Properties
    {
        _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
    }

    SubShader
    {
        Tags
        {
            "IgnoreProjector"="True"
            "RenderType"="Opaque"
        }
        Cull Off
        Lighting Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f 
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            StructuredBuffer<float3> _PositionBuffer;
            StructuredBuffer<float3> _HeadingBuffer;

            v2f vert (appdata_t v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                float4x4 matrix_ = UNITY_MATRIX_I_V;
                v.vertex.x *= lerp(1, -1, step(0, _HeadingBuffer[instanceID].x));
                matrix_._14_24_34 = _PositionBuffer[instanceID];
                o.vertex = UnityObjectToClipPos(mul(matrix_, v.vertex));
                o.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return tex2D(_MainTex, i.texcoord);
            }
            ENDCG
        }
    }
}