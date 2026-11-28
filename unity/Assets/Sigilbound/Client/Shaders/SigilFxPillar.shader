// Skill-effect pillars, shock walls, splash crowns and shield domes (Client/SkillFx.cs Wall / Dome), art review M3:
// they used to be opaque-looking striped cylinders that swallowed the target. Now they are additive light with
//   - a fresnel falloff: bright at the silhouette edges, nearly clear where the surface faces the camera, so the
//     unit inside always shows through (never more than _Opacity, and only _Core of that across the middle);
//   - a soft vertical fade (_VFade: x = bottom feather, y = top fade, in uv.y), so a pillar fades out at the top
//     and has no hard ring at the ground;
//   - a slow noise scroll (_MainTex, a soft streak or hex texture) for movement instead of hard stripes.
// One pass, no keywords, no lighting, no depth write: about the cost of a particle quad per pixel covered.
// SkillFx falls back to Legacy Particles/Additive (with the fade baked into the tube's vertex alpha) if this
// shader is missing.
Shader "Sigilbound/FxPillar"
{
    Properties
    {
        _MainTex ("Noise (soft streaks)", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1, 1, 1, 1)
        _Opacity ("Opacity (0-1, the brightest the edge gets)", Range(0, 1)) = 0.5
        _Core ("Middle (facing the camera), fraction of the edge", Range(0, 1)) = 0.12
        _Fresnel ("Fresnel power", Range(0.5, 6)) = 2
        _VFade ("Vertical fade (x bottom feather, y top fade)", Vector) = (0.12, 0.6, 0, 0)
        _Scroll ("Noise scroll (uv per second)", Vector) = (0.05, -0.35, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _TintColor;
            float _Opacity;
            float _Core;
            float _Fresnel;
            float4 _VFade;
            float4 _Scroll;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "FxPillar"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.uv = input.uv;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                // Fresnel: 1 at the silhouette, 0 facing the camera (abs: the far wall's inside counts too).
                float facing = abs(dot(n, v));
                float edge = lerp(_Core, 1.0, pow(saturate(1.0 - facing), _Fresnel));
                // Vertical: feathered at the ground, fading out over the top part.
                float y = i.uv.y;
                float vert = smoothstep(0.0, max(_VFade.x, 1e-3), y) * (1.0 - smoothstep(1.0 - max(_VFade.y, 1e-3), 1.0, y));
                if (_VFade.x <= 0.0 && _VFade.y <= 0.0) vert = 1.0;
                // Soft scrolling noise, never below 0.45 so it only breathes.
                float2 uv = i.uv * _MainTex_ST.xy + _MainTex_ST.zw + _Scroll.xy * _Time.y;
                float noise = 0.45 + 0.55 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a;
                float a = saturate(_Opacity) * edge * vert * noise;
                float3 col = _TintColor.rgb * a;
                col = MixFogColor(col, float3(0, 0, 0), i.fogFactor); // additive: fog fades it to nothing
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
