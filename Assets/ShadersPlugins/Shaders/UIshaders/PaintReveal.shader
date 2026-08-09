Shader "Custom/PaintReveal"
{
    Properties
    {
        _MainTex      ("Panel Texture", 2D)        = "white" {}
        _MaskTex      ("Video Mask",    2D)        = "white" {}
        _Threshold    ("Threshold",  Range(0,1))   = 0
        _EdgeSoftness ("Softness", Range(0,0.2))   = 0.05
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
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            sampler2D _MainTex;
            sampler2D _MaskTex;
            float4    _MainTex_ST;
            float     _Threshold;
            float     _EdgeSoftness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv     = TRANSFORM_TEX(v.uv, _MainTex);
                o.color  = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Tu textura del panel — sin modificar
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                // Canal R del video = máscara en blanco y negro
                float mask = tex2D(_MaskTex, i.uv).r;

                // Borde suave entre visible e invisible
                float alpha = smoothstep(
                    _Threshold - _EdgeSoftness,
                    _Threshold + _EdgeSoftness,
                    mask
                );

                // El video controla solo el alpha — la textura no cambia
                col.a *= alpha;
                return col;
            }
            ENDCG
        }
    }
}