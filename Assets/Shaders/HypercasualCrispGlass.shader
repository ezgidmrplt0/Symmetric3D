Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        _Color ("Glass Tint", Color) = (0.9, 0.95, 1.0, 0.02)
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 0.85)
        _SpecSize ("Specular Size", Range(0.005, 0.1)) = 0.03
        _SpecSmoothness ("Specular Sharpness", Range(0.001, 0.05)) = 0.01
        _RimColor ("Rim Color", Color) = (0.9, 0.95, 1.0, 0.4)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 3.0
        _EdgeDarkness ("Edge Outline Darkness", Range(0.0, 1.0)) = 0.35
        _EdgeOutlineColor ("Edge Outline Color", Color) = (0.35, 0.45, 0.6, 1.0)
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

                // 1. Base transparent glass tint
                float baseA = _Color.a;
                float3 baseC = _Color.rgb * baseA;

                // 2. Crisp cartoon specular highlight dot
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(normal, halfVector));
                float specThreshold = 1.0 - _SpecSize;
                float spec = smoothstep(specThreshold - _SpecSmoothness, specThreshold + _SpecSmoothness, NdotH);
                float3 specC = _SpecColor.rgb * spec * _SpecColor.a;

                // 3. Fresnel contour defining spherical glass boundary
                float fresnel = 1.0 - saturate(dot(viewDir, normal));
                float edgeFactor = pow(fresnel, 3.5) * _EdgeDarkness;
                float3 edgeC = _EdgeOutlineColor.rgb * edgeFactor;

                // 4. Subtle Fresnel rim glow
                float rim = pow(fresnel, _RimPower);
                float3 rimC = _RimColor.rgb * rim * _RimColor.a;

                // Composition
                float3 finalColor = baseC + rimC + specC + edgeC;
                float finalAlpha = saturate(baseA + rim * _RimColor.a + spec * _SpecColor.a + edgeFactor);

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
    FallBack Off
}
