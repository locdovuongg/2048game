using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TileUI : MonoBehaviour
{
    [SerializeField] private Image background;
    [SerializeField] private TMP_Text valueText;

    public int Value { get; private set; }

    public void Setup(int value)
    {
        Value = value;
        valueText.text = value.ToString();
        UpdateColor();
        UpdateFontSize();
    }

    void UpdateFontSize()
    {
        int digits = Value.ToString().Length;
        valueText.fontSize = digits switch
        {
            1 => 64,
            2 => 60,
            3 => 52,
            4 => 44,
            _ => 36
        };
    }

    void UpdateColor()
    {
        // Text màu tối cho giá trị nhỏ, màu trắng cho giá trị lớn
        bool darkText = Value <= 4;
        valueText.color = darkText
            ? new Color32(119, 110, 101, 255)
            : new Color32(249, 246, 242, 255);

        switch (Value)
        {
            case 2:    background.color = new Color32(238, 228, 218, 255); break;
            case 4:    background.color = new Color32(237, 224, 200, 255); break;
            case 8:    background.color = new Color32(242, 177, 121, 255); break;
            case 16:   background.color = new Color32(245, 149,  99, 255); break;
            case 32:   background.color = new Color32(246, 124,  95, 255); break;
            case 64:   background.color = new Color32(246,  94,  59, 255); break;
            case 128:  background.color = new Color32(237, 207, 114, 255); break;
            case 256:  background.color = new Color32(237, 204,  97, 255); break;
            case 512:  background.color = new Color32(237, 200,  80, 255); break;
            case 1024: background.color = new Color32(237, 197,  63, 255); break;
            case 2048: background.color = new Color32(237, 194,  46, 255); break;
            default:   background.color = new Color32( 60,  58,  50, 255); break;
        }
    }
}