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
        Debug.Log("NeoPixelStrip: Start called");
        InitializePixels();
    }

    public void InitializePixels()
    {
        Debug.Log("NeoPixelStrip: InitializePixels called");
        // Find all LightColorSetter components in children
        LightColorSetter[] foundPixels = GetComponentsInChildren<LightColorSetter>(true);

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
        Debug.Log($"NeoPixelStrip: SetStripColor called with color {color}");
        StopCurrentAnimation();

        for (int i = 0; i < pixels.Length; i++)
        {
            if (pixels[i] != null)
            {
                pixels[i].SetHue(color);
            }
        }
    }

    /// <summary>
    /// Sets individual colors for each pixel in the strip
    /// </summary>
    public void SetPixelColors(Color[] colors)
    {
        //Debug.Log($"NeoPixelStrip: SetPixelColors called with {colors.Length} colors");
        //StopCurrentAnimation();

        int count = Mathf.Min(colors.Length, pixels.Length);

        for (int i = 0; i < count; i++)
        {
            if (pixels[i] != null)
            {
                //if black, turn off the pixel
                if (colors[i] == Color.black)
                {
                    pixels[i].TurnDarker();
                }
                else
                {
                    pixels[i].SetHue(colors[i]);
                }
            }
        }
    }

    /// <summary>
    /// Sets the color of a specific pixel by index
    /// </summary>
    public void SetPixelColor(int index, Color color)
    {
        Debug.Log($"NeoPixelStrip: SetPixelColor called for index {index} with color {color}");
        if (index >= 0 && index < pixels.Length && pixels[index] != null)
        {
            pixels[index].SetHue(color);
        }
    }


    public void TurnOffAll()
    {
        Debug.Log("NeoPixelStrip: TurnOffAll called");
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
        Debug.Log($"NeoPixelStrip: TurnOffPixel called for index {index}");
        if (index >= 0 && index < pixels.Length && pixels[index] != null)
        {
            pixels[index].TurnOff();
        }
    }


    public void AnimateFlashing()
    {
        Debug.Log("NeoPixelStrip: AnimateFlashing called");
        StopCurrentAnimation();
        currentAnimation = StartCoroutine(FlashingAnimation(500f));
    }

    public void AnimateRainbow()
    {
        Debug.Log("NeoPixelStrip: AnimateRainbow called");
        StopCurrentAnimation();
        currentAnimation = StartCoroutine(RainbowAnimation());
    }


    public void AnimateColorWave(Color color)
    {
        Debug.Log($"NeoPixelStrip: AnimateColorWave called with color {color}");
        StopCurrentAnimation();
        currentAnimation = StartCoroutine(ColorWaveAnimation(color));
    }

    private IEnumerator FlashingAnimation(float delay)
    {
        Debug.Log($"NeoPixelStrip: FlashingAnimation started with delay {delay}ms");
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
                        pixels[i].SetHue(Color.red);
                    }
                }
            }

            isOn = !isOn;
            yield return new WaitForSeconds(delay / 1000f);
        }
    }

    private IEnumerator RainbowAnimation()
    {
        Debug.Log("NeoPixelStrip: RainbowAnimation started");
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
                    pixels[i].SetHue(pixelColor);
                }
            }

            hue = (hue + 0.01f * animationSpeed) % 1.0f;
            yield return new WaitForSeconds(0.05f);
        }
    }

    private IEnumerator ColorWaveAnimation(Color baseColor)
    {
        Debug.Log($"NeoPixelStrip: ColorWaveAnimation started with color {baseColor}");
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
                    pixels[i].SetHue(pixelColor);
                }
            }

            offset = (offset + 0.1f * animationSpeed) % (Mathf.PI * 2);
            yield return new WaitForSeconds(0.05f);
        }
    }

    private void StopCurrentAnimation()
    {
        Debug.Log("NeoPixelStrip: StopCurrentAnimation called");
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
        Debug.Log("NeoPixelStrip: StopAnimations called");
        StopCurrentAnimation();
    }
}