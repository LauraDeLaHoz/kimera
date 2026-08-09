using UnityEngine;

public class PainterlyShaderController : MonoBehaviour
{
    [Header("Textures")]
    public Texture2D albedoTexture;
    public Texture2D strokeIDMap;
    public Texture2D normalMap;

    [Header("Color Settings")]
    [Range(0f, 0.1f)] public float colorRandomness = 0.005f;
    [Range(0f, 2f)] public float colorSaturation = 1f;

    [Header("Lighting")]
    public Color ambientColor = Color.white;
    [Range(0f, 3f)] public float ambientIntensity = 1.7f;
    [Range(1f, 8f)] public float lightSteps = 3f;

    [Header("Specular")]
    [Range(0f, 1f)] public float specularOpacity = 0.5f;
    [Range(1f, 64f)] public float shininess = 4f;

    [Header("Shell Settings")]
    [Range(1, 8)] public int numShells = 2;
    [Range(0f, 0.05f)] public float shellInflation = 0.02f;
    [Range(0f, 2f)] public float shellDetailLevel = 1.565f;
    [Range(0f, 1f)] public float shellOpacityFalloff = 0.151f;
    [Range(0f, 1f)] public float shellFresnelOpacity = 0.101f;
    [Range(0f, 0.01f)] public float perShellUVOffset = 0.004f;

    [Header("Refraction")]
    [Range(0f, 1f)] public float refractionOpacity = 0.333f;
    [Range(0f, 1f)] public float refractionIntensity = 0.52f;
    [Range(0f, 1f)] public float refractionCutoff = 0.951f;

    private Renderer[] renderers;
    private MaterialPropertyBlock propBlock;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>();
        propBlock = new MaterialPropertyBlock();
    }

    void OnValidate()
    {
        UpdateMaterials();
    }

    public void UpdateMaterials()
    {
        if (renderers == null) return;

        foreach (Renderer rend in renderers)
        {
            rend.GetPropertyBlock(propBlock);

            // Texturas
            if (albedoTexture != null) propBlock.SetTexture("_BaseMap", albedoTexture);
            if (strokeIDMap != null) propBlock.SetTexture("_StrokeIDMap", strokeIDMap);
            if (normalMap != null) propBlock.SetTexture("_NormalMap", normalMap);

            // Color
            propBlock.SetFloat("_ColorRandomness", colorRandomness);
            propBlock.SetFloat("_ColorSaturation", colorSaturation);

            // Lighting
            propBlock.SetColor("_AmbientColor", ambientColor);
            propBlock.SetFloat("_AmbientIntensity", ambientIntensity);
            propBlock.SetFloat("_LightSteps", lightSteps);

            // Specular
            propBlock.SetFloat("_SpecularOpacity", specularOpacity);
            propBlock.SetFloat("_Shininess", shininess);

            // Shells
            propBlock.SetInt("_NumShells", numShells);
            propBlock.SetFloat("_ShellInflation", shellInflation);
            propBlock.SetFloat("_ShellDetailLevel", shellDetailLevel);
            propBlock.SetFloat("_ShellOpacityFalloff", shellOpacityFalloff);
            propBlock.SetFloat("_ShellFresnelOpacity", shellFresnelOpacity);
            propBlock.SetFloat("_PerShellUVOffset", perShellUVOffset);

            // Refraction
            propBlock.SetFloat("_RefractionOpacity", refractionOpacity);
            propBlock.SetFloat("_RefractionIntensity", refractionIntensity);
            propBlock.SetFloat("_RefractionCutoff", refractionCutoff);

            rend.SetPropertyBlock(propBlock);
        }
    }
}
