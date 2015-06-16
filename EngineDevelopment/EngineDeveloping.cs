using System;
using SolverEngines;
using UnityEngine;

namespace EngineDevelopment
{
    public class EngineDeveloping:EngineSolver
    {
        protected bool combusting = true;

        protected double Isp_tc;//Not considering ullages
        protected FloatCurve Isptc_APressureCurve = null;
        protected double runningTime;
        protected double dynamicReliability;

        //Chamber
        protected double chamberPressure, chamberTemp;

        //Nozzle
        protected double nozzle_ePressure, nozzleTemp;
    }
}
