Shader "Hidden/GlitchRGBSplitURP" {
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        Pass {
            Name "GlitchRGBSplit"
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex VertGlitch
            #pragma fragment FragGlitch

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _GlitchStrength;
            float _GlitchRGBSplit;
            float _GlitchJitter;
            float _GlitchScanline;
            float _GlitchTimeScale;
            float _GlitchUnscaledTime;

            float _GlitchSmear;
            float _GlitchSmearRadius;

            float _GlitchEdgeStart;
            float _GlitchEdgePower;
            float _GlitchEdgeSmearBoost;
            float _GlitchEdgeRadiusBoost;

            float _GlitchEdgeSensitivity;
            float _GlitchEdgeThreshold;
            float _GlitchEdgeSoftness;
            float _GlitchGlow;
            float _GlitchBleed;

            float Hash21(float2 p) {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float Luma(half3 c) {
                return dot(c, half3(0.299, 0.587, 0.114));
            }

            struct GlitchAttributes {
                uint vertexID : SV_VertexID;
            };

            struct GlitchVaryings {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            GlitchVaryings VertGlitch(GlitchAttributes IN) {
                GlitchVaryings OUT;
                OUT.positionCS = GetFullScreenTriangleVertexPosition(IN.vertexID);
                OUT.uv = GetFullScreenTriangleTexCoord(IN.vertexID);
                return OUT;
            }

            half4 SampleSrc(float2 uv) {
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
            }

            float Edge01(float2 uv) {
                float2 d = uv - 0.5;
                float dist01 = saturate(length(d) * 1.41421356);
                float start = saturate(_GlitchEdgeStart);
                float x = saturate((dist01 - start) / max(1.0 - start, 0.0001));
                return pow(x, max(_GlitchEdgePower, 0.001));
            }

            float SobelEdge(float2 uv, out float2 gradDir) {
                float2 texel = 1.0 / _ScreenParams.xy;

                half tl = Luma(SampleSrc(uv + texel * float2(-1,  1)).rgb);
                half  t = Luma(SampleSrc(uv + texel * float2( 0,  1)).rgb);
                half tr = Luma(SampleSrc(uv + texel * float2( 1,  1)).rgb);

                half  l = Luma(SampleSrc(uv + texel * float2(-1,  0)).rgb);
                half  r = Luma(SampleSrc(uv + texel * float2( 1,  0)).rgb);

                half bl = Luma(SampleSrc(uv + texel * float2(-1, -1)).rgb);
                half  b = Luma(SampleSrc(uv + texel * float2( 0, -1)).rgb);
                half br = Luma(SampleSrc(uv + texel * float2( 1, -1)).rgb);

                float gx = (tr + 2.0 * r + br) - (tl + 2.0 * l + bl);
                float gy = (bl + 2.0 * b + br) - (tl + 2.0 * t + tr);

                gradDir = float2(gx, gy);
                float edge = sqrt(gx * gx + gy * gy);

                edge *= max(_GlitchEdgeSensitivity, 0.0);
                float th = saturate(_GlitchEdgeThreshold);
                float soft = max(_GlitchEdgeSoftness, 0.0001);

                return smoothstep(th, th + soft, edge);
            }

            half3 SmearColor(float2 uv, float seed, float radius01, float2 edgeDir, float screenEdge01) {
                const float TAU = 6.2831853;

                float2 outDir = uv - 0.5;
                float outLen = max(length(outDir), 0.0001);
                outDir /= outLen;

                float2 dir = edgeDir;
                float dirLen = max(length(dir), 0.0001);
                dir /= dirLen;

                float2 mainDir = normalize(lerp(dir, outDir, screenEdge01));

                float radius = radius01 * 0.07;

                half3 acc = 0;
                half count = 0;

                [unroll]
                for (int i = 0; i < 16; i++) {
                    float a = (i + seed) * (TAU / 16.0);
                    float2 rnd = float2(cos(a), sin(a));

                    float2 finalDir = normalize(lerp(rnd, mainDir, 0.65));

                    float k = 0.25 + frac(seed * 3.1 + i * 0.17) * 1.35;
                    acc += SampleSrc(uv + finalDir * radius * k).rgb;
                    count += 1;
                }

                return acc / max(count, 1);
            }

            half4 FragGlitch(GlitchVaryings IN) : SV_Target {
                float2 uv = IN.uv;

                float timeScaled = _GlitchUnscaledTime * max(_GlitchTimeScale, 0.001);
                float strength = saturate(_GlitchStrength);

                if (strength <= 0.0001)
                    return SampleSrc(uv);

                float screenEdge = Edge01(uv);

                float2 gradDir;
                float edgeMask = SobelEdge(uv, gradDir);
                float edgeDriven = saturate(edgeMask * (0.35 + screenEdge * 1.25));

                float lineIndex = floor(uv.y * 240.0);
                float n = Hash21(float2(lineIndex, floor(timeScaled * 60.0)));

                float jitterMask = step(1.0 - _GlitchJitter * strength, n);
                float xJitter = (n - 0.5) * 0.08 * jitterMask * strength;
                uv.x += xJitter;

                float scan = sin((uv.y * 900.0) + timeScaled * 20.0) * 0.5 + 0.5;
                float scanMask = (scan * _GlitchScanline) * strength;

                float rgbAmp = (1.0 + edgeDriven * 2.0 + screenEdge * 1.5);
                float2 rgbOff = float2((n - 0.5) * 0.01, 0.0) * (_GlitchRGBSplit * strength) * rgbAmp;

                half r = SampleSrc(uv + rgbOff).r;
                half g = SampleSrc(uv).g;
                half b = SampleSrc(uv - rgbOff).b;
                half3 col = half3(r, g, b);

                float noise = (Hash21(uv * 200.0 + timeScaled) - 0.5) * 0.12 * strength;
                col += noise;

                float smearBase = saturate(_GlitchSmear) * strength;

                float smear = saturate(smearBase
                    * (1.0 + screenEdge * max(_GlitchEdgeSmearBoost, 0.0))
                    * (0.25 + edgeDriven * 2.0));

                float radius01 = saturate(_GlitchSmearRadius
                    * (1.0 + screenEdge * max(_GlitchEdgeRadiusBoost, 0.0))
                    * (0.35 + edgeDriven * 1.65));

                if (smear > 0.0001) {
                    half3 smeared = SmearColor(uv, n + Hash21(IN.uv * 11.3), radius01, gradDir, screenEdge);

                    half3 bleed = saturate(smeared - col) * 2.2;

                    float bleedMix = saturate(_GlitchBleed) * edgeDriven;
                    col = lerp(col, smeared, smear);
                    col += bleed * bleedMix;

                    float glow = max(_GlitchGlow, 0.0) * edgeDriven;
                    col += smeared * glow * 0.35;
                }

                col *= (1.0 - scanMask * 0.15);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}