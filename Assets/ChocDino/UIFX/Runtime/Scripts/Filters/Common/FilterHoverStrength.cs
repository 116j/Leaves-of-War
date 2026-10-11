//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.EventSystems;

namespace ChocDino.UIFX
{
	[RequireComponent(typeof(RectTransform))]
	public class FilterHoverStrength : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
	{
		[Header("Triggers")]
		[SerializeField] bool _pointerEvents = true;
		[SerializeField] bool _selectEvents = true;

		[Header("Filters")]
		[SerializeField] FilterBase _filter = null;
		[SerializeField] FilterBase[] _filters = null;
		
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
			if (_filters == null || _filters.Length == 0)
			{
				if (_filter == null)
				{
					// Find the first enabled filter
					var filters = GetComponents<FilterBase>();
					foreach (var filter in filters)
					{
						if (filter.enabled)
						{
							_filter = filter;
							break;
						}
					}

					// If no filter is enabled then assign the first filter
					if (_filter == null && filters.Length > 0)
					{
						_filter = filters[0];
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

			ApplyToFilters(dampSpeed, target, force);
		}

		private void ApplyToFilters(float dampSpeed, float target, bool force)
		{
			if (_filters == null || _filters.Length == 0)
			{
				ApplyToFilter(_filter, dampSpeed, target, force);
			}
			else
			{
				foreach (var filter in _filters)
				{
					ApplyToFilter(filter, dampSpeed, target, force);
				}
			}
		}

		private static void ApplyToFilter(FilterBase filter, float dampSpeed, float target, bool force)
		{
			if (filter != null && filter.isActiveAndEnabled)
			{
				if (Mathf.Abs(filter.Strength - target) > 0.001f)
				{
					filter.Strength = MathUtils.DampTowards(filter.Strength, target, dampSpeed, Time.deltaTime);
				}
				else
				{
					force = true;
				}
				if (force)
				{
					filter.Strength = target;
					filter.ForceUpdate();
				}
			}
		}

		private void OnOver()
		{
			_isOver = true;
			if (_resetOnUp)
			{
				ApplyToFilters(0f, 0f, true);
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
				ApplyToFilters(0f, 0f, true);
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