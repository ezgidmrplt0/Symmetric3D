Shader "Custom/ProgressBar"
{
    Properties
    {
        _MainTex      ("Texture", 2D)           = "white" {}
        _FillAmount   ("Fill Amount", Range(0,1)) = 0.0
        _FillColor    ("Fill Color",  Color)     = (0.4, 0.85, 0.3, 1)
        _EmptyColor   ("Empty Color", Color)     = (0.15, 0.15, 0.15, 1)
        _EdgeSoftness ("Edge Softness", Range(0, 0.05)) = 0.01
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType"     = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                float2 uv    : TEXCOORD0;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            float     _FillAmount;
            float4    _FillColor;
            float4    _EmptyColor;
            float     _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Texture'dan alpha al (rounded rect için kullanılabilir)
                fixed4 tex = tex2D(_MainTex, i.uv);

                // UV.x ile fill pozisyonunu karşılaştır (yumuşak kenar ile)
                float t = smoothstep(_FillAmount - _EdgeSoftness, _FillAmount + _EdgeSoftness, i.uv.x);

                // Dolu taraf → _FillColor, boş taraf → _EmptyColor
                fixed4 col = lerp(_FillColor, _EmptyColor, t);

                // Vertex rengi alpha'sını ve texture alpha'sını uygula (UI maskeleme için)
                col.a = tex.a * i.color.a;

                return col;
            }
            ENDCG
        }
    }
}
