
//#define UI_FANAOA
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

namespace firstinksp
{
    class AOAMgr : MonoBehaviour
    {


        private static List<ModuleControlSurface> moduleCS;
        private static List<ModuleFanBladeAOACtrl> moduleMod;
        private static List<PID> pidCS;
        private static List<ModuleRoboticServoRotor> moduleRSR;
        private static List<ModuleRoboticServoRotor> moduleRSR1;
        private static List<PID> pidRSR;
        private static List<float[]> eRSR;
        public static readonly List<string> ROTOR_NAMES = new List<string>(6) { "Rotor_03_s", "Rotor_02_s", "Rotor_01s", "Rotor_03", "Rotor_02", "Rotor_01" };
        Vector3 nv, uv;
        public static float targetAOA = 5f;
        public static float oldTargetAOA = 5f;

        public static GameObject obj = null;
        public static AOAMgr instance = null;
        static FieldInfo baseTransform, ctrlSurface, liftVector;
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

        public static bool AOACtrlEnabled = false;
        public static bool TorqueCtrlEnabled = false;
        private static int toggleState = 3;
        private const string AOAMsg = ":Fan Blade AOA Ctrl\n", TorqueMsg = ":Rotor Torque Ctrl";

        bool jointsInit = true;
        List<ConfigurableJoint> joints;
        int validCount = 0;
        float decrease = .03f;
        ModuleRoboticServoRotor rsr;
        private static FieldInfo currentRPM;
        private bool RotorAvail = false;


        public static void RegisterModuleCS(ModuleControlSurface cs, float targetAOA, ModuleFanBladeAOACtrl mf)
        {
            if (moduleCS == null)
            {
                moduleCS = new List<ModuleControlSurface>();
                moduleMod = new List<ModuleFanBladeAOACtrl>();
                pidCS = new List<PID>();
            }
            if (!moduleCS.Contains(cs))
            {
                AOAMgr.targetAOA = targetAOA;
                moduleCS.Add(cs);
                moduleMod.Add(mf);
                pidCS.Add(new PID());
                pidCS.Last<PID>().setTarget(targetAOA);
            }
        }
        public static void RemoveModuleCS(ModuleControlSurface cs)
        {
            if (moduleCS == null)
                return;
            if (moduleCS.Contains(cs))
            {
                pidCS.RemoveAt(moduleCS.IndexOf(cs));
                moduleMod.RemoveAt(moduleCS.IndexOf(cs));
                moduleCS.RemoveAt(moduleCS.IndexOf(cs));
            }
        }

        public static void RegisterModuleRSR(ModuleRoboticServoRotor rsr)
        {
            if (moduleRSR == null)
            {
                moduleRSR = new List<ModuleRoboticServoRotor>();
                pidRSR = new List<PID>();
                eRSR = new List<float[]>();
            }
            if (!moduleRSR.Contains(rsr))
            {
                moduleRSR.Add(rsr);
                pidRSR.Add(new PID(0.08f, 0.13f, 0.1f));
                eRSR.Add(new float[3] { 0, 0, 0 });
            }
        }
        public static void RemoveModuleRSR(ModuleRoboticServoRotor rsr)
        {
            if (moduleRSR == null)
                return;
            if (moduleRSR.Contains(rsr))
            {
                pidRSR.RemoveAt(moduleRSR.IndexOf(rsr));
                eRSR.RemoveAt(moduleRSR.IndexOf(rsr));
                moduleRSR1.RemoveAt(moduleRSR1.IndexOf(rsr));
                moduleRSR.RemoveAt(moduleRSR.IndexOf(rsr));
            }
        }


        public static void ReInit()
        {
            if (obj != null)
                Destroy(obj);
            obj = null;
            moduleCS = null;
            moduleMod = null;
            pidCS = null;
            moduleRSR = null;
            moduleRSR1 = null;
            pidRSR = null;
            eRSR = null;
            if (instance != null)
            {
                GameEvents.onVesselDestroy.Remove(instance.OnVesselDestroy);
                Destroy(instance);
                instance = null;
            }
        }
        public static void Init()
        {
            if (obj == null)
            {
                ReInit();
                obj = new GameObject();
            }
            if (instance == null)
            {
                instance = obj.AddComponent<AOAMgr>();
                if (baseTransform == null)
                {
                    var c = typeof(ModuleControlSurface);
                    baseTransform = c.GetField("baseTransform", BindingFlags.Instance | BindingFlags.NonPublic);
                    ctrlSurface = c.GetField("ctrlSurface", BindingFlags.Instance | BindingFlags.NonPublic);
                    liftVector = c.GetField("liftVector", BindingFlags.Instance | BindingFlags.NonPublic);
                    currentRPM = typeof(ModuleRoboticRotationServo).GetField("transformRateOfMotion", BindingFlags.Instance | BindingFlags.NonPublic);
                }
#if DEBUG
                strKp = "" + Kp;
                strKi = "" + Ki;
                strTd = "" + Td;
                styleErr = new GUIStyle();
                styleErr.normal.textColor = Color.red;
                styleNorm = new GUIStyle();
#endif
            }
            if (moduleCS == null)
            {
                moduleCS = new List<ModuleControlSurface>();
                moduleMod = new List<ModuleFanBladeAOACtrl>();
                pidCS = new List<PID>();
            }
            if (moduleRSR == null)
            {
                moduleRSR = new List<ModuleRoboticServoRotor>();
                pidRSR = new List<PID>();
                eRSR = new List<float[]>();
            }
            for (int i = 0; i < moduleCS.Count; i++)
            {
                if (moduleCS[i] == null)
                {
                    moduleCS.RemoveAt(i);
                    moduleMod.RemoveAt(i);
                    pidCS.RemoveAt(i);
                }
            }
            for (int i = 0; i < moduleRSR.Count; i++)
            {
                if (moduleRSR[i] == null)
                {
                    moduleRSR.RemoveAt(i);
                    pidRSR.RemoveAt(i);
                    eRSR.RemoveAt(i);
                }
            }

            if (AOACtrlEnabled)
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

        void Start()
        {
            if (HighLogic.LoadedSceneIsFlight)
                GameEvents.onVesselDestroy.Add(OnVesselDestroy);
            jointsInit = true;
        }
        private void OnVesselDestroy(Vessel data)
        {
            ReInit();
        }

        void Update()
        {
            if (!HighLogic.LoadedSceneIsFlight)
            {
                jointsInit = true;
                return;
            }
            if (jointsInit || validCount == 0)
            {
                if (moduleRSR1 != null) moduleRSR1.Clear();
                 validCount = 0;
                int count = moduleRSR.Count;
                joints = new List<ConfigurableJoint>(count);
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
                    for (int i = 0; i < count; i++)
                    {
                        for (int j = 0; j < cjs.Length; j++)
                        {
                            if (!flags[j])
                            {
                                var item = cjs[j];
                                //if (item.gameObject.GetComponentInParent<ModuleRoboticServoRotor>().Equals(moduleRSR[i]))
                                //{
                                    joints.Add(item);
                                    flags[j] = true;
                                    if (moduleRSR1 == null || moduleRSR1.Count == 0)
                                        moduleRSR1 = new List<ModuleRoboticServoRotor>(count);
                                    rsr = item.gameObject.GetComponentInParent<ModuleRoboticServoRotor>();
                                    moduleRSR1.Add(rsr);
                                //}
                            }
                        }
                    }
                    jointsInit = false;
                    foreach (var item in joints)
                        if (item != null)
                            validCount++;
                    if (validCount == 0)
                        RotorAvail = false;
                    else RotorAvail = true;
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
                        AOACtrlEnabled = false;
                        TorqueCtrlEnabled = false;
                        break;
                    case 1:
                        AOACtrlEnabled = true;
                        TorqueCtrlEnabled = true;
                        break;
                    case 2:
                        AOACtrlEnabled = false;
                        TorqueCtrlEnabled = true;
                        break;
                    case 3:
                        AOACtrlEnabled = true;
                        TorqueCtrlEnabled = false;
                        break;
                }
                var s = (AOACtrlEnabled ? "En" : "Dis") + "abled" + AOAMsg + (TorqueCtrlEnabled ? "En" : "Dis") + "abled" + TorqueMsg;
                ScreenMessages.PostScreenMessage(s, 3f, ScreenMessageStyle.UPPER_RIGHT);
            }


            if (AOACtrlEnabled)

                for (int b = 0; b < moduleCS.Count; b++)
                {
                    ModuleControlSurface cs = moduleCS[b];
                    if (cs != null)
                    {
                        if (cs.deploy)
                        {
                            float action = 0;
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
                                action = num2 * num3 * Mathf.Sign(cs.ctrlSurfaceRange);
                            }
                            else if (!cs.usesMirrorDeploy)
                            {
                                float num5;
                                if (!cs.deployInvert)
                                    num5 = 1f;
                                else
                                    num5 = -1f;
                                action = -num5 * Mathf.Sign((Quaternion.Inverse(cs.vessel.ReferenceTransform.rotation) * (bt.position - cs.vessel.CurrentCoM)).x);
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
                                action = -num9 * num10;
                            }
                            invert = action < 0;


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
                }
            if (TorqueCtrlEnabled && RotorAvail)
            {
                for (int b = 0; b < moduleRSR1.Count; b++)
                {
                    var m = moduleRSR1[b];
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
                    Debug.Log("controlling");
                }
            }
        }

        private void OnGUI()
        {
#if DEBUG
#if UI_FANAOA
            if (!HighLogic.LoadedSceneIsFlight|| moduleCS == null || moduleCS.Count == 0) return;
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
            GUI.Label(new Rect(120, 120 + 11 * 18, 360, 64), "DD: " + DD);

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
            GUI.Label(new Rect(120, 120 + 0 * 18, 360, 64), "output limit:" + moduleRSR1[0].servoMotorLimit);
            GUI.Label(new Rect(120, 120 + 1 * 18, 360, 64), "U:" + pidRSR[0].u + "\tErr:" + pidRSR[0].e);

            GUI.Label(new Rect(120, 120 + 2 * 18, 360, 64), "Uk:" + pidRSR[0].Uk);
            GUI.Label(new Rect(120, 120 + 3 * 18, 360, 64), "Ui:" + pidRSR[0].Ui);
            GUI.Label(new Rect(120, 120 + 4 * 18, 360, 64), "Ud:" + pidRSR[0].Ud);
            GUI.Label(new Rect(120, 120 + 5 * 18, 360, 64), "targetRPM: " + moduleRSR1[0].rpmLimit);//ok
            GUI.Label(new Rect(120, 120 + 6 * 18, 360, 64), "currentRPM: " + (float)currentRPM.GetValue(moduleRSR1[0]));//ok
            GUI.Label(new Rect(120, 120 + 7 * 18, 360, 64), "targetAngularVelocity: " + joints[0].targetAngularVelocity.x);
            GUI.Label(new Rect(120, 120 + 8 * 18, 360, 64), "valid: " + validCount + "/RSR1:" + moduleRSR1.Count);

            ConfigurableJoint[] cjs = FindObjectsOfType<ConfigurableJoint>();
            int count = 0;
            foreach (var cj in cjs)
            {
                if (cj.gameObject.name == "TopJoint")
                    GUI.Label(new Rect(120, 120 + (9 + count) * 18, 360, 64), "find 1! total: " + ++count);
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
