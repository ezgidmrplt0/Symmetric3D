Shader "Custom/LiquidFullControl"
{
    Properties
    {
        _LiquidColor ("Liquid Color (Fallback)", Color) = (1,0,0,1)
        _Color0 ("Color Slice 0 (Bottom)", Color) = (1,0,0,1)
        _Color1 ("Color Slice 1", Color) = (0,1,0,1)
        _Color2 ("Color Slice 2", Color) = (0,0,1,1)
        _Color3 ("Color Slice 3 (Top)", Color) = (1,1,0,1)

        _Split0 ("Split Y 0-1", Float) = -0.18
        _Split1 ("Split Y 1-2", Float) = 0.02
        _Split2 ("Split Y 2-3", Float) = 0.22
        _SliceCount ("Slice Count", Float) = 1

        _FillAmount ("Fill Amount", Range(-0.6,0.6)) = 0
        _Mode ("Mode (0=Y,1=X)", Range(0,1)) = 0

        _TiltX ("Tilt X", Range(-1,1)) = 0
        _TiltZ ("Tilt Z", Range(-1,1)) = 0
        _WobbleStrength ("Wobble Strength", Range(0,0.1)) = 0.02
        _WobbleSpeed ("Wobble Speed", Range(0,10)) = 3

        _IsFrozen ("Is Frozen", Float) = 0

        // Hypercasual Parlaklık Ayarları
        _RimPower ("Rim Power", Range(0.1,8.0)) = 1.5
        _RimIntensity ("Rim Intensity", Range(0, 5.0)) = 1.2
        _HighlightIntensity ("Highlight", Range(0, 3.0)) = 1.0
        _ColorBoost ("Color Boost", Range(1.0, 3.0)) = 1.5
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

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 objPos = mul(unity_WorldToObject, float4(IN.worldPos, 1)).xyz;

            float tilt = objPos.x * _TiltX + objPos.z * _TiltZ;
            float freezeFactor = saturate(_IsFrozen);
            float wobble = (1.0 - freezeFactor) * (sin(_Time.y * _WobbleSpeed) * _WobbleStrength);
            float baseAxis = lerp(objPos.y, objPos.x, _Mode);
            float axis = baseAxis + tilt + wobble;

            if (axis < _FillAmount)
            {
                // Hangi katman olduğunu belirle (objPos.y ekseninde segmentasyon)
                fixed4 currentSliceColor = _Color0;
                float sliceBottomY = -0.45;
                float sliceTopY = _Split0;

                if (objPos.y >= _Split2 && _SliceCount >= 4.0)
                {
                    currentSliceColor = _Color3;
                    sliceBottomY = _Split2;
                    sliceTopY = 0.44;
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

                // Canlılık artırmak için baz rengi patlatıyoruz
                fixed3 vibrantColor = currentSliceColor.rgb * _ColorBoost;

                // Donmuş sıvı: Renk buz kristali tonuyla pürüzsüz harmanlanır
                if (freezeFactor > 0.001)
                {
                    fixed3 frozenTint = lerp(vibrantColor * 0.65, fixed3(0.75, 0.93, 1.0), 0.50);
                    vibrantColor = lerp(vibrantColor, frozenTint, freezeFactor);
                }

                // Her katmanın kendi içinde tatmin edici iç derinlik gradyanı (altı tok, üstü parlak)
                float sliceNormalizedY = saturate((objPos.y - sliceBottomY) / max(0.01, sliceTopY - sliceBottomY));
                float curveY = pow(sliceNormalizedY, 1.2);
                
                fixed3 bottomColor = vibrantColor * 0.55;
                fixed3 topColor = vibrantColor * 1.25; 
                fixed3 gradientColor = lerp(bottomColor, topColor, curveY);

                // İç buz kristalleri ve prizmatik buz lifleri
                if (freezeFactor > 0.05)
                {
                    float iceFibers = sin(objPos.x * 35.0 + objPos.y * 25.0) * cos(objPos.y * 30.0 - objPos.z * 35.0);
                    float crystalGleam = saturate(pow(abs(iceFibers), 3.0)) * 0.35 * freezeFactor;
                    gradientColor += fixed3(0.75, 0.95, 1.0) * crystalGleam;
                }

                // Katmanlar arasındaki sınır çizgisi (farklı sıvılar arasındaki kristal menisküs / sınır parıltısı)
                if (_SliceCount > 1.0)
                {
                    float d0 = (_SliceCount >= 2.0) ? abs(objPos.y - _Split0) : 10.0;
                    float d1 = (_SliceCount >= 3.0) ? abs(objPos.y - _Split1) : 10.0;
                    float d2 = (_SliceCount >= 4.0) ? abs(objPos.y - _Split2) : 10.0;
                    float minBoundaryDist = min(d0, min(d1, d2));
                    float boundaryLine = 1.0 - smoothstep(0.002, 0.018, minBoundaryDist);
                    gradientColor += fixed3(1,1,1) * boundaryLine * 0.35;
                }
                
                // Sıvının en tepe serbest yüzeyine parıltı efekti (donmuşken kristalize buz kabuğu)
                float topDist = axis - (_FillAmount - 0.06);
                if (topDist > 0)
                {
                    float topGlow = pow(saturate(topDist / 0.06), 2.0) * _HighlightIntensity;
                    gradientColor += fixed3(1,1,1) * topGlow * 0.45;

                    if (freezeFactor > 0.05)
                    {
                        float iceCrust = saturate(topDist / 0.05) * freezeFactor;
                        gradientColor = lerp(gradientColor, fixed3(0.85, 0.97, 1.0), iceCrust * 0.60);
                    }
                }
                
                o.Albedo = saturate(gradientColor);
                o.Alpha = 1;

                // RIM GLOW: Köşeler çok daha tatmin edici ve parlak renk saçsın
                float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
                float rimStrength = pow(rim, _RimPower);
                
                // Kenar ışığına kendi renginin yanı sıra biraz da beyaz ekleyip patlatıyoruz
                fixed3 rimColor = lerp(vibrantColor, fixed3(1,1,1), 0.3) * rimStrength * _RimIntensity;
                if (freezeFactor > 0.05)
                {
                    rimColor = lerp(rimColor, fixed3(0.85, 0.98, 1.0) * rimStrength * 2.2, freezeFactor * 0.65);
                }
                
                // Gölgelerin içindeyken bile çok hafif kendi ışığını satsın (ön planda kalsın diye)
                o.Emission = rimColor + (vibrantColor * 0.15); 
            }
            else
            {
                o.Alpha = 0;
                o.Albedo = float3(0,0,0);
                o.Emission = float3(0,0,0);
            }

            // Pürüzsüz Camsı / Sulu yüzey
            o.Smoothness = 0.95;
            o.Metallic = 0.0;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
