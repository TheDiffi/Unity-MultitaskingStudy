Shader "Custom/ThreeZoneCircularGradient"
{
    Properties
    {
        _Color ("Outer Color", Color) = (1,0,0,1)
        _ColorInner ("Inner Color", Color) = (1,1,1,1)
        _InnerRadius ("Inner White Radius", Range(0,1)) = 0.3
        _MiddleRadius ("Middle Gradient Radius", Range(0,1)) = 0.7
        _StretchX ("Horizontal Stretch", Range(0.1,5)) = 1.0
        _StretchY ("Vertical Stretch", Range(0.1,5)) = 1.0
        _OffsetX ("Horizontal Offset", Range(-0.5,0.5)) = 0.0
        _OffsetY ("Vertical Offset", Range(-0.5,0.5)) = 0.0
        _Glossiness ("Smoothness", Range(0,1)) = 0.5
        _Metallic ("Metallic", Range(0,1)) = 0.0
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        // Physically based Standard lighting model, and enable shadows on all light types
        #pragma surface surf Standard fullforwardshadows
        // Use shader model 3.0 target for better compatibility
        #pragma target 3.0
        sampler2D _MainTex;
        struct Input
        {
            float2 uv_MainTex;
        };
        half _Glossiness;
        half _Metallic;
        fixed4 _Color;
        fixed4 _ColorInner;
        float _InnerRadius;
        float _MiddleRadius;
        float _StretchX;
        float _StretchY;
        float _OffsetX;
        float _OffsetY;
        // Add instancing support for this shader
        UNITY_INSTANCING_BUFFER_START(Props)
            // put more per-instance properties here
        UNITY_INSTANCING_BUFFER_END(Props)
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            // Calculate distance from center with offset and stretching applied
            // 1. Center the UV coordinates (0.5, 0.5 is center)
            // 2. Apply the offset to shift the center position
            float2 centeredUV = IN.uv_MainTex - float2(0.5 + _OffsetX, 0.5 + _OffsetY);
           
            // Apply stretching by dividing the UV coordinates
            float2 stretchedUV = float2(centeredUV.x / _StretchX, centeredUV.y / _StretchY);
           
            // Calculate distance with the stretched coordinates
            float distFromCenter = length(stretchedUV);
           
            // Calculate color based on distance from center
            fixed4 finalColor;
           
            if (distFromCenter < _InnerRadius)
            {
                // Inner zone: inner color
                finalColor = _ColorInner;
            }
            else if (distFromCenter < _MiddleRadius)
            {
                // Middle zone: gradient from inner color to outer color
                float gradientPosition = (distFromCenter - _InnerRadius) / (_MiddleRadius - _InnerRadius);
                finalColor = lerp(_ColorInner, _Color, gradientPosition);
            }
            else
            {
                // Outer zone: solid color
                finalColor = _Color;
            }
           
            o.Albedo = finalColor.rgb;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Alpha = finalColor.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}