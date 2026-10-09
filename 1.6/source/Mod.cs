using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace Mechanoid_Sentience
{
    public class Mod : Verse.Mod
    {
        public const string HarmonyId = "sk.mechanoidsentience";

        public static ModContentPack ContentPack { get; private set; }

        public Mod(ModContentPack content) : base(content)
        {
            ContentPack = content;
            GetSettings<ModSettings>();

            new Harmony(HarmonyId).PatchAll();
        }

        public override string SettingsCategory()
        {
            return "MechanoidSentience.SettingsTitle".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            ModSettingsWindow.Draw(inRect);
            base.DoSettingsWindowContents(inRect);
        }
    }
}
