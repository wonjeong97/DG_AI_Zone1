Shader "DG/DamWaterFlow"
{
    Properties
    {
        _BaseColor  ("Base Color", Color) = (0.12, 0.45, 0.78, 0.85)
        _FoamColor  ("Foam Color", Color) = (0.92, 0.97, 1.0, 1.0)
        _Speed      ("Flow Speed", Float) = 1.2
        _Tiling     ("Flow Tiling", Float) = 6.0
        _FoamAmount ("Foam Amount", Range(0,1)) = 0.35
        _Opening    ("Opening (0-1)", Range(0,1)) = 1.0
        _WidthFrac  ("Stream Width (0-1)", Range(0,1)) = 1.0
        _HeightFrac ("Stream Height (0-1)", Range(0,1)) = 1.0
        _FlowFrac   ("Flow Reach (0-1)", Range(0,1)) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"       = "Transparent"
            "RenderPipeline"   = "UniversalPipeline"
            "Queue"            = "Transparent"
        }

        Pass
        {
            Name "ForwardUnlitWater"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _FoamColor;
                float  _Speed;
                float  _Tiling;
                float  _FoamAmount;
                float  _Opening;
                float  _WidthFrac;
                float  _HeightFrac;
                float  _FlowFrac;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 normalWS   : TEXCOORD1;
            };

            // 값 노이즈 - 외부 텍스처 없이 물결 패턴 생성
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1, 0));
                float c = hash21(i + float2(0, 1));
                float d = hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS   = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv         = IN.uv;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 수문 개방량만큼 물줄기 폭을 중앙 기준으로 좁힌다 (스케일 대신 셰이더에서 처리)
                float distFromCenter = abs(IN.uv.x - 0.5) * 2.0;
                clip(_WidthFrac - distFromCenter);
                float edgeFade = smoothstep(_WidthFrac, _WidthFrac - 0.15, distFromCenter);

                // 높이 - V=0이 마루, V=1이 토우. 아래(토우)에서부터 위로 물기둥이 자란다.
                float topCut = 1.0 - _HeightFrac;
                clip(IN.uv.y - topCut);
                float topFade = smoothstep(topCut, topCut + 0.10, IN.uv.y);

                // 도달 거리 - 수문이 열린 뒤 물이 마루에서 토우까지 내려가는 연출.
                // 선단은 leadWidth만큼 부드럽게 사라지는데, 그 폭만큼 여유를 두지 않으면
                // 완전히 도달한 상태(_FlowFrac=1)에서도 토우 끝단이 투명하게 남는다.
                const float leadWidth = 0.08;
                float flowEdge = _FlowFrac * (1.0 + leadWidth);
                clip(flowEdge - IN.uv.y);
                float leadFade = smoothstep(flowEdge, flowEdge - leadWidth, IN.uv.y);

                // V축이 흐름 방향(블렌더에서 호길이로 UV를 깔아둠)
                float t = _Time.y * _Speed;

                float n1 = vnoise(float2(IN.uv.x * _Tiling,        IN.uv.y * _Tiling * 3.0 - t * 2.0));
                float n2 = vnoise(float2(IN.uv.x * _Tiling * 2.0 + 3.7, IN.uv.y * _Tiling * 6.0 - t * 3.3));
                float flow = saturate(n1 * 0.6 + n2 * 0.4);

                // 아래로 갈수록 거품이 많아지도록
                float downBias = saturate(IN.uv.y * 0.6 + 0.4);
                float foam = smoothstep(1.0 - _FoamAmount * downBias, 1.0, flow);

                half3 col = lerp(_BaseColor.rgb, _FoamColor.rgb, foam);

                // 씬 조명과 어울리도록 약한 디퓨즈
                Light mainLight = GetMainLight();
                float3 n = normalize(IN.normalWS);
                float ndotl = saturate(abs(dot(n, mainLight.direction)));
                col *= lerp(0.75, 1.15, ndotl);

                half alpha = _BaseColor.a * saturate(_Opening) * edgeFade * topFade * leadFade;
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
