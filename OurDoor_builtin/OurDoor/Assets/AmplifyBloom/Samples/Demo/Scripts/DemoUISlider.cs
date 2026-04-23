// Amplify Bloom - Advanced Bloom Post-Effect for Unity
// Copyright (c) Amplify Creations, Lda <info@amplify.pt>

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AmplifyBloom
{
	public sealed class DemoUISlider : DemoUIElement, IPointerDownHandler, IPointerUpHandler
	{
		public bool SingleStep = false;
		private Slider m_slider;
		private bool m_lastStep = false;
		private bool m_draggingSlider;

		private static List<DemoUISlider> s_allSliders = new List<DemoUISlider>();

		void Start()
		{
			m_slider = GetComponent<Slider>();
			s_allSliders.Add( this );
		}

		public override void DoAction( DemoUIElementAction action, params object[] vars )
		{
			if ( !m_slider.IsInteractable() )
				return;

			if ( action == DemoUIElementAction.Slide )
			{
				float slideAmount = ( float ) vars[ 0 ];
				if ( SingleStep )
				{
					if ( m_lastStep )
					{
						return;
					}
					m_lastStep = true;
				}

				if ( m_slider.wholeNumbers )
				{
					if ( slideAmount > 0 )
					{
						m_slider.value += 1;
					}
					else if ( slideAmount < 0 )
					{
						m_slider.value -= 1;
					}
				}
				else
				{
					m_slider.value += slideAmount;
				}
			}
		}

		public override void Idle()
		{
			m_lastStep = false;
		}

		public void OnPointerDown( PointerEventData eventData )
		{
			m_draggingSlider = true;
		}

		public void OnPointerUp( PointerEventData eventData )
		{
			m_draggingSlider = false;
		}

		public static bool IsAnyDragging()
		{
			bool dragging = false;
			foreach ( var slider in s_allSliders )
			{
				if ( slider.m_draggingSlider )
				{
					dragging = true;
				}
			}
			return dragging;
		}
	}
}