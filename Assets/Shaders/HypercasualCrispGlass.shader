Shader "Custom/HypercasualCrispGlass"
{
    Properties
    {
        [Header(Glass Body Tint)]
        _Color ("Base Tint", Color) = (0.90, 0.96, 1.0, 0.03)
        
        [Header(Outer Rim Fresnel)]
        _RimColor ("Outer Rim Color", Color) = (0.88, 0.94, 1.0, 0.38)
        _RimPower ("Outer Rim Power", Range(0.5, 8.0)) = 2.8
        
        [Header(Inner Backface Rim)]
        _InnerRimColor ("Inner Rim Color", Color) = (0.85, 0.94, 1.0, 0.06)
        _InnerRimPower ("Inner Rim Power", Range(0.5, 8.0)) = 3.5
        
        [Header(Specular Highlight)]
        _SpecColor ("Specular Color", Color) = (1.0, 1.0, 1.0, 0.28)
        _Shininess ("Shininess", Range(0.01, 1)) = 0.70
        _LightDirX ("Light X", Range(-1, 1)) = -0.35
        _LightDirY ("Light Y", Range(0, 1)) = 0.85
        _LightDirZ ("Light Z", Range(-1, 1)) = -0.40

        [Header(Vertical Highlight Streaks)]
        _StreakIntensity ("Streak Intensity", Range(0, 1)) = 0.20
        _StreakWidth ("Streak Soft Width", Range(0.05, 0.6)) = 0.26
        _StreakPower ("Streak Sharpness", Range(4, 64)) = 24.0
        _StreakOffsetLeft ("Left Streak Pos", Range(-1, 0)) = -0.42
        _StreakOffsetRight ("Right Streak Pos", Range(0, 1)) = 0.52

        [Header(Top Shoulder Rim)]
        _TopRimStrength ("Top Shoulder Rim", Range(0, 1)) = 0.18
    }
    SubShader
    {
        Tags {"Queue"="Transparent+10" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 100

        // ====================================================================
        // PASS 1: BACKFACE / INNER GLASS DEPTH (Cull Front)
        // Camın et kalınlığını hissettiren, sıvıyı asla perdelemeyen zarif iç kontür
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

                return fixed4(_InnerRimColor.rgb, innerRimAlpha);
            }
            ENDCG
        }

        // ====================================================================
        // PASS 2: FRONTFACE / CRYSTAL CLEAR REALISTIC GLASS (Cull Back)
        // Gerçekçi fiziksel Fresnel, yumuşak stüdyo yansıması ve berrak kristal cam
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
                float3 objPos : TEXCOORD2;
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
            float _StreakWidth;
            float _StreakPower;
            float _StreakOffsetLeft;
            float _StreakOffsetRight;
            float _TopRimStrength;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.viewDir = WorldSpaceViewDir(v.vertex);

                float3 wNormal = UnityObjectToWorldNormal(v.normal);
                o.viewNormal = mul((float3x3)UNITY_MATRIX_V, wNormal);
                o.objPos = v.vertex.xyz;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 vNormal = normalize(i.viewNormal);

                float NdotV = saturate(dot(normal, viewDir));

                // 1. Fiziksel Cam Fresnel (Cam siluetini ve kavisli kenarları netleştirir, 3D cam derinliği katar)
                float fresnelFactor = pow(1.0 - NdotV, _RimPower);
                float rimAlpha = fresnelFactor * _RimColor.a;

                // 2. Üst Omuz & Boyun Kavis Işığı (Şişenin kavisli boyun ve ağız boğumuna zarif derinlik verir, asla göz almaz)
                float topCurvature = saturate(vNormal.y) * pow(1.0 - NdotV, 2.0) * (_TopRimStrength * 0.35);

                // 3. Stüdyo Tipi Yumuşak Dikey Işık Yansıması (Softbox Cam Parıltısı - Aşırı parlamayan, zarif sheen)
                float streakWidth = (_StreakWidth > 0.01) ? _StreakWidth : 0.26;
                float distLeft = abs(vNormal.x - _StreakOffsetLeft);
                float streakLeft = smoothstep(streakWidth, 0.0, distLeft);

                float distRight = abs(vNormal.x - _StreakOffsetRight);
                float streakRight = smoothstep(streakWidth * 1.25, 0.0, distRight) * 0.55;

                // Üst ve alt uçlarda yumuşak sönümlenme
                float verticalFade = smoothstep(-0.05, 0.15, i.objPos.y) * smoothstep(1.15, 0.85, i.objPos.y);
                float totalStreak = (streakLeft + streakRight) * _StreakIntensity * verticalFade;

                // 4. Kamera-Bağıl Stüdyo Işık Parıltısı (Sahne ışıklarının açısından ve şiddetinden %100 bağımsızdır)
                float3 viewLightDir = normalize(float3(_LightDirX, _LightDirY, _LightDirZ));
                float3 viewCamDir = float3(0, 0, 1);
                float3 halfVector = normalize(viewLightDir + viewCamDir);
                float NdotH = max(0.0, dot(vNormal, halfVector));
                float spec = pow(NdotH, lerp(16.0, 96.0, _Shininess));
                float specAlpha = spec * _SpecColor.a;

                // 5. Dengeli ve Gerçekçi Şeffaflık Bileşimi
                // Şişenin içi %95+ berrak kalır; kenarlar, boyun ve yansımalar gerçek bir cam şişe hissi verir
                float totalAlpha = saturate(
                    _Color.a + 
                    rimAlpha + 
                    topCurvature + 
                    totalStreak + 
                    specAlpha
                );

                // Temiz, berrak kristal cam yansıması (karanlık veya soluk leke bırakmaz)
                float3 glassTint = _Color.rgb;
                float3 reflectionColor = lerp(_RimColor.rgb, _SpecColor.rgb, saturate(specAlpha + totalStreak * 0.4));
                float3 finalColor = lerp(glassTint, reflectionColor, saturate(fresnelFactor * 0.8 + totalStreak + specAlpha));

                return fixed4(finalColor, totalAlpha);
            }
            ENDCG
        }
    }
}
