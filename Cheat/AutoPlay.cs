using HarmonyLib;
using MAI2.Util;
using Manager;
using Monitor;
using System;
using System.Collections.ObjectModel;
using static NoteJudge;
using MelonLoader;

namespace SinmaiAssist.Cheat
{
    public class AutoPlay
    {
        private static readonly System.Reflection.FieldInfo NoteBaseAppearMsecFieldInfo = AccessTools.Field(typeof(NoteBase), "AppearMsec");
        private static readonly System.Reflection.FieldInfo NoteBaseIsExNoteFieldInfo = AccessTools.Field(typeof(NoteBase), "IsExNote");
        private static readonly System.Reflection.MethodInfo NoteBasePlayJudgeSeMethodInfo = AccessTools.Method(typeof(NoteBase), "PlayJudgeSe");
        private static readonly System.Reflection.FieldInfo NoteBaseJudgeResultFieldInfo = AccessTools.Field(typeof(NoteBase), "JudgeResult");
        private static readonly System.Reflection.MethodInfo SlideRootIsNoteCheckTimeStartMethodInfo = AccessTools.Method(typeof(SlideRoot), "IsNoteCheckTimeStart");
        private static readonly System.Reflection.FieldInfo SlideRootJudgeTimingDiffMsecFieldInfo = AccessTools.Field(typeof(SlideRoot), "JudgeTimingDiffMsec");
        private static readonly System.Reflection.FieldInfo SlideRootJudgeResultFieldInfo = AccessTools.Field(typeof(SlideRoot), "JudgeResult");
        private static readonly System.Reflection.FieldInfo SlideRootTailMsecFieldInfo = AccessTools.Field(typeof(SlideRoot), "TailMsec");
        private static readonly System.Reflection.FieldInfo SlideRootlastWaitTimeFieldInfo = AccessTools.Field(typeof(SlideRoot), "lastWaitTime");
        private static readonly System.Reflection.FieldInfo SlideRootJudgeTypeFieldInfo = AccessTools.Field(typeof(SlideRoot), "JudgeType");
        private static readonly System.Reflection.FieldInfo SlideRootlastWaitTimeForJudgeFieldInfo = AccessTools.Field(typeof(SlideRoot), "lastWaitTimeForJudge");
        private static readonly System.Reflection.MethodInfo NoteBaseIsNoteCheckTimeStartMethodInfo = AccessTools.Method(typeof(NoteBase), "IsNoteCheckTimeStart");
        private static readonly System.Reflection.FieldInfo NoteBaseJudgeTimingDiffMsecFieldInfo = AccessTools.Field(typeof(NoteBase), "JudgeTimingDiffMsec");
        private static readonly System.Reflection.FieldInfo NoteBaseJudgeTypeFieldInfo = AccessTools.Field(typeof(NoteBase), "JudgeType");
        private static readonly System.Reflection.FieldInfo TouchNoteBTouchAreaFieldInfo = AccessTools.Field(typeof(TouchNoteB), "TouchArea");
        private static readonly System.Reflection.PropertyInfo NoteBaseButtonIdProperty = AccessTools.Property(typeof(NoteBase), "ButtonId");
        private static readonly System.Reflection.MethodInfo TouchNoteBPlayJudgeSeMethodInfo = AccessTools.Method(typeof(TouchNoteB), "PlayJudgeSe");

        [HarmonyPrepare]
        public static bool Prepare()
        {
            bool ready = NoteBaseButtonIdProperty != null && NoteBaseJudgeTypeFieldInfo != null && NoteBaseAppearMsecFieldInfo != null &&
                SlideRootJudgeTypeFieldInfo != null && SlideRootlastWaitTimeForJudgeFieldInfo != null &&
                TouchNoteBTouchAreaFieldInfo != null && NoteBasePlayJudgeSeMethodInfo != null &&
                SlideRootIsNoteCheckTimeStartMethodInfo != null && SlideRootlastWaitTimeFieldInfo != null &&
                NoteBaseJudgeResultFieldInfo != null && NoteBaseIsNoteCheckTimeStartMethodInfo != null &&
                SlideRootTailMsecFieldInfo != null && NoteBaseJudgeTimingDiffMsecFieldInfo != null &&
                NoteBaseIsExNoteFieldInfo != null && TouchNoteBPlayJudgeSeMethodInfo != null &&
                SlideRootJudgeTimingDiffMsecFieldInfo != null && SlideRootJudgeResultFieldInfo != null;
            if (!ready) MelonLogger.Error("AutoPlay disabled: required note interfaces are missing.");
            return ready;
        }

        public static void HandleInput()
        {
            var keys = SinmaiAssist.KeyBindConfig.AutoPlay;
            if (DebugInput.GetKeyDown(keys.None.KeyCode)) autoPlayMode = AutoPlayMode.None;
            if (DebugInput.GetKeyDown(keys.Critical.KeyCode)) autoPlayMode = AutoPlayMode.Critical;
            if (DebugInput.GetKeyDown(keys.Perfect.KeyCode)) autoPlayMode = AutoPlayMode.Perfect;
            if (DebugInput.GetKeyDown(keys.Great.KeyCode)) autoPlayMode = AutoPlayMode.Great;
            if (DebugInput.GetKeyDown(keys.Good.KeyCode)) autoPlayMode = AutoPlayMode.Good;
            if (DebugInput.GetKeyDown(keys.Random.KeyCode)) autoPlayMode = AutoPlayMode.Random;
            if (DebugInput.GetKeyDown(keys.RandomAllPerfect.KeyCode)) autoPlayMode = AutoPlayMode.RandomAllPerfect;
            if (DebugInput.GetKeyDown(keys.RandomFullComboPlus.KeyCode)) autoPlayMode = AutoPlayMode.RandomFullComboPlus;
            if (DebugInput.GetKeyDown(keys.RandomFullCombo.KeyCode)) autoPlayMode = AutoPlayMode.RandomFullCombo;
        }

        public enum AutoPlayMode
        {
            None,
            Critical,
            Perfect,
            Great,
            Good,
            Random,
            RandomAllPerfect,
            RandomFullComboPlus,
            RandomFullCombo
        }

        private static readonly ReadOnlyCollection<NoteJudge.ETiming> RandCriticalTiming = Array.AsReadOnly(new NoteJudge.ETiming[5]
        {
            NoteJudge.ETiming.FastPerfect2nd,
            NoteJudge.ETiming.FastPerfect,
            NoteJudge.ETiming.Critical,
            NoteJudge.ETiming.LatePerfect,
            NoteJudge.ETiming.LatePerfect2nd
        });

        private static readonly ReadOnlyCollection<NoteJudge.ETiming> RandomJudgeTiming = Array.AsReadOnly(new NoteJudge.ETiming[]
        {
            NoteJudge.ETiming.Critical,
            NoteJudge.ETiming.FastPerfect,
            NoteJudge.ETiming.FastPerfect2nd,
            NoteJudge.ETiming.LatePerfect,
            NoteJudge.ETiming.LatePerfect2nd,
            NoteJudge.ETiming.LateGreat2nd,
            NoteJudge.ETiming.FastGreat2nd,
            NoteJudge.ETiming.LateGood,
            NoteJudge.ETiming.FastGood,
            NoteJudge.ETiming.TooLate
        });


        public static AutoPlayMode autoPlayMode = AutoPlayMode.None;
        public static bool DisableUpdate = false;

        public static bool IsAutoPlay()
        {
            return autoPlayMode != AutoPlayMode.None;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), "IsAutoPlay")]
        public static bool SetIsAutoPlay(ref bool __result)
        {
            __result = IsAutoPlay();
            return false;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "set_AutoPlay")]
        public static void AutoPlayUpdate()
        {
            var mode = GameManager.AutoPlay;
            if (DisableUpdate) return;
            autoPlayMode = (AutoPlayMode)mode;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), "AutoJudge")]
        public static void AutoJudge(ref NoteJudge.ETiming __result)
        {
            if (!IsAutoPlay()) return;
            var random = UnityEngine.Random.Range(0, 1000);
            var random2 = 2;
            if (random < 350)
            {
                random2 += UnityEngine.Random.Range(-2, 3);
            }

            switch (autoPlayMode)
            {
                case AutoPlayMode.Critical:
                    __result = NoteJudge.ETiming.Critical;
                    break;
                // 随机Fast或Late
                case AutoPlayMode.Perfect:
                    __result = UnityEngine.Random.Range(0, 2) == 0 
                        ? NoteJudge.ETiming.LatePerfect2nd 
                        : NoteJudge.ETiming.FastPerfect2nd;
                    break;
                case AutoPlayMode.Great:
                    __result = UnityEngine.Random.Range(0, 2) == 0 
                        ? NoteJudge.ETiming.LateGreat 
                        : NoteJudge.ETiming.FastGreat;
                    break;
                case AutoPlayMode.Good:
                    __result = UnityEngine.Random.Range(0, 2) == 0 
                        ? NoteJudge.ETiming.LateGood 
                        : NoteJudge.ETiming.FastGood;
                    break;
                case AutoPlayMode.Random:
                    __result = RandomJudgeTiming[UnityEngine.Random.Range(0, RandomJudgeTiming.Count)];
                    break;
                case AutoPlayMode.RandomAllPerfect:
                    __result = RandCriticalTiming[random2];
                    break;
                case AutoPlayMode.RandomFullComboPlus:
                    if (random >= 10)
                    {
                        __result = RandCriticalTiming[random2];
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0, 2) == 1)
                        {
                            __result = NoteJudge.ETiming.LateGreat;
                        }
                        else
                        {
                            __result = NoteJudge.ETiming.FastGreat;
                        }
                    }
                    break;
                case AutoPlayMode.RandomFullCombo:
                    if (random >= 80)
                    {
                        __result = RandCriticalTiming[random2];
                    }
                    else if (random >= 20)
                    {
                        if (UnityEngine.Random.Range(0, 2) == 1)
                        {
                            __result = NoteJudge.ETiming.LateGreat;
                        }
                        else
                        {
                            __result = NoteJudge.ETiming.FastGreat;
                        }
                    }
                    else
                    {
                        if (UnityEngine.Random.Range(0, 2) == 1)
                        {
                            __result = NoteJudge.ETiming.LateGood;
                        }
                        else
                        {
                            __result = NoteJudge.ETiming.FastGood;
                        }
                    }
                    break;
                default:
                    __result = NoteJudge.ETiming.TooFast;
                    break;
            }
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(NoteBase), "SetAutoPlayJudge")]
        public static bool NoteBaseAutoPlayJudge(NoteBase __instance)
        {
            if (!IsAutoPlay()) return true;
            var appearMsec = (float)NoteBaseAppearMsecFieldInfo.GetValue(__instance);
            var isExNote = (bool)NoteBaseIsExNoteFieldInfo.GetValue(__instance);
            var playJudgeSeMethod = NoteBasePlayJudgeSeMethodInfo;

            if ((ETiming)NoteBaseJudgeResultFieldInfo.GetValue(__instance) != ETiming.End) return false;
            if (NotesManager.GetCurrentMsec() > appearMsec - 4.1666665f)
            {
                if ((autoPlayMode == AutoPlayMode.RandomAllPerfect || autoPlayMode == AutoPlayMode.RandomFullCombo || autoPlayMode == AutoPlayMode.RandomFullComboPlus) && (isExNote || __instance is TouchNoteB))
                {
                    NoteBaseJudgeResultFieldInfo.SetValue(__instance, NoteJudge.ETiming.Critical);
                }
                else
                {
                    NoteBaseJudgeResultFieldInfo.SetValue(__instance, GameManager.AutoJudge());
                }
                playJudgeSeMethod.Invoke(__instance, null);
            }
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SlideRoot), "Judge")]
        public static bool SlideRootJudge(SlideRoot __instance, ref bool __result)
        {
            if (!IsAutoPlay()) return true;
            var isNoteCheckTimeStartMethod = SlideRootIsNoteCheckTimeStartMethodInfo;
            bool isNoteCheckTimeStart = (bool)isNoteCheckTimeStartMethod.Invoke(__instance, new object[] { Singleton<GamePlayManager>.Instance.GetGameScore(__instance.MonitorId).UserOption.GetJudgeTimingFrame() });
            if (!isNoteCheckTimeStart)
            {
                __result = false;
                return false;
            }

            var judgeTimingDiffMsecField = SlideRootJudgeTimingDiffMsecFieldInfo;
            var judgeResultField = SlideRootJudgeResultFieldInfo;
            var tailMsecField = SlideRootTailMsecFieldInfo;
            var lastWaitTimeField = SlideRootlastWaitTimeFieldInfo;
            var judgeTypeField = SlideRootJudgeTypeFieldInfo;
            var lastWaitTimeForJudgeField = SlideRootlastWaitTimeForJudgeFieldInfo;

            float judgeTimingDiffMsec = NotesManager.GetCurrentMsec() - (float)tailMsecField.GetValue(__instance) + (float)lastWaitTimeField.GetValue(__instance);
            judgeTimingDiffMsecField.SetValue(__instance, judgeTimingDiffMsec);
            ETiming judgeResult = (ETiming)judgeResultField.GetValue(__instance);
            judgeResult = (IsAutoPlay() ? GameManager.AutoJudge() : NoteJudge.GetSlideJudgeTiming(ref judgeTimingDiffMsec, Singleton<GamePlayManager>.Instance.GetGameScore(__instance.MonitorId).UserOption.GetJudgeTimingFrame(), (EJudgeType)judgeTypeField.GetValue(__instance), (int)lastWaitTimeForJudgeField.GetValue(__instance)));

            if (autoPlayMode == AutoPlayMode.RandomAllPerfect ||
                autoPlayMode == AutoPlayMode.RandomFullComboPlus ||
                autoPlayMode == AutoPlayMode.RandomFullCombo)
            {
                judgeResult = NoteJudge.ETiming.Critical;
            }

            if (judgeResult != NoteJudge.ETiming.End)
            {
                if (judgeResult == NoteJudge.ETiming.TooFast)
                {
                    judgeResult = NoteJudge.ETiming.FastGood;
                }
                judgeResultField.SetValue(__instance, judgeResult);
                __result = true;
                return false;
            }
            __result = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(TouchNoteB), "Judge")]
        public static bool TouchNoteBJudge(TouchNoteB __instance, ref bool __result)
        {
            if (!IsAutoPlay()) return true;
            var isNoteCheckTimeStartMethod = NoteBaseIsNoteCheckTimeStartMethodInfo;
            bool isNoteCheckTimeStart = (bool)isNoteCheckTimeStartMethod.Invoke(__instance, new object[] { Singleton<GamePlayManager>.Instance.GetGameScore(__instance.MonitorId).UserOption.GetJudgeTimingFrame() });
            if (!isNoteCheckTimeStart)
            {
                __result = false;
                return false;
            }
            var judgeTimingDiffMsecField = NoteBaseJudgeTimingDiffMsecFieldInfo;
            var judgeResultField = NoteBaseJudgeResultFieldInfo;
            var appearMsecField = NoteBaseAppearMsecFieldInfo;
            var judgeTypeField = NoteBaseJudgeTypeFieldInfo;
            var TouchAreaField = TouchNoteBTouchAreaFieldInfo;
            var playJudgeSeMethod = TouchNoteBPlayJudgeSeMethodInfo;

            var judgeTimingDiffMsec = NotesManager.GetCurrentMsec() - (float)appearMsecField.GetValue(__instance);
            judgeTimingDiffMsecField.SetValue(__instance, judgeTimingDiffMsec);

            ETiming judgeResult = (ETiming)judgeResultField.GetValue(__instance);
            judgeResult = NoteJudge.GetJudgeTiming(ref judgeTimingDiffMsec, Singleton<GamePlayManager>.Instance.GetGameScore(__instance.MonitorId).UserOption.GetJudgeTimingFrame(), (EJudgeType)judgeTypeField.GetValue(__instance));
            if (autoPlayMode == AutoPlayMode.RandomAllPerfect ||
                autoPlayMode == AutoPlayMode.RandomFullComboPlus ||
                autoPlayMode == AutoPlayMode.RandomFullCombo)
            {
                judgeResult = NoteJudge.ETiming.Critical;
            }
            judgeResultField.SetValue(__instance, judgeResult);

            TouchSensorType touchArea = (TouchSensorType)TouchAreaField.GetValue(__instance);
            if (judgeResult != NoteJudge.ETiming.End)
            {
                playJudgeSeMethod.Invoke(__instance, null);
                int buttonId = (int)NoteBaseButtonIdProperty.GetValue(__instance, null);
                if (touchArea == TouchSensorType.B)
                {
                    InputManager.SetUsedThisFrame(__instance.MonitorId, (InputManager.TouchPanelArea)(8 + buttonId));
                }
                else if (touchArea == TouchSensorType.E)
                {
                    InputManager.SetUsedThisFrame(__instance.MonitorId, (InputManager.TouchPanelArea)(26 + buttonId));
                }
                else if (touchArea == TouchSensorType.A)
                {
                    InputManager.SetUsedThisFrame(__instance.MonitorId, (InputManager.TouchPanelArea)(0 + buttonId));
                }
                else if (touchArea == TouchSensorType.D)
                {
                    InputManager.SetUsedThisFrame(__instance.MonitorId, (InputManager.TouchPanelArea)(18 + buttonId));
                }
                else if (touchArea == TouchSensorType.C)
                {
                    InputManager.SetUsedThisFrame(__instance.MonitorId, InputManager.TouchPanelArea.C1);
                    InputManager.SetUsedThisFrame(__instance.MonitorId, InputManager.TouchPanelArea.C2);
                }
                __result = true;
                return false;
            }
            __result = false;
            return false;
        }

    }
}
