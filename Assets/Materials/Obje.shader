Shader "Custom/LiquidFullControl"
{
    Properties
    {
        _LiquidColor ("Liquid Color", Color) = (1,0,0,1)
        _FillAmount ("Fill Amount", Range(-0.5,0.5)) = 0
        _Mode ("Mode (0=Y,1=X)", Range(0,1)) = 0

        _TiltX ("Tilt X", Range(-1,1)) = 0
        _TiltZ ("Tilt Z", Range(-1,1)) = 0
        _WobbleStrength ("Wobble Strength", Range(0,0.1)) = 0.02
        _WobbleSpeed ("Wobble Speed", Range(0,10)) = 3
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" }
        LOD 200
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:fade

        struct Input
        {
            float3 worldPos;
        };

        fixed4 _LiquidColor;
        float _FillAmount;
        float _Mode;
        float _TiltX;
        float _TiltZ;
        float _WobbleStrength;
        float _WobbleSpeed;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 objPos = mul(unity_WorldToObject, float4(IN.worldPos,1)).xyz;

            // Eðilme (plane tilt)
            float tilt = objPos.x * _TiltX + objPos.z * _TiltZ;

            // Hafif salýným
            float wobble = sin(_Time.y * _WobbleSpeed) * _WobbleStrength;

            float baseAxis = lerp(objPos.y, objPos.x, _Mode);

            float axis = baseAxis + tilt + wobble;

            if (axis < _FillAmount)
            {
                o.Albedo = _LiquidColor.rgb;
                o.Alpha = 1;
            }
            else
            {
                o.Alpha = 0;
            }

            o.Smoothness = 0.85;
            o.Metallic = 0;
        }
        ENDCG
    }
}