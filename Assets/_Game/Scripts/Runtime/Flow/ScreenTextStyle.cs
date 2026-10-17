using TMPro;
using UnityEngine;

namespace Hortensia.Runtime
{
    [RequireComponent(typeof(TMP_Text))]
    public sealed class ScreenTextStyle : MonoBehaviour
    {
        private TMP_Text text;
        private float size;

        public static void Attach(TMP_Text target, float baseSize)
        {
            ScreenTextStyle style = target.GetComponent<ScreenTextStyle>() ?? target.gameObject.AddComponent<ScreenTextStyle>();
            style.text = target;
            style.size = baseSize;
            style.Apply();
        }

        private void OnEnable() => GameSettings.Applied += Apply;

        private void OnDisable() => GameSettings.Applied -= Apply;

        private void OnDestroy() => GameSettings.Applied -= Apply;

        private void Apply()
        {
            if (text == null)
                return;
            TextStyle style = GameSettings.Current.screenStyle;
            text.font = MenuLook.Current.HintFont;
            text.fontSize = size * style.scale;
            TextStyling.Apply(text, style, style.textColor, MenuLook.Current.hintBold);
        }
    }
}
