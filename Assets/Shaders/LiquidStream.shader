Shader "Custom/LiquidStream"
{
    Properties
    {
        _Color ("Liquid Color", Color) = (0.9, 0.2, 0.2, 1)
        _InnerGlow ("Glow Multiplier", Float) = 1.8
        _FlowSpeed ("Flow Speed", Float) = 3.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+10" }
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
                // Akıntı kesit genişliği boyunca yuvarlak tüp görünümü (UV.x)
                float edge = sin(i.uv.x * 3.14159265);
                float coreGlow = pow(edge, 0.5) * _InnerGlow;

                // Akıntı boyunca akan animasyonlu iksir dalgası (UV.y)
                float wave = sin((i.uv.y * 14.0) - (_Time.y * _FlowSpeed * 10.0)) * 0.12 + 0.88;

                fixed4 col = _Color * i.color;
                col.rgb *= coreGlow * wave;
                col.a *= pow(edge, 0.35);

                return col;
            }
            ENDCG
        }
    }
}
