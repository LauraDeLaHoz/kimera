Shader "Custom/PainterlyShell"
{
    Properties
    {
        _BaseMap ("Base Texture", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _NumShells ("Number of Shells", Range(1, 8)) = 2
        _ShellInflation ("Shell Inflation", Range(0, 0.05)) = 0.01
        _ShellOpacityFalloff ("Opacity Falloff", Range(0.01, 1)) = 0.15
        _ShellFresnelOpacity ("Fresnel Opacity", Range(0, 1)) = 0.1
        _PerShellUVOffset ("Per Shell UV Offset", Range(0, 0.01)) = 0.004
    }

    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        
        // Pass base (shell index 0 = mesh original)
        Pass
        {
            Name "ShellBase"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2g
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };
            
            struct g2f
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float2 uv : TEXCOORD1;
                float shellIndex : TEXCOORD2;
                float3 positionWS : TEXCOORD3;
            };
            
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                int _NumShells;
                float _ShellInflation;
                float _ShellOpacityFalloff;
                float _ShellFresnelOpacity;
                float _PerShellUVOffset;
            CBUFFER_END
            
            v2g vert(Attributes input)
            {
                v2g output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }
            
            [maxvertexcount(24)] // 3 vértices × 8 shells máximo
            void geom(triangle v2g input[3], inout TriangleStream<g2f> stream)
            {
                for (int s = 0; s < _NumShells; s++)
                {
                    float t = (float)s / (float)(_NumShells - 1 + 0.001);
                    float inflation = s * _ShellInflation;
                    float uvOffset = s * _PerShellUVOffset;
                    
                    for (int v = 0; v < 3; v++)
                    {
                        g2f o;
                        
                        // Inflar en dirección normal
                        float3 inflatedWS = input[v].positionWS + input[v].normalWS * inflation;
                        o.positionCS = TransformWorldToHClip(inflatedWS);
                        o.normalWS = input[v].normalWS;
                        o.uv = input[v].uv + float2(uvOffset, uvOffset);
                        o.shellIndex = t;
                        o.positionWS = inflatedWS;
                        
                        stream.Append(o);
                    }
                    stream.RestartStrip();
                }
            }
            
            float4 frag(g2f input) : SV_Target
            {
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                
                // Calcular Fresnel para opacidad de shells exteriores
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = 1.0 - saturate(dot(input.normalWS, viewDir));
                
                // Shells exteriores son más transparentes
                float shellOpacity = 1.0 - (input.shellIndex * _ShellOpacityFalloff);
                shellOpacity *= lerp(1.0, fresnel, _ShellFresnelOpacity);
                shellOpacity = saturate(shellOpacity);
                
                baseColor.a *= shellOpacity;
                
                // Si es muy transparente, descartar
                clip(baseColor.a - 0.01);
                
                return baseColor;
            }
            ENDHLSL
        }
    }
}