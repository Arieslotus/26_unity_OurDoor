Shader "Custom/RimLight"
{
    Properties
    {
        [Header(Rim)]
        _Color("Color", Color) = (1,1,1,1)
        _Emiss("Emiss", Float) = 1     // 边缘光强度
        _RimPower("RimPower", Float) = 1 // 边缘光对比度

        [Space(20)]
        [Header(Flow)]
        _FlowTex("FlowTex", 2D) = "white" {}
        _FlowSpeed("FlowSpeed", Vector) = (0.0, 1.0, 0.0, 0.0)
        _FlowIntensity("FlowIntensity", Float) = 1
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        LOD 200

        // 深度写入 Pass
        Pass
        {
            Name "DepthPass"

            Cull Off
            ZWrite On
            ColorMask 0

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;

            float4 vert(float4 vertexPos : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertexPos);
            }

            float4 frag() : SV_Target
            {
                return _Color;
            }
            ENDCG
        }

        // 主体 Rim + Flow Pass
        Pass
        {
            Name "RimPass"

            Tags { "LightMode" = "ForwardBase" } // Built-in 主光照 Pass

            ZWrite Off
            Blend SrcAlpha One   // 叠加式发光
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv     : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float3 worldNormal : TEXCOORD2;
                float3 localPos : TEXCOORD3;
            };

            sampler2D _FlowTex;
            float4 _FlowTex_ST;
            float4 _Color;
            float _Emiss, _RimPower;
            float4 _FlowSpeed;
            float _FlowIntensity;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.localPos = v.vertex.xyz;
                o.uv = TRANSFORM_TEX(v.uv, _FlowTex);
                
                // 计算世界空间法线
                o.worldNormal = normalize(mul((float3x3)unity_WorldToObject, v.normal)); // 注意 Built-in 使用 _World2Object
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - worldPos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(i.viewDir);

                // Fresnel 边缘光
                float rim = 1.0 - saturate(dot(n, v));
                float fresnel = pow(rim, _RimPower);

                half3 baseColor = _Color.rgb * _Emiss;
                half alpha = saturate(fresnel * _Emiss);

                // Flow 动画
                half2 uv_flow = i.localPos.xy + _FlowSpeed.xy * _Time.y;
                half4 flow = tex2D(_FlowTex, uv_flow * _FlowTex_ST.xy) * _FlowIntensity;

                // 混合
                half3 finalColor = baseColor + flow.rgb;
                half finalAlpha = saturate(flow.r + alpha);

                return half4(finalColor, finalAlpha);
            }
            ENDCG
        }
    }

    FallBack "Transparent/Diffuse"
}