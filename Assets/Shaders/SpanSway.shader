// A rope bridge swaying a little in the wind, drawn as its own sprite: the picture shifts most in the middle of the
// span and not at all at its two ends (_EndA, _EndB: the anchors in the sprite's UV), so it stays tied to its
// pylons. Same clock as the island's other motion (IslandAnimationClock); still in the Editor.
Shader "ParadiseTycoon/Span Sway"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _EndA ("Anchor A (UV)", Vector) = (0, 0.5, 0, 0)
        _EndB ("Anchor B (UV)", Vector) = (1, 0.5, 0, 0)
        _Amount ("Sway (UV: x, y)", Vector) = (0.004, 0.002, 0, 0)
        _Speed ("Speed", Float) = 0.9

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

            #pragma vertex SwayVertex
            #pragma fragment SwayFragment

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
                half4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _IslandTime;
            float _IslandAnim;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float4 _EndA;
                float4 _EndB;
                float4 _Amount;
                float _Speed;
            CBUFFER_END

            Varyings SwayVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 SwayFragment(Varyings i) : SV_Target
            {
                float2 ab = _EndB.xy - _EndA.xy;
                float s = saturate(dot(i.uv - _EndA.xy, ab) / max(dot(ab, ab), 1e-6));
                float w = sin(3.14159265 * s);
                float t = _IslandTime * _Speed;
                float2 off = float2(sin(t) + 0.35 * sin(t * 2.31 + 1.7), 0.7 * sin(t * 0.83 + 0.6)) * _Amount.xy * w * _IslandAnim;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv - off) * i.color;
            }
            ENDHLSL
        }
    }
}
