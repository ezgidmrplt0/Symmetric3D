Shader "Custom/HypercasualIceShader"
{
    Properties
    {
        _Color ("Ice Tint", Color) = (0.65, 0.92, 1.0, 0.45)
        _DeepColor ("Deep Ice Color", Color) = (0.18, 0.62, 0.95, 0.65)
        _RimColor ("Crystal Edge Glow", Color) = (0.95, 1.0, 1.0, 0.95)
        _RimPower ("Rim Power", Range(0.1, 8.0)) = 2.0
        _SpecColor ("Sun Specular", Color) = (1.0, 1.0, 1.0, 1.0)
        _Shininess ("Shininess", Range(0.01, 1.0)) = 0.75
        _FrostStrength ("Frost Strength", Range(0.0, 1.0)) = 0.22
        _CrackScale ("Crack Scale", Range(1.0, 20.0)) = 5.5
        _LightDirX ("Light X", Range(-1, 1)) = -0.35
        _LightDirY ("Light Y", Range(0, 1)) = 0.9
        _LightDirZ ("Light Z", Range(-1, 1)) = -0.4
    }
    SubShader
    {
        Tags {"Queue"="Transparent+50" "IgnoreProjector"="True" "RenderType"="Transparent"}
        LOD 200

        ZWrite Off
        Blend One OneMinusSrcAlpha // Premultiplied Alpha for crisp glossy glass/ice
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 viewDir : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            fixed4 _Color;
            fixed4 _DeepColor;
            fixed4 _RimColor;
            float _RimPower;
            fixed4 _SpecColor;
            float _Shininess;
            float _FrostStrength;
            float _CrackScale;
            float _LightDirX;
            float _LightDirY;
            float _LightDirZ;

            // Procedural Voronoi / Crack noise function
            float2 hash2(float2 p)
            {
                return frac(sin(float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)))) * 43758.5453);
            }

            float voronoi(float2 uv)
            {
                float2 g = floor(uv);
                float2 f = frac(uv);
                float minDist = 1.0;
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 offset = hash2(g + lattice);
                        float2 v = lattice + offset - f;
                        float d = length(v);
                        minDist = min(minDist, d);
                    }
                }
                return minDist;
            }

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = WorldSpaceViewDir(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(i.viewDir);
                float3 lightDir = normalize(float3(_LightDirX, _LightDirY, _LightDirZ));

                // 1. Procedural subtle crystal facets & fine ice cracks
                float2 crackUv = (i.worldPos.xy + i.worldPos.z * 0.5) * _CrackScale;
                float v = voronoi(crackUv);
                float cracks = smoothstep(0.06, 0.0, abs(v - 0.5)) * _FrostStrength;
                float frost = (v * 0.3) * _FrostStrength;

                // 2. Base & Deep Color Gradient with smooth spherical curvature
                float NdotV = saturate(dot(viewDir, normal));
                float depthFactor = pow(1.0 - NdotV, 1.4);
                fixed4 iceColor = lerp(_Color, _DeepColor, depthFactor * 0.7);
                
                // Add internal crystalline crack brightness
                iceColor.rgb += float3(0.6, 0.9, 1.0) * cracks * 1.2;
                iceColor.rgb += float3(0.15, 0.35, 0.55) * frost;

                float baseA = saturate(iceColor.a + cracks * 0.3 + frost * 0.2);
                float3 baseC = iceColor.rgb * baseA;

                // 3. Rim Lighting (Smooth Crystal Glow around sphere contour)
                float rim = 1.0 - NdotV;
                rim = pow(rim, _RimPower);
                float rimAlpha = rim * _RimColor.a;
                float3 rimC = _RimColor.rgb * rimAlpha * 1.3;

                // 4. Sharp Glossy Specular Gleam
                float3 halfVector1 = normalize(lightDir + viewDir);
                float NdotH1 = max(0.0, dot(normal, halfVector1));
                float spec1 = pow(NdotH1, _Shininess * 160.0);

                float3 lightDir2 = normalize(float3(-_LightDirX, _LightDirY * 0.8, -_LightDirZ));
                float3 halfVector2 = normalize(lightDir2 + viewDir);
                float NdotH2 = max(0.0, dot(normal, halfVector2));
                float spec2 = pow(NdotH2, _Shininess * 60.0) * 0.45;

                float totalSpec = spec1 + spec2;
                float specAlpha = saturate(totalSpec * _SpecColor.a);
                float3 specC = _SpecColor.rgb * specAlpha * 1.5;

                // 5. Combine with Additive Highlights
                float3 finalColor = baseC + rimC + specC;
                float finalAlpha = saturate(baseA + rimAlpha + specAlpha);

                return fixed4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }
}
