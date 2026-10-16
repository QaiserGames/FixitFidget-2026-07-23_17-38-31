// The line round a part at the bench (the bench, v2, 7 Oct 2026; claude/bench-spec-v2.md §2.1): white under the cursor, gold
// while a hold is under way, and since checklist v2 (14 Oct 2026) a breathing mint glow on the next thing to touch and gold
// for "what's left" (BenchOutline, BenchGlow).
//
// An inverted hull: the part's mesh drawn again with its back faces only, pushed out along smoothed normals by a width that
// grows with the distance to the camera, so the line stays about the same thickness on screen whether the part is a 4 mm
// screw or a 14 cm cover. BenchOutline.cs makes the hull objects and sets _Color, _Width and _XRay per hull; this lives in
// Resources so a build keeps it.
//
// HIDDEN PARTS (checklist v2, §5.4; KeenGamer on ReStory: "highlight parts hidden under other parts"). With _XRay at 1 a
// second pass draws the hull's front faces only where something nearer covers them (ZTest Greater), near the silhouette
// only (the rim: the smoothed normal across the view), in diagonal dashes 4 px on and 4 px off at 1080p, at 70 % of the
// colour. Where the part is in plain sight its own surface is behind the hull's front faces, so the pass draws nothing
// there; where the part is behind the camera body, its outline shows through it, dashed. With _XRay at 0 the pass draws
// nothing at all (the hover, the busy hold and a glow in sight).
//
// The second pass is tagged SRPDefaultUnlit: URP draws one pass per LightMode tag in its forward pass, so a second
// UniversalForward pass would never be drawn.
Shader "Fixit Fidget/Bench outline"
{
    Properties
    {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Width ("Width per metre of distance", Float) = 0.0035
        _XRay ("Drawn dashed where hidden (0 off, 1 on)", Float) = 0
        _Dash ("Dash length, pixels at 1080p", Float) = 4
        _Rim ("Hidden: how near the silhouette (0 all of it, 1 only the very edge)", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry+5" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _Color;
        float _Width;
        float _XRay;
        float _Dash;
        float _Rim;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        ENDHLSL

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

        Pass
        {
            Name "Hidden"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Back
            ZWrite Off
            ZTest Greater
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vertHidden
            #pragma fragment fragHidden

            struct VaryingsHidden { float4 positionHCS : SV_POSITION; float rim : TEXCOORD0; };

            VaryingsHidden vertHidden(Attributes IN)
            {
                VaryingsHidden OUT;
                float3 positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(IN.normalOS));
                float3 toEye = _WorldSpaceCameraPos - positionWS;
                float distanceToEye = max(length(toEye), 1e-4);
                OUT.rim = 1.0 - abs(dot(normalWS, toEye / distanceToEye));
                positionWS += normalWS * _Width * max(distanceToEye, 0.05);
                OUT.positionHCS = TransformWorldToHClip(positionWS);
                return OUT;
            }

            half4 fragHidden(VaryingsHidden IN) : SV_Target
            {
                clip(_XRay - 0.5);
                clip(IN.rim - _Rim);
                // Diagonal dashes in screen pixels, sized for 1080p: every other band of _Dash pixels is left out.
                float band = max(_Dash, 1.0) * max(_ScreenParams.y / 1080.0, 0.5);
                float stripe = floor((IN.positionHCS.x + IN.positionHCS.y) / band);
                clip(fmod(stripe, 2.0) - 0.5);
                return half4(_Color.rgb, _Color.a * 0.7);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
