Shader "LowPoly/BlobShadow"
{
    Properties
    {
        _ShadowTex ("Shadow Texture", 2D) = "white" {}
        _Opacity ("Opacity", Range(0, 1)) = 0.4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BlobShadow"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            // 乗算ブレンド: dst * src
            Blend DstColor Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_ShadowTex);
            SAMPLER(sampler_ShadowTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowTex_ST;
                half _Opacity;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _ShadowTex);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 tex = SAMPLE_TEXTURE2D(_ShadowTex, sampler_ShadowTex, input.uv);
                // 乗算シャドウ: 暗い部分を白→灰で表現
                half shadow = 1.0h - tex.r * _Opacity;
                return half4(shadow, shadow, shadow, 1.0h);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
