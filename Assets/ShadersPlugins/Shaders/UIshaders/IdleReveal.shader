Shader "Custom/IdleReveal"
{
    Properties
    {
        _Color        ("Color",      Color)        = (1,0,0,1)
        _MaskTex      ("Video Mask", 2D)           = "white" {}
        _Threshold    ("Threshold",  Range(0,1))   = 0.6
        _EdgeSoftness ("Softness", Range(0,0.2))   = 0.08
    }

    SubShader
    {
        Tags
        {
            "Queue"           = "Transparent"
            "RenderType"      = "Transparent"
            "IgnoreProjector" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            fixed4    _Color;
            sampler2D _MaskTex;
            float     _Threshold;
            float     _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Canal R del video = máscara
                float mask = tex2D(_MaskTex, i.uv).r;

                float alpha = smoothstep(
                    _Threshold - _EdgeSoftness,
                    _Threshold + _EdgeSoftness,
                    mask
                );

                // Color fijo, solo el alpha cambia con el video
                fixed4 col = _Color;
                col.a = alpha;
                return col;
            }
            ENDCG
        }
    }
}
