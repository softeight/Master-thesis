Shader "Unlit/NewUnlitShader"
{
    
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100 //level of details

        Pass //one render pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc" //include unity help functions

            StructuredBuffer<float4> _Colors;
            StructuredBuffer<float4> _Positions;
            
            struct appdata //vertex data give to vertex shader
            {
                float4 vertex : POSITION;  
                uint id : SV_VertexID;
            };

            struct v2f //data given from vs to fragment shader
            {             
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR0;
            };

            

            v2f vert (appdata v, uint instanceID : SV_InstanceID) // in: vertex data; out: v2f
            {     
                float4 data = _Positions[instanceID];
                float3 localPos = v.vertex.xyz * data.w; 
                float3 worldPos = data.xyz + localPos;  

                v2f o;
               // o.vertex = UnityObjectToClipPos(_Positions[instanceID]); 
                o.vertex = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                o.color = _Colors[instanceID];             
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = i.color;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
