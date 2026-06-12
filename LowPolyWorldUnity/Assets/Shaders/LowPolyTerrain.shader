// 地形専用 Unlit シェーダー（world-creation.md §15.11 / §15.16 / §15.17 / §15.18）
// - texel × 頂点カラー AO × _AmbientColor（ライティング・影なし・カットアウトのみ）
// - _CullHeightY: Height Culling のシェーダークリップ（world Y ≥ 閾値を破棄。既定値は無効相当）
// - _HIDDEN_TOP_MODE: 上面中間フェイス用。UV2.x（ブロック上面の grid Y）== _CullGridY の面のみ表示
Shader "LowPoly/Terrain"
{
    Properties
    {
        _MainTex ("Terrain Atlas", 2D) = "white" {}
        _Cutoff ("Alpha Cutoff", Range(0, 1)) = 0.5
        // Height Culling（TerrainRenderer が設定。1e6 = 無効）
        _CullHeightY ("Cull Height (World Y)", Float) = 1000000
        _CullGridY ("Cull Threshold (Grid Y)", Float) = 1000000
        // 地形タブの上方半透明（市松模様ディザ。1e6 = 無効）
        _DitherHeightY ("Dither Height (World Y)", Float) = 1000000
        [Toggle(_HIDDEN_TOP_MODE)] _HiddenTopMode ("Hidden Top Mode", Float) = 0
        // Set globally via Shader.SetGlobalColor("_AmbientColor", ...) by WorldEnvironmentController
        [HideInInspector] _AmbientColor ("Ambient Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "TransparentCutout"
            "Queue" = "AlphaTest"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // Linear fog via Unity RenderSettings (world-creation.md §15.18)
            #pragma multi_compile_fog
            #pragma shader_feature_local _HIDDEN_TOP_MODE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half _Cutoff;
                float _CullHeightY;
                float _CullGridY;
                float _DitherHeightY;
            CBUFFER_END

            // Global ambient color set by WorldEnvironmentController
            half4 _AmbientColor;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                #if defined(_HIDDEN_TOP_MODE)
                float2 uv2 : TEXCOORD1; // x = ブロック上面の Y グリッドインデックス
                #endif
                half4 color : COLOR; // 頂点 AO（無彩色・§15.16）
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                float worldY : TEXCOORD2;
                #if defined(_HIDDEN_TOP_MODE)
                float gridTopY : TEXCOORD3;
                #endif
                half4 color : COLOR;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(output.positionHCS.z);
                output.worldY = positionWS.y;
                #if defined(_HIDDEN_TOP_MODE)
                output.gridTopY = input.uv2.x;
                #endif
                output.color = input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                #if defined(_HIDDEN_TOP_MODE)
                // 上面中間フェイス: 閾値の 1 段下のブロック上面（grid Y == 閾値）のみ表示
                clip(0.5 - abs(input.gridTopY - _CullGridY));
                #else
                // Height Culling: world Y ≥ 閾値のフラグメントを破棄（§15.11 表示反映方式）。
                // 上向きの面（頂点カラー α = 1）はカット平面と一致する高さでも表示し、
                // 非表示ブロックの下面（同じ高さ・α = 0）は浮動小数の揺らぎなく確実に破棄する
                clip(_CullHeightY + input.color.a * 0.25 - input.worldY - 0.0002);

                // 地形タブの上方半透明: _DitherHeightY 以上を市松模様ディザで間引く
                // （screens-and-modes.md §11.7.2 — 透明禁止制約のためアルファブレンドは使わない）
                if (input.worldY + 0.0002 - input.color.a * 0.25 >= _DitherHeightY)
                {
                    uint2 pixel = (uint2)input.positionHCS.xy;
                    clip((float)((pixel.x + pixel.y) & 1) - 0.5);
                }
                #endif

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                // texel × 頂点 AO × _AmbientColor（§15.16 / §15.17）
                half3 rgb = texColor.rgb * input.color.rgb * _AmbientColor.rgb;

                clip(texColor.a - _Cutoff);

                // Apply linear fog (world-creation.md §15.18)
                rgb = MixFog(rgb, input.fogFactor);

                return half4(rgb, 1.0);
            }
            ENDHLSL
        }

        // Shadow caster pass omitted (no shadows per rendering constraints)
    }

    FallBack Off
}
