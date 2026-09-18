Shader "Custom/LiquidFullControl"
{
    Properties
    {
        _LiquidColor ("Liquid Color", Color) = (1, 0.22, 0.32, 1)
        _FillAmount ("Fill Amount", Range(-0.5, 0.5)) = 0
        _Mode ("Mode (0=Y,1=X)", Range(0,1)) = 0

        _TiltX ("Tilt X", Range(-1, 1)) = 0
        _TiltZ ("Tilt Z", Range(-1, 1)) = 0
        _WobbleStrength ("Wobble Strength", Range(0, 0.1)) = 0.02
        _WobbleSpeed ("Wobble Speed", Range(0, 10)) = 3

        // Toony Colors Cel-Shading & Stil Ayarları
        _RampThreshold ("Toon Ramp Threshold", Range(0, 1)) = 0.5
        _RampSmooth ("Toon Ramp Smoothness", Range(0.001, 0.2)) = 0.05
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 2.0
        _RimIntensity ("Rim Intensity", Range(0, 5.0)) = 1.4
        _MeniscusWidth ("Meniscus Width", Range(0.005, 0.06)) = 0.025
        _MeniscusIntensity ("Meniscus Intensity", Range(0, 2.0)) = 1.0
        _HighlightIntensity ("Specular Highlight", Range(0, 3.0)) = 1.2
        _ColorBoost ("Color Boost", Range(1.0, 2.5)) = 1.35
        _VibranceNorm ("Vibrance Normalization", Range(0, 1)) = 0.88
        _InnerGlow ("Internal Candy Glow", Range(0, 1)) = 0.38
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf ToonLiquid alpha:fade fullforwardshadows

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        fixed4 _LiquidColor;
        float _FillAmount;
        float _Mode;
        float _TiltX;
        float _TiltZ;
        float _WobbleStrength;
        float _WobbleSpeed;
        float _RampThreshold;
        float _RampSmooth;
        float _RimPower;
        float _RimIntensity;
        float _MeniscusWidth;
        float _MeniscusIntensity;
        float _HighlightIntensity;
        float _ColorBoost;
        float _VibranceNorm;
        float _InnerGlow;

        // Toony Colors Cel-Shaded Lighting Modeli (Hypercasual canlı ve parlak cel-shading)
        half4 LightingToonLiquid (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            half halfLambert = NdotL * 0.5 + 0.5;

            // Kademeli Toon Cel Ramp (Renkleri soldurmaz, net ışık/gölge ayrımı verir)
            half toonRamp = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, halfLambert);

            // Gölgeler asla çamurlaşmaz ve canlılığını korur (0.92), ışıklı kısımlar parlak ve dolgun (1.15)
            fixed3 shadowColor = s.Albedo * 0.92;
            fixed3 litColor = s.Albedo * 1.15;
            fixed3 rampResult = lerp(shadowColor, litColor, toonRamp);

            // Işık dengesi: sarımsı yönlü ışığın mor, mavi ve yeşil gibi soğuk renklerin canlılığını öldürmesini engeller
            fixed3 balancedLight = lerp(fixed3(1.0, 1.0, 1.0), _LightColor0.rgb, 0.35);

            half4 c;
            c.rgb = rampResult * balancedLight * atten;
            c.a = s.Alpha;
            return c;
        }

        void surf (Input IN, inout SurfaceOutput o)
        {
            float3 objPos = mul(unity_WorldToObject, float4(IN.worldPos, 1)).xyz;

            float tilt = objPos.x * _TiltX + objPos.z * _TiltZ;
            float wobble = sin(_Time.y * _WobbleSpeed) * _WobbleStrength;
            float baseAxis = lerp(objPos.y, objPos.x, _Mode);
            float axis = baseAxis + tilt + wobble;

            if (axis < _FillAmount)
            {
                fixed3 rawColor = _LiquidColor.rgb;

                // 1. Akıllı Renk Canlılığı (Vibrance & Value Normalization):
                // Koyu/çamurlu kalan renkleri (0.5 mor, 0.6 koyu yeşil gibi) parlak şeker/sıvı renklerine yükseltir.
                // Siyah ve gri gibi akromatik renklerin asaletini korur.
                float maxC = max(rawColor.r, max(rawColor.g, rawColor.b));
                float minC = min(rawColor.r, min(rawColor.g, rawColor.b));
                float chroma = maxC - minC;

                fixed3 vibrantColor = rawColor;
                if (chroma > 0.06 && maxC > 0.05)
                {
                    // Parlaklık normalizasyonu (Koyu tonları parlak seviyeye çeker)
                    float boostRatio = 1.0 / maxC;
                    fixed3 normalizedColor = rawColor * boostRatio;
                    vibrantColor = lerp(rawColor, normalizedColor, _VibranceNorm);

                    // Saf renk doygunluğu artırımı
                    half luma = dot(vibrantColor, half3(0.299, 0.587, 0.114));
                    vibrantColor = lerp(half3(luma, luma, luma), vibrantColor, 1.25);
                }

                // Global canlılık çarpanı
                vibrantColor = saturate(vibrantColor * _ColorBoost);

                // 2. Yumuşak alttan üste derinlik geçişi (kararma yapmaz, zengin ton verir)
                float normalizedY = saturate(objPos.y + 0.5);
                fixed3 bottomColor = vibrantColor * 0.95;
                fixed3 topColor = vibrantColor * 1.05;
                fixed3 gradientColor = lerp(bottomColor, topColor, normalizedY);

                // 3. Sıvı üst menisküs / su yüzeyi çizgisi
                float distToSurface = _FillAmount - axis;
                if (distToSurface >= 0.0 && distToSurface < _MeniscusWidth)
                {
                    float mFactor = (1.0 - (distToSurface / _MeniscusWidth)) * _MeniscusIntensity;
                    gradientColor = lerp(gradientColor, saturate(vibrantColor * 1.35), mFactor * 0.45);
                }

                o.Albedo = saturate(gradientColor);
                o.Alpha = 1.0;

                // 4. Sıvı iç ışıması (Self-illumination / Candy glow) ve dış Fresnel parıltısı:
                // Sıvının merkezden dışa canlı ve parlak bir enerji yaymasını sağlar, sönük/plastik görüntüyü yok eder.
                half rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
                fixed3 innerRadiance = vibrantColor * (_InnerGlow * 0.35);
                fixed3 rimRadiance = vibrantColor * pow(rim, _RimPower) * (_RimIntensity * 0.28);
                o.Emission = innerRadiance + rimRadiance;
            }
            else
            {
                o.Alpha = 0;
                o.Albedo = float3(0, 0, 0);
                o.Emission = float3(0, 0, 0);
            }
        }
        ENDCG
    }
    FallBack "Diffuse"
}
