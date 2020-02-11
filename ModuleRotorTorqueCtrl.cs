using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine;
using System.Text.RegularExpressions;
using Expansions.Serenity;

namespace firstinksp
{
    public class ModuleRotorTorqueCtrl : PartModule
    {
        [KSPField(isPersistant = true)]
        private bool Enabled = false;

        ModuleRoboticServoRotor moduleRSR;
        public override void OnLoad(ConfigNode node)
        {
            if (HighLogic.LoadedSceneIsEditor) return;
            moduleRSR = part.FindModuleImplementing<ModuleRoboticServoRotor>();
            if (moduleRSR == null) Destroy(this);
            AOAMgr.TorqueCtrlEnabled = Enabled;
            AOAMgr.Init();
            AOAMgr.RegisterModuleRSR(moduleRSR);
        }

        public override void OnSave(ConfigNode node)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            base.OnSave(node);
            Enabled = AOAMgr.TorqueCtrlEnabled;
        }


        void OnDestroy()
        {
            if (!HighLogic.LoadedSceneIsFlight) return;
            AOAMgr.RemoveModuleRSR(moduleRSR);
            if (!HighLogic.LoadedSceneIsEditor)
                AOAMgr.ReInit();
        }
    }

}
