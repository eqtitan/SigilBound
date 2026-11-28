// Hollow enemies' red outline (art review M2, PMO P2-9): an inverted hull. Client/HollowLook.cs adds this as an
// extra material on each of the enemy's renderers, so Unity draws the mesh a second time with it: back faces only,
// pushed out along their normals by a constant number of PIXELS (_Width, from Core BattleFx.OutlineWidth), unlit.
// The front of the body covers the middle, leaving a clean continuous line around the silhouette, instead of the
// speckled fresnel the normal-mapped rim gave on a near-black body. Works on skinned meshes (it runs after skinning).
// One pass, no textures, no lighting, no keywords: the cheapest outline there is, so Low keeps it.
Shader "Sigilbound/HollowOutline"
{
    Properties
    {
        [HDR] _OutlineColor ("Outline colour", Color) = (1, 0.13, 0.2, 1)
        _Width ("Width (pixels)", Float) = 2.5
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Cull Front
        ZWrite On
        ZTest LEqual

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _OutlineColor;
            float _Width;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "HollowOutline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float4 cs = TransformObjectToHClip(input.positionOS.xyz);
                float3 nWS = TransformObjectToWorldNormal(input.normalOS);
                float2 nCS = mul((float3x3)UNITY_MATRIX_VP, nWS).xy;
                float len = length(nCS);
                // Push out in screen space by _Width pixels whatever the distance (x w undoes the perspective divide).
                float2 dir = len > 1e-5 ? nCS / len : float2(0, 0);
                float2 px = 2.0 / max(_ScreenParams.xy, float2(1, 1));
                cs.xy += dir * px * _Width * cs.w;
                o.positionCS = cs;
                o.fogFactor = ComputeFogFactor(cs.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 col = MixFog(_OutlineColor.rgb, i.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
