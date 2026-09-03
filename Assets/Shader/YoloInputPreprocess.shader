Shader "Hidden/YOLO/InputPreprocess"
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

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            float _AspectMode;
            float _RotationSteps;
            float _FlipX;
            float _FlipY;
            float4 _SourceSize;
            float4 _TargetSize;

            float2 ApplyAspect(float2 uv, out bool outside)
            {
                outside = false;

                if (_AspectMode < 0.5)
                    return uv;

                float sourceAspect = max(_SourceSize.x, 1.0) / max(_SourceSize.y, 1.0);
                float targetAspect = max(_TargetSize.x, 1.0) / max(_TargetSize.y, 1.0);

                if (_AspectMode < 1.5)
                {
                    if (sourceAspect > targetAspect)
                    {
                        float scaleX = targetAspect / sourceAspect;
                        uv.x = (uv.x - 0.5) * scaleX + 0.5;
                    }
                    else
                    {
                        float scaleY = sourceAspect / targetAspect;
                        uv.y = (uv.y - 0.5) * scaleY + 0.5;
                    }

                    return uv;
                }

                if (sourceAspect > targetAspect)
                {
                    float scaleY = sourceAspect / targetAspect;
                    uv.y = (uv.y - 0.5) * scaleY + 0.5;
                }
                else
                {
                    float scaleX = targetAspect / sourceAspect;
                    uv.x = (uv.x - 0.5) * scaleX + 0.5;
                }

                outside = uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0;
                return uv;
            }

            float2 ApplyRotation(float2 uv)
            {
                int steps = (int)round(_RotationSteps) % 4;

                if (steps == 1)
                    return float2(uv.y, 1.0 - uv.x);

                if (steps == 2)
                    return float2(1.0 - uv.x, 1.0 - uv.y);

                if (steps == 3)
                    return float2(1.0 - uv.y, uv.x);

                return uv;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.texcoord = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord.xy;
                bool outside;

                uv = ApplyAspect(uv, outside);
                uv = ApplyRotation(uv);

                if (_FlipX > 0.5)
                    uv.x = 1.0 - uv.x;

                if (_FlipY > 0.5)
                    uv.y = 1.0 - uv.y;

                if (outside)
                    return half4(0, 0, 0, 1);

                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
            }

            ENDHLSL
        }
    }
}
