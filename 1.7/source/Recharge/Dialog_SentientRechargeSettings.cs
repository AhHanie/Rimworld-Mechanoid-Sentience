using RimWorld;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public class Dialog_SentientRechargeSettings : Window
    {
        private const float HeaderHeight = 30f;
        private const float SliderHeight = 30f;

        private readonly CompSentientRecharge comp;
        private readonly string title;
        private readonly string text;
        private FloatRange range;

        public override Vector2 InitialSize => new Vector2(450f, 300f);

        public Dialog_SentientRechargeSettings(CompSentientRecharge comp, Pawn pawn)
        {
            this.comp = comp;
            range = comp.Thresholds;
            title = "MechanoidSentience.Recharge.DialogTitle".Translate(pawn.LabelShortCap);
            text = "MechanoidSentience.Recharge.Explanation".Translate();
            forcePause = true;
            closeOnClickedOutside = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float y = inRect.y;
            var titleRect = new Rect(inRect.x, y, inRect.width, HeaderHeight);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(titleRect, title);
            Text.Anchor = TextAnchor.UpperLeft;
            Text.Font = GameFont.Small;
            y += titleRect.height + 17f;

            var textRect = new Rect(inRect.x, y, inRect.width, Text.CalcHeight(text, inRect.width));
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(textRect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            y += textRect.height + 17f;

            Widgets.FloatRange(new Rect(inRect.x, y, inRect.width, SliderHeight), GetHashCode(), ref range, 0f, 1f, null, ToStringStyle.PercentZero, 0.05f, GameFont.Small, Color.white);
            range = SentientRechargeUtility.Sanitize(range);
            y += SliderHeight + 4f;

            Text.Anchor = TextAnchor.UpperCenter;
            Widgets.Label(new Rect(inRect.x, y, inRect.width, Text.LineHeight), "MechanoidSentience.Recharge.Pending".Translate(range.min.ToStringPercent(), range.max.ToStringPercent()));
            Text.Anchor = TextAnchor.UpperLeft;

            float buttonY = inRect.yMax - Window.CloseButSize.y;
            if (Widgets.ButtonText(new Rect(inRect.x, buttonY, Window.CloseButSize.x, Window.CloseButSize.y), "CancelButton".Translate()))
            {
                Close();
            }
            if (Widgets.ButtonText(new Rect(inRect.x + inRect.width / 2f - Window.CloseButSize.x / 2f, buttonY, Window.CloseButSize.x, Window.CloseButSize.y), "Reset".Translate()))
            {
                range = SentientRechargeUtility.DefaultThresholds;
            }
            if (Widgets.ButtonText(new Rect(inRect.xMax - Window.CloseButSize.x, buttonY, Window.CloseButSize.x, Window.CloseButSize.y), "SaveGameButton".Translate()))
            {
                comp.SetThresholds(range);
                Close();
            }
        }
    }
}
