Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        _Color ("Base Tint", Color) = (0.75, 0.88, 1.0, 0.18)
        _RimColor ("Rim Color", Color) = (0.85, 0.95, 1.0, 0.65)
        _RimPower ("Rim Power", Range(0.5, 8.0)) = 2.3
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 0.85)
        _Shininess ("Shininess", Range(0.01, 1)) = 0.70
        _LightDirX ("Light X", Range(-1, 1)) = -0.3
        _LightDirY ("Light Y", Range(0, 1)) = 1.0
        _LightDirZ ("Light Z", Range(-1, 1)) = -0.2
    }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 100

        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
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
                float3 viewDir : TEXCOORD0;
            };

            fixed4 _Color;
            fixed4 _RimColor;
            float _RimPower;
            fixed4 _SpecColor;
            float _Shininess;
            float _LightDirX;
            float _LightDirY;
            float _LightDirZ;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(float3(_LightDirX, _LightDirY, _LightDirZ));
                
                // 1. Realistic glass body tint
                float baseA = _Color.a;
                float3 baseC = _Color.rgb * baseA;
                
                // 2. Smooth 3D Fresnel Glass Contour
                float NdotV = saturate(dot(normal, viewDir));
                float rim = pow(1.0 - NdotV, _RimPower);
                float rimAlpha = rim * _RimColor.a;
                float3 rimC = _RimColor.rgb * rimAlpha;
                
                // 3. Crisp Glossy Specular Highlight
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0, dot(normal, halfVector));
                float spec = pow(NdotH, lerp(32.0, 256.0, _Shininess));
                float specAlpha = spec * _SpecColor.a;
                float3 specC = _SpecColor.rgb * specAlpha;
                
                // Balanced additive + alpha blend
                float3 finalColor = baseC + rimC + specC;
                float finalAlpha = saturate(baseA + rimAlpha * 0.7 + specAlpha * 0.8);
                
                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
