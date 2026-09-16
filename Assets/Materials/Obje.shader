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
        _ColorBoost ("Color Boost", Range(1.0, 2.5)) = 1.25
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

        // Toony Colors Cel-Shaded Lighting Modeli (Temiz ve net cel-shading)
        half4 LightingToonLiquid (SurfaceOutput s, half3 lightDir, half3 viewDir, half atten)
        {
            half NdotL = dot(s.Normal, lightDir);
            half halfLambert = NdotL * 0.5 + 0.5;

            // Kademeli Toon Cel Ramp (Asıl rengi beyazlatmaz, gölge ve ışık ayrımı verir)
            half toonRamp = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, halfLambert);

            fixed3 shadowColor = s.Albedo * 0.72;
            fixed3 litColor = s.Albedo * 1.05;
            fixed3 rampResult = lerp(shadowColor, litColor, toonRamp);

            half4 c;
            c.rgb = rampResult * _LightColor0.rgb * atten;
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
                fixed3 baseColor = _LiquidColor.rgb;

                // Hafif ve yumuşak derinlik (alttan üste sıvı derinliği, asla beyazlatmaz)
                float normalizedY = saturate(objPos.y + 0.5);
                fixed3 bottomColor = baseColor * 0.82;
                fixed3 topColor = baseColor * 1.05;
                fixed3 gradientColor = lerp(bottomColor, topColor, normalizedY);

                // İnce, zarif su seviyesi çizgisi (sadece suyun bittiği sınırda 0.015 birimlik minik vurgu)
                float distToSurface = _FillAmount - axis;
                if (distToSurface >= 0.0 && distToSurface < 0.018)
                {
                    gradientColor = lerp(gradientColor, baseColor * 1.3, 0.4);
                }

                o.Albedo = saturate(gradientColor);
                o.Alpha = 1.0;

                // Beyaz patlamayı önlemek için Emission tamamen kapatıldı
                o.Emission = float3(0, 0, 0);
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
