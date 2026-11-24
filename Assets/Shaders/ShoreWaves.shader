// Small waves rolling in onto a little beach and foam breathing at its waterline, drawn over the still render as
// an own sprite: R = distance from the shore / _Reach, A = where that water shows. Same clock, noise and rhythm as
// IslandWater's waves (IslandAnimationClock); in the Editor (_IslandAnim 0) nothing is drawn.
Shader "ParadiseTycoon/Shore Waves"
{
    Properties
    {
        _MainTex ("Shore Data (R distance / reach, A water)", 2D) = "black" {}
        _Noise ("Tileable Noise", 2D) = "gray" {}
        _Reach ("Reach (grid cells, as encoded in R)", Float) = 0.9
        _Fade ("Waves Fade Out From (grid cells)", Float) = 0.6
        _WaveStrength ("Wave Strength", Range(0, 1)) = 0.38
        _WaveFrequency ("Waves Per Cell", Float) = 3.0
        _WaveSpeed ("Wave Speed", Float) = 0.35
        _BandWidth ("Crest Width", Range(0.01, 0.2)) = 0.06
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

            #pragma vertex WavesVertex
            #pragma fragment WavesFragment

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

            float _IslandTime;
            float _IslandAnim;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Reach;
                float _Fade;
                float _WaveStrength;
                float _WaveFrequency;
                float _WaveSpeed;
                float _BandWidth;
                float _ShoreFoam;
            CBUFFER_END

            Varyings WavesVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS);
                o.positionWS = TransformObjectToWorld(input.positionOS).xy;
                o.uv = input.uv;
                o.color = input.color * _Color;
                return o;
            }

            half4 WavesFragment(Varyings i) : SV_Target
            {
                half4 data = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                float water = data.a * i.color.a * _IslandAnim;
                float dist = data.r * _Reach;
                float2 p = i.positionWS;
                float t = _IslandTime;

                float n1 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * float2(0.18, 0.36) + float2(t * 0.011, t * 0.005)).r;
                float n2 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * float2(0.29, 0.58) - float2(t * 0.008, -t * 0.012)).g;
                float n3 = SAMPLE_TEXTURE2D(_Noise, sampler_Noise, p * 0.09 + t * 0.003).b;

                // Waves rolling in towards the beach, fading out before they reach it and far out.
                float phase = dist * _WaveFrequency + t * _WaveSpeed + n3 * 1.6;
                float band = exp(-pow((frac(phase) - 0.5) / _BandWidth, 2.0));
                float fade = smoothstep(0.02, 0.12, dist) * (1.0 - smoothstep(0.45 * _Fade, _Fade, dist));
                float crest = band * fade * smoothstep(0.3, 0.6, n2) * _WaveStrength;

                // Foam along the waterline breathes in and out.
                float lap = 0.5 + 0.5 * sin(t * 1.25 + n3 * 6.2832);
                float shore = 1.0 - smoothstep(0.01, 0.04 + 0.05 * lap, dist);
                float foam = shore * _ShoreFoam * (0.4 + 0.6 * lap) * smoothstep(0.35, 0.6, n1 + 0.2);

                return half4(0.96, 0.98, 0.97, saturate(max(crest, foam)) * water);
            }
            ENDHLSL
        }
    }
}
