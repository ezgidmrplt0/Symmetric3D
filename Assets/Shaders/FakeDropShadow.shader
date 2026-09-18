Shader "Custom/FakeDropShadow"
{
    Properties
    {
        _Color ("Shadow Color", Color) = (0.06, 0.09, 0.2, 0.4)
        _Softness ("Edge Softness", Range(0.5, 10.0)) = 3.0
        [Toggle] _IsCircle ("Is Circular", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent-5" "IgnoreProjector"="True" "RenderType"="Transparent" }
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
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Color;
            float _Softness;
            float _IsCircle;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Soft edge falloff - supports both rectangular box and circular shape
                float2 centered = abs(i.uv - 0.5) * 2.0;
                float boxEdge = max(centered.x, centered.y);
                float circleEdge = length(i.uv - 0.5) * 2.0;
                float edge = lerp(boxEdge, circleEdge, _IsCircle);
                float falloff = saturate((1.0 - edge) * _Softness);
                falloff = smoothstep(0.0, 1.0, falloff);

                return fixed4(_Color.rgb, _Color.a * falloff);
            }
            ENDCG
        }
    }
    FallBack Off
}
