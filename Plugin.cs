using BepInEx;
using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using BepInEx.Logging;

namespace TaxAssistant
{
    [BepInPlugin("taxassistant", "Tax Assistant", "1.0.4")]
    public class Plugin : BaseUnityPlugin
    {
        private static ManualLogSource StaticLogger;

        // Cached reflection info
        private static Type GameMgrType, TUnitMgrType, PolicyUIType, NpcAlarmUIType, SysMgrType;
        private static PropertyInfo GameMgrInstanceProp;
        private static FieldInfo TUnitMgrField, ListCitizenField, PolicyUIField, NpcAlarmUIField, SysMgrField, DayField;
        private static MethodInfo TaxExecutionMethod, NpcAlarmCallMethod;
        private static Type AlarmStateType;
        private static object AlarmStateBasic;

        private void Awake()
        {
            StaticLogger = Logger;
            Logger.LogInfo("Tax Assistant by Xenoyia loaded.");

            // Cache all reflection info here
            GameMgrType = AccessTools.TypeByName("GameMgr");
            TUnitMgrType = AccessTools.TypeByName("T_UnitMgr");
            PolicyUIType = AccessTools.TypeByName("PolicyUI");
            NpcAlarmUIType = AccessTools.TypeByName("NpcAlarmUI");
            SysMgrType = AccessTools.TypeByName("SystemMgr");

            GameMgrInstanceProp = GameMgrType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public);
            TUnitMgrField = GameMgrType.GetField("_T_UnitMgr", BindingFlags.Instance | BindingFlags.Public);
            ListCitizenField = TUnitMgrType.GetField("List_Citizen", BindingFlags.Instance | BindingFlags.Public);
            PolicyUIField = GameMgrType.GetField("_PolicyUI", BindingFlags.Instance | BindingFlags.Public);
            NpcAlarmUIField = GameMgrType.GetField("_NpcAlarmUI", BindingFlags.Instance | BindingFlags.Public);
            SysMgrField = GameMgrType.GetField("_SysMgr", BindingFlags.Instance | BindingFlags.Public);
            DayField = SysMgrType.GetField("m_Day", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            TaxExecutionMethod = PolicyUIType.GetMethod("TaxExecution", BindingFlags.Instance | BindingFlags.Public);
            // NpcAlarm_Call signature: (string, bool, AlarmState, int)
            AlarmStateType = NpcAlarmUIType.GetNestedType("AlarmState");
            NpcAlarmCallMethod = NpcAlarmUIType.GetMethod("NpcAlarm_Call", new[] { typeof(string), typeof(bool), AlarmStateType, typeof(int) });
            AlarmStateBasic = Enum.Parse(AlarmStateType, "Basic");

            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        private static void DoCollection()
        {
            try
            {
                var gameMgr = GameMgrInstanceProp.GetValue(null);
                var tUnitMgr = TUnitMgrField.GetValue(gameMgr);
                var citizens = (System.Collections.IEnumerable)ListCitizenField.GetValue(tUnitMgr);
                var policyUI = PolicyUIField.GetValue(gameMgr);

                float totalCollections = 0f;
                int count = 0;
                foreach (var citizen in citizens)
                {
                    float collected = (float)TaxExecutionMethod.Invoke(policyUI, new object[] { citizen, false });
                    totalCollections += collected;
                    count++;
                }
                if (totalCollections > 0f)
                {
                    StaticLogger.LogInfo($"[Tax Assistant] Collection complete. {count} citizens processed. Total: {totalCollections}");

                    var npcAlarmUI = NpcAlarmUIField.GetValue(gameMgr);
                    string message = $"<sprite name=FS_Tax> The Tax Assistant has collected <color=#FFE331>{totalCollections:N0}</color> from our ratizens!";
                    NpcAlarmCallMethod.Invoke(npcAlarmUI, new object[] { message, false, AlarmStateBasic, 0 });
                }
            }
            catch (Exception ex)
            {
                StaticLogger.LogError($"[Tax Assistant] Error during collection: {ex}");
            }
        }

        [HarmonyPatch]
        public static class SystemMgrProsHappyRefreshPatch
        {
            private static int _lastProcessedDay = -1;

            static MethodInfo TargetMethod()
            {
                return SysMgrType?.GetMethod("ProsHappyRefresh", BindingFlags.Instance | BindingFlags.Public);
            }

            static void Postfix()
            {
                var gameMgr = GameMgrInstanceProp.GetValue(null);
                var sysMgr = SysMgrField.GetValue(gameMgr);
                int currentDay = (int)DayField.GetValue(sysMgr);

                if (currentDay != _lastProcessedDay)
                {
                    _lastProcessedDay = currentDay;
                    DoCollection();
                }
            }
        }
    }
} 