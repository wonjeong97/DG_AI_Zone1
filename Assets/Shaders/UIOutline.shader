Shader "Custom/UI/Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}
        _OutlineColor     ("Outline Color",     Color)        = (1, 1, 1, 1)
        _OutlineThickness ("Outline Thickness", Range(0, 10)) = 1

        // Unity UI 내부 프로퍼티 — Mask 컴포넌트 스텐실 지원
        _StencilComp     ("Stencil Comparison", Float) = 8
        _Stencil         ("Stencil ID",         Float) = 0
        _StencilOp       ("Stencil Operation",  Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask",  Float) = 255
        _ColorMask       ("Color Mask",         Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"             = "Transparent"
            "IgnoreProjector"   = "True"
            "RenderType"        = "Transparent"
            "PreviewType"       = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull      Off
        Lighting  Off
        ZWrite    Off
        ZTest     [unity_GUIZTestMode]
        Blend     SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

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
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;   // (1/texW, 1/texH, texW, texH)
            fixed4    _OutlineColor;
            float     _OutlineThickness;
            fixed4    _TextureSampleAdd;    // UI 시스템 보조 블렌딩값
            float4    _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex        = UnityObjectToClipPos(o.worldPosition);
                o.uv            = v.texcoord;
                o.color         = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // ── 현재 픽셀 색상 ─────────────────────────────────────────
                fixed4 color = (tex2D(_MainTex, i.uv) + _TextureSampleAdd) * i.color;

                // ── 8방향 이웃 픽셀 최대 알파 ─────────────────────────────
                // _OutlineThickness 텍셀 거리만큼 오프셋 샘플
                float2 d = _MainTex_TexelSize.xy * _OutlineThickness;

                float nAlpha = 0;
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2( d.x,    0)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2(-d.x,    0)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2(   0,  d.y)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2(   0, -d.y)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2( d.x,  d.y)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2(-d.x,  d.y)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2( d.x, -d.y)).a);
                nAlpha = max(nAlpha, tex2D(_MainTex, i.uv + float2(-d.x, -d.y)).a);

                // ── 테두리 판정 ────────────────────────────────────────────
                // (1 - color.a): 현재 픽셀이 투명할수록 1에 가까움
                // → 반투명 경계(안티앨리어싱 픽셀)도 자연스럽게 블렌딩됨
                float isOutline = (1.0 - saturate(color.a)) * step(0.01, nAlpha);

                // ── 테두리 색 합성 ─────────────────────────────────────────
                color.rgb = lerp(color.rgb, _OutlineColor.rgb, isOutline);
                // 테두리 알파도 CanvasGroup/애니메이션 vertex alpha를 따르게 처리
                color.a   = max(color.a, _OutlineColor.a * isOutline * i.color.a);

                // ── Mask 컴포넌트 클리핑 ───────────────────────────────────
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
