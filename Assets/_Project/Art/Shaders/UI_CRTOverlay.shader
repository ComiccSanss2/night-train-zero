// CRT overlay for UI Images (title screen, handheld OS, hacking interface).
// Put it on a full-screen, raycast-disabled Image at the top of the canvas.
// Draws: rolling scanlines, vignette, film noise, brightness flicker and horizontal glitch bands.
// _Glitch (0..1) is animated from script (MainMenuController / later the hacking UI).
Shader "Subnet/UI/CRTOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint (alpha = overall strength)", Color) = (0, 0, 0, 1)

        _ScanlineCount ("Scanline Count", Float) = 270
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.22
        _ScanlineSpeed ("Scanline Scroll Speed", Float) = 1.5

        _VignetteIntensity ("Vignette Intensity", Range(0, 2)) = 0.75
        _VignetteRoundness ("Vignette Inner Radius", Range(0, 1)) = 0.35

        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.05
        _FlickerIntensity ("Flicker Intensity", Range(0, 0.3)) = 0.025

        _Glitch ("Glitch Amount", Range(0, 1)) = 0
        _GlitchColorA ("Glitch Color A", Color) = (0.2, 1, 0.95, 1)
        _GlitchColorB ("Glitch Color B", Color) = (1, 0.3, 0.85, 1)
        _GlitchBands ("Glitch Band Count", Float) = 28

        // Standard UI stencil / mask support.
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
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

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
            Name "CRTOverlay"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            float4 _ClipRect;

            float _ScanlineCount;
            float _ScanlineIntensity;
            float _ScanlineSpeed;
            float _VignetteIntensity;
            float _VignetteRoundness;
            float _NoiseIntensity;
            float _FlickerIntensity;
            float _Glitch;
            fixed4 _GlitchColorA;
            fixed4 _GlitchColorB;
            float _GlitchBands;

            // Cheap hash, stable across platforms.
            float hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                p *= p + p;
                return frac(p);
            }

            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.texcoord;
                float t = _Time.y;

                // Rolling scanlines (dark stripes).
                float scan = sin((uv.y + t * _ScanlineSpeed * 0.01) * _ScanlineCount * 3.14159265) * 0.5 + 0.5;
                float scanDarkness = scan * _ScanlineIntensity;

                // Vignette.
                float2 centered = uv - 0.5;
                float dist = length(centered * float2(1.0, 1.15));
                float vignette = smoothstep(_VignetteRoundness, 0.85, dist) * _VignetteIntensity;

                // Animated grain.
                float grain = hash21(floor(uv * _ScreenParams.xy) + floor(t * 60.0)) * _NoiseIntensity;

                // Global brightness flicker (stepped at 24 fps for a mechanical feel).
                float flicker = (hash11(floor(t * 24.0)) - 0.5) * 2.0 * _FlickerIntensity;

                // Horizontal glitch bands: random rows light up in cyan/magenta while _Glitch > 0.
                float row = floor(uv.y * _GlitchBands);
                float frameSeed = floor(t * 30.0);
                float bandRandom = hash21(float2(row, frameSeed));
                float band = step(1.0 - _Glitch * 0.45, bandRandom) * step(0.001, _Glitch);
                float tear = step(0.5, hash21(float2(row * 3.7, frameSeed + 11.0)));
                fixed3 glitchColor = lerp(_GlitchColorA.rgb, _GlitchColorB.rgb, tear);

                float darkness = saturate(scanDarkness + vignette + grain + max(flicker, 0.0));
                float glitchAlpha = band * (0.25 + 0.35 * hash11(row + frameSeed));

                fixed4 col;
                col.rgb = lerp(i.color.rgb, glitchColor, saturate(glitchAlpha * 2.0));
                col.a = saturate(darkness + glitchAlpha) * i.color.a;

                // Keep sprite alpha so masks / rounded handheld screens still work.
                col.a *= tex2D(_MainTex, uv).a;

                #ifdef UNITY_UI_CLIP_RECT
                col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(col.a - 0.001);
                #endif

                return col;
            }
            ENDCG
        }
    }
}
