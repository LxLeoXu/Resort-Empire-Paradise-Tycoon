// Animated sea on top of the pre-rendered island base (only while the game runs, see
// IslandAnimationClock): drifting ripples and glints, waves
// rolling in over the shallows and foam that breathes along the beach. _WaterData is aligned
// with the base sprite: R = distance from the coast (0..4 grid cells), G = 1 where there is water.
Shader "ParadiseTycoon/Island Water"
{
    Properties
    {
        _MainTex ("Island Base", 2D) = "white" {}
        _WaterData ("Water Data (R distance, G water)", 2D) = "black" {}
        _Noise ("Tileable Noise", 2D) = "gray" {}
        _RippleStrength ("Ripple Strength", Range(0, 0.2)) = 0.045
        _GlintStrength ("Glint Strength", Range(0, 1)) = 0.3
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.55
        _WaveFrequency ("Waves Per Cell", Float) = 1.25
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _ShoreFoam ("Shore Foam Strength", Range(0, 1)) = 0.45

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

            #pragma vertex WaterVertex
            #pragma fragment WaterFragment

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
            TEXTURE2D(_WaterData);
            SAMPLER(sampler_WaterData);
            TEXTURE2D(_Noise);
            SAMPLER(sampler_Noise);

            // Set by IslandAnimationClock while the game runs; 0 in the Editor, so the sea stays still there.
            float _IslandTime;
            float _IslandAnim;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RippleStrength;
                float _GlintStrength;
                float _WaveStrength;
                float _WaveFrequency;
                float _WaveSpeed;
                float _ShoreFoam;
            CBUFFER_END

            Varyings WaterVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.positionWS = TransformObjectToWorld(input.positionOS).xy;
                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 WaterFragment(Varyings i) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                half4 data = SAMPLE_TEXTURE2D(_WaterData, sampler_WaterData, i.uv);
                float water = data.g * _IslandAnim;  // no animated effects outside Play mode
                float dist = data.r * 4.0;
                float2 p = i.positionWS;
                float t = _IslandTime;

                // Ripples: two noise layers drifting in different directions (stretched like wind ripples).
                float n1 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * float2(0.18, 0.36) + float2(t * 0.011, t * 0.005)).r;
                float n2 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * float2(0.29, 0.58) - float2(t * 0.008, -t * 0.012)).g;
                float ripple = n1 + n2 - 1.0;
                col.rgb *= 1.0 + ripple * _RippleStrength * water;

                // Sun glints: small, rare sparkles out on the deeper water.
                float g1 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 1.1 + float2(t * 0.02, t * 0.013)).r;
                float g2 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 1.45 - float2(t * 0.017, -t * 0.021)).g;
                float glint = smoothstep(0.86, 0.93, (g1 + g2) * 0.5);
                col.rgb += glint * _GlintStrength * water * smoothstep(0.8, 2.0, dist);

                // Waves rolling in towards the beach across the shallow band.
                float n3 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 0.09 + t * 0.003).b;
                float phase = dist * _WaveFrequency + t * _WaveSpeed + n3 * 1.6;
                float band = exp(-pow((frac(phase) - 0.5) / 0.06, 2.0));
                float fade = smoothstep(0.08, 0.45, dist) * (1.0 - smoothstep(1.3, 2.6, dist));
                float crest = band * fade * smoothstep(0.3, 0.6, n2) * water;
                col.rgb = lerp(col.rgb, half3(0.96, 0.98, 0.97), crest * _WaveStrength);

                // Foam along the beach breathes in and out.
                float lap = 0.5 + 0.5 * sin(t * 1.25 + n3 * 6.2832);
                float shore = (1.0 - smoothstep(0.02, 0.1 + 0.12 * lap, dist)) * water;
                col.rgb = lerp(col.rgb, half3(0.97, 0.99, 0.97), shore * _ShoreFoam * (0.4 + 0.6 * lap) * smoothstep(0.35, 0.6, n1 + 0.2));

                return col;
            }
            ENDHLSL
        }
    }
}
