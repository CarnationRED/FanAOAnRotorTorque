using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using System.Text.RegularExpressions;

namespace PropellerPithAndRotorTorque
{
    public class ModulePropellerPitchCtrl : PartModule
    {
        [UI_FloatRange(stepIncrement = 0.5f, maxValue = 45f, minValue = 0f, affectSymCounterparts = UI_Scene.All)]
        [KSPAxisField(incrementalSpeed = 100f, guiFormat = "F1", isPersistant = true, axisMode = KSPAxisMode.Incremental, guiActive = true, guiActiveEditor = true, guiName = "Target AOA")]
        public float fanTargetAOA = 5f;

        public ModuleControlSurface ModuleCS;

        private PPARTMgr mgr;

        [KSPField(isPersistant = true)]
        private bool Enabled = false;
        public void Init(PPARTMgr m)
        {
            if (m == null) return;
            ModuleCS = part.FindModuleImplementing<ModuleControlSurface>();
            if (ModuleCS == null) { Destroy(this); return; }
            var l = part.FindModulesImplementing<ModulePropellerPitchCtrl>();
            for (int i = 1; i < l.Count; Destroy(l[i++])) ;//Destroy duplicate modules (idk why there's duplicate when I play modded game)
            mgr = m;
            mgr.RegisterModuleCS(ModuleCS, fanTargetAOA, this);
            mgr.PitchCtrlEnabled = Enabled;
        }

        /// <summary>
        /// This is called on revert flight, and would cause problem if I initialize plugin here
        /// </summary>
        /// <param name="node"></param>
        public override void OnLoad(ConfigNode node)
        {
            //if (vessel != null)
            //    PPARTMgr.OnModuleLoading(vessel);//This won't be reached when lunching a vessel
        }
        public override void OnSave(ConfigNode node)
        {
            base.OnSave(node);
            if (mgr != null)
            {
                Enabled = mgr.PitchCtrlEnabled;
            }
        }

        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            base.Fields["fanTargetAOA"].OnValueModified += OnTargetAOAChanged;
        }


        protected void OnTargetAOAChanged(object field)
        {
            if (mgr != null)
            {
                if (fanTargetAOA != mgr.targetAOA)
                {
                    mgr.oldTargetAOA = mgr.targetAOA;
                    mgr.targetAOA = fanTargetAOA;
                }
            }
        }
        public void FixedUpdate()
        {
            if (vessel != null)
                if (!PPARTMgr.IsPPARTMgrInitialized(vessel))
                {
                    Debug.Log("[PropellerPithAndRotorTorque]: Initializing plugin from part");
                    PPARTMgr.OnModuleLoading(vessel);
                }
            if (mgr == null) return;
            if (fanTargetAOA != mgr.targetAOA)
            {
                Fields["fanTargetAOA"].SetValue(mgr.targetAOA, this);
            }
        }
        void OnDestroy()
        {
            base.Fields["fanTargetAOA"].OnValueModified -= OnTargetAOAChanged;
        }
    }

}
