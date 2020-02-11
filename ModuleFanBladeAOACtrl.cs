using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using System.Text.RegularExpressions;

namespace firstinksp
{
    public class ModuleFanBladeAOACtrl : PartModule
    {
        [UI_FloatRange(stepIncrement = 0.5f, maxValue = 45f, minValue = 0f, affectSymCounterparts = UI_Scene.All)]
        [KSPAxisField(incrementalSpeed = 100f, guiFormat = "F1", isPersistant = true, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true, guiName = "Target AOA")]
        public float fanTargetAOA = 5f;

        ModuleControlSurface ModuleCS;

        [KSPField(isPersistant = true)]
        private bool Enabled = false;

        public override void OnLoad(ConfigNode node)
        {
            ModuleCS = part.FindModuleImplementing<ModuleControlSurface>();
            if (ModuleCS == null) Destroy(this);

            AOAMgr.AOACtrlEnabled = Enabled;
            AOAMgr.Init();
            AOAMgr.RegisterModuleCS(ModuleCS, fanTargetAOA, this);
        }

        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            Enabled = AOAMgr.AOACtrlEnabled;
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            base.Fields["fanTargetAOA"].OnValueModified += OnTargetAOAChanged;
        }


        protected void OnTargetAOAChanged(object field)
        {
            if (fanTargetAOA != AOAMgr.targetAOA)
            {
                AOAMgr.oldTargetAOA = AOAMgr.targetAOA;
                AOAMgr.targetAOA = fanTargetAOA;
            }

        }

        public void FixedUpdate()
        {
            if (fanTargetAOA != AOAMgr.targetAOA)
            {
                Fields["fanTargetAOA"].SetValue(AOAMgr.targetAOA, this);
            }
        }

        void OnDestroy()
        {
            AOAMgr.RemoveModuleCS(ModuleCS);
            base.Fields["fanTargetAOA"].OnValueModified -= OnTargetAOAChanged;
            if (!HighLogic.LoadedSceneIsEditor)
                AOAMgr.ReInit();
        }
    }

}
