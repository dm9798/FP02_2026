Shader "Custom/GradientBackground"
{
    Properties
    {
        _ColorTop("Top Color", Color) = (0.4, 0.2, 0.8, 1)
        _ColorBottom("Bottom Color", Color) = (0.1, 0.2, 0.6, 1)
        _Angle("Gradient Angle (Degrees)", Range(0, 360)) = 90
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Background" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            fixed4 _ColorTop;
            fixed4 _ColorBottom;
            float _Angle;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float rad = radians(_Angle);
                float2 dir = float2(cos(rad), sin(rad));
                float t = dot(i.uv - 0.5, dir) + 0.5;
                t = saturate(t);
                return lerp(_ColorBottom, _ColorTop, t);
            }
            ENDCG
        }
    }
}