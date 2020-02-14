
//#define UI_FANAO
//#define UI_TORQUE
using Expansions.Serenity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

namespace PropellerPithAndRotorTorque
{
    public class PPARTMgr : MonoBehaviour
    {
        private Vessel hostVessel;
        private bool initialized = false;
        private static Dictionary<Guid, PPARTMgr> instances = new Dictionary<Guid, PPARTMgr>();//associate plugin with vessel using id, preventing bug when game instantiate a vessel

        private List<ModuleControlSurface> moduleCS;
        private List<ModulePropellerPitchCtrl> moduleMod;
        private List<PID> pidCS;
        private List<ModuleRoboticServoRotor> moduleRSR;
        private List<PID> pidRSR;
        private List<float[]> eRSR;
        Vector3 nv, uv;
        public float targetAOA = 5f;
        public float oldTargetAOA = 5f;

        public static readonly List<string> ROTOR_NAMES = new List<string>(6) { "Rotor_03_s", "Rotor_02_s", "Rotor_01s", "Rotor_03", "Rotor_02", "Rotor_01" };
        private static FieldInfo baseTransform, ctrlSurface, liftVector, deflectionDirection;
        private static FieldInfo currentRPM;
        float angleOfAttack;
#if DEBUG
        static float Kp = 0.008f, Ki = 0.04f, Td = .002f;
        bool validK = true, validI = true, validD = true;

        int a = 5;
        float u = 0;
        static string strKp = "", strKi = "", strTd = "";
        bool temp_inv = false;
        static GUIStyle styleErr, styleNorm;

#endif
        bool invert = false;

        public bool PitchCtrlEnabled = false;
        public bool TorqueCtrlEnabled = false;
        private int toggleState = 3;
        private const string PitchMsg = ":Propeller Pitch Ctrl\n", TorqueMsg = ":Rotor Torque Ctrl";

        bool jointsInit = true;
        List<ConfigurableJoint> joints;
        int validRotors = 0;
        float decrease = .03f;
        private bool hasRotors = false;

        public static bool IsPPARTMgrInitialized(Vessel v)
        {
            return instances.TryGetValue(v.id, out _);
        }

        static PPARTMgr()
        {
            var c = typeof(ModuleControlSurface);
            baseTransform = c.GetField("baseTransform", BindingFlags.Instance | BindingFlags.NonPublic);
            ctrlSurface = c.GetField("ctrlSurface", BindingFlags.Instance | BindingFlags.NonPublic);
            liftVector = c.GetField("liftVector", BindingFlags.Instance | BindingFlags.NonPublic);
            currentRPM = typeof(ModuleRoboticRotationServo).GetField("transformRateOfMotion", BindingFlags.Instance | BindingFlags.NonPublic);
            if (Versioning.fetch.versionMinor == 9 && Versioning.fetch.versionMajor == 1)
                deflectionDirection = typeof(ModuleControlSurface).GetField("deflectionDirection", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        public static void OnModuleLoading(Vessel v)
        {
            if (v == null) return;
            PPARTMgr mgr;
            if (!instances.TryGetValue(v.id, out _))
            {
                Debug.Log("[PropellerPithAndRotorTorque]: Initializing plugin for " + v.GetName());
                instances.Add(v.id, (mgr = new GameObject().AddComponent<PPARTMgr>()));
                mgr.hostVessel = v;
                mgr.moduleCS = new List<ModuleControlSurface>();
                mgr.moduleMod = new List<ModulePropellerPitchCtrl>();
                mgr.pidCS = new List<PID>();

                mgr.moduleRSR = new List<ModuleRoboticServoRotor>();
                mgr.pidRSR = new List<PID>();
                mgr.eRSR = new List<float[]>();
                GameEvents.onVesselLoaded.Add(mgr.OnVesselLoaded);
                GameEvents.onVesselDestroy.Add(mgr.OnVesselDestroy);
            }
        }

        private void OnVesselLoaded(Vessel data)
        {
            FindModules();
        }
        private void OnVesselDestroy(Vessel data)
        {
            if (data.id == hostVessel.id)
            {
                GameEvents.onVesselLoaded.Remove(OnVesselLoaded);
                GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
                instances.Remove(hostVessel.id);
                Destroy(this.gameObject);
            }
        }

        private void Destroy()
        {
            GameEvents.onVesselDestroy.Remove(OnVesselDestroy);
        }

        private void FindModules()
        {
            var cs = hostVessel.FindPartModulesImplementing<ModulePropellerPitchCtrl>();
            Debug.Log("[PropellerPithAndRotorTorque]: " + cs.Count + " blades to control on " + hostVessel.GetName());
            for (int i = 0; i < cs.Count; i++)
            {
                cs[i].Init(this);
            }
            if (HighLogic.LoadedSceneIsFlight)
            {
                var rtc = hostVessel.FindPartModulesImplementing<ModuleRotorTorqueCtrl>();
                Debug.Log("[PropellerPithAndRotorTorque]: " + rtc.Count + " rotors to control on " + hostVessel.GetName());
                for (int i = 0; i < rtc.Count; i++)
                {
                    rtc[i].Init(this);
                }
            }
            Init();
            initialized = true;
        }

        public void RegisterModuleCS(ModuleControlSurface cs, float aoa, ModulePropellerPitchCtrl mf)
        {
            if (HighLogic.LoadedSceneIsEditor || HighLogic.LoadedSceneIsFlight)
            {
                if (!moduleCS.Contains(cs))
                {
                    targetAOA = aoa;
                    moduleCS.Add(cs);
                    moduleMod.Add(mf);
                    pidCS.Add(new PID());
                    pidCS.Last<PID>().setTarget(targetAOA);
                }
            }
        }

        public void RemoveModuleCS(ModuleControlSurface cs)
        {
            if (moduleCS == null)
                return;
            if (moduleCS.Contains(cs))
            {
                pidCS.RemoveAt(moduleCS.IndexOf(cs));
                moduleCS.RemoveAt(moduleCS.IndexOf(cs));
            }
            if (moduleMod != null)
                for (int i = 0; i < moduleMod.Count; i++)
                {
                    var m = moduleMod[i];
                    if (m.ModuleCS.Equals(cs))
                        moduleMod.RemoveAt(i);
                }
        }

        private void Init()
        {
#if DEBUG
            strKp = "" + Kp;
            strKi = "" + Ki;
            strTd = "" + Td;
            styleErr = new GUIStyle();
            styleErr.normal.textColor = Color.red;
            styleNorm = new GUIStyle();
#endif
            if (PitchCtrlEnabled)
            {
                if (TorqueCtrlEnabled)
                    toggleState = 1;
                else
                    toggleState = 3;
            }
            else
            {
                if (TorqueCtrlEnabled)
                    toggleState = 2;
                else
                    toggleState = 0;
            }
        }

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight || !hostVessel.isActiveVessel)
            {
                jointsInit = true;
                return;
            }
            if (!initialized && hostVessel.loaded) OnVesselLoaded(null);
            if (jointsInit || validRotors == 0)
            {
                if (moduleRSR != null) moduleRSR.Clear();
                validRotors = 0;
                joints = new List<ConfigurableJoint>();
                ConfigurableJoint[] cjs = FindObjectsOfType<ConfigurableJoint>();
                if (cjs.Length > 0)
                {
                    List<bool> flags = new List<bool>(cjs.Length);
                    for (int i = 0; i < cjs.Length; i++)
                    {
                        flags.Add(true);
                        var item = cjs[i];
                        if (item.gameObject.name == "TopJoint" && ROTOR_NAMES.Contains(item.gameObject.transform.parent.transform.parent.name))
                            flags[i] = false;
                    }
                    for (int j = 0; j < cjs.Length; j++)
                    {
                        if (!flags[j])
                        {
                            var item = cjs[j];
                            var module = item.gameObject.GetComponentInParent<ModuleRoboticServoRotor>();//Find all modules named ModuleRoboticServoRotor on all vessels within physics range
                            if (module.vessel.id == hostVessel.id)
                            {
                                moduleRSR.Add(module);
                                pidRSR.Add(new PID(0.08f, 0.13f, 0.1f));
                                eRSR.Add(new float[3] { 0f, 0f, 0f });
                                joints.Add(item);
                                flags[j] = true;
                            }
                        }
                    }
                    jointsInit = false;
                    foreach (var item in joints)
                        if (item != null)
                            validRotors++;
                    hasRotors = validRotors != 0;
                }
            }
#if DEBUG
            a--;
            if (0 == a)
            {
                a = 5;
                try
                {
                    var p = Kp;
                    var i = Ki;
                    var d = Td;

                    validK = false;
                    Kp = p;
                    Kp = float.Parse(strKp);
                    validK = true;
                    validI = false;
                    Ki = i;
                    Ki = float.Parse(strKi);
                    validI = true;
                    validD = false;
                    Td = d;
                    Td = float.Parse(strTd);
                    validD = true;
                }
                catch (ArgumentNullException ae) { }
                catch (FormatException fe) { }
                catch (OverflowException oe) { }
            }
#endif

            if (Input.GetKeyDown(KeyCode.Equals))
            {
                toggleState = toggleState == 3 ? 0 : toggleState + 1;
                switch (toggleState)
                {
                    case 0:
                        PitchCtrlEnabled = false;
                        TorqueCtrlEnabled = false;
                        break;
                    case 1:
                        PitchCtrlEnabled = true;
                        TorqueCtrlEnabled = true;
                        break;
                    case 2:
                        PitchCtrlEnabled = false;
                        TorqueCtrlEnabled = true;
                        break;
                    case 3:
                        PitchCtrlEnabled = true;
                        TorqueCtrlEnabled = false;
                        break;
                }
                var s = (PitchCtrlEnabled ? "En" : "Dis") + "abled" + PitchMsg + (TorqueCtrlEnabled ? "En" : "Dis") + "abled" + TorqueMsg;
                ScreenMessages.PostScreenMessage(s, 3f, ScreenMessageStyle.UPPER_RIGHT);
            }


            if (PitchCtrlEnabled)
                for (int b = 0; b < moduleCS.Count; b++)
                {
                    ModuleControlSurface cs = moduleCS[b];
                    if (cs != null)
                    {
                        if (cs.deploy)
                        {
                            float inv = 0;
                            var bt = (Transform)baseTransform.GetValue(cs);
                            if (cs.displaceVelocity)
                            {
                                float num2;
                                if (!cs.deployInvert)
                                    num2 = 1f;
                                else
                                    num2 = -1f;
                                float num3;
                                if (!cs.partDeployInvert)
                                    num3 = 1f;
                                else
                                    num3 = -1f;
                                inv = num2 * num3 * Mathf.Sign(cs.ctrlSurfaceRange);
                            }
                            else if (!cs.usesMirrorDeploy)
                            {
                                float num5;
                                if (!cs.deployInvert)
                                    num5 = 1f;
                                else
                                    num5 = -1f;
                                inv = -num5 * Mathf.Sign((Quaternion.Inverse(cs.vessel.ReferenceTransform.rotation) * (bt.position - cs.vessel.CurrentCoM)).x);
                            }
                            else
                            {
                                float num7;
                                if (!cs.deployInvert)
                                    num7 = 1f;
                                else
                                    num7 = -1f;
                                float num8;
                                if (!cs.partDeployInvert)
                                    num8 = 1f;
                                else
                                    num8 = -1f;
                                float num9 = num7 * num8;
                                float num10;
                                if (!cs.mirrorDeploy)
                                    num10 = 1f;
                                else
                                    num10 = -1f;
                                inv = -num9 * num10;
                            }
                            if (Versioning.fetch.versionMinor == 9 && Versioning.fetch.versionMajor == 1)//patch for update 1.9
                                inv *= (float)deflectionDirection.GetValue(moduleCS[b]);
                            invert = inv < 0;


                            Rigidbody rigidbody = cs.part.Rigidbody;
                            Vector3 worldPoint;
                            if (!cs.displaceVelocity)
                                worldPoint = bt.position;
                            else
                                worldPoint = bt.TransformPoint(cs.velocityOffset);
                            nv = rigidbody.GetPointVelocity(worldPoint) + Krakensbane.GetFrameVelocityV3f();
                            if (nv.sqrMagnitude < 0.1f) continue;
                            nv = nv.normalized;

                            var t = (Transform)ctrlSurface.GetValue(cs);
                            uv = t.up;
                            var luv = (Vector3)liftVector.GetValue(cs);
                            angleOfAttack = Vector3.Angle(nv, Vector3.ProjectOnPlane(nv, uv));
                            if (Vector3.Dot(nv, uv) > 0) angleOfAttack *= -1;
                            var pid = pidCS[b];
                            pid.setTarget(targetAOA);
                            pid.setInvert((invert ? -1 : 1) * Mathf.Sign(Vector3.Dot(nv, t.forward)) > 0);
                            cs.deployAngle -= Mathf.Clamp(pid.getU(angleOfAttack), -12f, 12f);
                            cs.deployAngle = Mathf.Clamp(cs.deployAngle, -90f, 90f);
                        }
                    }
                    else
                        Debug.Log("[PropellerPithAndRotorTorque]Error: moduleCS is not valid");
                }
            if (TorqueCtrlEnabled && hasRotors)
            {
                for (int b = 0; b < moduleRSR.Count; b++)
                {
                    var m = moduleRSR[b];
                    var target = m.rpmLimit;
                    var current = (float)currentRPM.GetValue(m);
                    pidRSR[b].setTarget(target - 0.5f);
                    var t = (float)m.Fields["servoMotorLimit"].GetValue(m);
#if UI_TORQUE
                    pidRSR[b].setPID(Kp, Ki, Td);
#endif
                    t -= pidRSR[b].getU(current);
                    if (current + 0.5f >= target) t -= decrease;
                    t = Mathf.Clamp(t, 0f, 100f);
                    m.Fields["servoMotorLimit"].SetValue(t, m);//OK
#if DEBUG
                    Debug.Log("Controlling rotor");
#endif
                }
            }
        }

        private void OnGUI()
        {
#if DEBUG
            if (!hostVessel.isActiveVessel) return;
#if UI_PITCH
            if (!HighLogic.LoadedSceneIsFlight || moduleCS == null || moduleCS.Count == 0) return;
            GUI.Label(new Rect(120, 120 + 0 * 18, 360, 64), "invert\tdeInv\tpDInv\tmiDep");
            GUI.Label(new Rect(120, 120 + 1 * 18, 360, 64), "" + invert + "\t" + moduleCS[0].deployInvert + "\t" + moduleCS[0].partDeployInvert + "\t" + moduleCS[0].mirrorDeploy);
            GUI.Label(new Rect(120, 120 + 2 * 18, 360, 64), "AOA:" + angleOfAttack);
            GUI.Label(new Rect(120, 120 + 4 * 18, 360, 64), "U:" + pidCS[0].u + "\tErr:" + pidCS[0].e);
            GUI.Label(new Rect(120, 120 + 5 * 18, 360, 64), "Uk:" + pidCS[0].Uk);
            GUI.Label(new Rect(120, 120 + 6 * 18, 360, 64), "Ui:" + pidCS[0].Ui);
            GUI.Label(new Rect(120, 120 + 7 * 18, 360, 64), "Ud:" + pidCS[0].Ud);
            GUI.Label(new Rect(120, 120 + 8 * 18, 360, 64), "nVel" + nv);
            GUI.Label(new Rect(120, 120 + 9 * 18, 360, 64), "csUp:" + uv);
            GUI.Label(new Rect(120, 120 + 10 * 18, 360, 64), "targetAOA: " + pidCS[0].target);

            GUILayout.BeginArea(new Rect(120, 480, 160, 200));
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Kp");
            strKp = GUILayout.TextField(strKp, 6, validK ? styleNorm : styleErr);
            strKp = Regex.Replace(strKp, @"[^0-9.]", "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ki");
            strKi = GUILayout.TextField(strKi, 6, validI ? styleNorm : styleErr);
            strKi = Regex.Replace(strKi, @"[^0-9.]", "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Td");
            strTd = GUILayout.TextField(strTd, 6, validD ? styleNorm : styleErr);
            strTd = Regex.Replace(strTd, @"[^0-9.]", "");
            GUILayout.EndHorizontal();
            temp_inv = GUILayout.Toggle(temp_inv, "Invert PID");
            GUILayout.EndVertical();
            GUILayout.EndArea();
#endif
#if UI_TORQUE

            if (!HighLogic.LoadedSceneIsFlight || moduleRSR == null || moduleRSR.Count == 0) return;
            GUI.Label(new Rect(120, 120 + 0 * 18, 360, 64), "output limit:" + moduleRSR[0].servoMotorLimit);
            GUI.Label(new Rect(120, 120 + 1 * 18, 360, 64), "U:" + pidRSR[0].u + "\tErr:" + pidRSR[0].e);

            GUI.Label(new Rect(120, 120 + 2 * 18, 360, 64), "Uk:" + pidRSR[0].Uk);
            GUI.Label(new Rect(120, 120 + 3 * 18, 360, 64), "Ui:" + pidRSR[0].Ui);
            GUI.Label(new Rect(120, 120 + 4 * 18, 360, 64), "Ud:" + pidRSR[0].Ud);
            GUI.Label(new Rect(120, 120 + 5 * 18, 360, 64), "targetRPM: " + moduleRSR[0].rpmLimit);//ok
            GUI.Label(new Rect(120, 120 + 6 * 18, 360, 64), "currentRPM: " + (float)currentRPM.GetValue(moduleRSR[0]));//ok
            GUI.Label(new Rect(120, 120 + 7 * 18, 360, 64), "targetAngularVelocity: " + joints[0].targetAngularVelocity.x);
            GUI.Label(new Rect(120, 120 + 8 * 18, 360, 64), "valid: " + validRotors + "/RSR1:" + moduleRSR.Count);
            for (int i = 0; i < FlightGlobals.VesselsLoaded.Count; i++)
            {
                GUI.Label(new Rect(120, 120 + (11 + i) * 18, 360, 64), "ID: " + FlightGlobals.VesselsLoaded.ElementAt(i).id + " Name: " + FlightGlobals.VesselsLoaded.ElementAt(i).GetName());
            }
            ConfigurableJoint[] cjs = FindObjectsOfType<ConfigurableJoint>();
            int count = 0;
            foreach (var cj in cjs)
            {
                if (cj.gameObject.name == "TopJoint")
                    GUI.Label(new Rect(120, 120 + (14 + count) * 18, 360, 64), "find 1! total: " + ++count);
            }

            GUILayout.BeginArea(new Rect(120, 480, 160, 200));
            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Kp");
            strKp = GUILayout.TextField(strKp, 6, validK ? styleNorm : styleErr);
            strKp = Regex.Replace(strKp, @"[^0-9.]", "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Ki");
            strKi = GUILayout.TextField(strKi, 6, validI ? styleNorm : styleErr);
            strKi = Regex.Replace(strKi, @"[^0-9.]", "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Td");
            strTd = GUILayout.TextField(strTd, 6, validD ? styleNorm : styleErr);
            strTd = Regex.Replace(strTd, @"[^0-9.]", "");
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            GUILayout.Label("Decrease");
            string ss = decrease.ToString();
            ss = GUILayout.TextField(ss, 6);
            ss = Regex.Replace(ss, @"[^0-9.]", "");
            float temp = decrease; ;
            decrease = float.TryParse(ss, out temp) ? temp : decrease;
            GUILayout.EndHorizontal();
            temp_inv = GUILayout.Toggle(temp_inv, "Invert PID");
            GUILayout.EndVertical();
            GUILayout.EndArea();
#endif
#endif

        }
    }

}
