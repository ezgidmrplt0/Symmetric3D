Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        _Color ("Base Tint", Color) = (0.8, 0.9, 1.0, 0.05)
        _RimColor ("Rim Color", Color) = (1.0, 1.0, 1.0, 0.85)
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 1.3
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _Shininess ("Shininess", Range(0.01, 1)) = 0.55
        _LightDirX ("Light X", Range(-1, 1)) = -0.3
        _LightDirY ("Light Y", Range(0, 1)) = 1.0
        _LightDirZ ("Light Z", Range(-1, 1)) = -0.2
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent"}
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
                o.viewDir = WorldSpaceViewDir(v.vertex); // Towards camera
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(float3(_LightDirX, _LightDirY, _LightDirZ));
                
                // 1. Base color (Tint)
                float baseA = _Color.a;
                float3 baseC = _Color.rgb * baseA;
                
                // 2. Rim Lighting (Fresnel edge glow)
                float rim = 1.0 - saturate(dot(viewDir, normal));
                rim = pow(rim, _RimPower);
                float rimAlpha = rim * _RimColor.a;
                float3 rimC = _RimColor.rgb * rimAlpha;
                
                // 3. Fake Specular Highlight (Shiny point)
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0, dot(normal, halfVector));
                float spec = pow(NdotH, _Shininess * 128.0);
                float specAlpha = saturate(spec * _SpecColor.a);
                float3 specC = _SpecColor.rgb * specAlpha;
                
                // Combine everything
                // Final color uses additive math, so highlights make it shine brightly
                float3 finalColor = baseC + rimC + specC;
                
                // Final alpha determines how much of the background is obscured
                float finalAlpha = saturate(baseA + rimAlpha + specAlpha);
                
                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
