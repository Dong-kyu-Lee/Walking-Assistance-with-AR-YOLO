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
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            TEXTURE2D(_CameraFeedTex);
            SAMPLER(sampler_CameraFeedTex);

            float4 _CameraFeed_ST;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;
                uv = uv * _CameraFeed_ST.xy + _CameraFeed_ST.zw;

                return SAMPLE_TEXTURE2D(_CameraFeedTex, sampler_CameraFeedTex, uv);
            }

            ENDHLSL
        }
    }
}