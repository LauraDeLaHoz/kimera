#ifndef PAINTERLY_LIGHTING_INCLUDED
#define PAINTERLY_LIGHTING_INCLUDED

void PainterlyDiffuse_float(
    float3 normalWS,
    float3 lightDir,
    float3 lightColor,
    float steps,
    float strokeModulation,
    out float3 diffuse)          // ← la salida DEBE ser un parámetro out
{
    float NdotL = saturate(dot(normalWS, lightDir));
    NdotL = NdotL * (1.0 + strokeModulation * 0.3);
    
    float posterized = floor(NdotL * steps) / steps;
    float edge = fwidth(NdotL) * 1.5;
    float smoothed = smoothstep(posterized - edge, posterized + edge, NdotL);
    
    diffuse = smoothed * lightColor;
}

#endif