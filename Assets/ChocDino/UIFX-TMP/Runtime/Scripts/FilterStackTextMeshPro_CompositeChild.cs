//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

#if UIFX_TMPRO

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;
using TMPro;

namespace ChocDino.UIFX
{
	public partial class FilterStackTextMeshPro : UIBehaviour, ICompositeChild
	{
		private ICompositeParent _compositeLayer;
		private Color _previousColor = Color.white;
		private float _previousAlpha = 1f;

		ICompositeParent ICompositeChild.GetCompositeParent()
		{
			return _compositeLayer;
		}

		void ICompositeChild.SetCompositeParent(ICompositeParent compositeParent)
		{
			if (_compositeLayer != compositeParent)
			{
				_compositeLayer = compositeParent;

				if (!CanvasUpdateRegistry.IsRebuildingGraphics() && !CanvasUpdateRegistry.IsRebuildingLayout())
				{
					CanvasUpdateRegistry.RegisterCanvasElementForGraphicRebuild(this.GraphicComponent);
				}

				var graphic = GraphicComponent;
				if (graphic != null)// && graphic.isActiveAndEnabled)
				{
					graphic.SetVerticesDirty();
					graphic.SetMaterialDirty();
				}
				MarkDirtyForParentRender(true);
			}
		}

		bool ICompositeChild.IsRenderable()
		{
			// NOTE: Check for null here to fix error "The object of type 'X' has been destroyed but you are still trying to access it."
			if (this == null) return false;
			return this.isActiveAndEnabled && _textMeshPro.enabled;
		}

		Material ICompositeChild.GetMaterial()
		{
			if (_overrideDisplayMaterial) return _overrideDisplayMaterial;
			return _displayMaterial;
		}

		Mesh ICompositeChild.GetMesh()
		{
			return _quadMesh;
		}

		Transform ICompositeChild.GetTransform()
		{
			return this.transform;
		}

		void ICompositeChild.UpdateMeshMaterial()
		{
		}

		public int GetAbsoluteDepth()
		{
			if (_textMeshPro != null)
			{
				return _textMeshPro.canvasRenderer.absoluteDepth;
			}
			return -1;
		}

		public bool IsMasked()
		{
			if (_textMeshPro != null)
			{
				return (_textMeshPro.maskable && _textMeshPro.isMaskingGraphic);
			}
			return false;
		}

		private bool CanParentRender()
		{
			return this.isActiveAndEnabled && _compositeLayer != null && _compositeLayer.IsFilterEnabled();
		}

		private void MarkDirtyForParentRender(bool force = false)
		{
			if (_compositeLayer != null)
			{
				_compositeLayer.MarkDirty(this, force);
			}
		}
	}
}

#endif