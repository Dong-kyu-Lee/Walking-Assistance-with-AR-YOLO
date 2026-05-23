Shader "Hidden/CameraFeed/FullScreen"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "CameraFeedFullScreen"

            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CameraFeedTex);
            SAMPLER(sampler_CameraFeedTex);
            
            TEXTURE2D(_PolygonOverlayTex); // 텍스처 데이터 자체를 받는 슬롯
            SAMPLER(sampler_PolygonOverlayTex); // 텍스처를 읽을 때 쓸 필터/래핑 설정
            float _PolygonOverlayAvailable; // 폴리곤이 실제로 그려져 있는지 여부

            float4 _CameraFeed_ST;

            float _ViewScale;
            float2 _ViewCenter;
            float4 _BackgroundColor;

            float _Contrast;
            float _Brightness;
            float _Saturation;
            float _GrayscaleAmount;

            float4 _CameraAspectCrop;

            float2 ApplyCameraAspectCrop(float2 uv)
            {
                float2 crop = max(_CameraAspectCrop.xy, float2(0.0001, 0.0001));
                return (uv - 0.5) * crop + 0.5;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 screenUV = input.texcoord.xy;

                float scale = max(_ViewScale, 0.01);

                float2 halfSize = float2(scale * 0.5, scale * 0.5);
                float2 minUV = _ViewCenter - halfSize;
                float2 maxUV = _ViewCenter + halfSize;

                if (screenUV.x < minUV.x || screenUV.x > maxUV.x ||
                    screenUV.y < minUV.y || screenUV.y > maxUV.y)
                {
                    return _BackgroundColor;
                }

                float2 localUV = (screenUV - minUV) / scale;

                // 화면 출력용 UV에서 카메라 원본 비율을 유지하기 위한 crop UV 생성
                float2 cameraUV = ApplyCameraAspectCrop(localUV);

                // 기존 _CameraFeed_ST, Y flip, scale/offset 반영
                cameraUV = cameraUV * _CameraFeed_ST.xy + _CameraFeed_ST.zw;

                half4 color = SAMPLE_TEXTURE2D(_CameraFeedTex, sampler_CameraFeedTex, cameraUV);
                // ��� ����
                color.rgb += _Brightness;

                // ��� ����
                color.rgb = (color.rgb - 0.5) * _Contrast + 0.5;

                // ���� ���
                float gray = dot(color.rgb, float3(0.299, 0.587, 0.114));

                // ä�� ����
                color.rgb = lerp(gray.xxx, color.rgb, _Saturation);

                // ��� ���� ����
                color.rgb = lerp(color.rgb, gray.xxx, _GrayscaleAmount);

                color.rgb = saturate(color.rgb);
                // 카메라 피드와 폴리곤 오버레이 텍스처를 동일한 UV 좌표로 샘플링
                if (_PolygonOverlayAvailable > 0.5) {
                    half4 poly = SAMPLE_TEXTURE2D(_PolygonOverlayTex, sampler_PolygonOverlayTex, cameraUV);
                    color.rgb = lerp(color.rgb, poly.rgb, poly.a);
                }

                return color;
            }

            ENDHLSL
        }
    }
}