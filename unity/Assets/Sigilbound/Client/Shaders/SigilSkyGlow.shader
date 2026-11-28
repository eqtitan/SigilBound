// The sun or moon disc and its soft halo (Client/SkyDisc.cs): a camera-facing quad of additive light, drawn after the
// painted sky arc and before the backdrop ridges (render queue set from code), so the ridges hide a low sun.
// One pass, no lighting, no fog (the disc sits ~42 m out, where the arena's linear fog would erase it), no depth
// write: about the cost of a particle quad per pixel covered. _MainTex's alpha is the radial profile.
// SkyDisc falls back to Sprites/Default (alpha-blended, the same profile) if this shader is missing.
Shader "Sigilbound/SkyGlow"
{
    Properties
    {
        _MainTex ("Profile (alpha)", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-4" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" "PreviewType" = "Plane" }
        Blend One One
        ZWrite Off
        ZTest LEqual
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _TintColor;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "SkyGlow"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv * _MainTex_ST.xy + _MainTex_ST.zw;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * _TintColor.a;
                return half4(_TintColor.rgb * a, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
