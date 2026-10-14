Shader "WhoisntCitizen/UI/DayOcean"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _OceanTime ("Ocean time", Float) = 0
        _SpriteUV ("Sprite UV bounds", Vector) = (0,0,1,1)
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            sampler2D _MainTex;
            fixed4 _Color, _TextureSampleAdd;
            float4 _ClipRect, _SpriteUV;
            float _OceanTime;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float OutsideRock(float2 p, float2 center, float2 radius)
            {
                return smoothstep(0.95, 1.15, length((p - center) / radius));
            }

            float SeaMask(float2 p)
            {
                // Normalized coordinates, top-left origin, matching the 941 x 1672 Deck painting.
                // Feather the horizon/rail and leave ropes, lanterns, islands and foreground rocks still.
                float mask = smoothstep(0.166, 0.177, p.y) * (1.0 - smoothstep(0.291, 0.300, p.y));
                float left = lerp(0.246, 0.217, saturate((p.y - 0.17) / 0.13));
                float right = lerp(0.772, 0.809, saturate((p.y - 0.17) / 0.13));
                mask *= smoothstep(left, left + 0.014, p.x) * (1.0 - smoothstep(right - 0.014, right, p.x));
                mask *= OutsideRock(p, float2(0.300, 0.185), float2(0.073, 0.029));
                mask *= OutsideRock(p, float2(0.260, 0.224), float2(0.074, 0.031));
                mask *= OutsideRock(p, float2(0.431, 0.220), float2(0.077, 0.020));
                mask *= OutsideRock(p, float2(0.758, 0.224), float2(0.047, 0.027));
                mask *= OutsideRock(p, float2(0.309, 0.276), float2(0.039, 0.040));
                mask *= OutsideRock(p, float2(0.684, 0.278), float2(0.043, 0.042));
                return mask;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uvSize = max(_SpriteUV.zw - _SpriteUV.xy, float2(0.0001, 0.0001));
                float2 p = (i.uv - _SpriteUV.xy) / uvSize;
                p.y = 1.0 - p.y;
                fixed4 original = tex2D(_MainTex, i.uv) + _TextureSampleAdd;
                float mask = SeaMask(p);
                // Blue water/white foam move; warm ship details remain pinned even at the feathered edges.
                mask *= smoothstep(0.02, 0.16, original.b - original.r);
                float depth = saturate((p.y - 0.166) / 0.134);
                float t = _OceanTime;
                float swell = sin(p.y * 290.0 - t * 1.35 + sin(p.x * 19.0 + t * 0.3));
                float ripple = sin(p.y * 530.0 + p.x * 32.0 - t * 2.1);
                float2 offset = float2(swell * 0.0028 + ripple * 0.0008, cos(p.x * 36.0 - t * 1.1) * 0.00065);
                offset *= mask * lerp(0.25, 1.0, depth);
                fixed4 water = tex2D(_MainTex, i.uv + offset * uvSize) + _TextureSampleAdd;
                fixed4 color = lerp(original, water, mask);
                // The painted whitecaps catch light at different times, with no new particles over the ship.
                float foam = smoothstep(0.58, 0.95, original.g);
                color.rgb *= 1.0 + mask * foam * sin(p.x * 76.0 + p.y * 220.0 - t * 1.8) * 0.055;
                color *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif
                return color;
            }
            ENDCG
        }
    }
}
