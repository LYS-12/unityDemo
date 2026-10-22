Shader "ForestParty/GhostPearl"
{
    Properties
    {
        _TopColor ("Head Pearl",Color)=(1,.92,.97,1)
        _BottomColor ("Skirt Lavender",Color)=(.42,.40,.76,1)
        _RimColor ("Soft Rim",Color)=(.65,.84,1,1)
        _Opacity ("Opacity",Range(.15,1))=.98
        _GhostBaseY ("Ghost World Base",Float)=0
        _GhostHeight ("Ghost Height",Float)=2.15
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; float height:TEXCOORD2; };
            CBUFFER_START(UnityPerMaterial)
            half4 _TopColor,_BottomColor,_RimColor;
            half _Opacity;
            float _GhostBaseY,_GhostHeight;
            CBUFFER_END
            Varyings vert(Attributes input)
            {
                Varyings o; o.positionWS=TransformObjectToWorld(input.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(input.normalOS); o.height=(o.positionWS.y-_GhostBaseY)/max(.01,_GhostHeight); return o;
            }
            half4 frag(Varyings input):SV_Target
            {
                half3 normal=normalize(input.normalWS); Light light=GetMainLight();
                half3 view=normalize(GetWorldSpaceViewDir(input.positionWS));
                half rim=pow(1-saturate(abs(dot(normal,view))),2.4);
                half shade=.72+.28*saturate(dot(normal,light.direction));
                half3 pearl=lerp(_BottomColor.rgb,_TopColor.rgb,smoothstep(.05,.95,input.height));
                return half4(pearl*shade+_RimColor.rgb*rim*.32,_Opacity);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
