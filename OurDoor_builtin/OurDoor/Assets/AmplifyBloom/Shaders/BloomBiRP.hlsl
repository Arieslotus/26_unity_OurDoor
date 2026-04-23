// Amplify Bloom - Advanced Bloom Post-Effect for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

#ifndef AMPLIFY_BLOOMBIRP_INCLUDED
#define AMPLIFY_BLOOMBIRP_INCLUDED

#include "UnityCG.cginc"

#define ASETexDeclare(textureName) sampler2D textureName
#define ASETexPass(textureName) textureName
#define ASETexArgs(textureName) sampler2D textureName

#define ASETexLoad(textureName,uvName) tex2D(textureName,uvName)
#define ASETexSample(textureName,uvName) tex2D(textureName,uvName)

#ifndef UNITY_DECLARE_DEPTH_TEXTURE
#define UNITY_DECLARE_DEPTH_TEXTURE(tex) sampler2D_float tex
#endif

#define ASEDefineDepthTexture(textureName) UNITY_DECLARE_DEPTH_TEXTURE( textureName )

#define ASESampleDepthTexture(textureName,uvName) SAMPLE_DEPTH_TEXTURE(textureName,uvName)

#endif
