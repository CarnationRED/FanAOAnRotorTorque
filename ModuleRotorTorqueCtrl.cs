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

namespace PropellerPithAndRotorTorque
{
    public class ModuleRotorTorqueCtrl : PartModule
    {
        [KSPField(isPersistant = true)]
        private bool Enabled = false;

        private PPARTMgr mgr;

        public void Init(PPARTMgr m)
        {
            if (HighLogic.LoadedSceneIsEditor || m == null) return;
            if (part.FindModuleImplementing<ModuleRoboticServoRotor>() == null) { Destroy(this); return; }
            mgr = m;
            mgr.TorqueCtrlEnabled = Enabled;
        }

        public override void OnSave(ConfigNode node)
        {
            if (!HighLogic.LoadedSceneIsFlight) return;//What if the game saves vessel in editor?
            base.OnSave(node);
            if (mgr != null)
            {
                Enabled = mgr.TorqueCtrlEnabled;
            }
        }
    }
}
