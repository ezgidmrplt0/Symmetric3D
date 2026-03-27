Shader "UI/Silhouette"
{
    Properties
    {
        _MainTex          ("Texture", 2D)         = "white" {}
        _Color            ("Silhouette Color", Color) = (0.4, 0.4, 0.4, 1)
        _OutlineColor     ("Outline Color", Color)    = (1, 1, 1, 1)
        _OutlineWidth     ("Outline Width (UV)", Float) = 0.01
        _StencilComp      ("Stencil Comparison", Float) = 8
        _Stencil          ("Stencil ID",         Float) = 0
        _StencilOp        ("Stencil Operation",  Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask  ("Stencil Read Mask",  Float) = 255
        _ColorMask        ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True"
               "RenderType"="Transparent" "PreviewType"="Plane"
               "CanUseSpriteAtlas"="True" }

        Stencil
        {
            Ref       [_Stencil]
            Comp      [_StencilComp]
            Pass      [_StencilOp]
            ReadMask  [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                float2 uv            : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 color         : COLOR;
            };

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            fixed4    _Color;
            fixed4    _OutlineColor;
            float     _OutlineWidth;
            float4    _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv    = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float  w  = _OutlineWidth;

                float centerAlpha = tex2D(_MainTex, uv).a;

                // 8 komşuya bak — herhangi birinde alpha varsa outline bölgesi
                float neighborAlpha =
                    tex2D(_MainTex, uv + float2( w,  0)).a +
                    tex2D(_MainTex, uv + float2(-w,  0)).a +
                    tex2D(_MainTex, uv + float2( 0,  w)).a +
                    tex2D(_MainTex, uv + float2( 0, -w)).a +
                    tex2D(_MainTex, uv + float2( w,  w)).a +
                    tex2D(_MainTex, uv + float2(-w,  w)).a +
                    tex2D(_MainTex, uv + float2( w, -w)).a +
                    tex2D(_MainTex, uv + float2(-w, -w)).a;

                fixed4 col;
                float clip = UnityGet2DClipping(i.worldPosition.xy, _ClipRect) * i.color.a;

                if (centerAlpha > 0.01)
                {
                    // İç alan → gri silüet
                    col   = _Color;
                    col.a = centerAlpha * clip;
                }
                else if (neighborAlpha > 0.01)
                {
                    // Kenar → beyaz outline
                    col   = _OutlineColor;
                    col.a = clamp(neighborAlpha, 0, 1) * clip;
                }
                else
                {
                    discard;
                    col = fixed4(0,0,0,0);
                }

                return col;
            }
            ENDCG
        }
    }
}
