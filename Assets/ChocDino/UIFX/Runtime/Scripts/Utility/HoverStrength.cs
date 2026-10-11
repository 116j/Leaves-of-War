//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.EventSystems;

namespace ChocDino.UIFX
{
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu(FilterBase.AddComponentsUtilityPath + "UIFX - Hover Strength", 1000)]
	public class HoverStrength : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
	{
		[Header("Effects")]
		[SerializeReference] Behaviour _component = null;
		[SerializeReference] Behaviour[] _components = null;

		[Header("Triggers")]
		[SerializeField] bool _pointerEvents = true;
		[SerializeField] bool _selectEvents = true;

		[Header("Speed")]
		[SerializeField] float _upSpeed = 8f;
		[SerializeField] float _downSpeed = 6f;
		[SerializeField] bool _resetOnUp = false;
		[SerializeField] bool _resetOnDown = false;

		[Header("Range")]
		[SerializeField, Range(0f, 1f)] float _minValue = 0f;
		[SerializeField, Range(0f, 1f)] float _maxValue = 1f;

		[Header("Audio")]
		[SerializeField] AudioClip _upAudio = null;
		[SerializeField] AudioClip _downAudio = null;

		private bool _isOver = false;

		void Awake()
		{
			if (_components == null || _components.Length == 0)
			{
				if (_component == null)
				{
					// Assign the first enabled component, or the first component found.
					var components = GetComponents<Behaviour>();
					foreach (var component in components)
					{
						if (component is IEffectStrength)
						{
							if (_component == null) _component = component;
							if (component.enabled) break;
						}
					}
				}
			}
			UpdateAnimation(true);
		}

		void Start()
		{
			UpdateAnimation(true);
		}

		void Update()
		{
			UpdateAnimation(false);
		}

		void UpdateAnimation(bool force)
		{
			if (!isActiveAndEnabled) return;
			
			float target = _minValue;
			float dampSpeed = _downSpeed;
			if (_isOver)
			{
				target = _maxValue;
				dampSpeed = _upSpeed;
			}

			ApplyToComponents(dampSpeed, target, force);
		}

		private void ApplyToComponents(float dampSpeed, float target, bool force)
		{
			if (_components == null || _components.Length == 0)
			{
				ApplyToComponent(_component as IEffectStrength, dampSpeed, target, force);
			}
			else
			{
				foreach (var component in _components)
				{
					ApplyToComponent(component as IEffectStrength, dampSpeed, target, force);
				}
			}
		}

		private static void ApplyToComponent(IEffectStrength component, float dampSpeed, float target, bool force)
		{
			if (component != null && component.IsEnabled)
			{
				if (Mathf.Abs(component.Strength - target) > 0.001f)
				{
					component.Strength = MathUtils.DampTowards(component.Strength, target, dampSpeed, Time.deltaTime);
				}
				else
				{
					force = true;
				}
				if (force)
				{
					component.Strength = target;
					component.ForceUpdate();
				}
			}
		}

		private void OnOver()
		{
			_isOver = true;
			if (_resetOnUp)
			{
				ApplyToComponents(0f, 0f, true);
			}
			if (_upAudio)
			{
				AudioSource.PlayClipAtPoint(_upAudio, Vector3.zero);
			}
		}

		private void OnDown()
		{
			_isOver = false;
			if (_resetOnDown)
			{
				ApplyToComponents(0f, 0f, true);
			}
			if (_downAudio)
			{
				AudioSource.PlayClipAtPoint(_downAudio, Vector3.zero);
			}
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (_pointerEvents)
			{
				OnOver();
			}
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (_pointerEvents)
			{
				OnDown();
			}
		}

		public void OnSelect(BaseEventData eventData)
		{
			if (_selectEvents)
			{
				OnOver();
			}
		}

		public void OnDeselect(BaseEventData eventData)
		{
			if (_selectEvents)
			{
				OnDown();
			}
		}
	}
}