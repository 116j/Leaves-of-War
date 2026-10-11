//--------------------------------------------------------------------------//
// Copyright 2023-2026 Chocolate Dinosaur Ltd. All rights reserved.         //
// For full documentation visit https://www.chocolatedinosaur.com           //
//--------------------------------------------------------------------------//

namespace ChocDino.UIFX
{
	/// <summary>Which delta time to use for updating animations.</summary>
	public enum TimeDeltaMode
	{
		/// <summary>Use Time.deltaTime</summary>
		Normal,
		/// <summary>Use Time.unscaledDeltaTime, but use Time.captureDeltaTime if it is set.</summary>
		Unscaled,
	}

	/// <summary>Which vertex modifiers affect are used to calculate the vertex modifier effect.</summary>
	public enum VertexModifierSource
	{
		/// <summary>Only Transform changes affect the effect.</summary>
		Transform,
		/// <summary>Only vertex changes (usually through IMeshModifier effects) affect the effect.</summary>
		Vertex,
		/// <summary>Both Transform changes and vertex changes (usually through IMeshModifier effects) affect the effect.  This is the most expensive mode.</summary>
		TranformAndVertex,
	}

	/// <summary>Modes describing how a gradient wraps.</summary>
	public enum GradientWrapMode
	{
		/// <summary>No wrapping, edge values will be used.</summary>
		None,
		/// <summary>The gradient repeats.</summary>
		Wrap,
		/// <summary>The gradient repeats with mirroring.</summary>
		Mirror,
	}

	/// <summary>How to much downsample the texture by.</summary>
	public enum Downsample
	{
		/// <summary>Automatic downsampling will depend on the platform.</summary>
		Auto = 0,
		/// <summary>No downsampling.</summary>
		None = 1,
		/// <summary>Downsample to half the size.</summary>
		Half = 2,
		/// <summary>Downsample to a quarter the size.</summary>
		Quarter = 4,
		/// <summary>Downsample to an eighth the size.</summary>
		Eighth = 8,
	}

	/// <summary>
	/// Interface for controlling whether or not an effect/filter is applied and how strongly it is applied.
	/// </summary>
	public interface IEffectStrength
	{
		/// <summary>Strength 0.0 means the effect is not applied, Strength 1.0 means the effect is fully applied.</summary>
		float Strength { get; set; }

		/// <summary>Basically `Component.enabled`.</summary>
		bool IsEnabled { get; }

		/// <summary>Force rendering update.</summary>
		void ForceUpdate();
	}
}