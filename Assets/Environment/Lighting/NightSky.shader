// Night sky for Environment.unity, ported from the prototype's sky dome (prototype/src/world/sky.js): a horizon to
// zenith gradient, hashed twinkling stars, a moon disc with craters and a halo, and drifting fbm clouds lit from the
// moon side. All colours are linear, as in the prototype. Blackout/World/Setup Lighting sets the colours, the cloud
// cover and the moon direction from LightingLayout.cs.
// Owner: World Builder.
Shader "Blackout/NightSky"
{
    Properties
    {
        _HorizonColor ("Horizon", Color) = (0.27, 0.33, 0.43, 1)
        _ZenithColor ("Zenith", Color) = (0.1, 0.13, 0.2, 1)
        _MoonDirection ("Direction toward the moon", Vector) = (-0.349, 0.419, 0.838, 0)
        _CloudCover ("Cloud cover", Range(0, 1)) = 0.5
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _HorizonColor;
                float4 _ZenithColor;
                float4 _MoonDirection;
                float _CloudCover;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // The prototype's hashes and value noise, unchanged.
            float Hash3(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash2(i), Hash2(i + float2(1, 0)), f.x), lerp(Hash2(i + float2(0, 1)), Hash2(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float sum = 0.0;
                float amplitude = 0.5;
                [unroll]
                for (int i = 0; i < 5; i++)
                {
                    sum += amplitude * Noise(p);
                    p *= 2.03;
                    amplitude *= 0.5;
                }

                return sum;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz; // the skybox mesh is centred on the camera
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 d = normalize(input.direction);
                float h = saturate(d.y);
                float3 col = lerp(_HorizonColor.rgb, _ZenithColor.rgb, pow(h, 0.55));

                // Stars: 0.35 % of the 1/220 rad cells hold one, jittered inside its cell, twinkling, fading in
                // above the horizon.
                float3 sp = d * 220.0;
                float3 cell = floor(sp);
                float s = Hash3(cell);
                if (s > 0.9965)
                {
                    float3 c = cell + 0.5 + (float3(Hash3(cell + 1.3), Hash3(cell + 2.7), Hash3(cell + 5.1)) - 0.5) * 0.4;
                    float dist = length(sp - c);
                    float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + s * 9.0) + s * 100.0);
                    float star = (1.0 - smoothstep(0.0, 0.26, dist)) * twinkle * smoothstep(0.02, 0.25, h);
                    col += float3(0.85, 0.9, 1.0) * star * min(1.0 + (s - 0.9965) * 500.0, 2.2);
                }

                // Moon: a disc 1.3 to 1.7 degrees across (about 6 times the real moon) at 3.2 times white, so it
                // blooms, with craters and a two-part halo.
                float md = dot(d, normalize(_MoonDirection.xyz));
                float disc = smoothstep(0.99955, 0.99975, md);
                float craters = 0.82 + 0.18 * Noise(float2(d.x, d.y) * 900.0 * 0.05);
                col = lerp(col, float3(1.0, 0.97, 0.9) * 3.2 * craters, disc);
                col += float3(0.45, 0.55, 0.75) * pow(max(md, 0.0), 220.0) * 0.6;
                col += float3(0.2, 0.26, 0.38) * pow(max(md, 0.0), 12.0) * 0.18;

                // Clouds: fbm on a flat layer above the camera, scrolling slowly, brighter on the moon side. The
                // prototype's z is Unity's -z, so the pattern and its drift match the prototype.
                if (d.y > 0.0)
                {
                    float2 cp = float2(d.x, -d.z) / (d.y + 0.12) * 1.4 + float2(_Time.y * 0.012, _Time.y * 0.004);
                    float cloud = smoothstep(0.52 - _CloudCover * 0.2, 0.85, Fbm(cp)) * smoothstep(0.0, 0.3, d.y);
                    float3 cloudCol = lerp(float3(0.03, 0.045, 0.07), float3(0.16, 0.19, 0.26), pow(max(md, 0.0), 6.0) + 0.15);
                    col = lerp(col, cloudCol, cloud * 0.88);
                }

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
