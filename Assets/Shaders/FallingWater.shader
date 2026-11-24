// Water falling from a fountain's bowls (only while the game runs, see IslandAnimationClock): bright streaks
// running down the pre-rendered sheets of water and a soft flicker, drawn over the building's sprite. The sprite is
// the sheets' mask (white where the falling water shows; whatever stands in front cut out), framed round the fountain.
Shader "ParadiseTycoon/Falling Water"
{
    Properties
    {
        _MainTex ("Falling Water Mask", 2D) = "white" {}
        _Noise ("Tileable Noise", 2D) = "gray" {}
        _StreakColor ("Streak Colour", Color) = (0.94, 1, 1, 1)
        _Strength ("Strength", Range(0, 1)) = 0.7
        _Across ("Streaks Across (per unit)", Float) = 7
        _Along ("Streaks Along (per unit)", Float) = 1.2
        _Fall ("Fall Speed (units per second)", Float) = 0.8
        _Flicker ("Flicker", Range(0, 0.5)) = 0.12

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

            #pragma vertex FallVertex
            #pragma fragment FallFragment

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

            // Set by IslandAnimationClock while the game runs; 0 in the Editor, so the water shows its still render there.
            float _IslandTime;
            float _IslandAnim;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _StreakColor;
                float _Strength;
                float _Across;
                float _Along;
                float _Fall;
                float _Flicker;
            CBUFFER_END

            Varyings FallVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.uv = input.uv;
                o.positionWS = TransformObjectToWorld(input.positionOS).xy;
                o.color = input.color * _Color;
                return o;
            }

            half4 FallFragment(Varyings i) : SV_Target
            {
                half mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                float t = _IslandTime;
                // Narrow across, long along the fall; the pattern slides down the screen at the fall speed.
                float2 p = float2(i.positionWS.x * _Across, (i.positionWS.y + t * _Fall) * _Along);
                float a = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p).r;
                float b = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, float2(p.x * 1.37 + 0.31, (i.positionWS.y + t * _Fall * 1.45) * _Along * 1.6)).g;
                float streak = smoothstep(0.56, 0.82, a) + 0.7 * smoothstep(0.6, 0.86, b);
                float f = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, float2(p.x * 0.5, t * 0.9)).b;
                half alpha = mask * _IslandAnim * saturate(streak * _Strength + f * _Flicker);
                return half4(_StreakColor.rgb, alpha * i.color.a);
            }
            ENDHLSL
        }
    }
}
