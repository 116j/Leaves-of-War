//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using ChocDino.UIFX;

namespace ChocDino.UIFX.Demos
{
	[RequireComponent(typeof(RectTransform))]
	[RequireComponent(typeof(DropShadowFilter))]
	public class Demo01DropShadowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		private DropShadowFilter _shadow;
		private Graphic _graphic;
		private RectTransform _xform;
		private bool _isOver = false;
		private Color _startColor = Color.white;
		private Vector2 _startSize;

		void Awake()
		{
			_shadow = GetComponent<DropShadowFilter>();
			_graphic = GetComponent<Graphic>();
			_xform = GetComponent<RectTransform>();
			_startColor = _graphic.color;
			_startSize = _xform.sizeDelta;

			UpdateAnimation(true);
		}

		void Update()
		{
			UpdateAnimation(false);
		}

		void UpdateAnimation(bool force)
		{
			const float dampSpeedFall = 6f;
			const float dampSpeedOver = 8f;
			const float colorScale = 0.92f;

			Vector2 targetSize = _startSize;
			Color targetColor = new Color(_startColor.r * colorScale, _startColor.g * colorScale, _startColor.b * colorScale, 1f);
			float target = 0f;
			float dampSpeed = dampSpeedFall;
			if (_isOver)
			{
				target = 1f;
				targetColor = _startColor;
				targetSize = _startSize * 1.1f;
				dampSpeed = dampSpeedOver;
			}

			if (force)
			{
				_shadow.Strength = target;
				_graphic.color = targetColor;
				_xform.sizeDelta = targetSize;
			}
			else if (Mathf.Abs(_shadow.Strength - target) > 0.001f)
			{
				_shadow.Strength = MathUtils.DampTowards(_shadow.Strength, target, dampSpeed, Time.deltaTime);
				_graphic.color = MathUtils.DampTowards(_graphic.color, targetColor, dampSpeed, Time.deltaTime);
				_xform.sizeDelta = MathUtils.DampTowards(_xform.sizeDelta, targetSize, dampSpeed, Time.deltaTime);
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			_isOver = true;
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			_isOver = false;
		}
	}
}