Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        [Header(Glass Body Tint)]
        _Color ("Base Tint", Color) = (0.85, 0.95, 1.0, 0.02)
        
        [Header(Outer Rim Fresnel)]
        _RimColor ("Outer Rim Color", Color) = (0.75, 0.90, 1.0, 0.20)
        _RimPower ("Outer Rim Power", Range(0.5, 8.0)) = 4.2
        
        [Header(Inner Backface Rim)]
        _InnerRimColor ("Inner Rim Color", Color) = (0.30, 0.70, 1.0, 0.04)
        _InnerRimPower ("Inner Rim Power", Range(0.5, 8.0)) = 4.0
        
        [Header(Specular Highlight)]
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 0.35)
        _Shininess ("Shininess", Range(0.01, 1)) = 0.88
        _LightDirX ("Light X", Range(-1, 1)) = -0.35
        _LightDirY ("Light Y", Range(0, 1)) = 0.90
        _LightDirZ ("Light Z", Range(-1, 1)) = -0.40

        [Header(Vertical Highlight Streaks)]
        _StreakIntensity ("Streak Intensity", Range(0, 1)) = 0.0
        _StreakPower ("Streak Sharpness", Range(4, 64)) = 24.0
        _StreakOffsetLeft ("Left Streak Pos", Range(-1, 0)) = -0.62
        _StreakOffsetRight ("Right Streak Pos", Range(0, 1)) = 0.68
    }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 100

        // ====================================================================
        // PASS 1: BACKFACE / INNER GLASS DEPTH (Cull Front)
        // Çok hafif, sıvıyı perdelemeyen arka cam hissi
        // ====================================================================
        Pass
        {
            Name "GlassBackface"
            Cull Front
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
            fixed4 _InnerRimColor;
            float _InnerRimPower;

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
                float3 normal = -normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float NdotV = saturate(dot(normal, viewDir));
                float innerRim = pow(1.0 - NdotV, _InnerRimPower);
                float innerRimAlpha = innerRim * _InnerRimColor.a;

                float3 finalColor = _InnerRimColor.rgb;
                float finalAlpha = innerRimAlpha;

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }

        // ====================================================================
        // PASS 2: FRONTFACE / CRYSTAL CLEAR GLASS (Cull Back)
        // Zarif, ince dış siluet kenarı ve net şeffaf cam
        // ====================================================================
        Pass
        {
            Name "GlassFrontface"
            Cull Back
            ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha

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
                float3 viewNormal : TEXCOORD1;
            };

            fixed4 _Color;
            fixed4 _RimColor;
            float _RimPower;
            fixed4 _SpecColor;
            float _Shininess;
            float _LightDirX;
            float _LightDirY;
            float _LightDirZ;
            float _StreakIntensity;
            float _StreakPower;
            float _StreakOffsetLeft;
            float _StreakOffsetRight;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);

                float3 wNormal = UnityObjectToWorldNormal(v.normal);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, wNormal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 vNormal = normalize(i.viewNormal);
                float3 lightDir = normalize(float3(_LightDirX, _LightDirY, _LightDirZ));

                // 1. Kristal şeffaf gövde
                float baseA = _Color.a;
                float3 baseC = _Color.rgb;

                // 2. Çok ince, zarif siluet kenar kontürü (parlama yapmaz)
                float NdotV = saturate(dot(normal, viewDir));
                float rim = pow(1.0 - NdotV, _RimPower);
                float rimAlpha = rim * _RimColor.a;
                float3 rimC = _RimColor.rgb;

                // 3. Zarif noktasal ışık parıltısı (specular)
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(normal, halfVector));
                float spec = pow(NdotH, lerp(32.0, 256.0, _Shininess));
                float specAlpha = spec * _SpecColor.a;
                float3 specC = _SpecColor.rgb;

                // 4. Dikey yansıma (varsayılan kapalı - sıvıyı asla perdelemez)
                float streakLeft = pow(saturate(1.0 - abs(vNormal.x - _StreakOffsetLeft)), _StreakPower);
                float streakRight = pow(saturate(1.0 - abs(vNormal.x - _StreakOffsetRight)), _StreakPower) * 0.65;
                float totalStreak = saturate(streakLeft + streakRight) * _StreakIntensity;
                float3 streakC = _SpecColor.rgb;

                // Şeffaflık dengesi: Sıvı %95+ berrak kalır
                float finalAlpha = saturate(baseA + rimAlpha * 0.35 + specAlpha * 0.45 + totalStreak * 0.30);
                float3 blendedColor = baseC * baseA + rimC * rimAlpha + specC * specAlpha + streakC * totalStreak;
                float3 finalColor = (finalAlpha > 0.001) ? (blendedColor / finalAlpha) : float3(0, 0, 0);

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
