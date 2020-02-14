using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace PropellerPithAndRotorTorque
{
   public class PID
    {
        public bool invertControl = false;
        public float Ki, Kp, Td;
        public float i, e, lastE;
        private float[] eSqr;
        public float u, Uk, Ui, Ud;

        public float target;

        public PID()
        {
            eSqr = new float[3];
            setPID(0.015f, 0.07f, 0.002f);
        }
        public PID(float p,float i,float d)
        {
            eSqr = new float[3];
            Ki = i;
            Kp = p;
            Td = d;
        }

        public void setPID(float Kp, float Ki, float Td)
        {
            this.Ki = Ki;
            this.Kp = Kp;
            this.Td = Td;
        }

        public void setTarget(float t)
        {
            target = t;
        }

        public void setInvert(bool inv)
        {
            invertControl = inv;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="y">系统输出的观测值</param>
        /// <returns>返回u</returns>
        public float getU(float y)
        {
            e = y - target;
            var sum = 0.25f * (e * e + eSqr[0] + eSqr[1] + eSqr[2]);
            var multK = 28f - 392f / (sum + 14f);//随着方差和的增加衰减加快，逼近28
            var multI = 1 - 1 / (sum + 1.5f);
            multI = Mathf.Clamp(multI, 0.6f, 1000f);
            multK = Mathf.Clamp(multK, 0.6f, 1000f);
            i *= multI;
            Uk = Kp * e;
            i += TimeWarp.fixedDeltaTime * lastE;
            i = Mathf.Clamp(i, -2048, 2048);
            Ui = i * Ki;
            Ud = Td * (e - lastE) / TimeWarp.fixedDeltaTime;
            u = Uk + Kp * Ui + Kp * Ud;

            eSqr[2] = eSqr[1];
            eSqr[1] = eSqr[0];
            eSqr[0] = e*e;

            lastE = e;
            var uu = (invertControl ? -1 : 1) * u * multK;
            return uu;
        }
    }
}
