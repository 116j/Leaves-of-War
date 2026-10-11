#if true
//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;
using ChocDino.UIFX;

namespace ChocDino.UIFX.Demos
{
	public class Demo03DropShadow : MonoBehaviour
	{
		[Header("Elements")]
		[SerializeField] DropShadowFilter _shadow = null;

		[Header("UI")]
		[SerializeField] Button _buttonDefault = null;
		[SerializeField] Button _buttonInset = null;
		[SerializeField] Button _buttonGlow = null;
		[SerializeField] Button _buttonCutout = null;
		[SerializeField] Slider _sliderColorR = null;
		[SerializeField] Slider _sliderColorG = null;
		[SerializeField] Slider _sliderColorB = null;
		[SerializeField] Slider _sliderAngle = null;
		[SerializeField] Slider _sliderDistance = null;
		[SerializeField] Slider _sliderSpread = null;
		[SerializeField] Slider _sliderBlur = null;
		[SerializeField] Slider _sliderHardness = null;
		[SerializeField] Slider _sliderSourceAlpha = null;
		[SerializeField] Slider _sliderStrength = null;

		void Start()
		{
			Time.timeScale = 1f;

			// Create UI
			_buttonDefault.onClick.AddListener(OnButtonDefault);
			_buttonInset.onClick.AddListener(OnButtonInset);
			_buttonGlow.onClick.AddListener(OnButtonGlow);
			_buttonCutout.onClick.AddListener(OnButtonCutout);
			_sliderColorR.onValueChanged.AddListener(OnSliderColorR);
			_sliderColorG.onValueChanged.AddListener(OnSliderColorG);
			_sliderColorB.onValueChanged.AddListener(OnSliderColorB);
			_sliderAngle.onValueChanged.AddListener(OnSliderAngle);
			_sliderDistance.onValueChanged.AddListener(OnSliderDistance);
			_sliderSpread.onValueChanged.AddListener(OnSliderSpread);
			_sliderBlur.onValueChanged.AddListener(OnSliderBlur);
			_sliderHardness.onValueChanged.AddListener(OnSliderHardness);
			_sliderSourceAlpha.onValueChanged.AddListener(OnSliderSourceAlpha);
			_sliderStrength.onValueChanged.AddListener(OnSliderStrength);

			// Set initial state
			OnButtonDefault();
			OnSliderColorR(_sliderColorR.value);
			OnSliderColorG(_sliderColorG.value);
			OnSliderColorB(_sliderColorB.value);
			OnSliderAngle(_sliderAngle.value);
			OnSliderDistance(_sliderDistance.value);
			OnSliderSpread(_sliderSpread.value);
			OnSliderBlur(_sliderBlur.value);
			OnSliderHardness(_sliderHardness.value);
			OnSliderSourceAlpha(_sliderSourceAlpha.value);
			OnSliderStrength(_sliderStrength.value);
		}

		void OnButtonDefault()
		{
			_shadow.Mode = DropShadowMode.Default;

			_buttonDefault.image.color = Color.yellow;
			_buttonInset.image.color = Color.white;
			_buttonGlow.image.color = Color.white;
			_buttonCutout.image.color = Color.white;
		}

		void OnButtonInset()
		{
			_shadow.Mode = DropShadowMode.Inset;

			_buttonDefault.image.color = Color.white;
			_buttonInset.image.color = Color.yellow;
			_buttonGlow.image.color = Color.white;
			_buttonCutout.image.color = Color.white;
		}

		void OnButtonGlow()
		{
			_shadow.Mode = DropShadowMode.Glow;

			_buttonDefault.image.color = Color.white;
			_buttonInset.image.color = Color.white;
			_buttonGlow.image.color = Color.yellow;
			_buttonCutout.image.color = Color.white;
		}

		void OnButtonCutout()
		{
			_shadow.Mode = DropShadowMode.Cutout;

			_buttonDefault.image.color = Color.white;
			_buttonInset.image.color = Color.white;
			_buttonGlow.image.color = Color.white;
			_buttonCutout.image.color = Color.yellow;
		}

		void OnSliderColorR(float value)
		{
			_shadow.Color = new Color(value, _shadow.Color.g, _shadow.Color.b, 1f);
		}
		void OnSliderColorG(float value)
		{
			_shadow.Color = new Color(_shadow.Color.r, value, _shadow.Color.b, 1f);
		}
		void OnSliderColorB(float value)
		{
			_shadow.Color = new Color(_shadow.Color.r, _shadow.Color.g, value, 1f);
		}

		void OnSliderAngle(float value)
		{
			_shadow.Angle = value;
		}
		void OnSliderDistance(float value)
		{
			_shadow.Distance = value;
		}
		void OnSliderSpread(float value)
		{
			_shadow.Spread = value;
		}
		void OnSliderBlur(float value)
		{
			_shadow.Blur = value;
		}
		void OnSliderHardness(float value)
		{
			_shadow.Hardness = value;
		}
		void OnSliderSourceAlpha(float value)
		{
			_shadow.SourceAlpha = value;
		}
		void OnSliderStrength(float value)
		{
			_shadow.Strength = value;
		}
	}
}

#endif