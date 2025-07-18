  Shader "Unlit/ComputeBufferPointCloud" {
    Properties {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
     
    SubShader {
        Tags{
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
        }
        Cull Back
        Lighting Off
        ZWrite On
        Blend One OneMinusSrcAlpha
        Pass {
            CGPROGRAM
            // Upgrade NOTE: excluded shader from OpenGL ES 2.0 because it uses non-square matrices
            #pragma exclude_renderers gles

            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5

            #pragma multi_compile __ CROSS_SECTION_ON 
            #pragma multi_compile __ PER_FRAME_COLOR 
            #pragma multi_compile __ PER_FRAME_SCALE
            #pragma multi_compile __ CONSTANT_SCALE
 
            #include "UnityCG.cginc"
 
            sampler2D _MainTex;
 
            // xy for position, z for rotation, and w for scale
            StructuredBuffer<float4> frameBuffer;
            StructuredBuffer<float4> colorsBuffer;

            float4x4 transform;
            float diskSize;
 
            struct v2f{
                float4 pos : SV_POSITION;
                float2 uv: TEXCOORD0;
                float3 vertexLocal : TEXCOORD1;
                fixed4 color : COLOR0;
            };
 
            v2f vert (appdata_full v, uint instanceID : SV_InstanceID) {
                float4 current = frameBuffer[instanceID];
                v2f o;

                float scale;
                #if CONSTANT_SCALE
                    scale = diskSize;
                #else
                    scale = current.w;
                #endif

                // get the appropriate offset for our vertex to billboard it towards camera
                float3 vpos = mul((float3x3)unity_ObjectToWorld, v.vertex.xyz*scale);

                // get the correct data position in local space
                float4 pointpos = float4(current.xyz,1);

                // convert data from local space to world space
                float4 scaled = mul(transform, pointpos);

                // add in billboard adjustment, adjust for camera
                float4 viewPos = mul(UNITY_MATRIX_V, scaled) + float4(vpos, 0);

                // adjust for clip
                float4 outPos = mul(UNITY_MATRIX_P, viewPos);

                // pass into struct for fragment
                o.pos = outPos;
                o.vertexLocal = pointpos.xyz;
                o.color = colorsBuffer[instanceID];
                
                UNITY_TRANSFER_FOG(o, o.vertex);

                // XY here is the dimension (width, height). 
                // ZW is the offset in the texture (the actual UV coordinates)
                // o.uv =  v.texcoord * uv.xy + uv.zw;
                o.uv = v.texcoord- 0.5;
                return o;
            }
 
            fixed4 frag (v2f i) : SV_Target{

                fixed4 col = i.color;
                float dist = length(i.uv);
                float pwidth = fwidth(dist);
                float alpha = smoothstep(0.5, 0.5 - pwidth * 1.5, dist);

                clip(alpha - 50.0 / 255.0);
                col.rgb *= alpha;

                return col;
            }
 
            ENDCG
        }
    }
}