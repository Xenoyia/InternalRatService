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

        private void Awake()
        {
            StaticLogger = Logger;
            Logger.LogInfo($"Tax Assistant by Xenoyia loaded.");
            Harmony.CreateAndPatchAll(Assembly.GetExecutingAssembly());
        }

        private static void DoCollection()
        {
            try
            {
                var gameMgrType = AccessTools.TypeByName("GameMgr");
                var gameMgr = gameMgrType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                var tUnitMgr = gameMgrType.GetField("_T_UnitMgr", BindingFlags.Instance | BindingFlags.Public).GetValue(gameMgr);
                var listCitizenField = tUnitMgr.GetType().GetField("List_Citizen", BindingFlags.Instance | BindingFlags.Public);
                var citizens = listCitizenField.GetValue(tUnitMgr) as System.Collections.IEnumerable;
                var policyUI = gameMgrType.GetField("_PolicyUI", BindingFlags.Instance | BindingFlags.Public).GetValue(gameMgr);
                var collectMethod = policyUI.GetType().GetMethod("TaxExecution", BindingFlags.Instance | BindingFlags.Public);

                float totalCollections = 0f;
                int count = 0;
                foreach (var citizen in citizens)
                {
                    float collected = (float)collectMethod.Invoke(policyUI, new object[] { citizen, false });
                    totalCollections += collected;
                    count++;
                }
                if (totalCollections > 0f)
                {
                    StaticLogger.LogInfo($"[Tax Assistant] Collection complete. {count} citizens processed. Total: {totalCollections}");

                    var npcAlarmUI = gameMgrType.GetField("_NpcAlarmUI", BindingFlags.Instance | BindingFlags.Public).GetValue(gameMgr);
                    var npcAlarmType = npcAlarmUI.GetType();
                    var alarmStateType = npcAlarmType.GetNestedType("AlarmState");
                    var alarmStateBasic = Enum.Parse(alarmStateType, "Basic");
                    string message = $"<sprite name=FS_Tax> The Tax Assistant has collected <color=#FFE331>{totalCollections:N0}</color> from our ratizens!";
                    npcAlarmType.GetMethod("NpcAlarm_Call", new[] { typeof(string), typeof(bool), alarmStateType, typeof(int) })
                        .Invoke(npcAlarmUI, new object[] { message, false, alarmStateBasic, 0 });
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
                var systemMgrType = AccessTools.TypeByName("SystemMgr");
                return systemMgrType?.GetMethod("ProsHappyRefresh", BindingFlags.Instance | BindingFlags.Public);
            }

            static void Postfix()
            {
                // Get the current day from the game manager
                var gameMgrType = AccessTools.TypeByName("GameMgr");
                var gameMgr = gameMgrType.GetProperty("Instance", BindingFlags.Static | BindingFlags.Public).GetValue(null);
                var sysMgr = gameMgrType.GetField("_SysMgr", BindingFlags.Instance | BindingFlags.Public).GetValue(gameMgr);
                int currentDay = (int)sysMgr.GetType().GetField("m_Day", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(sysMgr);

                if (currentDay != _lastProcessedDay)
                {
                    _lastProcessedDay = currentDay;
                    // Call DoCollection directly
                    DoCollection();
                }
            }
        }
    }
} 