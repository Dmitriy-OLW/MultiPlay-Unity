Shader "Custom/CheckpointShader"
{
    Properties
    {
        [Header(Edge Band Settings)]
        _EdgeColor ("Edge Color", Color) = (0, 1, 0, 1)
        _EdgeWidth ("Edge Width (meters)", Range(0.01, 0.5)) = 0.1
        _EdgeSharpness ("Edge Sharpness", Range(0.5, 5)) = 2
        
        [Header(Gradient)]
        _GradientStart ("Gradient Start", Range(0, 1)) = 0.3
        _GradientEnd ("Gradient End", Range(0, 1)) = 0.8
        
        [Header(Center Texture)]
        _MainTex ("Center Texture", 2D) = "white" {}
        _TextureColor ("Texture Color", Color) = (1, 1, 1, 1)
        _TextureIntensity ("Texture Intensity", Range(0, 2)) = 1
        _TextureScale ("Texture Scale", Range(0.5, 5)) = 1
        
        [Header(Opacity)]
        _CenterAlpha ("Center Alpha", Range(0, 1)) = 0
        _EdgeAlpha ("Edge Alpha", Range(0, 1)) = 1
        
        [Header(Glow Effect)]
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.5
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "CheckpointPass"
            Tags { "LightMode" = "UniversalForward" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };
            
            // Edge Band Settings
            float4 _EdgeColor;
            float _EdgeWidth;
            float _EdgeSharpness;
            
            // Gradient Settings
            float _GradientStart;
            float _GradientEnd;
            
            // Center Texture
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _TextureColor;
            float _TextureIntensity;
            float _TextureScale;
            
            // Opacity
            float _CenterAlpha;
            float _EdgeAlpha;
            
            // Glow
            float _GlowIntensity;
            float _PulseSpeed;
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv * _TextureScale;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                // Пульсация
                float pulse = 0.7 + 0.3 * sin(_Time.y * _PulseSpeed);
                float currentGlow = _GlowIntensity * pulse;
                
                // Получаем нормаль и направление взгляда
                float3 viewDir = normalize(_WorldSpaceCameraPos - input.positionWS);
                float3 normal = normalize(input.normalWS);
                
                // ============ РАСЧЁТ РАССТОЯНИЯ ДО РЕБРА ============
                // Для куба: чем ближе нормаль к перпендикуляру взгляда, тем ближе к ребру
                float fresnel = 1.0 - abs(dot(normal, viewDir));
                
                // Преобразуем в расстояние до ребра (чем выше значение, тем ближе к ребру)
                float edgeDistance = pow(fresnel, _EdgeSharpness);
                
                // Чёткая полоса на ребре
                float edgeBand = step(_EdgeWidth * 0.5, edgeDistance);
                edgeBand = saturate(edgeBand);
                
                // Градиент от ребра к центру
                float gradient = smoothstep(_GradientStart, _GradientEnd, edgeDistance);
                
                // Комбинируем: полоса на ребре + градиент
                float edgeFactor = edgeBand * (1 - gradient * 0.7);
                
                // ============ ЦЕНТР С ТЕКСТУРОЙ ============
                float4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float3 textureColor = texColor.rgb * _TextureColor.rgb * _TextureIntensity;
                
                // Маска для текстуры: только в центре грани (подальше от рёбер)
                float textureMask = 1 - smoothstep(0.3, 0.7, edgeDistance);
                textureMask = saturate(textureMask);
                
                // ============ ФИНАЛЬНЫЙ ЦВЕТ ============
                // Цвет рёбер с градиентом
                float3 edgeColor = _EdgeColor.rgb * edgeFactor * (1 + currentGlow);
                
                // Цвет текстуры в центре
                float3 centerColor = textureColor * textureMask;
                
                // Комбинируем
                float3 finalColor = edgeColor + centerColor;
                
                // ============ АЛЬФА (ПРОЗРАЧНОСТЬ) ============
                // На рёбрах - непрозрачные, в центре - прозрачные
                float alpha = lerp(_CenterAlpha, _EdgeAlpha, edgeFactor);
                
                // Добавляем свечение к альфе
                alpha += currentGlow * 0.15;
                alpha = saturate(alpha);
                
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
        
        // Shadow caster pass
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            
            ZWrite On
            ZTest LEqual
            ColorMask 0
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    
    FallBack "Universal Render Pipeline/Unlit"
}