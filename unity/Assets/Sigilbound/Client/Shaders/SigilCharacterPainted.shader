// Sigilbound character shading for textured hero art (docs/sigilbound/art/ART_BIBLE.md, section 5):
// the same light model as Sigilbound/Character (wrap diffuse, painterly three-band ramp, cloth sheen,
// element-coloured rim) but reading a hand-painted albedo from UV0 instead of a flat colour, with
//   _MaskMap  R metallic, G ambient occlusion, B glow mask (eyes, runes, gems, trim), A smoothness
//   _BumpMap  tangent-space normal map (optional; the default "bump" texture is flat)
//   _GlowColor the glow-mask colour: the hero's glow colour, or red on Hollow enemies (HeroRig)
//   _Tint     multiplies the albedo (Hollow enemies are tinted dark violet)
//   _Cutoff   alpha clip for hair cards, feathers and fringes (0 = opaque)
// No keywords: every map is always sampled, so one variant serves every hero (cheap to warm up).
// Materials are made by Editor/Art/HeroImportPipeline. Falls back to URP Lit.
Shader "Sigilbound/CharacterPainted"
{
    Properties
    {
        _BaseMap ("Albedo (hand-painted)", 2D) = "white" {}
        _BaseColor ("Base colour", Color) = (1, 1, 1, 1)
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _MaskMap ("Mask (R metal, G AO, B glow, A smooth)", 2D) = "white" {}
        [Normal] _BumpMap ("Normal map", 2D) = "bump" {}
        _BumpScale ("Normal strength", Range(0, 2)) = 1
        _Metallic ("Metallic scale", Range(0, 1)) = 1
        _Smoothness ("Smoothness scale", Range(0, 1)) = 0.5
        _OcclusionStrength ("AO strength", Range(0, 1)) = 1
        _Wrap ("Wrap lighting", Range(0, 1)) = 0.3
        _Ramp ("Painterly ramp", Range(0, 1)) = 0.35
        [HDR] _RimColor ("Rim colour (element)", Color) = (0.62, 0.7, 1, 1)
        _Rim ("Rim strength", Range(0, 2)) = 0.7
        _Sheen ("Cloth sheen", Range(0, 1)) = 0.15
        [HDR] _GlowColor ("Glow mask colour", Color) = (0, 0, 0, 1)
        _Cutoff ("Alpha clip", Range(0, 1)) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Cull [_Cull]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _Tint;
            float4 _MaskMap_ST;
            float4 _BumpMap_ST;
            float _BumpScale;
            float _Metallic;
            float _Smoothness;
            float _OcclusionStrength;
            float _Wrap;
            float _Ramp;
            float4 _RimColor;
            float _Rim;
            float _Sheen;
            float4 _GlowColor;
            float _Cutoff;
            float _Cull;
        CBUFFER_END

        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MaskMap); SAMPLER(sampler_MaskMap);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 tangentWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
                float fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.tangentWS = float4(TransformObjectToWorldDir(input.tangentOS.xyz), input.tangentOS.w * GetOddNegativeScale());
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float Ramp(float d)
            {
                return 0.05 + 0.45 * smoothstep(0.03, 0.17, d) + 0.42 * smoothstep(0.42, 0.6, d) + 0.08 * smoothstep(0.86, 0.98, d);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float4 base = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                clip(base.a - _Cutoff);
                float4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_MaskMap, i.uv);
                float3 nTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, i.uv), _BumpScale);
                float3 n0 = normalize(i.normalWS);
                float3 t = normalize(i.tangentWS.xyz - n0 * dot(i.tangentWS.xyz, n0));
                float3 b = cross(n0, t) * i.tangentWS.w;
                float3 n = normalize(nTS.x * t + nTS.y * b + nTS.z * n0);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);

                float3 albedo = base.rgb * _Tint.rgb;
                float metal = mask.r * _Metallic;
                float smooth = mask.a * _Smoothness;
                float ao = lerp(1.0, mask.g, _OcclusionStrength);

                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = dot(n, light.direction);
                float wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));
                float diffuse = lerp(wrapped, Ramp(wrapped), _Ramp);
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                float3 diffuseCol = albedo * (1.0 - metal * 0.85);
                float3 col = diffuseCol * light.color * diffuse * atten;
                col += diffuseCol * SampleSH(n) * ao;

                float3 h = normalize(light.direction + v);
                float specPow = exp2(2.0 + smooth * 9.0);
                float spec = pow(saturate(dot(n, h)), specPow) * smooth * (specPow + 8.0) / 64.0;
                float3 specCol = lerp(float3(0.04, 0.04, 0.04), albedo, metal);
                col += specCol * light.color * spec * atten * saturate(ndl * 4.0);
                col += albedo * metal * lerp(float3(0.29, 0.23, 0.19), float3(0.81, 0.88, 1.0), n.y * 0.5 + 0.5) * 0.25 * ao;

                float nv = saturate(dot(n, v));
                col += albedo * _Sheen * (1.0 - metal) * pow(1.0 - nv, 3.0) * 0.6 * light.color;
                float rim = smoothstep(0.5, 0.95, 1.0 - nv) * (0.45 + 0.55 * saturate(n.y + 0.4));
                col += _RimColor.rgb * _Rim * rim;

                col += _GlowColor.rgb * mask.b;
                col = MixFog(col, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0

            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            float3 _LightDirection;

            struct ShadowAttributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct ShadowVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            ShadowVaryings ShadowVert(ShadowAttributes input)
            {
                ShadowVaryings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                o.positionCS = positionCS;
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return o;
            }
            half4 ShadowFrag(ShadowVaryings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct DepthVaryings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            DepthVaryings DepthVert(DepthAttributes input) { DepthVaryings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); o.uv = TRANSFORM_TEX(input.uv, _BaseMap); return o; }
            half4 DepthFrag(DepthVaryings i) : SV_Target
            {
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).a * _BaseColor.a - _Cutoff);
                return i.positionCS.z;
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Lit"
}
