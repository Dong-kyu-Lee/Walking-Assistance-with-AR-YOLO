Shader "Custom/Step2Background"
{
    Properties
    {
        _Step2BackgroundTex("Step2 Background Tex", 2D) = "red" {}
        _DebugMode("Debug Mode", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        ZTest Always
        Cull Off
        Blend Off

        Pass
        {
            Name "Step2BackgroundPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Step2BackgroundTex);
            SAMPLER(sampler_Step2BackgroundTex);

            float _DebugMode;

            struct Attributes
            {
                uint vertexID : SV_VertexID;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                float2 pos;
                float2 uv;

                if (input.vertexID == 0)
                {
                    pos = float2(-1.0, -1.0);
                    uv = float2(0.0, 0.0);
                }
                else if (input.vertexID == 1)
                {
                    pos = float2(-1.0,  3.0);
                    uv = float2(0.0, 2.0);
                }
                else
                {
                    pos = float2( 3.0, -1.0);
                    uv = float2(2.0, 0.0);
                }

                output.positionCS = float4(pos, 0.0, 1.0);
                output.uv = uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                if (_DebugMode > 1.5)
                    return half4(1, 0, 0, 1);   // »¡°­

                if (_DebugMode > 0.5)
                    return half4(uv.x, uv.y, 0, 1); // UV µð¹ö±×

                uv.y = 1.0 - uv.y;
                return SAMPLE_TEXTURE2D(_Step2BackgroundTex, sampler_Step2BackgroundTex, uv);
            }
            ENDHLSL
        }
    }
}