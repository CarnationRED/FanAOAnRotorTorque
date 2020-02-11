using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace firstinksp
{
    [KSPAddon(KSPAddon.Startup.EditorAny, false)]
    class AssignAxisGroup : MonoBehaviour
    {
        void Awake()
        {
            GameEvents.onEditorPartEvent.Add(OnPartEvent);
        }

        void OnDestroy()
        {
            GameEvents.onEditorPartEvent.Remove(OnPartEvent);
        }

        private void OnPartEvent(ConstructionEventType t, Part p)
        {
            if (t == ConstructionEventType.PartCreated)
               // if (AOAMgr.ROTOR_NAMES.Contains(p.partName))
                    AssignAxis(p);
        }

        private static void AssignAxis(Part p)
        {
#if DEBUG
                        Debug.Log("Rotor added");
#endif
            List<ModuleRoboticServoRotor> list = p.FindModulesImplementing<ModuleRoboticServoRotor>();
            int count = list.Count;
            while (count-- > 0)
            {
                ModuleRoboticServoRotor partModule = list[count];
                int count2 = partModule.Fields.Count;
                while (count2-- > 0)
                {
                    BaseAxisField baseAxisField = partModule.Fields[count2] as BaseAxisField;
                    if (baseAxisField == null)
                    {
                        continue;
                    }
                    if (baseAxisField.FieldInfo.FieldType == typeof(float) && baseAxisField.name == "rpmLimit")
                    {
#if DEBUG
                        Debug.Log("Found a baseAxisField");
#endif
                        baseAxisField.axisGroup = KSPAxisGroup.MainThrottle;
                        ((KSPAxisField)baseAxisField.Attribute).axisMode = KSPAxisMode.Absolute;
#if DEBUG
                        Debug.Log("Updated axis group and mode");
#endif
                    }
                }
            }
        }
    }
}
