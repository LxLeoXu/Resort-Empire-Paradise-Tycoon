// Soft unlit glow for sprites and particles: the texture times the vertex colour (the sprite's or the particle's
// colour) and a tint. The blend is a material setting: SrcAlpha + One adds light (lantern and bulb glows),
// SrcAlpha + OneMinusSrcAlpha lays it over like a veil (steam). A sprite renderer's colour can also come as
// unity_SpriteColor (with white vertex colours), as in URP's sprite shaders: _UseSpriteColor 1 for sprites, 0 for
// particles.
Shader "ParadiseTycoon/Soft Glow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] _UseSpriteColor ("Use Sprite Renderer Colour", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Source Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Destination Blend", Float) = 1
    }

    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend [_SrcBlend] [_DstBlend]
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #pragma vertex GlowVertex
            #pragma fragment GlowFragment

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _UseSpriteColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings GlowVertex(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = TRANSFORM_TEX(input.uv, _MainTex);
                o.color = input.color * _Color * lerp(half4(1, 1, 1, 1), (half4)unity_SpriteColor, _UseSpriteColor);
                return o;
            }

            half4 GlowFragment(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            }
            ENDHLSL
        }
    }
}
