// The white outline round the part under the cursor at the bench (the bench, v2, 7 Oct 2026; claude/bench-spec-v2.md §2.1).
// An inverted hull: the part's mesh drawn again with its back faces only, pushed out along smoothed normals by a width
// that grows with the distance to the camera, so the line stays about the same thickness on screen whether the part is
// a 12 mm screw or a 14 cm cover. Outline.cs makes the hull objects; this lives in Resources so a build keeps it.
Shader "Fixit Fidget/Bench outline"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Width ("Width per metre of distance", Float) = 0.0035
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+5" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }
            Cull Front
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            float _Width;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionHCS : SV_POSITION; };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float distanceToEye = distance(positionWS, _WorldSpaceCameraPos);
                positionWS += normalWS * _Width * max(distanceToEye, 0.05);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
    FallBack Off
}
