using System;
using SolverEngines;
using UnityEngine;

namespace EngineDevelopment
{
    public class EngineDeveloping:EngineSolver
    {
        protected double Isp_tc;
        protected FloatCurve atmosphereCurve = null, atmCurve = null, velCurve = null;
        protected double runningTime;

    }
}
