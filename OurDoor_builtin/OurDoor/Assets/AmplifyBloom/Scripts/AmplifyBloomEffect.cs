// Amplify Bloom - Advanced Bloom Post-Effect for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace AmplifyBloom
{
	[Serializable]
	[ExecuteAlways]
	[ImageEffectAllowedInSceneView]
	[RequireComponent( typeof( Camera ), typeof( AmplifyBloom ) )]
	[AddComponentMenu( "Image Effects/Amplify Bloom Image Effect")]
	public class AmplifyBloomEffect : MonoBehaviour
	{
		private AmplifyBloom m_bloom;
		private CommandBuffer m_cmd;

		public AmplifyBloom Bloom => m_bloom = ( m_bloom != null ) ? m_bloom : GetComponent<AmplifyBloom>();

		void OnRenderImage( RenderTexture src, RenderTexture dest )
		{
			Graphics.SetRenderTarget( dest ); // Ensure no warnings for OnRenderImage
			Bloom.RenderImage( src, dest );
		}
	}
}
