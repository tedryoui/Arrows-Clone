Shader "Custom/GridShader"
{
    Properties
    {
        _GridOffset("Grid Offset", Vector) = (0.5, 0.5, 0.0, 0.0)
        _GridLineColor("Grid Line Color", Color) = (0.7, 0.7, 0.7, 1)
        _FadeOutMin("Fade Out Min Alpha", Range(0, 1)) = 0.1
        _FadeOutMax("Fade Out Max Alpha", Range(0, 1)) = 1.0
        _GridBoundsMin("Grid Bounds Min", Vector) = (-14, -14, 0, 0)
        _GridBoundsMax("Grid Bounds Max", Vector) = (14, 14, 0, 0)
        _OrthoSize("Orthographic Size", Float) = 14
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionOS : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                float2 _GridOffset;
                half4 _GridLineColor;
                float _FadeOutMin;
                float _FadeOutMax;
                float2 _GridBoundsMin;
                float2 _GridBoundsMax;
                float _OrthoSize;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.worldPos = TransformObjectToWorld(IN.positionOS.xyz);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 worldPos = IN.worldPos.xy + _GridOffset;
                
                float gridSize = 1.0;
                if (_OrthoSize > 0)
                {
                    float steps = floor((_OrthoSize - 7.0) / 7.0);
                    steps = min(steps, 3.0);
                    gridSize = pow(2.0, steps);
                }
                
                float2 gridPos = worldPos + gridSize * 0.5;
                
                float2 gridFrac = frac(gridPos / gridSize);
                float2 gridDist = abs(gridFrac - 0.5);
                
                float gridLine = min(gridDist.x, gridDist.y);
                float grid = 1.0 - smoothstep(0.0, fwidth(gridLine) * 2.0, gridLine - 0.02);
                
                float halfGridSize = gridSize * 0.5;
                float2 halfGridPos = worldPos + halfGridSize * 0.5;
                float2 halfGridFrac = frac(halfGridPos / halfGridSize);
                float2 halfGridDist = abs(halfGridFrac - 0.5);
                float halfGridLine = min(halfGridDist.x, halfGridDist.y);
                float halfGrid = 1.0 - smoothstep(0.0, fwidth(halfGridLine) * gridSize, halfGridLine - 0.01);
                
                float2 fadeDist = 0.0;
                
                if (worldPos.x < _GridBoundsMin.x)
                    fadeDist.x = _GridBoundsMin.x - worldPos.x;
                else if (worldPos.x > _GridBoundsMax.x)
                    fadeDist.x = worldPos.x - _GridBoundsMax.x;
                    
                if (worldPos.y < _GridBoundsMin.y)
                    fadeDist.y = _GridBoundsMin.y - worldPos.y;
                else if (worldPos.y > _GridBoundsMax.y)
                    fadeDist.y = worldPos.y - _GridBoundsMax.y;
                
                float fade = 1.0;
                float2 boundsSize = _GridBoundsMax - _GridBoundsMin;
                float maxFadeDist = max(boundsSize.x, boundsSize.y) * 0.5;
                
                if (any(fadeDist > 0.0) && maxFadeDist > 0)
                {
                    float fadeFactor = saturate(length(fadeDist) / maxFadeDist);
                    fade = lerp(_FadeOutMax, _FadeOutMin, fadeFactor);
                }
                
                return half4(_GridLineColor.rgb, _GridLineColor.a * (grid + halfGrid * 0.5) * fade);
            }

            ENDHLSL
        }
    }
}