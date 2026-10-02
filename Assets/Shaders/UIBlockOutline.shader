// 블록 스프라이트 모양 둘레에 일정한 두께의 테두리를 그리는 셰이더 (컴파일 에러·성공, 스냅 하이라이트).
// 픽셀마다 둘레(반지름 = 테두리 두께)를 여러 방향으로 샘플링해 모양을 두께만큼 부풀린 실루엣을 칠한다.
// 본체 이미지 뒤에 깔리므로 본체 바깥으로 나온 띠만 보인다 — 홈·돌기 같은 연결부도 같은 두께로 따라간다.
// 샘플 위치는 블록 사각형 기준 좌표(0~1)에서 정하고 9-slice 대응식으로 UV를 구하므로, 늘어난 칸 경계를 넘어도 두께가 같다.
// 정점 확장과 사각형·9-slice 정보 전달은 BlockOutlineMesh가 맡는다(TEXCOORD0~3).
Shader "Custom/UI/BlockOutline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Main Texture", 2D) = "white" {}

        // 보일 영역 — 테두리 중 이 범위에 있는 픽셀만 그린다.
        // 스프라이트 기준 0~1 (아래·왼쪽이 0). 0이나 1이면 그쪽 바깥 테두리까지 연다
        _XMin ("Region X Min", Range(0, 1)) = 0
        _XMax ("Region X Max", Range(0, 1)) = 1
        _YMin ("Region Y Min", Range(0, 1)) = 0
        _YMax ("Region Y Max", Range(0, 1)) = 1

        // 원본 영역 — 블록 모양 중 이 범위만 부풀려 테두리를 만든다(예: ㄷ자 안쪽 하이라이트에서 왼쪽 팔 제외). 기준은 보일 영역과 같다
        _SrcXMin ("Source X Min", Range(0, 1)) = 0
        _SrcXMax ("Source X Max", Range(0, 1)) = 1
        _SrcYMin ("Source Y Min", Range(0, 1)) = 0
        _SrcYMax ("Source Y Max", Range(0, 1)) = 1

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
            #pragma target   3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            // 둘레 샘플 방향 수 — 많을수록 가는 돌기·홈도 놓치지 않지만 픽셀당 샘플이 늘어난다
            #define OUTLINE_DIRECTIONS 24

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
                float4 rect   : TEXCOORD0; // xy: 사각형 기준 좌표(0~1, 테두리 여백은 범위 밖), zw: 테두리 두께 / 사각형 크기
                float4 slice  : TEXCOORD1; // 9-slice 칸 경계 (x1, x2, y1, y2) — 사각형 기준 좌표
                float4 uvX    : TEXCOORD2; // 가로 경계 0·x1·x2·1에서의 UV
                float4 uvY    : TEXCOORD3; // 세로 경계 0·y1·y2·1에서의 UV
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float4 rect          : TEXCOORD0;
                float4 slice         : TEXCOORD1;
                float4 uvX           : TEXCOORD2;
                float4 uvY           : TEXCOORD3;
                float4 worldPosition : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4    _TextureSampleAdd;
            float4    _ClipRect;
            float     _XMin;
            float     _XMax;
            float     _YMin;
            float     _YMax;
            float     _SrcXMin;
            float     _SrcXMax;
            float     _SrcYMin;
            float     _SrcYMax;

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex        = UnityObjectToClipPos(o.worldPosition);
                o.rect          = v.rect;
                o.slice         = v.slice;
                o.uvX           = v.uvX;
                o.uvY           = v.uvY;
                o.color         = v.color;
                return o;
            }

            // 사각형 기준 좌표 한 축을 UV로 바꾼다 — 경계 칸은 원본 비율, 가운데 칸은 늘어난 비율(Image의 9-slice와 같은 대응)
            float MapAxis(float n, float n1, float n2, float4 u)
            {
                if (n < n1) return u.x + (u.y - u.x) * (n / max(n1, 1e-6));
                if (n > n2) return u.z + (u.w - u.z) * ((n - n2) / max(1.0 - n2, 1e-6));
                return u.y + (u.z - u.y) * ((n - n1) / max(n2 - n1, 1e-6));
            }

            float2 MapUV(float2 n, v2f i)
            {
                return float2(MapAxis(n.x, i.slice.x, i.slice.y, i.uvX), MapAxis(n.y, i.slice.z, i.slice.w, i.uvY));
            }

            // UV를 스프라이트 기준 좌표(0~1, 아래·왼쪽이 0)로 바꾼다 — 9-slice로 늘어나도 아트 위치가 기준
            float2 SpriteCoord(float2 uv, v2f i)
            {
                float2 uvMin  = float2(i.uvX.x, i.uvY.x);
                float2 uvSize = max(float2(i.uvX.w - i.uvX.x, i.uvY.w - i.uvY.x), 1e-6);
                return (uv - uvMin) / uvSize;
            }

            // 스프라이트 기준 좌표가 범위 안인지 — 경계가 0이나 1이면 그쪽은 열어 둔다
            float RangeMask(float2 s, float xMin, float xMax, float yMin, float yMax)
            {
                xMin = xMin > 0.0 ? xMin : -1e4;
                xMax = xMax < 1.0 ? xMax :  1e4;
                yMin = yMin > 0.0 ? yMin : -1e4;
                yMax = yMax < 1.0 ? yMax :  1e4;
                return step(xMin, s.x) * step(s.x, xMax) * step(yMin, s.y) * step(s.y, yMax);
            }

            // 블록 사각형 밖과 원본 영역 밖은 투명 — 넓힌 여백에서 가장자리 픽셀이 늘어나 보이지 않도록.
            // 밉맵이 없는 UI 텍스처라 원본 해상도(lod 0)로 샘플링한다(분기 안에서도 기울기 계산이 필요 없음).
            fixed SampleAlpha(float2 n, v2f i)
            {
                float  inside = step(0.0, n.x) * step(n.x, 1.0) * step(0.0, n.y) * step(n.y, 1.0);
                float2 uv     = MapUV(n, i);
                float  source = RangeMask(SpriteCoord(uv, i), _SrcXMin, _SrcXMax, _SrcYMin, _SrcYMax);
                return (tex2Dlod(_MainTex, float4(uv, 0, 0)) + _TextureSampleAdd).a * inside * source;
            }

            // 이 픽셀이 보일 영역 안인지
            float RegionMask(float2 n, v2f i)
            {
                return RangeMask(SpriteCoord(MapUV(n, i), i), _XMin, _XMax, _YMin, _YMax);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 n = i.rect.xy;

                // 하이라이트가 꺼져 있거나(투명) 보일 영역 밖이면 샘플링 없이 끝낸다
                if (i.color.a <= 0.0 || RegionMask(n, i) <= 0.0) return fixed4(i.color.rgb, 0.0);

                fixed alpha = SampleAlpha(n, i);

                // 바깥 원(두께)과 안쪽 원(두께의 절반)을 함께 훑어 방향 사이로 가는 돌기를 놓치지 않는다
                [unroll]
                for (int k = 0; k < OUTLINE_DIRECTIONS; k++)
                {
                    float  angle  = k * (UNITY_TWO_PI / OUTLINE_DIRECTIONS);
                    float2 offset = float2(cos(angle), sin(angle)) * i.rect.zw;
                    alpha = max(alpha, SampleAlpha(n + offset, i));
                    alpha = max(alpha, SampleAlpha(n + offset * 0.5, i));
                }

                fixed4 color = fixed4(i.color.rgb, alpha * i.color.a);

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
