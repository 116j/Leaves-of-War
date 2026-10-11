//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using UnityEngine;
using UnityEngine.UI;

namespace ChocDino.UIFX
{
	/// <summary>
	/// Allows a Camera to render directly to UGUI more gracefully than using doing it manually with a RenderTexture.
	/// </summary>
	[ExecuteAlways]
	[RequireComponent(typeof(CanvasRenderer)), DisallowMultipleComponent]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu(FilterBase.AddComponentsSourcesPath + "UIFX - Camera Source")]
	public class CameraSource : MaskableGraphic
	{
		[SerializeField] Camera _camera = null;

		public Camera Camera { get => _camera; set { _camera = value; ForceUpdate(); } }

		public RenderTexture Texture { get => _renderTexture; }

		private RenderTexture _renderTexture;
		private Camera _renderCamera;
		private Mesh _prefabLostSignal;

		public override Texture mainTexture => _renderTexture;

		private static RenderTextureFormat _formatSDR;
		private static RenderTextureFormat _formatHDR;

		protected override void Awake()
		{
			_formatSDR = new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.Default).colorFormat;
			_formatHDR = new RenderTextureDescriptor(1920, 1080, RenderTextureFormat.DefaultHDR).colorFormat;
			this.useLegacyMeshGeneration = false;
			base.Awake();
		}

		protected override void OnEnable()
		{
			if (_camera != null)
			{
				CreateTexture();
			}
			_prefabLostSignal = new Mesh();
			_prefabLostSignal.hideFlags = HideFlags.DontSave;
			base.OnEnable();
		}

		protected override void OnDisable()
		{
			ReleaseTexture();
			ObjectHelper.Destroy(ref _prefabLostSignal);
			base.OnDisable();
		}

		/// <summary>
		/// NOTE: OnDidApplyAnimationProperties() is called when the Animator is used to keyframe properties
		/// </summary>
		protected override void OnDidApplyAnimationProperties()
		{
			ForceUpdate();
			base.OnDidApplyAnimationProperties();
		}
		
		/// <summary>
		/// OnCanvasHierarchyChanged() is called when the Canvas is enabled/disabled
		/// </summary>
		protected override void OnCanvasHierarchyChanged()
		{
			ForceUpdate();
			base.OnCanvasHierarchyChanged();
		}

		/// <summary>
		/// OnTransformParentChanged() is called when a parent is changed, in which case we may need to get a new Canvas
		/// </summary>
		protected override void OnTransformParentChanged()
		{
			ForceUpdate();
			base.OnTransformParentChanged();
		}

		/// <summary>
		/// Forces the filter to update.  Usually this happens automatically, but in some cases you may want to force an update.
		/// </summary>
		public void ForceUpdate(bool force = false)
		{
			if (force || this.isActiveAndEnabled)
			{
				// There is no point setting the graphic dirty if it is not active/enabled (because SetMaterialDirty() will just return causing _forceUpdate to cleared prematurely)
				if (this.isActiveAndEnabled)
				{
					// We have to force the parent graphic to update so that the GetModifiedMaterial() and ModifyMesh() are called
					// TODO: This wasteful, so ideally find a way to prevent this
					this.SetMaterialDirty();
					this.SetVerticesDirty();
				}
			}
		}

		#if UNITY_EDITOR
		protected override void Reset()
		{
			base.Reset();

			// NOTE: Have to ForceUpdate() otherwise mesh doesn't update due to ModifyMesh being called multiple times a frame in this path and _lastModifyMeshFrame preventing update
			ForceUpdate();
		}
		
		protected override void OnValidate()
		{
			base.OnValidate();

			// NOTE: Have to ForceUpdate() otherwise the Game View sometimes doesn't update the rendering, even though the Scene View does...
			ForceUpdate();
		}
		#endif

		protected virtual void LateUpdate()
		{
			#if UNITY_EDITOR
			// NOTE: Since we started using [ExecuteAlways] if you have a prefab open in the editor (in Scene view) and then enter Play mode, then
			// exit play mode - Unity will DESTROY all the Object references (materials etc) in the prefab GameObjects, but it will do so
			// WITHOUT calling OnDisable()/OnDestroy() and will not call OnEnable(), so here we detect this case and manually call OnEnable().
			{
				if (this.isActiveAndEnabled && !Application.IsPlaying(this.gameObject))
				{
					if (_prefabLostSignal == null)
					{
						OnDisable();
						OnEnable();
						return;
					}
				}
			}
			#endif

			bool forceUpdate = false;

			if (CreateTexture())
			{
				forceUpdate = true;
			}
			if (_renderCamera && !_renderCamera.isActiveAndEnabled)
			{
				var prevTarget = _renderCamera.targetTexture;
				_renderCamera.targetTexture = _renderTexture;
				_renderCamera.Render();
				_renderTexture.IncrementUpdateCount();
				_renderCamera.targetTexture = prevTarget;
				forceUpdate = true;
			}

			if (forceUpdate)
			{
				ForceUpdate();
			}
		}

		bool CreateTexture()
		{
			bool resultContentChanged = false;

			// Build target texture properties
			int targetWidth = 0;
			int targetHeight = 0;
			RenderTextureFormat format = _formatSDR;
			int antiAliasing = 1;	
			if (_camera)
			{
				targetWidth = Mathf.CeilToInt(this.rectTransform.rect.width);
				targetHeight = Mathf.CeilToInt(this.rectTransform.rect.height);
				format = _camera.allowHDR ? _formatHDR : _formatSDR;
				antiAliasing = _camera.allowMSAA ? QualitySettings.antiAliasing : 1;
				antiAliasing = (antiAliasing == 0) ? 1 : antiAliasing;
			}

			// Destroy existing texture if not suitable
			if (_renderTexture)
			{
				if (_renderTexture.width != targetWidth || 
					_renderTexture.height != targetHeight ||
					_renderTexture.antiAliasing != antiAliasing ||
					_renderTexture.format != format ||
					_renderCamera != _camera)
				{
					ReleaseTexture();
					resultContentChanged = true;
				}
			}

			// Create new texture
			if (_camera && !_renderTexture && targetWidth > 0 && targetHeight > 0)
			{
				_renderTexture = RenderTexture.GetTemporary(targetWidth, targetHeight, 24, format, RenderTextureReadWrite.sRGB, antiAliasing, RenderTextureMemoryless.None, VRTextureUsage.None, _camera.allowDynamicResolution);
				_renderCamera = _camera;
				_renderCamera.targetTexture = _renderTexture;
				resultContentChanged = true;
			}

			return resultContentChanged;
		}

		void ReleaseTexture()
		{
			if (_renderTexture)
			{
				if (_renderCamera && _renderCamera.targetTexture == _renderTexture)
				{
					_renderCamera.targetTexture = null;
				}
				RenderTextureHelper.ReleaseTemporary(ref _renderTexture);
			}
			_renderCamera = null;
		}
	}
}