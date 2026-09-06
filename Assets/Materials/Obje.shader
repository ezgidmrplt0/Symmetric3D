Shader "Custom/LiquidFullControl"
{
    Properties
    {
        _LiquidColor ("Liquid Color (Fallback)", Color) = (1,0,0,1)
        _Color0 ("Color Slice 0 (Bottom)", Color) = (1,0.86,0.09,1)
        _Color1 ("Color Slice 1", Color) = (1,0.18,0.22,1)
        _Color2 ("Color Slice 2", Color) = (0.22,1.0,0.22,1)
        _Color3 ("Color Slice 3 (Top)", Color) = (0.18,0.52,0.95,1)

        // 4 Dilimli Şişe Y Yüksekliği Sınırları (0.06 ile 0.50 arası, geniş gövde içinde kalır)
        _Split0 ("Split Y 0-1", Float) = 0.17
        _Split1 ("Split Y 1-2", Float) = 0.28
        _Split2 ("Split Y 2-3", Float) = 0.39
        _SliceCount ("Slice Count", Float) = 1

        _FillAmount ("Fill Amount", Range(-0.1,1.1)) = 0.0
        _Mode ("Mode (0=Y,1=X)", Range(0,1)) = 0

        _TiltX ("Tilt X", Range(-5,5)) = 0
        _TiltZ ("Tilt Z", Range(-5,5)) = 0
        _WobbleStrength ("Wobble Strength", Range(0,0.1)) = 0.025
        _WobbleSpeed ("Wobble Speed", Range(0,10)) = 3.5

        _IsFrozen ("Is Frozen", Float) = 0

        // Hypercasual Parlaklık & Sihirli İksir Işığı Ayarları
        _RimPower ("Rim Power", Range(0.1,8.0)) = 1.6
        _RimIntensity ("Rim Intensity", Range(0, 5.0)) = 2.4
        _HighlightIntensity ("Highlight", Range(0, 3.0)) = 1.6
        _ColorBoost ("Color Boost", Range(1.0, 3.0)) = 1.8
        _InnerGlowStrength ("Inner Glow (Potion Radiance)", Range(0.0, 3.0)) = 1.4

        // 3D Jel/Sıvı Yüzeyi Dalga & Parlaklık Ayarları (Referans Görsel Uyumlu)
        _WaveScale ("3D Wave Scale", Range(5.0, 40.0)) = 18.0
        _WaveHeight ("3D Wave Height", Range(0.0, 0.06)) = 0.025
        _TopGlossiness ("Top Surface Glossiness", Range(4.0, 128.0)) = 32.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade fullforwardshadows

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        fixed4 _LiquidColor;
        fixed4 _Color0;
        fixed4 _Color1;
        fixed4 _Color2;
        fixed4 _Color3;
        float _Split0;
        float _Split1;
        float _Split2;
        float _SliceCount;
        float _IsFrozen;

        float _FillAmount;
        float _Mode;
        float _TiltX;
        float _TiltZ;
        float _WobbleStrength;
        float _WobbleSpeed;
        float _RimPower;
        float _RimIntensity;
        float _HighlightIntensity;
        float _ColorBoost;
        float _InnerGlowStrength;
        float _WaveScale;
        float _WaveHeight;
        float _TopGlossiness;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 objPos = mul(unity_WorldToObject, float4(IN.worldPos, 1)).xyz;

            float distFromCenter = length(objPos.xz);
            float tilt = objPos.x * _TiltX + objPos.z * _TiltZ;
            float freezeFactor = saturate(_IsFrozen);

            // 1. 3D Organik Sıvı/Jel Yüzey Dalgalanması (3D Water Ripple Wave Topology)
            float wave1 = sin(_Time.y * _WobbleSpeed + objPos.x * _WaveScale) * cos(_Time.y * (_WobbleSpeed * 0.85) + objPos.z * _WaveScale);
            float wave2 = cos(_Time.y * (_WobbleSpeed * 1.4) + objPos.x * (_WaveScale * 1.3)) * 0.4;
            float surfaceWave = (1.0 - freezeFactor) * (wave1 + wave2) * _WaveHeight;

            // 2. Cam Kenarı Menisküs Eğrisi (Surface Tension Meniscus Curve)
            float meniscusCurve = (1.0 - freezeFactor) * pow(saturate(distFromCenter / 0.45), 2.2) * 0.03;
            float baseAxis = lerp(objPos.y, objPos.x, _Mode);
            float axis = baseAxis + tilt + surfaceWave - meniscusCurve;

            if (axis < _FillAmount)
            {
                // 4 katmandan hangisi olduğunu belirle (0.06 ile 0.50 geniş gövde arasında)
                fixed4 currentSliceColor = _Color0;
                float sliceBottomY = 0.06;
                float sliceTopY = _Split0;

                if (objPos.y >= _Split2 && _SliceCount >= 4.0)
                {
                    currentSliceColor = _Color3;
                    sliceBottomY = _Split2;
                    sliceTopY = 0.50;
                }
                else if (objPos.y >= _Split1 && _SliceCount >= 3.0)
                {
                    currentSliceColor = _Color2;
                    sliceBottomY = _Split1;
                    sliceTopY = _Split2;
                }
                else if (objPos.y >= _Split0 && _SliceCount >= 2.0)
                {
                    currentSliceColor = _Color1;
                    sliceBottomY = _Split0;
                    sliceTopY = _Split1;
                }

                fixed3 vibrantColor = currentSliceColor.rgb * _ColorBoost;

                if (freezeFactor > 0.001)
                {
                    fixed3 frozenTint = lerp(vibrantColor * 0.65, fixed3(0.75, 0.93, 1.0), 0.50);
                    vibrantColor = lerp(vibrantColor, frozenTint, freezeFactor);
                }

                // Katman içi yumuşak dikey renk gradyanı
                float sliceNormalizedY = saturate((objPos.y - sliceBottomY) / max(0.01, sliceTopY - sliceBottomY));
                float curveY = pow(sliceNormalizedY, 1.1);

                fixed3 bottomColor = vibrantColor * 0.70;
                fixed3 topColor = vibrantColor * 1.15;
                fixed3 gradientColor = lerp(bottomColor, topColor, curveY);

                // Katmanlar arasındaki sınır çizgisi
                if (_SliceCount > 1.0)
                {
                    float d0 = (_SliceCount >= 2.0) ? abs(objPos.y - _Split0) : 10.0;
                    float d1 = (_SliceCount >= 3.0) ? abs(objPos.y - _Split1) : 10.0;
                    float d2 = (_SliceCount >= 4.0) ? abs(objPos.y - _Split2) : 10.0;
                    float minBoundaryDist = min(d0, min(d1, d2));
                    float boundaryLine = 1.0 - smoothstep(0.002, 0.015, minBoundaryDist);
                    gradientColor += fixed3(1,1,1) * boundaryLine * 0.25;
                }

                // 3. Sıvının En Tepe 3D Dalga Yüzeyindeki Parlak Jel Işıltısı (Glossy Top Wave Specular)
                float topDist = axis - (_FillAmount - 0.05);
                if (topDist > 0)
                {
                    float topGlow = pow(saturate(topDist / 0.05), 1.8) * _HighlightIntensity;

                    // 3D Dalga tepe noktalarındaki parlak yansıma dalgaları
                    float waveGleam = saturate(wave1 + wave2);
                    float topSpecular = pow(waveGleam, 2.5) * 0.65;

                    gradientColor += fixed3(1,1,1) * (topGlow * 0.35 + topSpecular);

                    if (freezeFactor > 0.05)
                    {
                        float iceCrust = saturate(topDist / 0.05) * freezeFactor;
                        gradientColor = lerp(gradientColor, fixed3(0.85, 0.97, 1.0), iceCrust * 0.60);
                    }
                }

                o.Albedo = saturate(gradientColor);
                o.Alpha = 1;

                // RIM GLOW & FRESNEL: Kenar cam parlaklığı
                float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
                float rimStrength = pow(rim, _RimPower);

                fixed3 rimColor = lerp(vibrantColor, fixed3(1,1,1), 0.35) * rimStrength * _RimIntensity;
                if (freezeFactor > 0.05)
                {
                    rimColor = lerp(rimColor, fixed3(0.85, 0.98, 1.0) * rimStrength * 2.2, freezeFactor * 0.65);
                }

                o.Emission = rimColor + (vibrantColor * _InnerGlowStrength);
            }
            else
            {
                o.Alpha = 0;
                o.Albedo = float3(0,0,0);
                o.Emission = float3(0,0,0);
            }

            o.Smoothness = 0.95;
            o.Metallic = 0.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
