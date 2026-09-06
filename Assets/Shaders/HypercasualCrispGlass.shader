Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        [Header(Glass Body Tint)]
        _Color ("Base Tint", Color) = (0.05, 0.10, 0.22, 0.12)
        
        [Header(Outer Rim Fresnel)]
        _RimColor ("Outer Rim Color", Color) = (0.45, 0.88, 1.0, 0.95)
        _RimPower ("Outer Rim Power", Range(0.5, 8.0)) = 2.4
        
        [Header(Inner Backface Rim)]
        _InnerRimColor ("Inner Rim Color", Color) = (0.22, 0.65, 0.95, 0.40)
        _InnerRimPower ("Inner Rim Power", Range(0.5, 8.0)) = 2.0
        
        [Header(Specular Highlight)]
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 0.95)
        _Shininess ("Shininess", Range(0.01, 1)) = 0.85
        _LightDirX ("Light X", Range(-1, 1)) = -0.35
        _LightDirY ("Light Y", Range(0, 1)) = 0.90
        _LightDirZ ("Light Z", Range(-1, 1)) = -0.40

        [Header(Vertical Cylindrical Streaks)]
        _StreakIntensity ("Streak Intensity", Range(0, 1)) = 0.70
        _StreakPower ("Streak Sharpness", Range(4, 64)) = 22.0
        _StreakOffsetLeft ("Left Streak Pos", Range(-1, 0)) = -0.62
        _StreakOffsetRight ("Right Streak Pos", Range(0, 1)) = 0.68
    }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 100

        // ====================================================================
        // PASS 1: BACKFACE / INNER GLASS DEPTH (Cull Front)
        // Renders the back lip, inner wall, and bottom curve seen through the glass
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
                // Invert normal for backface
                float3 normal = -normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);

                float NdotV = saturate(dot(normal, viewDir));
                float innerRim = pow(1.0 - NdotV, _InnerRimPower);
                float innerRimAlpha = innerRim * _InnerRimColor.a;

                float3 baseC = _Color.rgb * (_Color.a * 0.5);
                float3 rimC = _InnerRimColor.rgb * innerRimAlpha;

                float3 finalColor = baseC + rimC;
                float finalAlpha = saturate(_Color.a * 0.6 + innerRimAlpha * 0.8);

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }

        // ====================================================================
        // PASS 2: FRONTFACE / CRISP FRESNEL & SPECULAR (Cull Back)
        // Renders crisp electric ice-blue rim, vertical glossy streaks, and glossy spec
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

                // View-space normal for screen-aligned vertical highlights
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

                // 1. Crystal-clear dark-tinted body
                float baseA = _Color.a;
                float3 baseC = _Color.rgb * baseA;

                // 2. Crisp Hypercasual Fresnel Rim Sheen
                float NdotV = saturate(dot(normal, viewDir));
                float rim = pow(1.0 - NdotV, _RimPower);
                
                // Extra glow on bottom curve (normal pointing downwards)
                float bottomCurveBoost = saturate(-normal.y) * 0.45;
                rim = saturate(rim + bottomCurveBoost * rim);

                float rimAlpha = rim * _RimColor.a;
                float3 rimC = _RimColor.rgb * rimAlpha;

                // 3. Primary Directional Glossy Specular Highlight
                float3 halfVector = normalize(lightDir + viewDir);
                float NdotH = max(0.0, dot(normal, halfVector));
                float spec = pow(NdotH, lerp(32.0, 256.0, _Shininess));
                float specAlpha = spec * _SpecColor.a;
                float3 specC = _SpecColor.rgb * specAlpha;

                // 4. Vertical Cylindrical Glossy Highlight Streaks (Iconic Mobile Look)
                // Highlights running along the left and right curvatures of the cylindrical flask
                float streakLeft = pow(saturate(1.0 - abs(vNormal.x - _StreakOffsetLeft)), _StreakPower);
                float streakRight = pow(saturate(1.0 - abs(vNormal.x - _StreakOffsetRight)), _StreakPower) * 0.65;
                float totalStreak = saturate(streakLeft + streakRight) * _StreakIntensity;
                float3 streakC = _SpecColor.rgb * totalStreak;

                // 5. Final Balanced Additive + Transparent Color Composition
                float3 finalColor = baseC + rimC + specC + streakC;
                float finalAlpha = saturate(baseA + rimAlpha * 0.85 + specAlpha * 0.90 + totalStreak * 0.75);

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
