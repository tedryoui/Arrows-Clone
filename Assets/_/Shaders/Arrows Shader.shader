Shader "Custom/ArrowsShader"
{
    Properties
    {
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        
        _ArrowOffset("Arrow Offset", Float) = 0.2
        _ArrowSize("Arrow Size", Float) = 1
        [Vector2] _ArrowShape("Arrow Shape", Vector) = (1, -1, 0, 0)
        _ArrowCorner("Arrow Corner", Float) = 1
        
        _TrailSize("Trail Size", Float) = 1
        _TrailCorner("Trail Corner Round", Vector) = (0, 0, 0, 0)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            
            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SDF.hlsl"

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
            };
            
            struct Arrow
            {
                int      elementType; // 0 - Nip, 1 - Trail
                int elementDirection; // 0 - Vertical, 1 - Horizontal
                int elementDirectionNegate; // 0 - Up, Right; 1 - Down, Left
                float4           elementPoints; 
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _ArrowOffset;
                float _ArrowSize;
                float2 _ArrowShape;
                float _ArrowCorner;
                float _TrailSize;
                float4 _TrailCorner;
                StructuredBuffer<Arrow> _ArrowsData;
                int _ArrowsDataSize;
            CBUFFER_END 

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionOS = IN.positionOS.xyz;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }
            
            float2 Unity_Rotate_Degrees_float(float2 UV, float2 Center, float Rotation)
            {
                Rotation = Rotation * (3.1415926f/180.0f);
                UV -= Center;
                float s = sin(Rotation);
                float c = cos(Rotation);
                float2x2 rMatrix = float2x2(c, -s, s, c);
                rMatrix *= 0.5;
                rMatrix += 0.5;
                rMatrix = rMatrix * 2 - 1;
                UV.xy = mul(UV.xy, rMatrix);
                UV += Center;
                return  UV;
            }
            
            float sdfNip(Arrow data, Varyings input)
            {
                float3 objectPos = input.positionOS;
                
                int negateModifier = (data.elementDirectionNegate) ? 1 : -1;
                
                float2 arrowOffset = float2(0.0, 0.0);
                switch (data.elementDirection)
                {
                case 0:
                    arrowOffset.y = _ArrowOffset * negateModifier;
                    break;
                case 1:
                    arrowOffset.x = _ArrowOffset * negateModifier;
                    break;
                }
                
                float rotateDegrees = data.elementDirectionNegate == 0 ? 0 : -180;
                if (data.elementDirection == 1)
                    rotateDegrees -= 90;
                
                float3 worldPos = TransformObjectToWorld(objectPos);
                float2 arrowPos = -data.elementPoints.xy;
                float3 combinedPos = worldPos + float3(arrowPos, 0.0) + float3(arrowOffset, 0.0);
                
                float2 nArrowShape = normalize(_ArrowShape);
                float2 bArrowShape = float2(1, -1) * (_ArrowSize * 0.5);
                float2 arrowShape  = nArrowShape * bArrowShape;
                
                combinedPos = float3(Unity_Rotate_Degrees_float(combinedPos, float2(0, 0), rotateDegrees), 0.0);
                
                float value = 0.0;
                sdRoundedIsoscelesTriangle_float(combinedPos, arrowShape, _ArrowCorner * 0.115, value);
                
                float ddxy = abs(ddx(value)) + abs(ddy(value));
                float smoothValue = smoothstep(0, ddxy, value);
                
                return 1.0 - smoothValue;
            } 
            
            float sdfTrail(Arrow data, Varyings input)
            {
                float3 objectPos = input.positionOS;
                
                float2 from = -data.elementPoints.xy;
                float2 to = -data.elementPoints.zw;
                
                float3 worldPos = TransformObjectToWorld(objectPos);
                float2 length = to - from;
                float2 lengthDelta = abs(length);
                float3 combinedPos = worldPos + float3(from, 0.0) + float3(length * 0.5, 0.0);
                
                float value = 0.0;
                sdRoundedBox_float(combinedPos, lengthDelta * 0.5 + _TrailSize * 0.115, _TrailCorner * 0.115, value);
                
                float ddxy = abs(ddx(value)) + abs(ddy(value));
                float smoothValue = smoothstep(0, ddxy, value);
                
                return 1.0 - smoothValue;
            }
            
            half4 frag(Varyings IN) : SV_Target 
            {
                float value = 0.0f;
                
                for (int i = 0; i < _ArrowsDataSize; i++)
                {
                    Arrow arrowData = _ArrowsData[i];
                    
                    if (arrowData.elementType == 0)  
                        value += sdfNip(arrowData, IN);
                    else 
                        value += sdfTrail(arrowData, IN);
                }
                
                return half4(_BaseColor.rgb, _BaseColor.a * (value > 0.0f) ? 1.0f : 0.0f); 
            }
            
            ENDHLSL
        }
    }
}
