Shader "Custom/LiquidStream"
{
    Properties
    {
        _Color ("Liquid Color", Color) = (0.9, 0.2, 0.2, 0.95)
        _InnerGlow ("Glow Multiplier", Float) = 1.0
        _FlowSpeed ("Flow Speed", Float) = 3.5
        _SpecularStrength ("Specular Highlight", Float) = 0.4
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+15" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _InnerGlow;
            float _FlowSpeed;
            float _SpecularStrength;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Kesit koordinatı: [-1, 1] arası merkezlenmiş
                float u = i.uv.x * 2.0 - 1.0;
                float distFromCenter = abs(u);

                // Silindirik sıvı tüpü hacmi (3D Volume Arc)
                float cylinderCore = sqrt(max(0.001, 1.0 - distFromCenter * distFromCenter));

                // Aşağı doğru akan animasyonlu akış dalgaları ve mikro damlacık titreşimi
                float flowTime = _Time.y * _FlowSpeed;
                float flowWave1 = sin(i.uv.y * 24.0 - flowTime * 14.0) * 0.08 + 0.92;
                float flowWave2 = cos(i.uv.y * 50.0 - flowTime * 22.0) * 0.04 + 0.96;
                float flowPattern = flowWave1 * flowWave2;

                // 3D Parlak Islak Yansıma Çizgileri (Wet Specular Highlight Streaks)
                // Sıvının bombeli silindirik yüzeyinde parlayan çift yansıma
                float specPrimary = pow(saturate(1.0 - abs(u - 0.28) * 3.2), 14.0) * _SpecularStrength;
                float specSecondary = pow(saturate(1.0 - abs(u + 0.38) * 4.2), 8.0) * (_SpecularStrength * 0.45);
                float totalSpec = specPrimary + specSecondary;

                // Sıvının kenar şeffaflığı ve yoğun iç çekirdek parlaması
                float coreGlow = pow(cylinderCore, 0.45) * _InnerGlow;
                fixed4 baseColor = _Color * i.color;
                float gray = dot(baseColor.rgb, fixed3(0.299, 0.587, 0.114));
                baseColor.rgb = saturate(lerp(fixed3(gray, gray, gray), baseColor.rgb, 1.25));

                fixed3 finalRGB = baseColor.rgb * (coreGlow * flowPattern) + fixed3(1, 1, 1) * totalSpec;

                // Kenarlarda yumuşak kavisli alpha geçişi
                float alpha = pow(cylinderCore, 0.38) * baseColor.a;

                // Akıntı başlangıcı ve bitişinde yumuşak geçiş
                float tipFade = smoothstep(0.0, 0.06, i.uv.y) * smoothstep(1.0, 0.94, i.uv.y);
                alpha *= tipFade;

                return fixed4(saturate(finalRGB), saturate(alpha));
            }
            ENDCG
        }
    }
}
