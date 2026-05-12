using UnityEngine;
using UnityEngine.UI;

public class FogController : MonoBehaviour
{
    [SerializeField] private Material fogMaterial;
    [SerializeField] private RawImage fogRawImage;

    // ✅ Đúng tên từ ShaderGraph của bạn
    private static readonly int FogSpeed = Shader.PropertyToID("_FogSpeed");
    private static readonly int FogSize  = Shader.PropertyToID("_Fog_Size");
    private static readonly int FogColor = Shader.PropertyToID("_Color"); 

    private void Start()
    {
        if (fogRawImage != null && fogMaterial != null)
            fogRawImage.material = fogMaterial;
    }

    public void SetOpacity(float value)
    {
        if (fogRawImage == null) return;
        fogRawImage.color = new Color(1f, 1f, 1f, value);
    }

    public void SetSpeed(float x, float y)
    {
        if (fogMaterial)
            fogMaterial.SetVector(FogSpeed, new Vector2(x, y));
    }

    public void SetSize(float size)
    {
        if (fogMaterial)
            fogMaterial.SetFloat(FogSize, size);
    }

    public void SetColor(Color color)
    {
        if (fogMaterial)
            fogMaterial.SetColor(FogColor, color);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        fogRawImage.color = new Color(1f, 1f, 1f, 0f);
    }

    public void Hide() => gameObject.SetActive(false);
}