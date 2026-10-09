using NUnit.Framework;
using UnityEngine;

namespace Hortensia.Runtime.EditorTests
{
    public sealed class RetroUiThemeTests
    {
        [Test]
        public void PresentationSettings_ReferenceTheSharedResolutionGovernedTheme()
        {
            PresentationSettings settings =
                Resources.Load<PresentationSettings>("Narrative/PresentationSettings");
            RetroUiTheme theme = Resources.Load<RetroUiTheme>(RetroUiTheme.ResourcePath);

            Assert.That(settings, Is.Not.Null);
            Assert.That(theme, Is.Not.Null);
            Assert.That(settings.UiTheme, Is.SameAs(theme));
            Assert.That(
                theme.DiegeticScale,
                Is.EqualTo(RetroResolution.DiegeticUiScale));
            Assert.That(theme.DiegeticFontSize, Is.GreaterThan(0f));
            Assert.That(theme.PauseHeaderFontSize, Is.GreaterThan(0f));
            Assert.That(theme.PauseButtonSize.x, Is.GreaterThan(0f));
            Assert.That(theme.PauseButtonSize.y, Is.GreaterThan(0f));
        }
    }
}
