// Amplify Bloom - Advanced Bloom Post-Effect for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using UnityEngine;
using UnityEditor;

namespace AmplifyBloom
{
	public class About : EditorWindow
	{
		private const string AboutImageGUID = "418cb17cf3bdd854a8221f6b6c430702";
		private Vector2 m_scrollPosition = Vector2.zero;
		private Texture2D m_aboutImage;

		[MenuItem( "Window/Amplify Bloom/About...", false, 20 )]
		static void Init()
		{
			About window = ( About ) GetWindow( typeof( About ), true, "About Amplify Bloom" );
			window.minSize = new Vector2( 502, 290 );
			window.maxSize = new Vector2( 502, 290 );
			window.Show();
		}

		public void OnFocus()
		{

		}

		public void OnGUI()
		{
			m_scrollPosition = GUILayout.BeginScrollView( m_scrollPosition );

			GUILayout.BeginVertical();

			GUILayout.Space( 10 );

			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			if ( m_aboutImage == null )
			{
				string aboutImagePath = AssetDatabase.GUIDToAssetPath( AboutImageGUID );
				if ( !string.IsNullOrEmpty( aboutImagePath ) )
				{
					m_aboutImage = AssetDatabase.LoadAssetAtPath( aboutImagePath, typeof( Texture2D ) ) as Texture2D;
				}
			}
			GUILayout.Box( m_aboutImage, GUIStyle.none );

			if ( Event.current.type == EventType.MouseUp && GUILayoutUtility.GetLastRect().Contains( Event.current.mousePosition ) )
			{
				Application.OpenURL( "https://www.amplify.pt" );
			}

			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();

			GUIStyle labelStyle = new GUIStyle( EditorStyles.label );
			labelStyle.alignment = TextAnchor.MiddleCenter;
			labelStyle.wordWrap = true;

			GUILayout.Label( "\nAmplify Bloom " + VersionInfo.StaticToString(), labelStyle, GUILayout.ExpandWidth( true ) );

			GUILayout.Label( "\nCopyright (c) Amplify Creations, Lda. All rights reserved.\n", labelStyle, GUILayout.ExpandWidth( true ) );

			GUILayout.EndVertical();

			GUILayout.EndScrollView();
		}
	}
}
