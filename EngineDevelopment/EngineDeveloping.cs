using System;
using SolverEngines;
using UnityEngine;
using System.Collections.Generic;

namespace EngineDevelopment
{
    public struct RFTank
    {
        public string name;
        public double rate;
        public float temp;
        public bool pFed;
    }
    public class EngineDeveloping:EngineSolver
    {
        protected bool combusting = true;

        protected double Isp_tc;
        protected FloatCurve Isptc_APressureCurve = null;
        protected double runningTime;
        protected double dynamicReliability;
        protected double jerkTolerance;
        protected short multiChamber;
        public PhysicsSimulator physicsSimulator = new PhysicsSimulator();


        protected float Cse_per_sqrt_t;
        protected float Cscost;
        //Chamber
        /// <summary>
        /// Pc_ns;
        /// Nozzle stagnation pressure or chamber
        /// total pressure at nozzle inlet;
        /// </summary>
        protected double chamberPressure;
        /// <summary>
        /// Tc_ns
        /// Nozzle stagnation temperature or
        ///chamber total temperature;
        /// </summary>
        protected double chamberTemp;

        //Nozzle
        /// <summary>
        /// At
        /// Flow area at throat;
        /// </summary>
        protected double nozzle_tArea/*Ae*/;
        /// <summary>
        /// Pe
        /// Flow static pressures at exit;
        /// </summary>
        protected double nozzle_ePressure/*Pe*/;
        /// <summary>
        /// Ae
        /// Flow area at exit;
        /// </summary>
        protected double nozzle_eArea/*Pe*/;
        /// <summary>
        /// ε
        /// Nozzle expansion area ratio;
        /// </summary>
        protected double nozzle_eExpansionRatio/*e*/;

        //Cycle 
        /// <summary>
        /// Maximum mass flow can load
        /// </summary>
        protected double cycleMaxMassFlow;
        /// <summary>
        /// Maximum mass flow can load
        /// </summary>
        protected double cycleMinMassFlow;
        /// <summary>
        /// Fuel Efficienty
        /// </summary>
        protected double cycleFuelEfficiency;

        public void CalculatePhysics(Vessel vessel,Part engine,float deltaTime,float fuelRatio)
        {
            Debug.Log("CalculatePhysics:start");
            physicsSimulator.Update(vessel, engine, deltaTime, fuelRatio);
            Debug.Log("CalculatePhysics:end");
        }

        public void reset()
        {
            Debug.Log("reset:start");
            physicsSimulator.Reset();
            Debug.Log("reset:end");
        }

        public void InitializeOverallEngineData(
            float mCse_per_sqrt_t,
            float mCscost,
            float mPcns,
            float mTcns,
            float mPe,
            float mAe,
            float mmaxFuelFlow,
            float mminFuelFlow,
            float mfuelEfficiency = 1
            )
        {
            Cse_per_sqrt_t = mCse_per_sqrt_t;
            Cscost = mCscost;
            chamberPressure = mPcns;
            chamberTemp = mTcns;
            nozzle_eArea = mAe;
            nozzle_ePressure = mPe;
            cycleMaxMassFlow = mmaxFuelFlow;
            cycleMinMassFlow = mminFuelFlow;
            cycleFuelEfficiency = mfuelEfficiency;
            return;
        }
    }
}
