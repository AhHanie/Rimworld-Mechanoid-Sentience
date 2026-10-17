# Mechanoid Sentience: implementation notes

These notes record implementation choices for the mod, what was verified offline, and what still needs an in-game pass.
The completed RimHUD energy-bar plan is archived at `Docs/RimHudEnergyPlan.md`; the current plan (Biotech required, other DLCs optional) is `Plan.md`.
Target: RimWorld 1.6.4871. Biotech is required (declared in `About.xml` `modDependencies`); Ideology, Royalty, Anomaly and
Odyssey are each optional and are chosen at game startup, never toggled in a running game.

## How it works

* **Defs, generated at startup.** A prefix on `DefGenerator.GenerateImpliedDefs_PreResolve` (`SentientDefGenerator`) deep
  copies every mechanoid race (vanilla and modded) and pawn kind into `MS_Sentient_<original defName>` clones (plus per race life
  stage copies). The generator runs before the vanilla implied def generators, so corpse defs, short hashes, recipe
  links and cross references are created for the clones exactly like hand written defs. Names are deterministic, so
  saves resolve them by name. `SentientMechRegistry` holds the immutable original to sentient mapping and the
  `HashSet`s used on hot paths; `SentientMechExtension` records the original def name on every clone.
* **Clone changes.** `intelligence = Humanlike` (flesh type stays `Mechanoid`), custom think trees, `foodType = None`,
  `needsRest = false`, `mechEnabledWorkTypes` = every `WorkTypeDef`, no `CompProperties_OverseerSubject`, no flat
  `WorkSpeedGlobal` multiplier, life stages copied with a larger weapon draw distance. Body, render tree (`Misc`),
  graphics, tools, stats and other comps are the vanilla ones.
* **Conversion** (`SentientMechConverter.TryConvert`, called by the `Pawns > Make mechanoid sentient` debug action) keeps
  the same `Pawn` object: leave overseer, recruit through `SetFaction` (while still an ordinary mech), drop apparel,
  remove the live overseer comp, despawn once, swap def and kind, `CreateInitialComponents`, generate identity
  (`SentientMechGenerator`), set the player's primary ideo (only when Ideology is active), refresh needs, rebuild work
  priorities, respawn in place.
  A failure after the despawn restores def, kind, trackers, needs, the original comp list (same instances, same order) and
  faction (test with `Pawns > Make mechanoid sentient (force rollback)`, which now throws after the def swap and comp
  reconciliation so it also proves the comps roll back).
  Live comps are reconciled with the sentient def right after the def swap (see "Comps and VEF").
* **Mechanoid sentience catalyst** (`MS_MechSentienceCatalyst`, `1.6/Defs/ThingDefs_Items/Items_MechSentienceCatalyst.xml`).
  A Biotech-only item, separate from Odyssey's animal `SentienceCatalyst`; it needs no research or recipe. Interaction is the
  vanilla two-stage implant pattern: `CompUsable` + `CompTargetable_Mechanoid` (target picker; eligibility is exactly
  `SentientMechConverter.CanConvert`, any faction) start `UseItem`, whose target effect (`CompTargetEffect_MakeMechSentient`)
  queues `JobDriver_ApplyMechSentienceCatalyst` (`Jobs_MechSentienceCatalyst.xml`). That job reserves item and mech, carries
  one catalyst to the mech, waits 600 ticks, calls `TryConvert(mech, showMessages: true)` and only on success plays
  `MechSerumUsed` and destroys the one carried catalyst. A failed, rejected, or interrupted attempt leaves the item intact
  (vanilla drops a carried item when a job ends). The job also fails when `CanConvert` stops holding (mech died, deactivated,
  already converted, ...) and then reports the converter's reason.
  Values: `stackLimit` 1, market value 2500 (provisional), `techLevel` Ultra, mass 0.2, 80 hit points, flammability 0.2,
  `ItemsMisc` category, `thingSetMakerTags` = `RewardStandardLowFreq` only (the weight-1 low-frequency branch of
  `Reward_ItemsStandard`, so quest rewards), `tradeTags` = `ExoticMisc` only (exotic orbital and caravan traders; the
  outlander settlement base stock also uses this tag, so settlements can sell it too). No crate, map-gen or fishing entries.
* **Patches** live in `1.6/source/Patches` (and `1.7/source/Patches`), one class per target method in Harmony class patch style.
  In the 1.6 tree every transpiler throws if the expected IL is not found, so a game update fails loudly. The 1.7 tree uses
  Harmony 3 infix patches instead of those transpilers (see "RimWorld 1.7 port" below). The sections below describe the 1.6
  implementation unless they say otherwise; the behaviour is identical in 1.7.

## Required and optional DLC

Only Biotech is required. Sentient defs are generated, and conversion works, with Core + Harmony + Biotech alone; the
clone def names do not depend on which optional DLCs are active, so a save made with Biotech alone and one made with
more DLCs resolve the same `MS_Sentient_*` names. (Changing the DLC selection of an existing save is not covered.)

| DLC | Behavior when active | Behavior when inactive |
| --- | --- | --- |
| Ideology | Conversion needs a player primary ideoligion (`MechanoidSentience.Reject.NoIdeo` otherwise) and assigns it; tattoo and favorite color defaults are set; patches `Pawn_IdeoTracker_CertaintyChangeFactor`, `Pawn_StyleTracker_CanDesireLookChange`, `ThoughtWorker_AgeReversalDemanded_ShouldHaveThought` are applied | No ideoligion is assigned or required; tattoos use vanilla's `SetupTattoos_NoIdeology()` (the setters log an error without the DLC); the three patches are not applied |
| Royalty | `psychicEntropy` tracker is created in both `PawnComponentsUtility` patches | Runtime `ModsConfig.RoyaltyActive` checks skip the tracker |
| Anomaly | `AnomalyUtility_TryDuplicatePawn` refuses sentient mechs | Patch not applied |
| Odyssey | `CompStatue_GetAllPossiblePawnsToSelectFrom` excludes sentient mechs; the deactivated think node (`MayRequire`) is loaded | Patch not applied; node ignored by the XML loader |

Optional patch classes carry a class-level `[HarmonyPrepare]` (`ModsConfig.IdeologyActive` / `AnomalyActive` /
`OdysseyActive`), so their targets are neither resolved nor patched without the DLC. `PatchAll` still runs in the mod
constructor (the implied-def prefix has to exist before def generation); the flags are valid by then. Deliberately
shared patches stay unconditional because they also serve Core or several DLCs: `ApparelUtility_IsRequirementActive`
(Ideology roles and royal titles), `ThoughtUtility_CanGetThought` (Core, Ideology, Royalty thoughts) and the implant
patches (Biotech mechlink and Royalty neuroformer).

## Deviations from the plan

| Plan | Implementation | Reason |
| --- | --- | --- |
| Static XML clone files | Clones generated at startup with deterministic names | Concrete vanilla mech defs have no XML `Name`, so they cannot be inherited from; a copy would also drift from the game. Save identity is identical (defs are in `DefDatabase` before saves load). |
| Keep `CompProperties_OverseerSubject` on the clone, patch nothing else | Property removed from the clone; `Building_MechCharger.IsCompatibleWithCharger` patched | A live comp is then never created on load, `ScenPart_StartingMech` and other lists ignore the clone, and no load time comp stripping is needed. |
| Hidden hediff gives manipulation | Postfix on `PawnCapacityWorker_Manipulation.CalculateCapacityLevel` | `PawnCapacityUtility.CalculateCapacityLevel` skips hediff modifiers when the base level is 0, so a hediff cannot restore manipulation. The postfix only runs when the (cached) capacity is recomputed. |
| `mechFixedSkillLevel = 0` | `20`, with a transpiler on `QualityUtility.GenerateQualityCreatedByPawn` | Every skill gate (`GenConstruct`, `WorkGiver_GrowerSow`, `Bill`, `SkillRequirement`) checks the real skill tracker first; 0 would have needed three more transpilers to stop the leftover mech comparison rejecting work. |
| `PatchAll` in a queued long event | `PatchAll` in the mod constructor | The def generator hook must be applied before defs resolve. |
| Recruit after the def swap | Recruit before the swap | `Pawn.SetFaction` re-kinds humanlikes as colonists. A postfix also protects later `SetFaction` calls. |
| (not in plan) | Many extra patches from the source audit | See "Audit-driven patches". |
| Catalyst targeting: animals disabled | `canTargetAnimals = true`, `canTargetHumans = false`, validator = `CanConvert` | `TargetingParameters.CanTarget` treats a factionless mechanoid as an animal, so disabling animals would hide those mechs. The validator already rejects every non-convertible pawn. |
| Catalyst job: call `CanConvert`, then `TryConvert` | `TryConvert(mech, showMessages: true)` only | `TryConvert` runs the same check and shows the rejection itself, so a separate call would only duplicate it. |
| Catalyst job consumes the job's item target | Consumes `carryTracker.CarriedThing` (verified to be the catalyst def) | The carried stack is what was taken; avoids destroying a different item if the target reference went stale. |
| Texture "stays in the mod root" | `deploy.js` now also copies `Textures/` | The script only copied `About`, `Languages` and `1.6`, so the deployed mod would have lost the icon. |

## Comps and Vanilla Expanded Framework (optional)

No VEF reference or dependency exists; VEF types are recognized by full name only.

* **What VEF does.** VEF's `InitializeAbilityComp` (a `[StaticConstructorOnStartup]`) appends a
  `VEF.Apparels.CompProperties_ShieldBubble` with `compClass` `VEF.Abilities.CompAbilities` to every `ThingDef` whose race is
  humanlike. Sentient clones are humanlike, the original mechs are not, so each clone ends up with one more comp than its
  source. Its "already has one" guard compares a comp class with a properties type and is never true, so exactly one is added
  per run; a second initializer pass would add a second, which the validator reports.
* **Startup validator** (`SentientMechStartupValidator`) queues its work with `LongEventHandler.ExecuteWhenFinished` from its
  static constructor. `PlayDataLoader` runs all startup constructors inside one such callback, so the queued action runs after
  every constructor, whatever their order. The comp comparison is `CompListComparer.FirstDifference`: the source sequence
  minus every `CompProperties_OverseerSubject`, compared in order by property runtime type and `compClass` (never object
  identity, `DefCloner` copies the objects), optionally followed by exactly one VEF ability comp. Missing, changed,
  reordered, inserted, unexpected or repeated entries are reported with the first difference and both ordered sequences. The
  info line (debug logging) shows how many races carry the VEF comp; that count is the in-game check that validation ran after
  VEF's initializer.
* **Live conversion** (`SentientMechConverter.ReconcileComps`, matching in `CompReconciler`). A def swap does not recreate comps
  (`ThingWithComps.InitializeComps` runs only on make and load), so a freshly converted pawn would lack VEF's `CompAbilities`
  that a reloaded one has. After the swap each live comp is matched to the sentient def's properties in order by property type
  and `compClass` (k-th to k-th for duplicates); inherited instances are kept untouched (their `props` still point at the
  equivalent cloned original property, which is harmless and avoids a re-`Initialize` that could reset state), and each
  target-only property gets `Activator.CreateInstance(compClass)`, `parent`, `Initialize`, `PostPostMake`, inserted at its def
  position. The list, by-type index and `compStyleable`/`compForbiddable`/`compQuality` are rebuilt together (`SetComps`), and
  an empty list is stored as null like vanilla. A live comp with no counterpart, or a property without a usable `ThingComp`
  class, fails the conversion with a specific message and the rollback runs. The respawn that follows gives new comps their
  `PostSpawnSetup(false)`. Rollback restores the list captured before anything was removed; the lazy `Pawn.OverseerSubject`
  cache is cleared, and because the restore happens before the respawn the overseer comp's `PostSpawnSetup` runs again like
  every other original comp (its feral delay restarts).
* **Offline tests**: `tools/PatchTest/CompTest.cs` (stage `comps`) covers the comparer and the matching with real
  `CompProperties`/`ThingComp` types and stand-ins that carry VEF's full type names. It cannot cover `SpawnSetup`, a real
  `Pawn` or VEF itself.

**Hospitality guest comp (1.7 only).** Hospitality appends `Hospitality.CompProperties_Guest` to every humanlike race, so each sentient
clone (humanlike) gets it and the vanilla mechanoid does not. `CompListComparer` now permits it like the VEF comp: appended after the
source comps, at most once, in either order relative to the VEF comp, matched by full type name (properties type and comp class). Before this
the startup validator logged one drift error per race (27 in a reported log); the live conversion was never affected, because the comp
reconciler creates every target-only comp. Any other appended comp is still reported as drift. The 1.6 tree is unchanged.

## Audit-driven patches (humanlike + mechanoid + colonist + no overseer + no genes)

Rendering: silhouette (`RenderPawnAt`, `Graphic_PawnBodySilhouette`), no cached atlas frames, wound layout, portrait
framing, name label position, neutral `MS_SentientMech` body type, UI icon. Colonist behavior: draft gizmo, command range,
`CanDraftMech`, exit-map orders, auto undraft, manual verb orders, allowed area column, mechanitor right click. Items and
medicine: usable items (mechlink, neuroformer) and brain implant lookup, repair mech work giver, missing weapon repair
loop, downed rescue alert. Mood and ideology (Ideology only where noted in the DLC table): certainty curve poisoning, apparel requirements, bedroom/drug/scarification
thoughts, age reversal precept, look change requests, run-wild break, romance and marriage. Stats: age stat parts, glow,
toxic gas exposure. Misc: growth moments, genes null safety (statues, duplication, xenogerm).

## RimHUD compatibility (energy bar)

RimHUD treats a converted mech as a player humanlike, but its stock `NeedEnergy` row is `<Row Targets="Cc">`
(creatures only), so the HUD hides an energy need that exists. The vanilla Needs tab is unaffected (no mech/humanlike
filter on an existing `MechEnergy` need); if that tab is ever missing the bar, diagnose the tracker (`pawn.needs.energy`,
the `ShouldHaveNeed` prefix, other mods' need patches) instead of patching UI.

`Compatibility/RimHudEnergyRow.cs` holds the logic, optionally and through reflection only (no compile or load
dependency). The patches are ordinary class style patches picked up by `PatchAll`: `RimHud_RowLayer_Prepare`,
`RimHud_RowLayer_Draw` and `RimHud_WidgetLayer_Build`. Each one's `[HarmonyPrepare]` returns
`RimHudEnergyRow.IsAvailable`: `ModsConfig.IsActive("Jaxe.RimHUD")`
and every reflected member binding with the expected signature, evaluated once. Without RimHUD the classes are skipped
silently; with a RimHUD whose API no longer matches they are skipped with one warning, leaving the core mod untouched.

* While RimHUD runs `RowLayer.Prepare` / `RowLayer.Draw` for the selected sentient mech that has `needs.energy`, a row
  containing only `NeedEnergy` with a C-without-H mask temporarily also targets H; a Harmony finalizer restores it, so the
  layout editor and saved presets never see the change. Mixed creature rows are not touched.
* `WidgetLayer.Build` postfix: a standalone creature-targeted `NeedEnergy` widget that RimHUD replaced with a blank
  spacer is rebuilt with `HudContent.GetWidget`.
* Deviations from the plan: `BaseLayer.IsTarget` (tiny, inlinable) and `ContainerLayer<WidgetLayer>.IsVisible` (shared
  generic method; Harmony throws "The given generic instantiation was invalid") are not patched. The selected pawn is
  read from `Find.Selector` (what `Active.Pawn` does) instead of reflecting `Active.Pawn`, which throws when nothing is
  selected. There is no try/catch; `Bind()` validates every reflected member and signature once at startup instead.

**Manual workaround / fallback** (also what to do if the patch is disabled after a RimHUD update): in RimHUD's layout
editor add `H` to the row that holds `NeedEnergy` (stock `Cc` becomes `HCc`), for both the docked and floating layouts if
both are used. Do this in a user-owned preset; edits to the Workshop presets are overwritten by Steam. A mixed creature row
can instead get a separate energy-only row with `Targets="HCc"`.

`tools/PatchTest/build-and-run.ps1` has a `rimhud` stage: it binds against the installed `RimHUD.dll`, applies the three
patch classes (forcing `IsAvailable`, which needs `ModsConfig`) and checks the target mask and energy-only-row predicates (accepts `[NeedEnergy]`, rejects mixed, other and empty
rows). It skips when RimHUD is not installed.

## Known limitations (decisions, not oversights)

* A downed sentient mechanoid is not "rescued" to a bed (mechanoids cannot use beds); it is carried to a charger like any
  mech, and the colonist-needs-rescue alert is suppressed.
* Mechanoid wounds are not tendable and never heal; repair needs a colonist doing the repair mech job (now allowed for
  sentient mechanoids) or a mechanitor.
* The Mechs tab button, gravship boarding of colony mechs and the mech damaged alerts still treat a sentient mechanoid as
  a colony mech.
* Relations are not shown for non flesh pawns (vanilla `PawnRelationUtility.GetRelations`); direct relations still work.
* Modded mechanoids get clones too (any race with flesh type Mechanoid, a body and a weight class), but their bodies, graphics and comps are not guaranteed to survive the humanlike conversion; fix incompatibilities as they appear.
* A sentient mechanoid that dies still loses its built-in weapon with the corpse (vanilla behaviour; keeping it would make
  `destroyOnDrop` weapons such as the Hellsphere cannon obtainable). Only downing keeps the weapon.
* Mods that move a weapon directly in `pawn.equipment.innerContainer`, instead of through `AddEquipment` / `TryDropEquipment`, bypass the weapon lock.
* The weapon lock and the original-role skills exist in the 1.7 tree only; the 1.6 tree is unchanged.
* Saves that contain a sentient mechanoid require this mod.

## Verified offline (no game launch)

`tools/PatchTest/build-and-run.ps1` applies every patch class against the installed `Assembly-CSharp.dll`, dry runs every
transpiler (call site counts), JIT compiles the patched methods and runs `DefCloner` against real game types. Patches whose
IL cannot be read by the .NET Framework runtime outside Unity (`PawnUIOverlay`, anything touching Unity ECalls) are
reported as environment failures; they apply in game.

The five optional-DLC patch classes cannot run their `Prepare()` offline (`ModsConfig`'s static constructor needs Unity).
The harness therefore checks by IL that each `Prepare()` is a plain read of the expected `ModsConfig.*Active` flag, applies
the patch directly, and reports it as `optional ... Prepare bypassed` with its own `optional=n/5` count. This proves the
targets and patched IL are valid, not that the gate skips the patch with the DLC off; that is an in-game check (below).

## Needs an in-game pass (validation matrix from the plan)

1. Save/load spike: convert one mech, save, reload, confirm the clone defs resolve, genes stay null, skin color safe.
2. Convert hostile, player and previously overseen mechs of every body class; check identity, no feral timer, no duplicate
   pawn, repeated click is a no-op, force rollback restores.
3. Needs exactly mood, recreation, beauty, mech energy before/after save, caravan, despawn, resurrection.
4. Charging (auto, ordered, low energy shutdown, after reload and caravan return); weapon swap; apparel refused.
5. Work: cooking, research, construction, crafting, hauling, cleaning, growing, mining, medical on light and heavy bodies;
   quality follows skills.
6. Ideology (primary ideoligion, certainty, no look change or age reversal thoughts), psylink (debug and neuroformer item,
   Royalty), mechlink, reload.
7. UI: tabs, portraits of large mechs, draft controls, Work/Restrict/Assign tables.
8. Performance: 1, 10, 50 sentient mechs vs ordinary mechs; the only hot-path hook is the `JobGiver_Work.PawnCanUseWorkGiver`
   transpiler (runs only for mechanoids that would otherwise be rejected).
9. RimHUD (see above): convert a player mech and check the energy bar next to mood and recreation in the docked and
   floating HUD (Classic and Compact presets), that it follows charging and survives reload, that an ordinary colonist, a
   non sentient mech and a hostile mech get no new row, and that nothing is logged without RimHUD.
10. DLC matrix: Core + Harmony + Biotech only, then each of Ideology, Royalty, Anomaly, Odyssey alone, then all. Per
    configuration: clean startup log (no missing XML class or def cross reference, no `ModLister` Ideology error on
    conversion), clone defs exist, debug action visible, conversion succeeds with exactly four needs, charging and work,
    save/reload, the awakening incident can fire, force rollback restores the pawn. With a DLC off, its patch must be
    absent from Harmony's patched-method list (e.g. `Harmony.GetAllPatchedMethods()` owned by `sk.mechanoidsentience`).
    Also load an existing Biotech + Ideology save. Record, rather than assume, what happens when the DLC selection of an
    existing save changes.
11. After a game update re-run the offline harness and diff `MS_SentientMech` against `Humanlike.xml`.
11a. VEF comps (not yet run in game): with debug logging on, Core + Harmony + Biotech must log no `comp list differs`; with VEF
    the log must say `(24 with the optional VEF ability comp)` or however many races are affected, and no error. Dump the comp
    list of a sentient race and its original once to confirm the single appended `CompAbilities`. Convert a Militor, Lancer,
    Fabricor and a Centipede/Cyclops: before saving, `pawn.GetComp<VEF.Abilities.CompAbilities>()` is non-null, exactly one
    exists, inherited comps keep their identity and state, there is no overseer comp, and giving a VEF ability/hediff works.
    Save/reload: same comp types and order, learned abilities persist. `Make mechanoid sentient (force rollback)` with VEF: the
    original race/kind, comp instances and order, overseer behavior and position are restored and no `CompAbilities` remains.
    With a second mod that edits a comp list, the validator must name the specific difference.
12. Mechanoid sentience catalyst (not yet run in game; only the Release build and the offline harness were run):
    spawn one with dev mode, check the icon and tooltip, use it on hostile, player, overseen and deactivated mechs and on a
    human, animal, corpse and already-sentient mech (rejected, item kept); kill or deactivate the mech mid-job, block the path,
    and run `ForceFailureForTesting` (item kept); success consumes exactly one item and keeps the same pawn/ThingID; save and
    reload while the use job is queued, during each job stage and after success; generate quest rewards, exotic orbital and
    caravan stock and outlander settlements repeatedly to see it appear, and check it does not appear in ordinary stock,
    loot, ancient crates or fishing; Biotech alone, with Ideology (with and without a primary ideoligion) and with Odyssey.
    The final market value is still provisional until reward and trader generation has been observed.

## Dead Man's Switch / Fortified Features Framework (optional)

No reference to FFF or DMS exists; FFF comps are recognized by full type name (properties type + comp class) and reached by
reflection (`1.6/source/Compatibility/DeadMansSwitch.cs`). Plan: `Plan.md`. Classification (`DeadMansSwitchCompat.ClassifyRace`)
is computed once per original race while the sentient defs are generated and stored next to the registry:

| Kind | Rule | Result |
| --- | --- | --- |
| `Automatroid` | has `CompProperties_DeadManSwitch` or `CompPropertiesInteracte` | clone drops the pair; live pawn drops the pair at conversion |
| `HumanlikeAutomatroid` (Lady) | as above and the pawn class implements `Fortified.IHumanlikeMech` | also drops `CompProperties_MechApparel`; clone gets the Character and Social tabs (the Lady's tab list is `Inherit="False"` and has neither) |
| `PlatformDrone` | has `CompProperties_Drone` | clone is still generated (a released save may hold a converted drone) and unchanged; new conversions are rejected |

* **One removal rule.** `IsRemovedFromSentient(kind, signature)` drives the def generator, the startup validator
  (`CompListComparer.FirstDifference(..., isAllowedRemoval, ...)`, order still compared strictly) and the live removal.
* **Live conversion.** After `DeSpawn` and before the def swap, `WithoutOwnershipComps` (by signature, not position) trims the live
  list, `SetComps` rebuilds the index, and FFF's cached `cachedDeadManSwitch` field is cleared (FFF also refreshes it in
  `SpawnSetup`). `ConversionState` captured the full list first, so rollback (including the forced-failure action, which throws after
  this step) puts the original instances back in order. `GameComponent_DMS` is never modified.
* **Rejections** (`CanConvert`, shared by catalyst, debug action and incident): `Reject.PlatformDrone` (race kind or a live
  `CompDrone`), `Reject.DmsLeaving` (`CompDeadManSwitch.outgoing` or an entry in `GameComponent_DMS.OutgoingMeches`).
* **Skills.** `WeaponUsableMech.SpawnSetup` assigns every skill's `Level` (setter writes only `levelInt`; verified in the shipped
  IL by the `fff` test stage). `WeaponUsableMech_SpawnSetup` captures the levels of sentient pawns in a prefix and restores them in a
  postfix; ordinary FFF mechs keep FFF's reset. `HumanlikeMech.SpawnSetup` does not reset skills, so the Lady needs nothing.
* **Character tab.** `ITab_Pawn_Character_IsVisible` (`HarmonyAfter("Fortified")`, last priority) re-shows the tab for sentient
  humanlike FFF mechs only.
* **Not changed on purpose.** `SentientMechUtility.IsHumanlikeVisuals` is untouched: the original Lady is already a non-humanlike race
  and takes the same render branches, and its head comes from the FFF head node, not from `story.headType`. FFF's
  `StatWorker.IsDisabledFor` transpiler only hides *disabled* skill flags; the sentient backstories disable no work, so it masks
  nothing and no counter patch was added.
* **Deviation from the plan.** The Character tab needed more than a visibility postfix: the Lady def never lists the tab, so it is
  added to the sentient clone's `inspectorTabs`. Platform-drone clones are kept (not skipped) because 1.0.0 is already published.
* **Tests.** `tools/PatchTest/build-and-run.ps1` stages `dms` (stand-in FFF types: classification, removal, comparer, tab list,
  skill guard, patch prefix/postfix) and `fff` (separate process, real `Fortified.dll` from `tools/PatchTest/Assemblies`: member
  binding, classification with real types, skill-reset IL, both patches applied/JIT-compiled and ordered after FFF's).
  The generic stage reports 5 failures (`PawnUIOverlay_DrawPawnGUIOverlay`, `PawnWoundDrawer_WriteCache`, `PortraitsCache_Get`,
  `ThingDef_ResolveIcon`, `CompUseEffect_InstallImplant_BrainPart` dry run) that are identical on the commit before this work
  (Unity ECall / type load limits of the offline host).
* **Still needs an in-game pass** (cannot be run offline): resolved DMS def list in the debug log; converting unwoken, woken, overseen
  and hostile automatroids, Lady and Dogge; 12,000-tick and 900,000-tick DMS intervals; forced rollback on an unwoken and a woken
  mech; save/reload, caravan return; Lady Character/Social/Gear/Needs tabs, head and body at all zoom levels and portrait; the three
  platform drones rejected with the platform/power cell untouched; spontaneous awakening never picking a drone.

## Spontaneous awakening toggle (1.2.0)

* **Setting.** `ModSettings.EnableSpontaneousAwakening` (key `enableSpontaneousAwakening`, default `true`, so older settings files
  stay enabled). Mod-wide, stored in the mod config file, not the save. Checkbox in `ModSettingsWindow`; text in `common.xml`.
* **Gate.** `IncidentWorker_MechanoidAwakening.IsDisabledFor(parms)` (`!parms.forced && !setting`) is checked in both `CanFireNowSub`
  (removes the incident from the storyteller pool without scanning pawns) and `TryExecuteWorker` (queued incidents, non-forced direct
  calls and the per tick cached `CanFireNow` result). Forced incidents (developer action) still run. The catalyst, the
  `Make mechanoid sentient` debug action and existing sentient pawns are untouched; `SentientMechConverter` has no gate on purpose.
* **Validation.** Release build succeeds with 0 warnings. **Still needs an in game pass:** the checks in `Plan.md` (default with an old
  settings file, persistence across restart, disabled eligibility, a queued incident after turning it off, forced incident with the
  box off, catalyst and debug conversion with the box off).

## Mechanoid Upgrades compatibility (optional, 1.7 only)

Mechanoid Upgrades (MU, `GoGaTio.MechanoidUpgrades`) works on a sentient mechanoid without a mechanitor. MU stays optional: there is
no compile or load dependency, MU types are reached by full name and reflection (`Compatibility/MechanoidUpgrades.cs`,
`MechanoidUpgradesCompat`, same pattern as `DeadMansSwitchCompat`), every patch class has a `[HarmonyPrepare]` that returns false
unless its MU member bound, and a member that moved logs one warning and disables only that feature. MU is deliberately not in
`loadAfter` (patches and defs resolve regardless of load order). The 1.6 tree is untouched.

**Why it broke.** The clone has no `CompOverseerSubject` by design, and MU assumes every colony mech has one:
`Building_MechUpgrader.CanAcceptPawn` read `selPawn.OverseerSubject.State` (NullReferenceException, reproduced in game with the
patch disabled: the insert list, targeter, right click and `JobDriver_EnterBuilding` all go through it), and `TickInterval` treated
a null subject as a lost overseer, ejected the mech and cleared the queued operations (the upgrade silently never happened and the
items were lost). The clone also differs from the original def (a different reference, every work type) wherever MU compares races.
`CompUpgradableMechanoid` needs nothing: MU's XML patch adds it to every race with `CompProperties_OverseerSubject` before our def
generator clones the races, and the live comp (upgrades, abilities, hediffs) survives the conversion through `CompReconciler`.

**Patches** (all in `Patches/`, applied by `PatchAll`):

| Class | Technique | What it does |
| --- | --- | --- |
| `Building_MechUpgrader_CanAcceptPawn`, `Building_MechUpgrader_TickInterval` | two infixes each | MU reads `pawn.OverseerSubject` and then `.State` exactly once in each method. A postfix infix on the subject read returns a private stand-in `CompOverseerSubject` for a sentient pawn (`MechanoidUpgradesCompat.SubjectFor`), and a prefix infix on the state read skips the call for the stand-in and answers `Overseen`. Every other check in MU's methods (upgradable comp, colony mech, selected pawn, power, the tick's eject / clear / progress code) stays MU's own, and anyone else reads the real values. The stand-in never leaves those two methods. |
| `MechUpgradeDef_CanAdd` | prefix | Maps a clone race to its original (`SentientMechRegistry.OriginalRaceOf`, a dictionary probe) so white lists, mech packs, tag restrictions and the work type restrictions see the original: a sentient Lancer is offered what a Lancer is. |
| `CompUpgradableMechanoid_CompGetGizmosExtra`, `UpgradeCompTurretGun_CanShoot`, `UpgradeCompTurretGun_CompGetGizmosExtra`, `UpgradeComp_MechCarrier_CanSpawn`, `UpgradeComp_LaserDefence_CompGetGizmosExtra` | infix on `Pawn.IsColonyMechPlayerControlled` | `SentientMechUtility.IsPlayerControlledOrSentient`: force reload, turret toggle and fire at will, carrier spawn and laser defence toggle work for a sentient mech. |
| `UpgradeComp_MechCarrier_CompGetGizmosExtra`, `Bill_Improve_PawnAllowedToStartAnew` | infix on `MechanitorUtility.GetOverseer` | `CarrierGizmoOverseer`, the same non-null sentinel as the vanilla carrier (never stored): the carrier gizmo, and improver bills no longer fail with "not enough bandwidth" for a pawn without a mechlink. |
| `Building_MechUpgrader_GetInspectString`, `ScenPart_Upgrades_MechDefs` | postfix | Cosmetic: "waiting for upgrades to be configured" instead of "waiting for mechanitor"; the generated sentient kinds are hidden from MU's scenario editor. |

Not C#: `Defs/ThinkTreeDefs/SentientMech.xml` gets MU's `ThinkNode_ConditionalUpgradeNeedsReload` / `JobGiver_ReloadUpgrades` node
(`MayRequire`, ahead of emergency work in the colonist branch; MU itself only puts it in the vanilla `Mechanoid` tree), and
`Patches/MechanoidUpgrades.xml` sets `showOnHumanlikes` on the `MU_Upgradability` stat (a sentient mech is humanlike; the stat
worker still shows it only for pawns with the upgradable comp). Translations: `Languages/English/Keyed/MechanoidUpgrades.xml`.

**Fail closed.** An infix with no matching operation throws from `PatchAll` and would stop every later patch class, so
`MechanoidUpgradesCompat.OverseerInfixesSafe` counts the two reads in the real IL of both upgrader methods (`PatchProcessor.GetOriginalInstructions`)
before the infix classes' `[HarmonyPrepare]` lets them apply. If either count is not 1, one warning is logged, the infixes are skipped and
`Building_MechUpgrader_CanAcceptPawn_Guard` (an ordinary prefix) refuses a sentient mechanoid with `MechanoidSentience.MuUpgrader.Unsupported`
instead of letting MU throw or eject it and discard its queued upgrades. (An earlier version rewrote the tick with a strict IL matcher; the
infixes replace it and the hand written copy of `CanAcceptPawn`, and follow MU's own code when it changes.)

**Decisions.** A sentient mech configures its own upgrades with no mechanitor. MU's random generation is not mapped: a sentient pawn
generated directly with a sentient kind does not match `UpgradeCombinationDef.mechs` / `ScenPart_Upgrades.mechs` and falls back to
random upgrades; our scenario and incident create an ordinary mech and convert it, which keeps its upgrades. MU's Mechs tab column
for auto reload does not apply to a sentient mech (the checkbox in its upgrades tab does).

**Verified.**

* Offline (`tools/PatchTest/build-and-run-1.7.ps1`, stage `mu`, `MuCompatTest.cs`, loads the shipped `MechanoidUpgrades.dll`; `MU_DLL`
  overrides the path, skipped if absent): `Bind()` resolves every member; all 12 patch classes apply; each infix wraps exactly one
  operation of the expected kind (iterators included; the two upgrader methods have two infixes each, one per read); both upgrader
  methods read the overseer subject and its state exactly once and a method without the read counts zero (the guard's input); the guard
  prefix binds to the real `CanAcceptPawn`; both patched upgrader methods JIT compile; the identity mapping; the stand-in subject (a sentient
  pawn gets it, anyone else keeps the real value, only the stand-in's state is skipped and answered `Overseen`). The harness maps `UnityEngine.*`
  to `UnityStubs.dll` so MU's 1.6 assembly loads.
* In game, scripted (`tools/GameTest/run-autotest.ps1 -ExtraMods GoGaTio.MechanoidUpgrades`, `MechanoidUpgradesAutoTest`, Debug only,
  runs only when MU is active), Core + all DLCs + Harmony + MU + this mod: every MU upgrade is offered to every sentient race
  exactly as to its original (2160 comparisons); the sentient think tree has the reload node; an unpowered upgrader refuses with
  a reason, an ordinary unoverseen mech is refused by MU's own check, a second mech is refused while one is selected; a sentient
  Lancer is accepted, enters the upgrader, the configuration dialog opens and draws; an add operation completes (upgrade in the
  comp, mech ejected, no item spawned back); a remove operation returns the item; a long operation is not wiped by the tick;
  destroying the upgrader mid operation ejects the mech and returns the queued item; an upgrade installed before the conversion
  is the same instance after it; gizmo enumeration works. `failures=0`, and the log has only the forced-rollback `ERROR`.
  Negative control: with the `CanAcceptPawn` patch disabled the same run fails with the NullReferenceException.
  Without MU the run is unchanged (`failures=0`) and none of these classes is applied.

**Still needs a human pass** (plan checklist 4.3): the click paths (insert gizmo list, targeter, right click, drafted and
undrafted), the configuration dialog filters and load/save sets, save and load in the middle of an operation, auto and forced
reload of an ability and an ammo upgrade, turret toggle / carrier / laser defence / reactive armour, work chips on a sentient mech,
a sentient mechanitor with an upgraded ordinary mech (`Patch_CommandRange` and our `InMechanitorCommandRange` patch both postfix
the same method), save with MU then load without it, and Dead Man's Switch / RimHUD together with MU. `modVersion` and the 1.7 assembly version are 1.7.1 (`About.xml` is shared with 1.6, whose assembly stays 1.2.0.0).

## War queen: release war urchins

A player controlled sentient war queen shows the vanilla steel bar and **Release war urchins** command. The ability is a
`CompMechCarrier` command (not a `Pawn_AbilityTracker` ability) and the sentient clone keeps its original comp, steel and
cooldown, so two vanilla overseer gates are patched locally instead of faking an overseer:

* `CompMechCarrier_CanSpawn`: transpiler on the `CanSpawn` getter, its single `Pawn.IsColonyMechPlayerControlled` call becomes
  `SentientMechUtility.IsCarrierPlayerControlled` (renamed `IsPlayerControlledOrSentient` in 1.7, which also uses it for Mechanoid Upgrades; vanilla result, or a sentient colony mechanoid). All other reasons (shutdown,
  awake/downed, steel, cooldown) are the vanilla ones.
* `CompMechCarrier_CompGetGizmosExtra`: transpiler on the compiler generated `MoveNext` of the gizmo iterator (resolved with
  `AccessTools.EnumeratorMoveNext`), its single `MechanitorUtility.GetOverseer` call becomes
  `SentientMechUtility.CarrierGizmoOverseer`, which returns the pawn itself as a non-null sentinel for a sentient colony
  mechanoid. Nothing is stored and no relation is created; the global `GetOverseer` patch is unchanged.

Both use `ReplaceCalls(..., expectedCount: 1, ...)`, so a game update that changes the IL fails at startup. Spawning, refill
(`WorkGiver_HaulResourcesToCarrier`, which only needs `IsColonyMech`), the low resource alert and cleanup are vanilla.
The sentient think tree has no `JobGiver_AIReleaseMechs` branch, so the queen never releases urchins on its own.

**Offline:** both patches apply, dry run with one replacement each, and the patched `get_CanSpawn` and iterator `MoveNext` JIT
compile (`tools/PatchTest` now also dry runs patches that use `TargetMethod`/`TargetMethods`). **Needs an in game pass:** the
acceptance list in `Plan.md` (command and bar appear once, also after save/load; 3 urchins and 75 steel per release with the
900 tick cooldown; disabled reasons; refill; no AI release; ordinary unoverseen colony war queen unchanged; non-player
sentient queen gets no command).

## Skills follow the original mechanoid (1.5.0, 1.7 only)

A converted mechanoid keeps the role of the vanilla mechanoid it was; every other skill stays random.

* `SentientMechSpecialty.Resolve(pawn)` reads the *original* race and kind through `SentientMechRegistry.OriginalOf` (the clone
  itself has `mechFixedSkillLevel = 20` and every work type). Specialty skills, all at the original's `mechFixedSkillLevel`
  (10 in vanilla): the `relevantSkills` of every work type in the original's `mechEnabledWorkTypes`, plus Shooting for a ranged
  fighter (kind has `weaponTags`, the pawn carries a ranged weapon, or the race has a `CompProperties_TurretGun`), or Melee for a
  fighter with melee tools and no work types (the scyther). Tunneler: Mining. Constructoid: Construction. Agrihand: Plants.
  Fabricor: Crafting, Cooking, Intellectual. Paramedic: Medicine. Lancer, Militor, Diabolus, war queen (it has a charge blaster
  turret) and the other gun mechs: Shooting. Scyther: Melee. Lifter and cleansweeper have no skill in vanilla, so they have no
  specialty. `ClassifyCombat` is a pure function of those facts.
* The first specialty skill gets a Major passion, the others Minor (unless a trait forbids it). They are paid from the same
  passion budget as before, so the total stays comparable. A trait that conflicts with a specialty passion is never rolled.
* **All other skills use the old random roll unchanged** (random base, backstory gains, trait gains, clamped to 0 to 20 like
  vanilla): no extra cap, any level, any passion. Backstories remain random flavour.
* Existing converted mechs keep their skills (they may have trained them); only new conversions, starting pawns and rerolls use
  the rules. Dev action `Pawns > Re-roll sentient mech skills` calls `SentientMechGenerator.RegenerateSkills`.

## Built-in weapons are locked (1.5.0, 1.7 only)

* Root cause: every mechanoid kind has `destroyGearOnDrop = true`, so downing, spawning downed and losing manipulation destroyed
  the weapon, and most mech guns also have `destroyOnDrop`. `MakeSentientKind` now sets `destroyGearOnDrop = false` (kinds are
  regenerated each start, so existing saves are fixed).
* `SentientMechUtility.IsLockedWeapon(pawn, weapon)`: the pawn is a living sentient mechanoid and the weapon is built in, meaning its
  def has `destroyOnDrop` **or** shares a `weaponTag` with the pawn's kind (the Lancer's charge lance and the Tesseron's beam graser are
  real weapons without `destroyOnDrop`). A normal weapon given to a mechanoid is not locked: it can be swapped and is dropped, forbidden and
  picked up again when the mech is downed.
* Prefix patches refuse any drop or transfer of a locked weapon (`Pawn_EquipmentTracker.TryDropEquipment`,
  `TryTransferEquipmentToContainer`, `MakeRoomFor`), `AddEquipment` puts a replacement back on the ground, `EquipmentUtility.CanEquip`
  gives the disabled reason, `JobDriver_Equip.TryMakePreToilReservations` stops queued Equip jobs. Cosmetic patches (each skipped with a
  warning if its target is missing): the float menu Drop option shows disabled with the reason, the gear tab drop button shows a message,
  `JobGiver_DropWeapon` (Ideology ritual duty) gives no job, and caravan item demands never ask for a locked weapon.
* Ideology duels only pick prisoners or slaves, so no exclusion patch was needed.

## Sentient corpses and butchery (1.7 only)

* Root cause: the clones are `Humanlike`, so the vanilla corpse generator filed their corpses under `CorpsesHumanlike`. Shred and
  smash mechanoid (`fixedIngredientFilter` = `CorpsesMechanoid`/`CorpsesDrone`) could not see them, while "butcher creature" (only
  excludes the mechanoid and drone categories) accepted them.
* `MS_CorpsesSentientMech` ("sentient mechanoid corpses", `1.7/Defs/ThingCategoryDefs`) is a child of `CorpsesHumanlike`.
  `SentientCorpseCategories.Apply`, run from a postfix on `DefGenerator.GenerateImpliedDefs_PreResolve`, moves every sentient corpse
  def into it. They are still *within* `CorpsesHumanlike`, so graves, cremation, humanlike stockpiles and the colonist / stranger /
  slave special filters are unchanged. A top-level category would stop graves, sarcophagi and two Ideology buildings accepting them
  (their default or fixed filter names `CorpsesHumanlike`), so the placement is deliberate.
* **Separate recipes, not a checkbox.** `1.7/Defs/RecipeDefs/Recipes_SentientMechButchery.xml` defines `MS_ButcherCorpseSentientMech`
  ("shred sentient mechanoid") and `MS_SmashCorpseSentientMech`, children of `ButcherCorpseMechanoidBase` whose filters
  (`Inherit="False"`) contain only the sentient category, selected by default. Choosing the bill is the opt-in, and its description
  warns about the humanlike-butchery consequences. `1.7/Patches/Recipes_SentientCorpses.xml` adds them to `TableMachining` and
  `CraftingSpot` and adds the category to `ButcherCorpseFlesh.disallowedCategories`. The vanilla shred and smash recipes are untouched
  and never see sentient corpses. (An earlier iteration added the category to the vanilla recipes' fixed filter with an unchecked
  default row. It worked, but the opt-in was a hard to find checkbox under "Humanlike corpses". A `SpecialThingFilterDef` cannot do
  it: it only applies inside its `parentCategory`, and sentient corpses are in no mechanoid category.)
* **Special filters (found in play).** Vanilla's `AllowCorpsesColonist`, `AllowCorpsesStranger` and (Ideology) `AllowCorpsesSlave` have
  `allowedByDefault=false` and parent category `CorpsesHumanlike`, so every filter disallows them, and `ThingFilter.Allows(Thing)`
  then rejects any real sentient corpse (a player mech is a "colonist corpse", a hostile one a "stranger corpse") even though the
  `ThingDef` check passes. Graves work only because they have their own toggles. The sentient recipes list all three in
  `specialFiltersToAllow` of both filters. `ThingDef`-level asserts cannot see this: test with real corpses.
* `meatDef` stays `Steel` (setting `hasMeat = false` would drop the steel yield). Shredding a sentient mech still records
  `ButcheredHuman` and the humanlike butchery tale (`Corpse.ButcherProducts`): mood memories and Ideology precept reactions apply.
  Kept on purpose.
* `SentientMechStartupValidator` reports one error per race if the category, the vanilla recipe exclusions, or the sentient recipes
  (missing, not selected by default, on no work table) are wrong, which is what a non-applying patch looks like.
* `MechSentienceAutoTest.CheckSentientCorpseFilters` and `CheckRealSentientCorpses` (kills a player and a hostile sentient mech and
  asserts on the real corpses) cover the filters, steel yield, cremate / grave acceptance and new bills (pass with all DLCs and
  with Biotech only). Not covered: scribing a bill, and the manual shred pass in the plan.
* `tools/GameTest/run-autotest.ps1` writes a `Prefs.xml` (windowed 1280x720, muted, run in background) into its isolated profile, so
  the test keeps running with other windows in front. Do not minimize the game: it stops rendering and the test takes screenshots.
* 1.6 is not changed.

## WVC - Work Modes (optional, 1.7 only)

Package `wvc.sergkart.biotech.MoreMechanoidsWorkModes` (checked against v1.1.8). No compile or load dependency: types are recognized by
full name and bound by reflection (`Compatibility/WorkModes.cs`, `WorkModesCompat`). 1.6 is not changed and has no WVC support.

* **Root cause of "Could not bring the comps ... in line": Live comps with no counterpart ... CompMechSettings".**
  `SentientDefGenerator` clones every mechanoid race before any static constructor runs. WVC's
  `PostInitializationWorkModeRequirement.AutoPatch` (static constructor) then appends `CompProperties_MechSettings` to every
  mechanoid race that `HasComp<CompOverseerSubject>()`. The originals have the comp, the clones (no overseer comp, copied earlier) do
  not, so `CompReconciler` fails closed on every WVC-touched mech and the conversion rolls back. It is the Dead Man's Switch case:
  an original-only comp that is dropped on conversion.
* **One removal rule.** `SentientCompRules.IsRemovedFromSentient(dmsKind, signature)` = WVC's comp (properties type *and* comp
  class by full name) or the Dead Man's Switch rule. The def generator, the startup validator (now always passes the predicate; before,
  WVC made it report "Entry N is missing" for every overseer-capable race) and the live conversion all use it.
* **Live conversion.** `SentientMechConverter.RemoveOverseerBoundComps` runs in `SwapToSentientDefs` (so for `Convert` and
  `PrepareUnspawned`) after the Dead Man's Switch removal and before the reconcile. The WVC state of the pawn (allow shutdown,
  shutdown zone, escort target) is discarded on purpose: it configures overseer behavior the pawn no longer has. Rollback needs no new
  code (`ConversionState` captured the full comp list); after a restore `WorkModesCompat.NotifyCompsRemoved()` is called again.
* **WVC caches.** `ShutdownUtility.GetMechSettings` keeps a single-slot cache and `ThinkNode_CanShutdown` a pawn set; both can still
  reference the removed comp / pawn. `NotifyCompsRemoved` calls `ShutdownUtility.ResetAllStaticCache()` (one warning and carry on if
  that member ever changes; a conversion must not fail over a cache).
* **Mechs work tab.** WVC's Odyssey part lists `AllPawnsSpawned.Where(p => p.IsColonyMech)`, which includes sentient mechs
  (`IsColonyMech` is true for any player mechanoid), and its columns dereference `CompMechSettings` unguarded. The postfix
  `MainTabWindow_Work_Mechs_Pawns` on `MainTabWindow_Work_Mechs.get_Pawns` hides sentient pawns from that list (they have the normal
  Work tab). Applied only when the type and getter exist; one warning if the type exists without the getter.
* **Inherited auto repair.** WVC's `CompMechSettings.PostSpawnSetup` turns on vanilla `CompMechRepairable.autoRepair` for new mechs
  (setting `enableAutoRepairByDefault`). That comp is kept on conversion, so a converted mech keeps auto repair on and colonists repair
  it. Not a bug (it is inherited state); the in-game test pins it off for its downed lancer.
* **Tests.** Offline: `CompTest.RunWorkModes` (stand-in types with WVC's names, the exact reported message, comparer with the combined
  rule), and the `wvc` stage of `tools/PatchTest/build-and-run-1.7.ps1` against the real WVC dlls (full names, cache reset, tab getter,
  patch applies and JIT compiles; skipped if the dlls are absent, `WVC_DLL` / `WVC_ODYSSEY_DLL` override). In game:
  `Testing/WorkModesAutoTest.cs`, run when WVC is active (`-ExtraMods wvc.sergkart.biotech.MoreMechanoidsWorkModes`; the WVC folder must
  be in the game `Mods` folder, a junction is enough). `run-autotest.ps1` now lowercases `-ExtraMods` ids in the `ModsConfig.xml` it
  writes: `ModsConfig.IsActive` lowercases its argument, so a mixed-case id was loaded but reported inactive. Note that the script's
  non-"all" mode still ends up with every installed expansion active.
* Not covered: a real Biotech-only or Odyssey-less run (the harness cannot produce one, see above; the missing-tab path is covered
  offline), and the manual pass in `Plan.md` section 6.3.

## RimWorld 1.7 port

Two independent trees ship side by side: `1.6/` (net472, unchanged) and `1.7/` (net10.0). Both have their own `Defs` and
`Assemblies`; `About`, `Languages` and `Textures` are shared at the root. There is no `LoadFolders.xml`: 1.7 loads `1.7/` plus the
root, 1.6 loads `1.6/` plus the root. Fixes to shared logic have to be made in both trees.

* **Project.** `1.7/source/Mechanoid Sentience.csproj` is SDK style (`net10.0`, output `1.7/Assemblies`). It references the game's
  root `Assembly-CSharp.dll`, `UnityStubs.dll`, `Waylith.dll` and the Harmony 3 `0Harmony.dll` (overridable `RimWorldDir` /
  `HarmonyDll`, `Private=false`, existence checked before compiling). Only `Mechanoid Sentience.dll` is output. The source compiled
  unchanged on net10.0 with 0 warnings; no Publicizer was ever used.
* **Transpilers became infix patches.** All 15 call-swap transpilers (`SentientMechUtility.ReplaceCalls`) are now Harmony 3 infixes
  that wrap the one vanilla operation they used to replace: a postfix receives the vanilla result and returns the value the
  surrounding code should see, using the same `SentientMechUtility` helpers (now taking the vanilla result as their first
  argument). No IL is rewritten by the mod any more and no `ReplaceCalls` / `expectedCount` code remains.

  | Outer method | Wrapped operation | Sites |
  | --- | --- | --- |
  | `AutoUndrafter.ShouldAutoUndraft`, `FloatMenuOptionProvider_DraftedMove.PawnGotoAction` | getter `Pawn.IsColonyMech` | 1 each |
  | `CompMechCarrier.CanSpawn` (getter) | getter `Pawn.IsColonyMechPlayerControlled` | 1 |
  | `CompMechCarrier.CompGetGizmosExtra` (iterator, `StateMachineMoveNext`) | `MechanitorUtility.GetOverseer` (inner arg `pawn`) | 1 |
  | `CompUsable.CanBeUsedBy` | getter `RaceProperties.IsFlesh` | 1 |
  | `CompUseEffect_InstallImplant.DoEffect`, `.CanBeUsedBy`, `.GetExistingImplant` (one class each) | `BodyDef.GetPartsWithDef` | 1 each |
  | `Faction.Notify_PawnJoined`, `PawnRenderer.RenderPawnAt`, `PawnRenderer.ParallelGetPreRenderResults` | getter `RaceProperties.Humanlike` | 1 each |
  | `Graphic_PawnBodySilhouette.DrawWorker`, `PawnWoundDrawer.WriteCache` | getter `RaceProperties.Humanlike` | 2 each |
  | `FloatMenuOptionProvider.SelectedPawnValid`, `QualityUtility.GenerateQualityCreatedByPawn` | getter `RaceProperties.IsMechanoid` | 1 each |
  | `JobGiver_Work.PawnCanUseWorkGiver` (hot path) | field read `WorkGiverDef.canBeDoneByMechs`, outer `pawn` via `[HarmonyOuter]` | 1 |

  Trade-off to know: an infix wraps **every** matching operation and fails at patch time only when there is none, so the old
  "exactly N call sites" assertion no longer exists at runtime. `tools/PatchTest` now checks the counts in the table against the
  original IL of each outer method (and that Harmony registered exactly one inner patch for it), so run it after every game patch.
  The other patches (leaf prefix/postfix) gained nothing from infixes and are unchanged.

  **`VerbTracker.CreateVerbTargetCommand` is not an infix any more** (it was one on `MechanitorUtility.EverControllable`).
  Fortified Features Framework replaces that call with a transpiler, and Harmony resolves infixes after transpilers, so the infix
  found no site and FFF's `PatchAll()` threw `Cannot select Method MechanitorUtility::EverControllable` at startup. It is now a
  prefix and finalizer on `CreateVerbTargetCommand` that open a thread-static scope (`SentientMechUtility.EnterVerbCommand` /
  `ExitVerbCommand`), plus a postfix on `MechanitorUtility.EverControllable` that answers true for a sentient mechanoid only inside
  that scope. The scope matters: `CanControlMech`, `GetMechGizmos` and the caravan dialog call the same method and must not see a
  sentient mechanoid as controllable. No IL is wrapped, and FFF's helper calls the vanilla `EverControllable` first, so the postfix
  runs under it in either patch order. The 1.6 build keeps its transpiler. `RaceProperties.IsMechanoid` is not patched.
  `tools/PatchTest` stage `fffverb` applies the three classes with the installed FFF 1.7 `Fortified.dll` in both orders, checks the
  scope, and probes that an optimized caller compiled after patching still reaches the patched method (no inlining bypass).

  **`CompUsable.CanBeUsedBy` is the second FFF collision** (found in game after the first fix; 1.7.2 still threw
  `Cannot select Method RaceProperties::get_IsFlesh ... in CompUsable::CanBeUsedBy`). FFF's `Patch_CompUsable_CanBeUsedBy`
  transpiler replaces the `RaceProperties.IsFlesh` read with `IsFleshOrDeployableUser(race, comp, pawn)`. With FFF active the
  `IsFlesh` infix (`CompUsable_CanBeUsedBy`) is therefore not applied (its `Prepare` asks
  `DeadMansSwitchCompat.CompUsableIsFleshReplacement`, bound by name and signature), and `CompUsable_CanBeUsedBy_Fortified` puts a
  postfix on FFF's helper instead: a sentient race is a valid user, FFF's answer is kept for every other race. Without FFF the infix
  applies as before. Stage `fffverb` now discovers every FFF transpiler target by reflection and fails if an infix of the mod sits on
  one without being gated off, so a third collision is caught offline. FFF also transpiles `StatWorker.IsDisabledFor` and two
  `MainTabWindow_Research` methods, none of which the mod patches. Any other mod's transpiler that removes an infix's operation
  causes the same startup failure; the harness can only check mods it is given.
* **`DefCloner`** now treats every `Unity*` and `Waylith*` namespace as shared (1.7 engine types live in `UnityStubs`, `Unity.*` and
  `Waylith`, not only `UnityEngine`), so a def can never deep-copy an engine object.
* **No new try/catch** was added anywhere. `Logger` never had `[Conditional("DEBUG")]` (its output is gated by the debug logging
  setting, which is unchanged).

### Verification done for 1.7 (game 1.7.4976 rev1043, Harmony library 3.0.0.0-rc.2)

* **Offline** (`tools/PatchTest/build-and-run-1.7.ps1`, a net10 console app that shares its sources with the 1.6 harness): 58 patch
  classes plus the 5 optional-DLC ones apply with no `HarmonyException`; all 16 infix outer methods (17 before the verb command patch left the infix list) report the expected wrapped
  sites and one registered inner patch; every patched method JIT compiles; the clone, comp comparer / reconciler and Dead Man's Switch
  stand-in stages pass. The Harmony debug log shows the helper call emitted at each site (for example the `canBeDoneByMechs` read
  is followed by `ldarg 1; call Postfix`). The RimHUD and real Fortified stages are not run: no 1.7 build of either mod exists and the 1.6
  assemblies cannot load outside the game.
* **In game, scripted** (`tools/GameTest/run-autotest.ps1`, with the Debug-only `MechSentienceAutoTest` game component, enabled by
  `-mechsentienceautotest=<dir>`): launches 1.7 with an isolated profile and only Harmony plus this mod. It converts a mechanoid
  (same ThingID, `MS_Sentient_*` def, hediffs and weapon kept, humanlike trackers, player faction, no overseer, colonist bar entry),
  forces a rollback on a second one (def, ThingID, comps, hediffs, position restored, no identity left), fires the awakening
  incident, ticks 1500 ticks (no food or rest need, energy and mood present), renders a portrait, takes a screenshot, saves,
  reloads and compares the pawn's identity snapshot (name, backstories, traits, skills and passions, comps, needs). The log must
  contain no `ERROR` apart from the one forced-rollback line, no Harmony, type load, `Stub!` or material problems.
  Result (Debug build): `failures=0` and a clean log in six profiles: Core + Harmony + Biotech only, each of Ideology, Royalty,
  Anomaly and Odyssey added alone, and all expansions together. A screenshot of the converted mech on the colonist bar and in its
  info panel was inspected before and after the reload.

### Not verified (release limitations)

* `About.xml` now lists 1.7 and `modVersion` is 1.3.0 (1.7 assembly version 1.3.0.0; the 1.6 assembly stays 1.2.0.0). This was done at the
  owner's request before the human pass in `Plan.md` (work, charging, UI tabs, genes and implants, ideology, caravans, large mech
  portraits at every zoom) and before a backed-up 1.6 save with converted mechs was loaded under 1.7; both are still open.
* RimHUD, Vanilla Expanded Framework and Fortified Features / Dead Man's Switch were not run against 1.7 (no 1.7 builds). Their
  compatibility code is unchanged and still fails closed with a warning when the integration cannot bind.
* Hostile and caravan-return conversion paths, and the catalyst item use flow (`JobDriver_ApplyMechSentienceCatalyst`) were not
  driven by the script; only the converter, the incident and the save round trip were.
* The log of a Debug run shows `A hidden ritual precept was missing, adding: Funeral (no corpse)` warnings on load of the scripted
  save; they are logged by vanilla `Ideo.ExposeData` (PostLoadInit adds hidden ritual precepts the quicktest ideoligion lacks), not by this mod.
