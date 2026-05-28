Shader "Custom/3DFrame"
{
    Properties
    {
        _FrameColor ("Frame Color", Color) = (1, 1, 1, 1)
        _EdgeSize ("Edge Size", Range(0.001, 0.2)) = 0.05
        _Sharpness ("Sharpness", Range(1, 50)) = 10
    }
    
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 100
        
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 localPos : TEXCOORD0;
            };
            
            fixed4 _FrameColor;
            float _EdgeSize;
            float _Sharpness;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float3 pos = abs(i.localPos); // Локальные координаты от -0.5 до 0.5
                
                // Определяем расстояние до каждой из 6 граней
                float distX = abs(pos.x - 0.5);
                float distY = abs(pos.y - 0.5);
                float distZ = abs(pos.z - 0.5);
                
                // Находим минимальное расстояние до любой грани
                float minDist = min(min(distX, distY), distZ);
                
                // Определяем, насколько мы близко к ребру
                // Рёбра - это места, где ДВА расстояния до граней малы одновременно
                
                // Проверяем все комбинации:
                float edgeXY = (1.0 - saturate(distX / _EdgeSize)) * 
                              (1.0 - saturate(distY / _EdgeSize));
                float edgeXZ = (1.0 - saturate(distX / _EdgeSize)) * 
                              (1.0 - saturate(distZ / _EdgeSize));
                float edgeYZ = (1.0 - saturate(distY / _EdgeSize)) * 
                              (1.0 - saturate(distZ / _EdgeSize));
                
                // Комбинируем
                float edgeFactor = max(max(edgeXY, edgeXZ), edgeYZ);
                
                // Увеличиваем резкость
                edgeFactor = pow(edgeFactor, _Sharpness);
                
                fixed4 col = _FrameColor;
                col.a = edgeFactor;
                
                return col;
            }
            ENDCG
        }
    }
}