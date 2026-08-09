using UnityEngine;
using UnityEngine.UI;


[ExecuteInEditMode]
public class PainterlyTextureProcessor : MonoBehaviour
{
    public ComputeShader computeShader;
    public Texture2D sourceTexture;

    [Range(0f, 1f)]
    public float blendStrength = 0.6f;

    [Range(1, 10)]
    public int searchRadius = 3;

    public RenderTexture resultTexture;
    private RenderTexture inputRT;

    void Start()
    {
        ProcessTexture();
    }

    public void ProcessTexture()
    {
        if (computeShader == null || sourceTexture == null) return;

        int width = sourceTexture.width;
        int height = sourceTexture.height;

        if (resultTexture == null || resultTexture.width != width)
        {
            if (resultTexture != null) resultTexture.Release();
            resultTexture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            resultTexture.enableRandomWrite = true;
            resultTexture.Create();
        }

        if (inputRT == null || inputRT.width != width)
        {
            if (inputRT != null) inputRT.Release();
            inputRT = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32);
            inputRT.enableRandomWrite = true;
            inputRT.Create();
        }

        Graphics.Blit(sourceTexture, inputRT);

        int kernel = computeShader.FindKernel("StylizeTexture");
        computeShader.SetTexture(kernel, "Input", inputRT);
        computeShader.SetTexture(kernel, "Result", resultTexture);
        computeShader.SetFloat("_BlendStrength", blendStrength);
        computeShader.SetInt("_SearchRadius", searchRadius);
        computeShader.Dispatch(kernel,
            Mathf.CeilToInt(width / 8f),
            Mathf.CeilToInt(height / 8f), 1);

        // Buscar renderer en este objeto o en hijos
        Renderer rend = GetComponent<Renderer>();
        if (rend == null) rend = GetComponentInChildren<Renderer>();

        if (rend != null)
        {
            rend.sharedMaterial.SetTexture("_BaseMap", resultTexture);
        }
    }

    // Solo actualizar cuando se presiona Play, nunca en edit mode
    void OnValidate() { }
}