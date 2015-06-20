using System;
using SolverEngines;
using UnityEngine;
using System.Collections.Generic;

namespace EngineDevelopment
{ 
    public class EngineDeveloping : EngineSolver
    {
        protected float dynamicReliability;
        public PhysicsSimulator physicsSimulator = new PhysicsSimulator();


        protected double Cse_per_sqrt_t = -1;
        protected double Cscost = -1;
        protected double Cstar = -1;
        protected double Ct = -1;

        //Chamber
        /// <summary>
        /// Pc_ns;
        /// Nozzle stagnation pressure or chamber
        /// total pressure at nozzle inlet;
        /// </summary>
        protected double chamberPressure = -1;
        /// <summary>
        /// Tc_ns
        /// Nozzle stagnation temperature or
        ///chamber total temperature;
        /// </summary>
        protected double chamberTemp = -1;

        //Nozzle
        /// <summary>
        /// At
        /// Flow area at throat;
        /// </summary>
        protected double nozzle_tArea = -1/*Ae*/;
        /// <summary>
        /// Pe
        /// Flow static pressures at exit;
        /// </summary>
        protected double nozzle_ePressure = -1/*Pe*/;
        /// <summary>
        /// Ae
        /// Flow area at exit;
        /// </summary>
        protected double nozzle_eArea = -1/*Pe*/;
        /// <summary>
        /// ε
        /// Nozzle expansion area ratio;
        /// </summary>
        protected double nozzle_ExpansionRatio = -1/*e*/;

        //Cycle 
        /// <summary>
        /// Maximum mass flow can load
        /// </summary>
        protected double cycleMaxMassFlow = -1;
        /// <summary>
        /// Maximum mass flow can load
        /// </summary>
        protected double cycleMinMassFlow = -1;
        /// <summary>
        /// Fuel Efficienty
        /// </summary>
        protected double cycleFuelEfficiency = 1;

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
            double pR = chamberPressure / nozzle_ePressure;
            double gamma_t=CalculateGamma(chamberTemp,1);//TODO:Fuel Fraction?
            double inv_gamma_t= 1/gamma_t;
            double inv_gamma_tm1= 1/(gamma_t-1);
            nozzle_ExpansionRatio = 
                (Math.Pow(2d / (gamma_t + 1d), (inv_gamma_tm1)) * Math.Pow(pR, inv_gamma_t)) 
                /
                Math.Sqrt((gamma_t+1)*inv_gamma_tm1*(1- Math.Pow(1/pR,gamma_t/inv_gamma_t)));
            nozzle_tArea = nozzle_eArea / nozzle_ExpansionRatio;
            Debug.Log("InitEng::" + cycleMaxMassFlow + "  " + cycleMinMassFlow + "  " + nozzle_ePressure + "  " + nozzle_eArea + "  " + chamberPressure + "  " + chamberTemp);
            Debug.Log("InitEng::" + nozzle_ExpansionRatio + "  " + nozzle_tArea);
            return;
        }
        //Units:V-m/s    T-k    P-kPa   Isp-s   FF-ton/s

        //Cstar ∝ sqrt(Tc_ns)  (1.32a)
        //Ct==COST + ε(Pe-Pa)/Pc_ns
        //C==Cstar*Ct
        //C==Ve+Ae(Pe-Pa)(g/FF) (1-8)
        //Isp=C/9.80665f
        //C==Cstar*（COST+ε(Pe-Pa)/Pc_ns）==Cstar*（COST+εPe/Pc_ns-εPa/Pc_ns）=Cstar*COST+Cstar*ε(Pe-Pa)/Pc_ns
        //detC==Cstar*(Ct_vac-Ct_atm)==Cstar*(ε(Pe)/Pc_ns-ε(Pe-Pa)/Pc_ns)==Cstar*ε*Pa/Pc_ns
        //Cse_per_sqrt_t==[Cstar*ε/sqrt(Tc_ns)]
        //Cscost_per_sqrt_t==[Cstar*COST/sqrt(Tc_ns)]

        //NONONO,I forgot that ε isn't a COST but a var of Pe and Pc_ns
        //So we got a smaller Cstar
        public override void CalculatePerformance(double airRatio, double commandedThrottle, double flowMult, double ispMult)
        {
            Debug.Log("\nCalc:start");
            Debug.Log("cycleMaxMassFlow:" + cycleMaxMassFlow);
            Debug.Log("cycleMinMassFlow:" + cycleMinMassFlow);

            base.CalculatePerformance(airRatio, commandedThrottle, flowMult, ispMult);

            Cstar = Cse_per_sqrt_t * Math.Sqrt(chamberTemp) / nozzle_ExpansionRatio;//TODO:Calc the correct Cstar (
            Ct = Cscost / Cstar + nozzle_ExpansionRatio * (nozzle_ePressure - p0/1000) / chamberPressure;
            Isp = Cstar * Ct / 9.80665d;
            Isp *= ispMult;
            Debug.Log(":::p0:" + p0/1000 + ":::Cscost:" + Cscost + ":::chamberPressure:" + chamberPressure);
            Debug.Log(":::Cstar:" + Cstar+":::Ct:"+Ct+ ":::ExpansionRatio:"+ nozzle_ExpansionRatio+":::Isp:"+Isp);
            if (commandedThrottle * cycleMaxMassFlow < cycleMinMassFlow)
            {
                commandedThrottle = cycleMinMassFlow / cycleMaxMassFlow;
            }
            fuelFlow = commandedThrottle * cycleMaxMassFlow * flowMult / cycleFuelEfficiency;

            thrust = Isp * fuelFlow*9.80665;
            Debug.Log("!!!!!!!Thrust:"+thrust);
            Debug.Log("Calc:end\n");

        }

        public void CalculatePhysics(Vessel vessel, Part engine, float deltaTime, float fuelRatio)
        {
            Debug.Log("CalculatePhysics:start");
            physicsSimulator.Update(vessel, engine, deltaTime, fuelRatio);
            Debug.Log("CalculatePhysics:end");
        }

        public void Reset()
        {
            Debug.Log("reset:start");
            physicsSimulator.Reset();
            InitializeOverallEngineData(-1, -1, -1, -1, -1, -1, -1, -1, 1);
            Debug.Log("reset:end");
        }


        public bool CheckForIgnition(float minFuelRatio)
        {
            float fuelFlowStability = physicsSimulator.GetFuelFlowStability(minFuelRatio);

            return true;
        }

    }
}
