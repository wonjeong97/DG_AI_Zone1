Shader "Custom/URP/ToonOutline"
{
    Properties
    {
        [Header(Base Settings)]
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        [MainTexture] _BaseMap("Base Map (Albedo)", 2D) = "white" {}
        
        [Header(Toon Settings)]
        _ShadowColor("Shadow Color", Color) = (0.5, 0.5, 0.6, 1)
        _ToonStep("Toon Step", Range(0.0, 1.0)) = 0.5
        _ToonSmooth("Toon Smoothness", Range(0.0, 1.0)) = 0.05
        
        [Header(Outline Settings)]
        _OutlineColor("Outline Color", Color) = (0, 0, 0, 1)
        _OutlineWidth("Outline Width", Range(0.0, 0.1)) = 0.01
    }

    SubShader
    {
        Tags 
        { 
            "RenderType" = "Opaque" 
            "RenderPipeline" = "UniversalPipeline" 
            "Queue" = "Geometry"
        }

        // --- 1. Outline Pass ---
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 오브젝트 공간에서 노말 방향으로 버텍스를 약간 밀어내어 외곽선 형성
                float3 normalVS = TransformObjectToWorldNormal(input.normalOS);
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                
                // 클립 공간에서 노말을 계산하여 스케일에 상관없이 일정한 굵기의 라인 형성
                float3 normalCS = TransformWorldToHClipDir(normalVS);
                positionCS.xy += normalize(normalCS.xy) * _OutlineWidth * positionCS.w;
                
                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // --- 2. Toon Shading Pass ---
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 uv           : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float3 normalWS     : TEXCOORD0;
                float2 uv           : TEXCOORD1;
                float4 shadowCoord  : TEXCOORD3;
            };

            Texture2D _BaseMap;
            SamplerState sampler_BaseMap;

            float4 _BaseColor;
            float4 _ShadowColor;
            float _ToonStep;
            float _ToonSmooth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                
                // URP 14 style shadow coord transform
                #if defined(_MAIN_LIGHT_SHADOWS) || defined(_MAIN_LIGHT_SHADOWS_CASCADE)
                output.shadowCoord = TransformWorldToShadowCoord(vertexInput.positionWS);
                #else
                output.shadowCoord = 0;
                #endif
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 텍스처 및 기본 색상 샘플링
                float4 texColor = _BaseMap.Sample(sampler_BaseMap, input.uv);
                float4 albedo = texColor * _BaseColor;

                // 조명 계산
                float3 normal = normalize(input.normalWS);
                
                // URP 메인 라이트 가져오기
                Light mainLight = GetMainLight(input.shadowCoord);
                float3 lightDir = mainLight.direction;
                
                // N dot L (Lambertian 조명 모델)
                float NdotL = dot(normal, lightDir);
                
                // 음영 경계를 딱딱하게(Toon Shading) 분할
                // NdotL이 ToonStep 보다 큰 부분은 밝게, 작은 부분은 어둡게 처리
                float toonLight = smoothstep(_ToonStep - _ToonSmooth, _ToonStep + _ToonSmooth, NdotL * 0.5 + 0.5);
                
                // 그림자 감쇠(Shadow Attenuation) 반영
                toonLight *= mainLight.shadowAttenuation;
                
                // 밝은 영역과 어두운 영역 색상 보간
                float3 finalColor = lerp(_ShadowColor.rgb * albedo.rgb, albedo.rgb, toonLight);
                
                // 환경광(Ambient Light)을 약간 추가하여 완전한 검은색 그림자를 방지
                float3 ambient = SampleSH(normal) * albedo.rgb * 0.2;
                finalColor += ambient;

                return half4(finalColor, albedo.a);
            }
            ENDHLSL
        }
    }
}
