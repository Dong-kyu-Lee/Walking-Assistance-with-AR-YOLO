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

            float4 _CameraFeed_ST;

            float _ViewScale;
            float2 _ViewCenter;
            float4 _BackgroundColor;

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

                localUV = localUV * _CameraFeed_ST.xy + _CameraFeed_ST.zw;

                return SAMPLE_TEXTURE2D(_CameraFeedTex, sampler_CameraFeedTex, localUV);
            }

            ENDHLSL
        }
    }
}