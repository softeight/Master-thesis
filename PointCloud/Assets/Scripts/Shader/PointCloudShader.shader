Shader "Unlit/PointCloudShader"
{
   
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog
            #pragma multi_compile_instancing


            #include "UnityCG.cginc"

            StructuredBuffer<float4> _Colors;
            StructuredBuffer<float4> _Positions;
            float4x4 _RootMatrix;
            float4 average_z;

            struct appdata
            {
                float4 vertex : POSITION;
                //float2 id : SV_VertexID;

                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 color : COLOR0;

                UNITY_VERTEX_OUTPUT_STEREO
            };
           

            v2f vert (appdata v, uint instanceID : SV_InstanceID)
            {    
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v); 
                UNITY_INITIALIZE_OUTPUT(v2f, o); 
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); 

                float4 data = _Positions[instanceID];
                float z = average_z.x;
                float3 localPos = (v.vertex.xyz * data.w) + data.xyz - float3(0.0f, 0.0f, z); 
                //float4 worldPos = mul(unity_CameraToWorld, float4(cameraPos,1.0f));
                float4 worldPos = mul(_RootMatrix, float4(localPos,1.0));
               
                //o.vertex = mul(UNITY_MATRIX_P, cameraPos);
                o.vertex = mul(UNITY_MATRIX_VP, worldPos);
                o.color = _Colors[instanceID];  
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i); 
                // sample the texture
                fixed4 col = i.color;
                //col = 1 - col;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
