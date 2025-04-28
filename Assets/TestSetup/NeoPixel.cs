using UnityEngine;

public class NeoPixel : MonoBehaviour
{
    [SerializeField] private GameObject cylinderObject;
    [SerializeField] private GameObject pointLight1;
    [SerializeField] private GameObject pointLight2;

    private Renderer cylinderRenderer;
    private Light light1;
    private Light light2;

    private Color cylinderColor;
    private Color lightColor1;
    private Color lightColor2;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // If objects are not assigned, try to find them in children
        if (cylinderObject == null && transform.childCount > 0)
            cylinderObject = transform.GetChild(0).gameObject;

        if (pointLight1 == null && transform.childCount > 1)
            pointLight1 = transform.GetChild(1).gameObject;

        if (pointLight2 == null && transform.childCount > 2)
            pointLight2 = transform.GetChild(2).gameObject;

        // Get the components
        if (cylinderObject != null)
        {
            cylinderRenderer = cylinderObject.GetComponent<Renderer>();
            cylinderColor = cylinderRenderer.material.color;
        }
        if (pointLight1 != null)
        {
            light1 = pointLight1.GetComponent<Light>();
            lightColor1 = light1.color;
        }
        if (pointLight2 != null)
        {
            light2 = pointLight2.GetComponent<Light>();
            lightColor2 = light2.color;
        }
    }

    // Public method to set the color by changing only the hue
    public void SetColor(Color newColor)
    {
        Color.RGBToHSV(newColor, out var newH, out _, out _);

        // Apply the new hue to the cylinder material while preserving current saturation and value
        if (cylinderRenderer != null)
        {
            Color.RGBToHSV(cylinderColor, out _, out var fixS, out var fixV);
            cylinderRenderer.material.color = Color.HSVToRGB(newH, fixS, fixV);
        }

        if (light1 != null)
        {
            Color.RGBToHSV(lightColor1, out _, out var fixS, out var fixV);
            light1.color = Color.HSVToRGB(newH, fixS, fixV);
        }

        if (light2 != null)
        {
            Color.RGBToHSV(lightColor2, out _, out var fixS, out var fixV);
            light2.color = Color.HSVToRGB(newH, fixS, fixV);
        }
    }

    // sets the brightness of the pixel by setting the value of the color
    // input: 0-1
    // 0 = black, 1 = full brightness
    public void SetBrightness(float value)
    {
        if (cylinderRenderer != null)
        {
            Color.RGBToHSV(cylinderRenderer.material.color, out var h, out var s, out _);
            cylinderRenderer.material.color = Color.HSVToRGB(h, s, value);
        }

        if (light1 != null)
        {
            Color.RGBToHSV(light1.color, out var h, out var s, out _);
            light1.color = Color.HSVToRGB(h, s, value);
        }

        if (light2 != null)
        {
            Color.RGBToHSV(light2.color, out var h, out var s, out _);
            light2.color = Color.HSVToRGB(h, s, value);
        }
    }

    public void TurnOff()
    {
        if (cylinderRenderer != null)
            cylinderRenderer.material.color = Color.black;

        if (light1 != null)
            light1.color = Color.black;

        if (light2 != null)
            light2.color = Color.black;
    }
}


