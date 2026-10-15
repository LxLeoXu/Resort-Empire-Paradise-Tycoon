// Unlit sprite that sways in the wind (only while the game runs, see IslandAnimationClock).
// The pivot (foot of the plant) stays put and the sway
// grows with height above it, so trunks bend and crowns move most. Every object gets its own
// phase from its world position. With "Weight By Distance" the sway grows with distance from
// the pivot instead, which suits ground shadows of swaying plants.
Shader "ParadiseTycoon/Sprite Wind"
{
    Properties
    {
        _MainTex ("Sprite Texture", 2D) = "white" {}
        _SwayAmount ("Sway Amount (units)", Float) = 0.06
        _SwaySpeed ("Sway Speed", Float) = 1.2
        _SwayStart ("Sway Starts At Height (units)", Float) = 0.0
        _SwayHeight ("Full Sway At Height (units)", Float) = 2.0
        _Flutter ("Leaf Flutter (units)", Float) = 0.006
        [Toggle] _UseDistance ("Weight By Distance From Pivot", Float) = 0

        // Legacy sprite properties, kept so the material behaves like the default sprite material.
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] PixelSnap ("Pixel snap", Float) = 0
        [HideInInspector] _RendererColor ("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex WindVertex
            #pragma fragment WindFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            // Set by IslandAnimationClock while the game runs; 0 in the Editor, so nothing moves there.
            float _IslandTime;
            float _IslandAnim;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _SwayAmount;
                float _SwaySpeed;
                float _SwayStart;
                float _SwayHeight;
                float _Flutter;
                float _UseDistance;
            CBUFFER_END

            Varyings WindVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                float3 pivotWS = TransformObjectToWorld(float3(0, 0, 0));
                float phase = pivotWS.x * 1.7 + pivotWS.y * 2.3;
                float h = _UseDistance > 0.5 ? length(input.positionOS.xy) : input.positionOS.y;
                float w = saturate((h - _SwayStart) / max(_SwayHeight - _SwayStart, 0.001));
                w *= w;

                float t = _IslandTime * _SwaySpeed + phase;
                float gust = 0.6 + 0.4 * sin(_IslandTime * 0.31 + phase * 0.5);
                float sway = (sin(t) + 0.35 * sin(t * 2.31 + 1.7)) * _SwayAmount * gust * w * _IslandAnim;
                float flutter = sin(_IslandTime * 7.3 + input.positionOS.x * 13.0 + input.positionOS.y * 11.0)
                              * _Flutter * w * _IslandAnim;
                input.positionOS.x += sway + flutter;
                input.positionOS.y += flutter * 0.6 - abs(sway) * 0.12;

                Varyings o = CommonUnlitVertex(input);
                o.color = input.color * _Color * unity_SpriteColor;
                return o;
            }

            half4 WindFragment(Varyings input) : SV_Target
            {
                return CommonUnlitFragment(input, input.color);
            }
            ENDHLSL
        }
    }
}
