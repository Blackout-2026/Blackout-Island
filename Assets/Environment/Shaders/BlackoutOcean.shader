// Blackout Island sea surface (step OC1). One transparent URP forward pass, no textures.
//
//   Shape    A sum of three long swells moves the vertices (vertex shader). Each wave is a plain sine travelling along
//            a direction, with the deep-water speed sqrt(g * k), so longer waves travel faster.
//   Detail   Eight short ripples from scattered directions bend only the normal (fragment shader), so the 128 x 128 mesh
//            can stay small and the chop has no visible stripes.
//   Colour   Shallow-to-deep tint from the vertical water depth under the pixel (the surface height minus the height of
//            what the camera sees through it, rebuilt from the depth texture), the sky reflection with a Fresnel term,
//            the moon's glint, and the glow of nearby lights such as the boat lantern.
//   Shore    The same depth gives a soft transparent edge and a pulsing foam line where the water meets sand.
//
// Needs "Depth Texture" on the URP asset (PC_RPAsset has it). Without it the shore foam and depth tint are not drawn
// correctly, but the surface still renders. The surface does not cast or receive shadows.
Shader "Blackout/Ocean"
{
    Properties
    {
        _ShallowColor ("Shallow colour", Color) = (0.07, 0.20, 0.22, 1)
        _DeepColor ("Deep colour", Color) = (0.015, 0.04, 0.08, 1)
        _DepthRange ("Depth at full deep colour (m)", Float) = 5
        _MaxAlpha ("Opacity in deep water", Range(0, 1)) = 0.95
        _EdgeFade ("Depth over which the shore edge fades in (m)", Float) = 0.3
        _FoamColor ("Foam colour", Color) = (0.78, 0.84, 0.86, 1)
        _FoamWidth ("Shore foam width (m)", Float) = 0.9
        _CrestFoam ("Speckled foam on the highest crests", Range(0, 1)) = 0.2
        _WaveHeight ("Swell height scale", Range(0, 2)) = 1
        _RippleStrength ("Ripple normal strength", Range(0, 4)) = 1
        _Smoothness ("Smoothness (moon glint tightness)", Range(0.5, 1)) = 0.92
        _GlintStrength ("Moon glint strength", Range(0, 4)) = 0.45
        _SkyReflection ("Sky reflection strength", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardOcean"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _DeepColor;
                half4 _FoamColor;
                float _DepthRange;
                float _MaxAlpha;
                float _EdgeFade;
                float _FoamWidth;
                float _CrestFoam;
                float _WaveHeight;
                float _RippleStrength;
                float _Smoothness;
                float _GlintStrength;
                float _SkyReflection;
            CBUFFER_END

            // Each wave: xy = direction the crest travels (normalised), z = wavelength (m), w = amplitude (m).
            // Swells (move the mesh, wavelength >= 4 mesh cells so the grid can show them).
            static const int SWELL_COUNT = 3;
            // The three swells come from different directions (about 50, 100 and 135 degrees apart in heading), as a real
            // cross sea does. Waves that all travel the same way line their crests up across the view and read as stripes.
            static const float4 SWELLS[3] =
            {
                float4(0.643, 0.766, 38.0, 0.060),
                float4(-0.174, 0.985, 26.0, 0.050),
                float4(-0.707, 0.707, 17.0, 0.030)
            };

            // Ripples (normal only): eight short waves whose headings step by the golden angle (137.5 degrees) and whose
            // wavelengths shrink by about 0.76 each time, so no two share a direction or a spacing and the sum has no
            // visible pattern, just chop. The amplitude is 0.03 / k, so every ripple tilts the surface by a similar angle.
            static const int RIPPLE_COUNT = 8;
            static const float4 RIPPLES[8] =
            {
                float4(0.966, 0.259, 5.4, 0.0258),
                float4(-0.887, 0.462, 4.1, 0.0196),
                float4(0.342, -0.940, 3.1, 0.0148),
                float4(0.383, 0.924, 2.4, 0.0115),
                float4(-0.906, -0.423, 1.8, 0.0086),
                float4(0.954, -0.300, 1.4, 0.0067),
                float4(-0.500, 0.866, 1.05, 0.0050),
                float4(-0.216, -0.976, 0.8, 0.0038)
            };

            static const float GRAVITY = 9.81; // TWO_PI comes from the URP core macros

            // Height in x, slope along world x in y and along world z in z, for one wave table.
            float3 WaveSum(float4 waves[3], int count, float2 p, float t)
            {
                float3 result = 0;
                for (int i = 0; i < count; i++)
                {
                    float k = TWO_PI / waves[i].z;
                    float phase = k * dot(waves[i].xy, p) - sqrt(GRAVITY * k) * t;
                    float s = sin(phase);
                    float c = cos(phase);
                    result.x += waves[i].w * s;
                    result.yz += waves[i].w * k * c * waves[i].xy;
                }
                return result;
            }

            // A ripple is only drawn while a pixel is much smaller than its wavelength; further away it would alias into a
            // moire pattern, so each ripple fades out at 25 wavelengths from the camera (the 1.3 m one at 32 m).
            float3 RippleSum(float2 p, float t, float distanceToCamera)
            {
                float3 result = 0;
                for (int i = 0; i < RIPPLE_COUNT; i++)
                {
                    float fade = saturate(1.0 - distanceToCamera / (RIPPLES[i].z * 25.0));
                    float k = TWO_PI / RIPPLES[i].z;
                    float phase = k * dot(RIPPLES[i].xy, p) - sqrt(GRAVITY * k) * t;
                    result.x += fade * RIPPLES[i].w * sin(phase);
                    result.yz += fade * RIPPLES[i].w * k * cos(phase) * RIPPLES[i].xy;
                }
                return result;
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half fogFactor : TEXCOORD1;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 swell = WaveSum(SWELLS, SWELL_COUNT, positionWS.xz, _Time.y);
                positionWS.y += swell.x * _WaveHeight;
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float3 toCamera = GetCameraPositionWS() - input.positionWS;
                float distanceToCamera = length(toCamera);
                float3 viewDir = toCamera / max(distanceToCamera, 0.0001);

                // Normal from the analytic slope of every wave.
                float3 swell = WaveSum(SWELLS, SWELL_COUNT, input.positionWS.xz, t);
                float3 ripple = RippleSum(input.positionWS.xz, t, distanceToCamera);
                float2 slope = swell.yz * _WaveHeight + ripple.yz * _RippleStrength;
                float3 normalWS = normalize(float3(-slope.x, 1.0, -slope.y));

                // Vertical water depth under this pixel. The depth texture gives the eye depth of what is behind the
                // surface; scaling the camera-to-surface ray by (scene depth / surface depth) lands on that point, and
                // the difference in height is the depth. (Eye depth along the ray would grow at grazing angles and
                // smear the foam into a wide band.)
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneEyeDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float3 cameraPosition = GetCameraPositionWS();
                float sceneHeight = cameraPosition.y + (input.positionWS.y - cameraPosition.y) * (sceneEyeDepth / input.positionCS.w);
                float depthDifference = max(input.positionWS.y - sceneHeight, 0.0);

                half3 waterColor = lerp(_ShallowColor.rgb, _DeepColor.rgb, sqrt(saturate(depthDifference / _DepthRange)));

                Light mainLight = GetMainLight();
                half3 ambient = SampleSH(normalWS);
                half3 lit = waterColor * (ambient + mainLight.color * saturate(dot(normalWS, mainLight.direction)) * 0.5);

                // Sky reflection, stronger at grazing angles (Schlick Fresnel, water F0 = 0.02).
                float nDotV = saturate(dot(normalWS, viewDir));
                float fresnel = 0.02 + 0.98 * pow(1.0 - nDotV, 5.0);
                // The sky colour comes from the scene's own ambient (horizon to zenith), not from a reflection probe: the
                // nearest probe is baked from a brighter place and made the sea look milky against the dark sky overhead.
                float3 reflected = reflect(-viewDir, normalWS);
                half3 sky = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(reflected.y * 2.0));
                lit += sky * fresnel * _SkyReflection;

                // Moon glint: Blinn-Phong with a power that follows the smoothness.
                float specPower = exp2(10.0 * _Smoothness + 1.0);
                float3 halfDir = normalize(mainLight.direction + viewDir);
                lit += mainLight.color * pow(saturate(dot(normalWS, halfDir)), specPower) * _GlintStrength;

                // Nearby lights (boat lantern, torch): a soft diffuse pool plus the same glint.
                #if defined(_ADDITIONAL_LIGHTS)
                uint lightCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < lightCount; i++)
                {
                    Light light = GetAdditionalLight(i, input.positionWS);
                    half3 lightColor = light.color * light.distanceAttenuation;
                    float3 lightHalf = normalize(light.direction + viewDir);
                    lit += waterColor * lightColor * saturate(dot(normalWS, light.direction));
                    lit += lightColor * pow(saturate(dot(normalWS, lightHalf)), specPower) * _GlintStrength;
                }
                #endif

                // Shore foam: a band where the water is shallower than the foam width. The width pulses slowly like
                // a swash, and the ripples break the band up.
                // Two out-of-step sines along the shore make the swash run further up the sand in some places than others.
                float swashWave = (sin(t * 0.7 + input.positionWS.x * 0.09) + 0.6 * sin(t * 1.3 - input.positionWS.x * 0.21 + 1.7)) / 1.6;
                float swash = 0.7 + 0.3 * swashWave;
                float reach = _FoamWidth * swash;
                float breakup = saturate(0.5 + 9.0 * ripple.x); // lace: the chop punches holes in the foam
                float front = pow(saturate(1.0 - depthDifference / reach), 1.4) * breakup;
                // A thinner, fainter trail of foam left behind where the water has just been.
                float trail = saturate(1.0 - abs(depthDifference / (reach * 1.9) - 0.75) * 4.0) * 0.35 * breakup;
                // Only the highest crests (all three swells aligned, at most 0.14 m), and only as speckles.
                float crest = smoothstep(0.8, 1.0, (swell.x * _WaveHeight) / 0.14) * smoothstep(0.5, 0.9, breakup) * _CrestFoam;
                float foam = saturate(front + trail + crest);
                half3 foamLit = _FoamColor.rgb * 0.85 * saturate(ambient * 2.0 + mainLight.color);
                lit = lerp(lit, foamLit, foam);

                float edge = saturate(depthDifference / max(_EdgeFade, 0.001));
                float alpha = saturate(max(edge * _MaxAlpha + fresnel * 0.5, foam * edge));

                return half4(MixFog(lit, input.fogFactor), alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
