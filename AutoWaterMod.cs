using MelonLoader;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

[assembly: MelonInfo(typeof(AutoWater.AutoWaterMod), "AutoWater", "0.2.0", "Michiyo")]
[assembly: MelonGame("TVGS", "Schedule I")]

namespace AutoWater
{
    public class AutoWaterMod : MelonMod
    {

        private MelonPreferences_Category _category;
        private MelonPreferences_Entry<double> _waterThreshold;
        private MelonPreferences_Entry<double> _checkInterval;

        private float _lastCheckTime = 0f;

        public override void OnInitializeMelon()
        {
            _category = MelonPreferences.CreateCategory("AutoWater", "AutoWater Settings");
            _waterThreshold = _category.CreateEntry("WaterThreshold", 0.3);
            _waterThreshold.Comment = "Water pots when they fall below this percentage. Default is 0.3 (30%)";

            _checkInterval = _category.CreateEntry("CheckInterval", 60.0);
            _checkInterval.Comment = "Time (in seconds) between sprinkler checks";

            MelonLogger.Msg($"[AutoWater] Config loaded: Threshold={_waterThreshold.Value}, Interval={_checkInterval.Value}");
        }

        public override void OnUpdate()
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (currentScene != "Main") return;

            float now = Time.realtimeSinceStartup;
            float elapsed = now - _lastCheckTime;


            if (elapsed >= _checkInterval.Value)
            {
                _lastCheckTime = now;
                MelonCoroutines.Start(ProcessAllSprinklers());
            }
        }

        private IEnumerator ProcessAllSprinklers()
        {
            var sprinklers = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();

            foreach (var obj in sprinklers)
            {
                if (obj.GetType().FullName != "ScheduleOne.ObjectScripts.Sprinkler")
                    continue;

                var type = obj.GetType();

                var getPotsMethod = type.GetMethod("GetPots", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var waterMethod = type.GetMethod("Water", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (getPotsMethod == null || waterMethod == null)
                {
                    continue;
                }

                var potList = getPotsMethod.Invoke(obj, null);
                if (potList == null) continue;

                foreach (var pot in (IEnumerable)potList)
                {
                    if (pot == null) continue;

                    var potType = pot.GetType();

                    var isFilledField = potType.GetProperty("IsFilledWithSoil", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var normWaterLevelProp = potType.GetProperty("NormalizedWaterLevel", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (isFilledField == null || normWaterLevelProp == null)
                    {
                        continue;
                    }

                    bool isFilled = (bool)isFilledField.GetValue(pot);
                    float waterLevel = (float)normWaterLevelProp.GetValue(pot);

                    if (isFilled && waterLevel < _waterThreshold.Value)
                    {
                        waterMethod.Invoke(obj, null);
                        break; // one pot is enough to trigger watering
                    }
                }
            }

            yield return null;
        }
    }
}
