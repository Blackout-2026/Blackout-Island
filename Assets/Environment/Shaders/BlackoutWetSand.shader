// Blackout Island wet sand band (step OC1). A thin transparent strip laid just above the terrain along the waterline.
// Its vertex alpha is the wetness (strong at the waterline, zero at the dry edge), so the strip darkens and glosses the
// sand without a second terrain layer or a re-paint. One forward pass, lit by the main light, ambient and nearby lights.
Shader "Blackout/WetSand"
{
    Properties
    {
        _WetColor ("Wet sand colour", Color) = (0.10, 0.085, 0.065, 1)
        _Opacity ("Opacity at full wetness", Range(0, 1)) = 0.85
        _Smoothness ("Smoothness", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardWetSand"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _WetColor;
                float _Opacity;
                float _Smoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half wetness : TEXCOORD2;
                half fogFactor : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.wetness = input.color.a;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float3 viewDir = normalize(GetCameraPositionWS() - input.positionWS);
                Light mainLight = GetMainLight();

                half3 lit = _WetColor.rgb * (SampleSH(normalWS) + mainLight.color * saturate(dot(normalWS, mainLight.direction)));

                // Wet sand is glossy: a faint sky reflection at grazing angles and a glint of the moon.
                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(normalWS, viewDir)), 5.0);
                float3 reflected = reflect(-viewDir, normalWS);
                lit += lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(reflected.y * 2.0)) * fresnel;
                float specPower = exp2(10.0 * _Smoothness + 1.0);
                lit += mainLight.color * pow(saturate(dot(normalWS, normalize(mainLight.direction + viewDir))), specPower) * 0.5;

                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; i++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    half3 lightColor = light.color * light.distanceAttenuation;
                    lit += _WetColor.rgb * lightColor * saturate(dot(normalWS, light.direction));
                }
                #endif

                return half4(MixFog(lit, input.fogFactor), saturate(input.wetness * _Opacity));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
