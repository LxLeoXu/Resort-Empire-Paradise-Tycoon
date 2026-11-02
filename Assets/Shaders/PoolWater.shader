// Animated water in the pool (only while the game runs, see IslandAnimationClock): a web of bright caustic lines
// drifting over the pool floor and a soft shimmer, drawn over the pool's pre-rendered sprite. The sprite is the
// pool's water mask (white where the water shows; rim, ladder and rings cut out), framed like the pool sprite.
Shader "ParadiseTycoon/Pool Water"
{
    Properties
    {
        _MainTex ("Water Mask", 2D) = "white" {}
        _Noise ("Tileable Noise", 2D) = "gray" {}
        _CausticColor ("Caustic Colour", Color) = (0.86, 1, 1, 1)
        _CausticStrength ("Caustic Strength", Range(0, 1)) = 0.5
        _CausticScale ("Caustic Scale (per unit)", Float) = 0.9
        _CausticSharpness ("Caustic Sharpness", Float) = 5
        _ShimmerStrength ("Shimmer Strength", Range(0, 0.5)) = 0.1
        _Speed ("Speed", Float) = 1

        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex PoolVertex
            #pragma fragment PoolFragment

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 positionWS : TEXCOORD1;
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            // Set by IslandAnimationClock while the game runs; 0 in the Editor, so the pool shows its still render there.
            float _IslandTime;
            float _IslandAnim;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _CausticColor;
                float _CausticStrength;
                float _CausticScale;
                float _CausticSharpness;
                float _ShimmerStrength;
                float _Speed;
            CBUFFER_END

            Varyings PoolVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.positionWS = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * _Color;
                return o;
            }

            half4 PoolFragment(Varyings i) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float t = _IslandTime * _Speed;
                // The pool floor is seen from above at an angle: y counts double so the pattern lies flat on it.
                float2 p = float2(i.positionWS.x, i.positionWS.y * 2.0) * _CausticScale;
                float a = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p + float2(t * 0.045, t * 0.031)).r;
                float b = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 1.37 + float2(-t * 0.038, t * 0.052)).g;
                // Where the two drifting layers meet: a thin bright web, like sunlight focused by the ripples.
                float caustic = pow(saturate(1.0 - abs(a - b) * 4.0), _CausticSharpness);
                float s = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 0.6 + float2(t * 0.02, -t * 0.015)).b;
                float shimmer = abs(s - 0.5) * 2.0;
                half alpha = mask * _IslandAnim * saturate(caustic * _CausticStrength + shimmer * _ShimmerStrength);
                return half4(_CausticColor.rgb, alpha * i.color.a);
            }
            ENDHLSL
        }
    }
}
