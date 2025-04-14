Shader "Custom/OutlineShader"
{

	Properties
	{
		_Size ("Size", Float) = 1.1
		_Color ("Color", Color) = (.25, .5, .5, 1)
	}
	
    SubShader
    {
       Tags { "RenderType"="Opaque" }

        Pass {
			
            Cull Front
			Offset 100,0
			
			CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f {
                float4 pos : POSITION;
            };

            float _Size;
			float4 _Color;

            v2f vert(appdata_t v) {
				v2f o;
				float4 vertex = v.vertex;
				vertex.xyz *= _Size;
				o.pos = UnityObjectToClipPos(vertex);
				return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return _Color;
            }
            ENDCG
        }
    }
}