Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (1.0, 1.0, 1.0, 0.0)
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _SpecSize ("Specular Size", Range(0.0, 0.1)) = 0.035
        _SpecSmoothness ("Specular Sharpness", Range(0.001, 0.05)) = 0.005
        _RimColor ("Rim Color", Color) = (1.0, 1.0, 1.0, 0.85)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.0
        _EdgeDarkness ("Edge Outline Darkness", Range(0.0, 1.0)) = 0.35
        _EdgeOutlineColor ("Edge Outline Color", Color) = (0.18, 0.24, 0.36, 1.0)
    }
    SubShader
    {
        Tags {"Queue"="Transparent+1" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 100

        ZWrite Off
        Blend One OneMinusSrcAlpha // Premultiplied Alpha
        Cull Back

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
            };

            fixed4 _Color;
            fixed4 _SpecColor;
            float _SpecSize;
            float _SpecSmoothness;
            fixed4 _RimColor;
            float _RimPower;
            float _EdgeDarkness;
            fixed4 _EdgeOutlineColor;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = UnityWorldSpaceViewDir(o.worldPos);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(float3(-0.4, 0.75, -0.5));

                // 1. Tamamen Berrak Cam Gövdesi (Sıfır sis, sıfır solukluk, sıfır leke)
                float baseA = _Color.a;
                float3 baseC = _Color.rgb * baseA;

                // 2. Tek ve Jilet Gibi Keskin Karikatür Parlama Noktası (Ana Işık)
                // İkincil soluk noktalar tamamen kaldırıldı, sadece tek net parlama noktası vardır.
                float spec = 0.0;
                float3 specC = float3(0, 0, 0);
                if (_SpecSize > 0.001 && _SpecColor.a > 0.001)
                {
                    float3 halfVector = normalize(lightDir + viewDir);
                    float NdotH = max(0.0, dot(normal, halfVector));
                    float specThreshold = 1.0 - _SpecSize;
                    spec = smoothstep(specThreshold - _SpecSmoothness, specThreshold + _SpecSmoothness, NdotH);
                    specC = _SpecColor.rgb * spec * _SpecColor.a;
                }

                // 3. Jilet İnceliğinde Dış Kenar (Sadece en dış %8 sınırda devreye girer, ortaya ASLA taşmaz)
                float NdotV = saturate(dot(viewDir, normal));
                float fresnel = 1.0 - NdotV;

                // Dış kenar halkası (Yalnızca en dış sınır çizgisi)
                float rimBand = smoothstep(0.90, 0.99, fresnel);
                float rim = pow(rimBand, _RimPower);
                float3 rimC = _RimColor.rgb * rim * _RimColor.a;

                // 4. Net İnce Silüet Sınırı (Cam küreyi dıştan ayıran jilet gibi ince sınır çizgisi)
                float edgeBand = smoothstep(0.92, 0.995, fresnel);
                float edgeFactor = edgeBand * _EdgeDarkness;
                float3 edgeC = _EdgeOutlineColor.rgb * edgeFactor;

                // Premultiplied Alpha Kompozisyonu
                float3 finalColor = baseC + rimC + specC + edgeC;
                float finalAlpha = saturate(baseA + rim * _RimColor.a + spec * _SpecColor.a + edgeFactor);

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
