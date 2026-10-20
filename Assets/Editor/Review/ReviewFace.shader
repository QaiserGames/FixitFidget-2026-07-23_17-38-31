// The review's face shader (Review 1 - model sheets, 19 Oct 2026; ReviewModelSheets.cs).
//
// Grace's camera strap was see-through in the game: its leather faces point the wrong way, so Unity (front faces only)
// culls them, while Blender's review renders drew both sides and looked right. This shader is how the review sees a
// model the way a player does: unlit, no lighting to hide anything, and the two sides of every face in two flat colours.
//
//   _Cull  Back (2): what the game does; an inside-out part shows as missing (the x-ray sheet, the occluders).
//   _Cull  Off  (0): both sides drawn, _Front where a face points at the camera, _Back where its back does (the face
//                    test: white front, red back; the share of red is the share of the part a player would see through).
//
// Plain and tiny on purpose: one pass with no LightMode tag (URP draws untagged passes as SRPDefaultUnlit, and so does
// the built-in pipeline), UnityCG only, and the front-face macros from HLSLSupport.cginc so it compiles on Metal (his
// Mac) and D3D11 (his PC) alike; target 3.5, because SV_IsFrontFace needs a shader model 4 profile on D3D11 and the
// level 9.x profiles a lower target can pick reject it. The DepthOnly pass is insurance: if URP's depth priming is ever
// switched on, opaque objects are drawn ZTest Equal against a depth prepass, and a shader without a DepthOnly pass
// would vanish from it.
Shader "Hidden/FixitFidget/ReviewFace"
{
    Properties
    {
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 0
        _Front ("Front", Color) = (1,1,1,1)
        _Back ("Back", Color) = (1,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Faces"
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            Blend Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            // HLSLSupport.cginc (pulled in by UnityCG) defines these for D3D11, Metal, Vulkan and GL core; the fallback
            // is the same thing spelled out, for any compiler target that lacks the macros.
            #ifndef FRONT_FACE_TYPE
                #define FRONT_FACE_TYPE bool
                #define FRONT_FACE_SEMANTIC SV_IsFrontFace
                #define IS_FRONT_VFACE(VAL, FRONT, BACK) ((VAL) ? (FRONT) : (BACK))
            #endif

            float4 _Front;
            float4 _Back;

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                return o;
            }

            float4 frag(v2f i, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                return IS_FRONT_VFACE(face, _Front, _Back);
            }
            ENDCG
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            Cull [_Cull]
            ZWrite On
            ZTest LEqual
            ColorMask R

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "UnityCG.cginc"

            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.pos.z;
            }
            ENDCG
        }
    }
    Fallback Off
}
