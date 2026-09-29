// ---------------------------------------------------------------------------
// THE NIGHT SKY: STARS AND THE MOON (NightSky; the second playtest, 29 Sept)
//
// Draws NightSky's mesh: one small quad per star, and one for the moon. Every
// corner of a quad carries the same direction (a unit vector); the vertex
// shader spreads the corners round it, across the sky, and puts the quad just
// inside the far plane of whichever camera is drawing, centred on that camera.
// So the stars never come closer as the camera moves, and anything nearer than
// the far plane (a building, a lamp post) hides them.
//
// The light is added (Blend One One) after the sky and before glass and other
// transparent things; nothing is written to depth, and there is no fog.
// _Strength is 0 by day (NightSky switches its renderer off then anyway).
// ---------------------------------------------------------------------------
Shader "Fixit Fidget/Night sky"
{
    Properties
    {
        _Strength("Strength (0 by day)", Range(0.0, 1.0)) = 0.0
        _StarBrightness("Star brightness", Range(0.0, 4.0)) = 1.0
        [HDR] _MoonColor("Moon colour", Color) = (0.8, 0.83, 0.9, 1.0)
        _MoonDisc("Moon disc (share of its quad)", Range(0.05, 1.0)) = 0.24
        _MoonGlow("Moon glow", Range(0.0, 1.0)) = 0.22
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent-450"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }

        Pass
        {
            Name "NightSky"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex NightSkyVertex
            #pragma fragment NightSkyFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _Strength;
                half _StarBrightness;
                half4 _MoonColor;
                half _MoonDisc;
                half _MoonGlow;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;   // the way to the star: a unit vector
                half4 color : COLOR;            // colour times brightness; a: how much it twinkles
                float4 uv0 : TEXCOORD0;         // xy: the corner (-1 or 1); z: half-size (radians); w: 0 a star, 1 the moon
                float2 uv1 : TEXCOORD1;         // twinkle: phase, speed
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float4 corner : TEXCOORD0;      // xy: the corner; z: 0 a star, 1 the moon; w: the horizon's fade
                float2 twinkle : TEXCOORD1;
            };

            Varyings NightSkyVertex(Attributes input)
            {
                Varyings output;
                float3 direction = normalize(input.positionOS.xyz);
                float3 helper = abs(direction.y) > 0.999 ? float3(0.0, 0.0, 1.0) : float3(0.0, 1.0, 0.0);
                float3 right = normalize(cross(helper, direction));
                float3 up = cross(direction, right);
                float3 onSky = direction + (right * input.uv0.x + up * input.uv0.y) * input.uv0.z;
                // Just inside the far plane of the camera drawing it, centred on that camera.
                float radius = _ProjectionParams.z * 0.9;
                output.positionCS = TransformWorldToHClip(_WorldSpaceCameraPos + onSky * radius);
                output.color = input.color;
                output.corner = float4(input.uv0.xy, input.uv0.w, saturate((direction.y - 0.02) / 0.2));
                output.twinkle = input.uv1;
                return output;
            }

            half4 NightSkyFragment(Varyings input) : SV_Target
            {
                float2 c = input.corner.xy;
                float r2 = dot(c, c);
                if (input.corner.z < 0.5)
                {
                    // A star: a soft round point that fades towards the horizon.
                    half star = saturate(1.0 - r2);
                    star = star * star * star;
                    half shimmer = 1.0 + input.color.a * sin(_Time.y * input.twinkle.y + input.twinkle.x);
                    return half4(input.color.rgb * (star * shimmer * input.corner.w * _Strength * _StarBrightness), 0.0);
                }

                // The moon: a disc in the middle of its quad, darker towards its edge and in
                // three soft patches, with a glow round it that fades out before the quad's edge.
                float2 m = c / _MoonDisc;
                float d2 = dot(m, m);
                half disc = saturate((1.0 - sqrt(d2)) * 30.0);
                half limb = sqrt(saturate(1.0 - d2));
                float2 p1 = m - float2(-0.28, 0.22);
                float2 p2 = m - float2(0.22, -0.08);
                float2 p3 = m - float2(-0.05, -0.38);
                half patches = 0.16 * exp(-dot(p1, p1) * 7.0) + 0.12 * exp(-dot(p2, p2) * 10.0) + 0.10 * exp(-dot(p3, p3) * 14.0);
                half3 face = _MoonColor.rgb * (0.72 + 0.28 * limb) * (1.0 - patches);
                half glow = _MoonGlow * exp(-sqrt(r2) * 4.0) * (1.0 - disc) * saturate(1.0 - r2);
                return half4((face * disc + _MoonColor.rgb * glow) * (_Strength * input.corner.w), 0.0);
            }
            ENDHLSL
        }
    }
}
