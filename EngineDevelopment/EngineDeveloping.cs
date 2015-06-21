﻿using System;
using SolverEngines;
using UnityEngine;
using System.Collections.Generic;

namespace EngineDevelopment
{ 
    public class EngineDeveloping : EngineSolver
    {
        protected float dynamicReliability;
        public PhysicsSimulator physicsSimulator = new PhysicsSimulator();




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
        //Overall
        protected double pR = -1;


        private float Pe_d = -1, Ae_d = -1, maxFuelFlow_d = -1, minFuelFlow_d = -1, Tcns_d = -1, Pcns_d = -1, fuelEfficiency_d = -1;

        private float Isp_vac_o = -1;
        private float Isp_atm_o = -1;
        private double Cs_per_sqrt_t = -1;
        private double Cscost_per_pR_Frac_sqrt_t = -1;
        private double Cstar = -1;
        private double Ct = -1;
        private double FF_o = -1;
        private double detC_o = -1;

        public void InitializeDefaultEngineData(
            float mIsp_vac,
            float mIsp_atm,
            float mmaxThrust,
            float mPcns,
            float mTcns,
            float mPe,
            float mAe,
            float mmaxFuelFlow,
            float mminFuelFlow,
            float mfuelEfficiency = 1
        )
        {
            Isp_vac_o = mIsp_vac;
            Isp_atm_o = mIsp_atm;
            Pcns_d = mPcns;
            Tcns_d = mTcns;
            Pe_d = mPe;
            Ae_d = mAe;
            maxFuelFlow_d = mmaxFuelFlow;
            minFuelFlow_d = mminFuelFlow;
            FF_o = (mmaxThrust) / (Isp_vac_o * 9.80665f);
            //We have Pe and Pc_ns,so we can calc ε (1.20)
            //
            //Cstar ∝ sqrt(Tc_ns)  (1.32a)
            //Ct==COST + ε(Pe-Pa)/Pc_ns
            //C==Cstar*Ct?
            //C==Ve+Ae(Pe-Pa)(g/FF)?  (1-8)
            //Isp=C/9.80665f
            //Catm==Cstar*（COST+ε(Pe-Pa)/Pc_ns）==Cstar*（COST+εPe/Pc_ns-εPa/Pc_ns）
            //Catm==Cstar*COST+Cstar*ε(Pe-Pa)/Pc_ns
            //Cvac==Cstar*COST+Cstar*εPe/Pc_ns
            //Let defaults(_d) fit the raw data
            //
            detC_o = (Isp_vac_o - Isp_atm_o) * 9.80665f;
            //detC==Cstar*(Ct_vac-Ct_atm)==Cstar*(ε(Pe)/Pc_ns-ε(Pe-Pa)/Pc_ns)==Cstar*ε*Pa/Pc_ns
            //It seems that we can use [Cstar/sqrt(Tc_ns)] as a cost
            //Actually COST is a function of Pe and Pc_ns
            //We need to calc Sqrt(1-(1/pR_d)^((gamma_d-1)*inv_gamma_d)) as pR_Frac
            double pR_d = Pcns_d / Pe_d;
            double gamma_d = CalculateGamma(Tcns_d, 1);//TODO:Fuel Fraction?
            double inv_gamma_d = 1 / gamma_d;
            double inv_gamma_dm1 = 1 / (gamma_d - 1);
            double pR_Frac = Math.Sqrt(1 - Math.Pow(1/pR_d, ((gamma_d - 1) * inv_gamma_d)));
            double e_d =
                (Math.Pow(2d / (gamma_d + 1d), (inv_gamma_dm1)) * Math.Pow(pR_d, inv_gamma_d))
                 /
                Math.Sqrt((gamma_d + 1) * inv_gamma_dm1 * (1 - Math.Pow(1 / pR_d, gamma_d / inv_gamma_d)));
            double sqrtT_d = Mathf.Sqrt(Tcns_d);
            Cs_per_sqrt_t = detC_o * Pcns_d / (sqrtT_d * 101.3125f* e_d);//[Cstar/sqrt(Tc_ns)]
            Cscost_per_pR_Frac_sqrt_t = (Isp_vac_o * 9.80665f - detC_o * (Pe_d) / (101.3125f))/ (pR_Frac*sqrtT_d);//[Cstar/sqrt(Tc_ns)*COST_per_pR_Frac]
            Debug.Log("InitDef::" + cycleMaxMassFlow + "  " + cycleMinMassFlow + "  " + nozzle_ePressure + "  " + nozzle_eArea + "  " + chamberPressure + "  " + chamberTemp);
            Debug.Log("InitDef::" + nozzle_ExpansionRatio + "  " + nozzle_tArea);
            return;
        }
        public void InitializeOverallEngineData(
            float mPcns,
            float mTcns,
            float mPe,
            float mAe,
            float mmaxFuelFlow,
            float mminFuelFlow,
            float mfuelEfficiency = 1
            )
        {
            chamberPressure = mPcns;
            chamberTemp = mTcns;
            nozzle_eArea = mAe;
            nozzle_ePressure = mPe;
            cycleMaxMassFlow = mmaxFuelFlow;
            cycleMinMassFlow = mminFuelFlow;
            cycleFuelEfficiency = mfuelEfficiency;
            pR = chamberPressure / nozzle_ePressure;
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

        public override void CalculatePerformance(double airRatio, double commandedThrottle, double flowMult, double ispMult)
        {
            Debug.Log("\nCalc:start");
            Debug.Log("cycleMaxMassFlow:" + cycleMaxMassFlow);
            Debug.Log("cycleMinMassFlow:" + cycleMinMassFlow);

            base.CalculatePerformance(airRatio, commandedThrottle, flowMult, ispMult);
            double gamma = CalculateGamma(chamberTemp, 1);//TODO:Fuel Fraction? Can we cache gamma?
            double inv_gamma = 1 / gamma;
            double inv_gamma_tm1 = 1 / (gamma - 1);

            double pR_Frac = Math.Sqrt(1 - Math.Pow(1/pR, ((gamma - 1) * inv_gamma)));


            double sqrtT = Math.Sqrt(chamberTemp);
            Cstar = Cs_per_sqrt_t * sqrtT;//TODO:Calc the correct Cstar (
            Ct = Cscost_per_pR_Frac_sqrt_t * sqrtT * pR_Frac / Cstar + nozzle_ExpansionRatio * (nozzle_ePressure - p0 / 1000) / chamberPressure;
            Isp = Cstar * Ct / 9.80665d;
            Isp *= ispMult;
            Debug.Log(":::p0:" + p0/1000 + ":::Cscost_per_pR_Frac_per_sqrt_T:" + Cscost_per_pR_Frac_sqrt_t + ":::chamberPressure:" + chamberPressure+ ":::pR_Frac:"+ pR_Frac);
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
            InitializeOverallEngineData(-1, -1, -1, -1, -1, -1, 1);
            Debug.Log("reset:end");
        }


        public bool CheckForIgnition(float minFuelRatio)
        {
            float fuelFlowStability = physicsSimulator.GetFuelFlowStability(minFuelRatio);

            return true;
        }

    }
}
