Shader "Custom/LiquidFullControl"
{
    Properties
    {
        _LiquidColor ("Liquid Color", Color) = (1,0,0,1)
        _FillAmount ("Fill Amount", Range(-0.5,0.5)) = 0
        _Mode ("Mode (0=Y,1=X)", Range(0,1)) = 0

        _TiltX ("Tilt X", Range(-1,1)) = 0
        _TiltZ ("Tilt Z", Range(-1,1)) = 0
        _WobbleStrength ("Wobble Strength", Range(0,0.1)) = 0.02
        _WobbleSpeed ("Wobble Speed", Range(0,10)) = 3

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
            float wobble = sin(_Time.y * _WobbleSpeed) * _WobbleStrength;
            float baseAxis = lerp(objPos.y, objPos.x, _Mode);
            float axis = baseAxis + tilt + wobble;

            if (axis < _FillAmount)
            {
                // Canlılık artırmak için baz rengi patlatıyoruz
                fixed3 vibrantColor = _LiquidColor.rgb * _ColorBoost;

                // Daha kavisli ve belirgin bir Gradient (Aşağısı Tok, Yukarısı Parlak)
                float normalizedY = saturate(objPos.y + 0.5);
                float curveY = pow(normalizedY, 1.2); // Doğrusal değil, biraz daha şişkin bir gradyan
                
                fixed3 bottomColor = vibrantColor * 0.4;
                fixed3 topColor = vibrantColor * 1.3; 
                
                fixed3 gradientColor = lerp(bottomColor, topColor, curveY);
                
                // Tepe noktasına sıvı efekti (sahte tepeden ışık hilesi)
                float topGlow = pow(saturate(objPos.y * 2.0), 3.0) * _HighlightIntensity;
                gradientColor += fixed3(1,1,1) * topGlow * 0.4; // Tepeye hafif beyazımsı ekstra parlaklık
                
                o.Albedo = saturate(gradientColor);
                o.Alpha = 1;

                // RIM GLOW: Köşeler çok daha tatmin edici ve parlak renk saçsın
                float rim = 1.0 - saturate(dot(normalize(IN.viewDir), o.Normal));
                float rimStrength = pow(rim, _RimPower);
                
                // Kenar ışığına kendi renginin yanı sıra biraz da beyaz ekleyip patlatıyoruz
                fixed3 rimColor = lerp(vibrantColor, fixed3(1,1,1), 0.3) * rimStrength * _RimIntensity;
                
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
