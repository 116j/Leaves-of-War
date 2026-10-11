//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityInternal = UnityEngine.Internal;

// TODO: option to sort layers by Z value - this is useful because in zoom situations you want the trail to be rendered above the original UI element sometimes
// either that, or option to force reverse of order so trail is always on top..

// TODO: have option for trail to NOT follow...but instead be left behind and then die naturally after some time?

namespace ChocDino.UIFX
{
	/// <summary>
	/// This component is an effect for uGUI visual components which renders a trail that follows
	/// the motion of the component.
	/// </summary>
	/// <inheritdoc/>
	[ExecuteAlways]
	[RequireComponent(typeof(Graphic))]
	[HelpURL("https://www.chocdino.com/products/unity-assets/")]
	[AddComponentMenu(FilterBase.AddComponentsEffectsPath + "UIFX - Trail")]
	public partial class TrailEffect : TrailEffectBase
	{
		// Copy of the current frame vertices
		private List<UIVertex> _vertices;

		private class TrailLayer
		{
			internal UIVertex[] vertices;
			internal Matrix4x4 matrix;
			internal Color color;
			internal float alpha;
		}

		// TrailLayer index 0..trailCount order = newest..oldest, front..back
		private List<TrailLayer> _layers = new List<TrailLayer>(16);

		// Output vertices
		private List<UIVertex> _outputVerts;

		[UnityInternal.ExcludeFromDocs]
		protected override void OnEnable()
		{
			ResetMotion();
			SetDirty();

			if (MaskableGraphicComponent)
			{
				MaskableGraphicComponent.onCullStateChanged.AddListener(OnCullingChanged);
			}

			base.OnEnable();
		}

		private void OnCullingChanged(bool culled)
		{
			// If a Rect2DMask is used, culling occurs on the MaskableGraphic which will cause culling
			// which causes the trail logic to stop running, so we have to force disable culling if it is applied.
			// TODO: Find a more elegant solution for this
			if (culled)
			{
				CanvasRenderComponent.cull = false;
			}
		}

		[UnityInternal.ExcludeFromDocs]
		protected override void OnDisable()
		{
			if (MaskableGraphicComponent)
			{
				MaskableGraphicComponent.onCullStateChanged.RemoveListener(OnCullingChanged);
			}

			SetDirty();
			base.OnDisable();
		}

		/// <inheritdoc/>
		public override void ResetMotion()
		{
			_layers = null;
		}

		private bool HasGeometryToProcess()
		{
			return (_vertices != null && _vertices.Count > 0);
		}

		[UnityInternal.ExcludeFromDocs]
		public override void ModifyMesh(VertexHelper vh)
		{
			if (CanApply())
			{
				StoreOriginalVertices(vh);

				if (HasGeometryToProcess())
				{
					// Detected changes to vertex count or effective layer count
					/*if (_outputVerts != null)
					{
						int vertexCount = vh.currentIndexCount;
						int layerCount = _layerCount + (_showTrailOnly ? 0 : 1);
						int trailVertexCount = vertexCount * layerCount;
						if (trailVertexCount != _outputVerts.Count)
						{
							_isPrepared = false;
						}
					}*/

					//if (!_isPrepared)
					{
						PrepareTrail();
					}

					InterpolateTrail();
					UpdateTrailColors();

					GenerateTrailGeometry(vh);
				}
				else
				{
					ResetMotion();
				}
			}
			else if (_layerCount <= 0)
			{
				PrepareTrail();
			}
		}

		void LateUpdate()
		{
			if (CanApply() && HasGeometryToProcess())
			{
				// Update gradient animation
				if (_gradientOffsetSpeed != 0f)
				{
					_gradientOffset += DeltaTime * _gradientOffsetSpeed;
				}

				// TODO: Only dirty when state (transform/vertices) changes
				SetDirty();
			}
		}
		
		protected override void SetDirty()
		{
			GraphicComponent.SetVerticesDirty();
		}

		void StoreOriginalVertices(VertexHelper vh)
		{
			if (_vertices != null && vh.currentIndexCount != _vertices.Capacity)
			{
				_vertices = null;
			}
			if (_vertices == null)
			{
				_vertices = new List<UIVertex>(vh.currentIndexCount);
			}
			vh.GetUIVertexStream(_vertices);

			if (IsTrackingTransform() && IsTrackingVertices())
			{
				// Convert to world space
				int vertexCount = _vertices.Count;
				for (int i = 0; i < vertexCount; i++)
				{
					UIVertex vv = _vertices[i];
					vv.position = this.transform.localToWorldMatrix.MultiplyPoint3x4(vv.position);
					_vertices[i] = vv;
				}
			}
		}

		private void SetupLayer(TrailLayer layer, int layerIndex)
		{
			if (IsTrackingTransform())
			{
				if (layerIndex > 0)
				{
					// Use the last layer matrix
					layer.matrix = _layers[layerIndex - 1].matrix;
				}
				else
				{
					// Use the current matrix
					layer.matrix = this.transform.localToWorldMatrix;
				}
			}
			if (IsTrackingVertices())
			{
				SetupLayerVertices(layer, layerIndex);
			}
		}

		private void SetupLayerVertices(TrailLayer layer, int layerIndex)
		{
			// Generate and prime layer vertex arrays
			if (layer.vertices == null)
			{
				layer.vertices = new UIVertex[_vertices.Count];
				if (layerIndex > 0)
				{
					// Use the last layer vertices
					_layers[layerIndex - 1].vertices.CopyTo(layer.vertices, 0);
				}
				else
				{
					// Use the current vertices
					_vertices.CopyTo(layer.vertices);
				}
			}
			// If vertex count has changed, try to preserve existing vertex data
			else if (layer.vertices.Length != _vertices.Count)
			{
				UIVertex[] oldVertices = layer.vertices;
				layer.vertices = new UIVertex[_vertices.Count];

				if (layer.vertices.Length > 0)
				{	
					if (layerIndex == 0)
					{
						_vertices.CopyTo(layer.vertices);
					}
					else
					{
						// Copy from old vertices
						System.Array.Copy(oldVertices, 0, layer.vertices, 0, Mathf.Min(layer.vertices.Length, oldVertices.Length));
				
						// More vertices added
						if (layer.vertices.Length > oldVertices.Length)
						{
							// Copy from new vertices
							if (layerIndex > 0)
							{
								// Use the last layer vertices
								System.Array.Copy(_layers[layerIndex - 1].vertices, oldVertices.Length, layer.vertices, oldVertices.Length, (_vertices.Count - oldVertices.Length));
								_vertices.CopyTo(oldVertices.Length, layer.vertices, oldVertices.Length, (_vertices.Count - oldVertices.Length));
							}
							else
							{
								// Use the current vertices
								_vertices.CopyTo(oldVertices.Length, layer.vertices, oldVertices.Length, (_vertices.Count - oldVertices.Length));
							}
						}
					}
				}
			}
		}

		private void AddTrailLayer()
		{
			var layer = new TrailLayer();
			SetupLayer(layer, _layers.Count);
			_layers.Add(layer);
		}

		protected override void OnChangedVertexModifier()
		{
			if (_layers != null)
			{
				for (int i = 0; i < _layers.Count; i++)
				{
					SetupLayer(_layers[i], i);
				}
			}
		}

		private void PrepareTrail()
		{
			// Update vertices on existing layers if the count has changed
			if (_layers != null && _layers.Count > 0 && _vertices != null)
			{
				if (_layers[0].vertices != null && _layers[0].vertices.Length != _vertices.Count && IsTrackingVertices())
				{
					for (int i = 0; i < _layers.Count; i++)
					{
						SetupLayerVertices(_layers[i], i);
					}
				}
			}

			// Add / Remove trail layers
			if (_layers != null && _layers.Count != _layerCount)
			{
				int layersToRemove = _layers.Count - _layerCount;
				for (int i = 0; i < layersToRemove; i++)
				{
					_layers.RemoveAt(_layers.Count - 1);
				}
				int layersToAdd = _layerCount - _layers.Count;
				for (int i = 0; i < layersToAdd; i++)
				{
					AddTrailLayer();
				}
			}

			// Create trail layers
			if (_layers == null)
			{
				_layers = new List<TrailLayer>(_layerCount);
				for (int i = 0; i < _layerCount; i++)
				{
					AddTrailLayer();
				}
			}
		}

		void InterpolateTrail()
		{
			float tStart = Mathf.Clamp01(MathUtils.GetDampLerpFactor(_dampingFront, DeltaTime));
			float tEnd = Mathf.Clamp01(MathUtils.GetDampLerpFactor(_dampingBack, DeltaTime));

			if (_strength < 1f && _strengthMode == TrailStrengthMode.Damping)
			{
				tStart = Mathf.LerpUnclamped(1f, tStart, _strength);
				tEnd = Mathf.LerpUnclamped(1f, tEnd, _strength);
			}

			float tStep = 0f;
			if (_layerCount > 0)
			{
				tStep = (tEnd - tStart) / _layerCount;
			}

			if (IsTrackingTransform())
			{
				float t = tStart;
				// first trail layer chases the original (current) matrix
				Matrix4x4 targetMatrix = this.transform.localToWorldMatrix;
				for (int j = 0; j < _layerCount; j++)
				{
					TrailLayer layer = _layers[j];
					MathUtils.LerpUnclamped(ref layer.matrix, targetMatrix, t, true);

					// other trail layers chase the previous matrix
					targetMatrix = _layers[j].matrix;

					t += tStep;
				}

				// This makes the trail not follow (perhaps add this as an option in the future?):
				/*for (int j = (_layerCount - 1); j >= 0; j--)
				{
					if (j == 0)
					{
						_layers[j].matrix = this.transform.localToWorldMatrix;
					}
					else
					{
						_layers[j].matrix = _layers[j-1].matrix;
					}
				}*/
			}

			if (IsTrackingVertices())
			{
				float t = tStart;
				// first trail layer vertices chase the original vertices
				IList<UIVertex> targetVertices = _vertices;
				for (int j = 0; j < _layerCount; j++)
				{
					TrailLayer layer = _layers[j];
					
					int vertexCount = _vertices.Count;
					for (int i = 0; i < vertexCount; i++)
					{
						UIVertex source = layer.vertices[i];
						UIVertex target = targetVertices[i];
						target.position = Vector3.LerpUnclamped(source.position, target.position, t);
						// NOTE: In most cases interpolating UVs doesn't make sense, for example with text.. perhaps in the future this can be exposed as an option.
						//target.uv0 = Vector2.LerpUnclamped(source.uv0, target.uv0, t);
						target.color = Color.LerpUnclamped(source.color, target.color, t);
						layer.vertices[i] = target;
					}
					// other trail layers chase the previous vertices
					targetVertices = _layers[j].vertices;

					t += tStep;
				}
			}
		}

		void UpdateTrailColors()
		{
			for (int j = 0; j < _layerCount; j++)
			{
				TrailLayer layer = _layers[j];

				float t = 0f;
				if (_layerCount > 1)
				{
					// Prevent divide by zero
					t = j / (float)(_layerCount - 1);
				}

				layer.color = Color.white;
				if (_gradient != null)
				{
					layer.color = ColorUtils.EvalGradient(t, _gradient, GradientWrapMode.Mirror, _gradientOffset, _gradientScale);
				}
				
				layer.alpha = 1f;
				if (_alphaCurve != null)
				{
					layer.alpha = _alphaCurve.Evaluate(t);
				}

				if (_strength < 1f)
				{
					if (_strengthMode == TrailStrengthMode.Fade)
					{
						layer.alpha *= _strength;
					}
					else if (_strengthMode == TrailStrengthMode.Layers)
					{
						layer.alpha = t < _strength ? layer.alpha : 0f;
					}
					else if (_strengthMode == TrailStrengthMode.FadeLayers)
					{
						float step = 1f / _layerCount;
						float tmin = _strength - step;
						float tmax = _strength;
						float tt = 1f - Mathf.InverseLerp(tmin, tmax, t);
						layer.alpha *= tt;
					}
				}
			}
		}

		void GenerateTrailGeometry(VertexHelper vh)
		{
			int vertexCount = _vertices.Count;

			int trailVertexCount = vertexCount * _layerCount;
			if (!_showTrailOnly)
			{
				trailVertexCount += vertexCount;
			}
			
			if (_outputVerts != null && _outputVerts.Capacity != trailVertexCount)
			{
				_outputVerts = null;
			}
			if (_outputVerts == null)
			{
				_outputVerts = new List<UIVertex>(trailVertexCount);
			}

			_outputVerts.Clear();

			Matrix4x4 worldToLocal = this.transform.worldToLocalMatrix;

			// Add trail vertices (in back-to-front order for correct alpha blending)
			for (int j = (_layerCount - 1); j >= 0; j--)
			{
				TrailLayer layer = _layers[j];

				if (IsTrackingTransform())
				{
					if (IsTrackingVertices())
					{
						for (int i = 0; i < vertexCount; i++)
						{
							UIVertex vv = layer.vertices[i];

							vv.position = worldToLocal.MultiplyPoint3x4(vv.position);
							Color cc = ColorUtils.Blend(vv.color, layer.color, _blendMode);
							cc.a *= layer.alpha;
							vv.color = cc;
							_outputVerts.Add(vv);
						}
					}
					else
					{
						Matrix4x4 localToWorld = layer.matrix;
						Matrix4x4 xform = worldToLocal * localToWorld;
						for (int i = 0; i < vertexCount; i++)
						{
							UIVertex vv = _vertices[i];
							vv.position = xform.MultiplyPoint3x4(vv.position);
							Color cc = ColorUtils.Blend(vv.color, layer.color, _blendMode);
							cc.a *= layer.alpha;
							vv.color = cc;
							_outputVerts.Add(vv);
						}
					}
				}
				else
				{
					for (int i = 0; i < vertexCount; i++)
					{
						UIVertex vv = layer.vertices[i];
						Color cc = ColorUtils.Blend(vv.color, layer.color, _blendMode);
						cc.a *= layer.alpha;
						vv.color = cc;
						_outputVerts.Add(vv);
					}
				}
			}

			if (!_showTrailOnly)
			{
				AddOriginalVertices(vertexCount, worldToLocal);
			}

			// NOTE: despite its name, VertexHelper.AddUIVertexTriangleStream() actually replaces the vertices, it doesn't add to them
			vh.AddUIVertexTriangleStream(_outputVerts);
		}

		void AddOriginalVertices(int vertexCount, Matrix4x4 worldToLocal)
		{
			if (IsTrackingTransform() && IsTrackingVertices())
			{
				for (int i = 0; i < vertexCount; i++)
				{
					UIVertex vv = _vertices[i];
					vv.position = worldToLocal.MultiplyPoint3x4(vv.position);
					_outputVerts.Add(vv);
				}
			}
			else
			{
				_outputVerts.AddRange(_vertices);
			}
		}
	}
}