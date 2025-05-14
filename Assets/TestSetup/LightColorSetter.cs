using UnityEngine;
using System.Collections.Generic;

public class LightColorSetter : MonoBehaviour
{
    [SerializeField] private Light[] lights;
    [SerializeField] private Renderer[] renderers;

    private Color turnedOffColor = new Color(0.7f, 0.7f, 0.7f, 255);
    private Dictionary<Light, Color> originalLightColors = new Dictionary<Light, Color>();
    private Dictionary<Renderer, Color> originalRendererColors = new Dictionary<Renderer, Color>();
    private Dictionary<Renderer, Color> originalRendererInnerColors = new Dictionary<Renderer, Color>();
    private Dictionary<Light, float> originalLightValues = new Dictionary<Light, float>();
    private Dictionary<Renderer, float> originalRendererValues = new Dictionary<Renderer, float>();

    private void Start()
    {
        // If no lights are assigned, try to find lights on this GameObject or its children
        if (lights == null || lights.Length == 0)
        {
            List<Light> foundLights = new List<Light>();

            // Check if there's a light on this GameObject
            Light lightOnThis = GetComponent<Light>();
            if (lightOnThis != null)
                foundLights.Add(lightOnThis);

            // Check for lights in children
            Light[] childrenLights = GetComponentsInChildren<Light>();
            if (childrenLights.Length > 0)
                foundLights.AddRange(childrenLights);

            lights = foundLights.ToArray();
        }

        // If no renderers are assigned, try to find renderers on this GameObject or its children
        if (renderers == null || renderers.Length == 0)
        {
            List<Renderer> foundRenderers = new List<Renderer>();

            // Check if there's a renderer on this GameObject
            Renderer rendererOnThis = GetComponent<Renderer>();
            if (rendererOnThis != null)
                foundRenderers.Add(rendererOnThis);

            // Check for renderers in children
            Renderer[] childrenRenderers = GetComponentsInChildren<Renderer>();
            if (childrenRenderers.Length > 0)
                foundRenderers.AddRange(childrenRenderers);

            renderers = foundRenderers.ToArray();
        }

        // Store original colors and their HSV values
        foreach (Light light in lights)
        {
            if (light != null)
            {
                originalLightColors[light] = light.color;

                // Store original value component (brightness)
                Color.RGBToHSV(light.color, out _, out _, out float value);
                originalLightValues[light] = value;
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                originalRendererColors[renderer] = renderer.material.color;

                // Store original value component (brightness)
                Color.RGBToHSV(renderer.material.color, out _, out _, out float value);
                originalRendererValues[renderer] = value;

                // Store original inner color
                if (renderer.material.HasProperty("_ColorInner"))
                {
                    originalRendererInnerColors[renderer] = renderer.material.GetColor("_ColorInner");
                }
                else
                {
                    // Default to white if property doesn't exist
                    originalRendererInnerColors[renderer] = Color.white;
                }
            }
        }

        TurnOff();
    }

    /// <summary>
    /// Sets the color of all lights and renderers, changing only the hue while preserving saturation and value
    /// </summary>
    /// <param name="newColor">The new color to set (only hue will be used)</param>
    public void SetHue(Color newColor)
    {
        RestoreOriginalColors();
        Color.RGBToHSV(newColor, out float newHue, out _, out _);

        // Update lights
        foreach (Light light in lights)
        {
            if (light != null)
            {
                Color originalColor = originalLightColors[light];
                Color.RGBToHSV(originalColor, out _, out float saturation, out float value);
                light.color = Color.HSVToRGB(newHue, saturation, value);
            }
        }

        // Update renderers
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {

                Color originalColor = originalRendererColors[renderer];
                Color.RGBToHSV(originalColor, out _, out float saturation, out float value);
                var parsedColor = Color.HSVToRGB(newHue, saturation, value);
                renderer.material.color = parsedColor;

                // Apply the same hue transformation to inner color
                Color originalInnerColor = originalRendererInnerColors[renderer];
                Color.RGBToHSV(originalInnerColor, out _, out float innerSaturation, out float innerValue);
                var parsedInnerColor = Color.HSVToRGB(newHue, innerSaturation, innerValue);
                renderer.material.SetColor("_ColorInner", parsedInnerColor);
            }
        }
    }

    public void SetEdge(bool left)
    {
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                if (left)
                {
                    renderer.material.SetFloat("_OffsetX", 0.5f);
                    renderer.material.SetFloat("_OffsetY", 0);

                }
                else
                {
                    renderer.material.SetFloat("_OffsetX", -0.5f);
                    renderer.material.SetFloat("_OffsetY", 0);
                }
                renderer.material.SetColor("_ColorOuter", turnedOffColor);
            }
        }
    }

    /// <summary>
    /// Sets the brightness of all lights and renderers as a percentage of their original brightness
    /// </summary>
    /// <param name="brightnessPercent">Brightness percentage between 0 and 1.
    /// 0 = completely dark (black), 1 = original brightness</param>
    public void SetBrightness(float brightnessPercent)
    {
        // Clamp input to valid range
        float t = Mathf.Clamp(brightnessPercent, 0f, 1f);

        // Update lights
        foreach (Light light in lights)
        {
            if (light != null && originalLightColors.ContainsKey(light) && originalLightValues.ContainsKey(light))
            {
                // Get current color's HSV
                Color currentColor = light.color;
                Color.RGBToHSV(currentColor, out float hue, out float saturation, out _);

                // Lerp between 0 and original brightness
                float originalValue = originalLightValues[light];
                float newValue = Mathf.Lerp(0f, originalValue, t);

                // Apply new color with adjusted brightness
                light.color = Color.HSVToRGB(hue, saturation, newValue);
            }
        }

        // Update renderers
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && originalRendererColors.ContainsKey(renderer) && originalRendererValues.ContainsKey(renderer))
            {
                // Get current color's HSV
                Color currentColor = renderer.material.color;
                Color.RGBToHSV(currentColor, out float hue, out float saturation, out _);

                // Lerp between 0 and original brightness
                float originalValue = originalRendererValues[renderer];
                float newValue = Mathf.Lerp(0f, originalValue, t);

                // Apply new color with adjusted brightness
                renderer.material.color = Color.HSVToRGB(hue, saturation, newValue);

                // Apply brightness adjustment to inner color as well
                if (originalRendererInnerColors.ContainsKey(renderer))
                {
                    Color innerColor = originalRendererInnerColors[renderer];
                    Color.RGBToHSV(innerColor, out float innerHue, out float innerSaturation, out float innerValue);
                    float newInnerValue = Mathf.Lerp(0f, innerValue, t);
                    renderer.material.SetColor("_ColorInner", Color.HSVToRGB(innerHue, innerSaturation, newInnerValue));
                }
            }
        }
    }

    /// <summary>
    /// Turns off all lights and makes renderers black
    /// </summary>
    public void TurnDarker()
    {
        var color = new Color(0.5f, 0.5f, 0.5f, 255);
        TurnOff(color);
    }
    public void TurnOff() { TurnOff(turnedOffColor); }
    public void TurnOff(Color turnOffColor)
    {
        foreach (Light light in lights)
        {
            if (light != null)
            {
                light.color = Color.black;
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material.color = turnOffColor;
                renderer.material.SetColor("_ColorInner", turnOffColor);
            }
        }
    }

    public void TurnOn()
    {
        foreach (Light light in lights)
        {
            if (light != null)
            {
                light.color = originalLightColors[light];
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null)
            {
                renderer.material.color = originalRendererColors[renderer];
                renderer.material.SetColor("_ColorInner", originalRendererInnerColors[renderer]);
            }
        }
    }

    /// <summary>
    /// Restores lights and renderers to their original colors only if they've been modified
    /// </summary>
    public void RestoreOriginalColors()
    {
        foreach (Light light in lights)
        {
            if (light != null && originalLightColors.ContainsKey(light))
            {
                // Only update if the current color is different from the original
                if (light.color != originalLightColors[light])
                {
                    light.color = originalLightColors[light];
                }
            }
        }

        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && originalRendererColors.ContainsKey(renderer))
            {
                // Only update material color if it's different from the original
                if (renderer.material.color != originalRendererColors[renderer])
                {
                    renderer.material.color = originalRendererColors[renderer];
                }

                // Check if inner color needs to be updated
                if (renderer.material.HasProperty("_ColorInner") &&
                    renderer.material.GetColor("_ColorInner") != originalRendererInnerColors[renderer])
                {
                    renderer.material.SetColor("_ColorInner", originalRendererInnerColors[renderer]);
                }

                // Always reset offsets as there's no way to efficiently check if they've changed
                float currentOffsetX = 0;
                float currentOffsetY = 0;

                if (renderer.material.HasProperty("_OffsetX"))
                {
                    currentOffsetX = renderer.material.GetFloat("_OffsetX");
                }

                if (renderer.material.HasProperty("_OffsetY"))
                {
                    currentOffsetY = renderer.material.GetFloat("_OffsetY");
                }

                if (currentOffsetX != 0 || currentOffsetY != 0)
                {
                    renderer.material.SetFloat("_OffsetX", 0);
                    renderer.material.SetFloat("_OffsetY", 0);
                }
            }
        }
    }
}