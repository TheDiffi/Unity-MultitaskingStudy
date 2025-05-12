using System.Collections;
using UnityEngine;

public class NeoPixelStrip : MonoBehaviour
{
    [SerializeField] private float animationSpeed = 1.0f;

    [field: SerializeField]
    public int PixelCount { get; private set; } = 45;

    private LightColorSetter[] pixels;
    private Coroutine currentAnimation;

    void Start()
    {
        InitializePixels();
    }

    public void InitializePixels()
    {
        // Find all LightColorSetter components in children
        LightColorSetter[] foundPixels = GetComponentsInChildren<LightColorSetter>();

        // Validate that we have the expected number of pixels
        if (PixelCount > 0 && foundPixels.Length != PixelCount)
        {
            Debug.LogWarning($"NeoPixelStrip expected {PixelCount} pixels but found {foundPixels.Length}");
        }

        // Set the actual pixel count to what we found
        PixelCount = foundPixels.Length;
        pixels = foundPixels;

        Debug.Log($"NeoPixelStrip initialized with {PixelCount} pixels");
    }

    /// <summary>
    /// Sets the same color for all pixels in the strip
    /// </summary>
    public void SetStripColor(Color color)
    {
        StopCurrentAnimation();

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] != null)
            {
                pixels[i].SetColor(color);
            }
        }
    }

    /// <summary>
    /// Sets individual colors for each pixel in the strip
    /// </summary>
    public void SetPixelColors(Color[] colors)
    {
        StopCurrentAnimation();

        int count = Mathf.Min(colors.Length, pixels.Length);

        for (int i = 0; i < count; i++)
        {
            if (pixels[i] != null)
            {
                //if black, turn off the pixel
                if (colors[i] == Color.black)
                {
                    pixels[i].TurnOff();
                }
                else
                {
                    pixels[i].SetColor(colors[i]);
                }
            }
        }
    }

    /// <summary>
    /// Sets the color of a specific pixel by index
    /// </summary>
    public void SetPixelColor(int index, Color color)
    {
        if (index >= 0 && index < pixels.Length && pixels[index] != null)
        {
            pixels[index].SetColor(color);
        }
    }

    public void SetLeftEdge(int index)
    {
        if (index >= 0 && index < pixels.Length && pixels[index] != null)
        {
            pixels[index].SetEdge(true);
        }
    }
    
    public void SetRightEdge(int index)
    {
        if (index >= 0 && index < pixels.Length && pixels[index] != null)
        {
            pixels[index].SetEdge(false);
        }
    }


    public void TurnOffAll()
    {
        StopCurrentAnimation();

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] != null)
            {
                pixels[i].TurnOff();
            }
        }
    }

    public void TurnOffPixel(int index)
    {
        if (index >= 0 && index < pixels.Length && pixels[index] != null)
        {
            pixels[index].TurnOff();
        }
    }


    public void AnimateFlashing()
    {
        StopCurrentAnimation();
        currentAnimation = StartCoroutine(FlashingAnimation(500f));
    }

    public void AnimateRainbow()
    {
        StopCurrentAnimation();
        currentAnimation = StartCoroutine(RainbowAnimation());
    }


    public void AnimateColorWave(Color color)
    {
        StopCurrentAnimation();
        currentAnimation = StartCoroutine(ColorWaveAnimation(color));
    }

    private IEnumerator FlashingAnimation(float delay)
    {
        bool isOn = false;

        while (true)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] != null)
                {
                    if (isOn)
                    {
                        pixels[i].TurnOff();
                    }
                    else
                    {
                        pixels[i].SetColor(Color.red);
                    }
                }
            }

            isOn = !isOn;
            yield return new WaitForSeconds(delay);
        }
    }
    private IEnumerator RainbowAnimation()
    {
        float hue = 0f;

        while (true)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] != null)
                {
                    // Calculate hue offset based on position in strip
                    float pixelHue = (hue + i / (float)pixels.Length) % 1.0f;
                    Color pixelColor = Color.HSVToRGB(pixelHue, 1.0f, 1.0f);
                    pixels[i].SetColor(pixelColor);
                }
            }

            hue = (hue + 0.01f * animationSpeed) % 1.0f;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator ColorWaveAnimation(Color baseColor)
    {
        // Extract hue from base color
        Color.RGBToHSV(baseColor, out float baseHue, out float baseSaturation, out float baseValue);

        float offset = 0f;

        while (true)
        {
            for (int i = 0; i < pixels.Length; i++)
            {
                if (pixels[i] != null)
                {
                    // Calculate position in wave (0 to 1)
                    float wave = Mathf.Abs(Mathf.Sin(i / (float)pixels.Length * Mathf.PI * 2 + offset));
                    // Apply the wave to the value component
                    Color pixelColor = Color.HSVToRGB(baseHue, baseSaturation, baseValue * wave);
                    pixels[i].SetColor(pixelColor);
                }
            }

            offset = (offset + 0.1f * animationSpeed) % (Mathf.PI * 2);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void StopCurrentAnimation()
    {
        if (currentAnimation != null)
        {
            StopCoroutine(currentAnimation);
            currentAnimation = null;
        }
    }

    /// <summary>
    /// Stops any ongoing animations
    /// </summary>
    public void StopAnimations()
    {
        StopCurrentAnimation();
    }
}