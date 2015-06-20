﻿using System;
using SolverEngines;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace EngineDevelopment
{
    struct RFTank
    {
        public string name;
        public double rate;
        public float temp;
        public bool pFed;
    }
    class ModuleEngineDev : ModuleEnginesSolver, IPartCostModifier, IPartMassModifier
    {
        #region Fields
        [KSPField(isPersistant = false, guiActive = false)]
        public int maxBurnTime = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int burningTime = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int reliability = 0;
        [KSPField(isPersistant = false, guiActive = true)]
        public int ignitionsAvailable = 0;
        [KSPField(isPersistant = true)]
        public int ignitionsRemained = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public int jerkTolerance = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public int multiChamber = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int chamber = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int nozzle = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int powerCycle = 0;

        #endregion

        #region RF adapter
        private int vParts = -1;
        private static Type rfModule, rfTank;
        private static FieldInfo RFname, RFloss_rate, RFtemperature, RFfuelList, RFpressurizedFuels;
        private static bool noRFfields = true;
        private static bool noRFTankfields = true;
        private Dictionary<Part, Dictionary<string, RFTank>> vesselTanks = null;
        private bool fuelPressurized;

        private void UpdateRF(bool inFlight)
        {
            Debug.Log("UpdateRF");
            int curParts = -1;
            List<Part> partList = null;
            if (inFlight)
            {
                if (vessel == null)
                {
                    vParts = -1;
                    return;
                }
                else
                {
                    curParts = vessel.Parts.Count;
                    partList = vessel.parts;
                }
            }
            else
            {
                if (EditorLogic.SortedShipList.Count == 0)
                {
                    vParts = -1;
                    return;
                }
                else
                {
                    curParts = EditorLogic.SortedShipList.Count;
                    partList = EditorLogic.SortedShipList;
                }
            }
            if (vesselTanks == null || vParts != curParts)
            {
                vesselTanks = new Dictionary<Part, Dictionary<string, RFTank>>();
                for (int i = 0; i < partList.Count; i++)
                {
                    if (!partList[i].Modules.Contains("ModuleFuelTanks"))
                        continue;
                    PartModule mfsModule = partList[i].Modules["ModuleFuelTanks"];
                    if (noRFfields)
                    {
                        rfModule = mfsModule.GetType();
                        RFfuelList = rfModule.GetField("fuelList");
                        RFpressurizedFuels = rfModule.GetField("pressurizedFuels");
                        noRFfields = false;
                    }

                    IEnumerable tankList = (IEnumerable)(RFfuelList.GetValue(mfsModule));
                    Dictionary<string, bool> pfed = (Dictionary<string, bool>)RFpressurizedFuels.GetValue(mfsModule);
                    Dictionary<string, RFTank> tanks = new Dictionary<string, RFTank>();
                    foreach (var obj in tankList)
                    {
                        if (noRFTankfields)
                        {
                            rfTank = obj.GetType();
                            RFname = rfTank.GetField("name");
                            RFloss_rate = rfTank.GetField("loss_rate");
                            RFtemperature = rfTank.GetField("temperature");
                            noRFTankfields = false;
                        }
                        RFTank tank = new RFTank();
                        tank.name = (string)(RFname.GetValue(obj));
                        tank.rate = (double)(RFloss_rate.GetValue(obj));
                        tank.temp = (float)(RFtemperature.GetValue(obj));
                        if (pfed.ContainsKey(tank.name) && pfed[tank.name])
                            tank.pFed = true;
                        else
                            tank.pFed = false;
                        tanks[tank.name] = tank;
                    }
                    vesselTanks[partList[i]] = tanks;
                }
                vParts = curParts;
            }
        }
        private bool RFIsPressurized(PartResource pr)
        {
            Debug.Log("RFIsPressurized");
            if (pr.part == null || pr.amount <= 0)
                return false;
            if (vesselTanks != null) // should never be null, but let's not assume.
                if (vesselTanks.ContainsKey(pr.part))
                    if (vesselTanks[pr.part].ContainsKey(pr.resourceName))
                        return vesselTanks[pr.part][pr.resourceName].pFed;
            return false;
        }
        private float RFFuelRatio()
        {
            float minFuelRatio = 0;
            foreach (Propellant p in propellants)
            {
                double fuelAmount = 0.0;
                double fuelMaxAmount = 0.0;
                bool foundPressurizedSource = false;
                List<PartResource> resourceSources = new List<PartResource>();
                part.GetConnectedResources(p.id, p.GetFlowMode(), resourceSources);
                foreach (PartResource pr in resourceSources)
                {
                    if (foundPressurizedSource == false && RFIsPressurized(pr) == true)
                    {
                        foundPressurizedSource = true;
                    }

                    fuelAmount += pr.amount;
                    fuelMaxAmount += pr.maxAmount;
                }


                if (minFuelRatio > fuelAmount / fuelMaxAmount)
                    minFuelRatio = Convert.ToSingle(fuelAmount / fuelMaxAmount);

                if (foundPressurizedSource == false)
                {
                    fuelPressurized = false;
                }
            }
            return minFuelRatio;

        }
        #endregion

        public List<ConfigNode> nozzleAlts;
        public List<ConfigNode> cycleAlts;
        public List<ConfigNode> chamberAlts;
        private ConfigNode cycleDefault = null;
        private ConfigNode nozzleDefault = null;
        private ConfigNode chamberDefault = null;
        private ConfigNode cycleChoice = null;
        private ConfigNode nozzleChoice = null;
        private ConfigNode chamberChoice = null;
        private float massMult = 1;
        private float costMult = 1;

        private float Isp_vac_o = -1;
        private float Isp_atm_o = -1;
        private float FF_o = -1;
        private float V_e_o = -1;
        private float detC_o = -1;
        private float Cscost = -1;
        private float Pe_d = -1, Ae_d = -1, maxFuelFlow_d = -1, minFuelFlow_d = -1, Tcns_d = -1, Pcns_d = -1, Cse_per_sqrt_t_d = -1;
        private float Pe_c = -1, Ae_c = -1, maxFuelFlow_c = -1, minFuelFlow_c = -1, Tcns_c = -1, Pcns_c = -1, C_per_sqrt_t_c = -1;

        #region Overrides
        public override void OnLoad(ConfigNode node)
        {
            Debug.Log("OnLoad:start");
            base.OnLoad(node);
            if (node.GetValue("isSaved") == "1")
            {
                Debug.Log("OnLoad:LoadingSaved");
                chamberDefault = node.GetNode("CHAMBERDEFAULT");
                cycleDefault = node.GetNode("POWERCYCLEDEFAULT");
                nozzleDefault = node.GetNode("NOZZLEDEFAULT");
                chamberChoice = node.GetNode("CHAMBERCHOICE");
                cycleChoice = node.GetNode("POWERCYCLECHOICE");
                nozzleChoice = node.GetNode("NOZZLECHOICE");
            }
            else
            {
                //Load the setting nodes
                foreach (ConfigNode subNode in node.GetNodes("NOZZLEALT"))
                {
                    ConfigNode newNode = new ConfigNode("NOZZLEALT");
                    subNode.CopyTo(newNode);
                    nozzleAlts.Add(newNode);
                    if (newNode.GetValue("isDefault") == "1") subNode.CopyTo(nozzleDefault);
                    /*TEMPORARY*/
                    if (newNode.GetValue("type") == nozzle.ToString()) subNode.CopyTo(nozzleChoice);
                    Debug.Log("OnLoad:Nozzle:" + newNode.GetValue("name"));
                }
                foreach (ConfigNode subNode in node.GetNodes("POWERCYCLEALT"))
                {
                    ConfigNode newNode = new ConfigNode("POWERCYCLEALT");
                    subNode.CopyTo(newNode);
                    cycleAlts.Add(newNode);
                    if (newNode.GetValue("isDefault") == "1") subNode.CopyTo(cycleDefault);
                    /*TEMPORARY*/
                    if (newNode.GetValue("type") == powerCycle.ToString()) subNode.CopyTo(cycleChoice);
                    Debug.Log("OnLoad:PC:" + newNode.GetValue("name"));
                }
                foreach (ConfigNode subNode in node.GetNodes("CHAMBERALT"))
                {
                    ConfigNode newNode = new ConfigNode("CHAMBERALT");
                    subNode.CopyTo(newNode);
                    chamberAlts.Add(newNode);
                    if (newNode.GetValue("isDefault") == "1") subNode.CopyTo(chamberDefault);
                    /*TEMPORARY*/
                    if (newNode.GetValue("type") == chamber.ToString()) subNode.CopyTo(chamberChoice);
                    Debug.Log("OnLoad:Chamber:" + newNode.GetValue("name"));
                }
            }
            //Get the defaults first
            float outMaxFF, outMinFF;
            if (float.TryParse(cycleDefault.GetValue("maxMassFlow"), out outMaxFF)) maxFuelFlow_d = outMaxFF;
            if (float.TryParse(cycleDefault.GetValue("minMassFlow"), out outMinFF)) minFuelFlow_d = outMinFF;
            float outPe, outAe;
            if (float.TryParse(nozzleDefault.GetValue("exitPressure"), out outPe)) Pe_d = outPe;
            if (float.TryParse(nozzleDefault.GetValue("exitArea"), out outAe)) Ae_d = outAe;
            float outPcns, outTcns;
            if (float.TryParse(chamberDefault.GetValue("chamberPressure"), out outPcns)) Pcns_d = outPcns;
            if (float.TryParse(chamberDefault.GetValue("chamberTemperature"), out outTcns)) Tcns_d = outTcns;
            Debug.Log("OnLoad:Default::" + maxFuelFlow_d + "  " + minFuelFlow_d + "  " + Pe_d + "  " + Ae_d + "  " + Pcns_d + "  " + Tcns_d);
            //Start to think about performance calc:
            //Units:V-m/s    T-k    P-kPa   Isp-s   FF-ton/s
            //calc the raw data (_o)
            Isp_vac_o = atmosphereCurve.Evaluate(0);
            Isp_atm_o = atmosphereCurve.Evaluate(1);
            FF_o = (maxThrust) / (Isp_vac_o * 9.80665f);
            V_e_o = Isp_vac_o * 9.80665f;
            //We have Pe and Pc_ns,so we can calc ε (1.20),but Where is Gamma? 
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
            //It seems that we can use [Cstar*ε/sqrt(Tc_ns)] as a cost,so Gamma is included
            //NONONO,I forgot that ε isn't a COST but a var of Pe and Pc_ns
            //So we got a smaller Cstar
            Cse_per_sqrt_t_d = detC_o * Pcns_d / (Mathf.Sqrt(Tcns_d) * 101.3125f);//[Cstar*ε/sqrt(Tc_ns)]
            Cscost = Isp_vac_o * 9.80665f - detC_o  * (Pe_d)/ (101.3125f);//[Cstar*COST]


            //TEMPORARY：Load and calculate the choices
            try
            {
                if (float.TryParse(cycleChoice.GetValue("maxMassFlow"), out outMaxFF)) maxFuelFlow_c = outMaxFF;
                if (float.TryParse(cycleChoice.GetValue("minMassFlow"), out outMinFF)) minFuelFlow_c = outMinFF;
                if (float.TryParse(nozzleChoice.GetValue("exitPressure"), out outPe)) Pe_c = outPe;
                if (float.TryParse(nozzleChoice.GetValue("exitArea"), out outAe)) Ae_c = outAe;
                if (float.TryParse(chamberChoice.GetValue("chamberPressure"), out outPcns)) Pcns_c = outPcns;
                if (float.TryParse(chamberChoice.GetValue("chamberTemperature"), out outTcns)) Tcns_c = outTcns;
                Debug.Log("OnLoad:Choice::" + maxFuelFlow_c + "  " + minFuelFlow_c + "  " + Pe_c + "  " + Ae_c + "  " + Pcns_c + "  " + Tcns_c);
            }
            catch (Exception e) { Debug.Log(e); }


            Debug.Log("OnLoad:end:Cse_per_sqrt_t_d:"+ Cse_per_sqrt_t_d+ "   Cscost_per_sqrt_t_d:" + Cscost );
        }
        public override void OnSave(ConfigNode node) {
            Debug.Log("OnSave:start");
            base.OnSave(node);
            node.AddNode("CHAMBERDEFAULT", chamberDefault);
            node.AddNode("POWERCYCLEDEFAULT", cycleDefault);
            node.AddNode("NOZZLEDEFAULT", nozzleDefault);
            node.AddNode("CHAMBERCHOICE", chamberChoice);
            node.AddNode("POWERCYCLECHOICE", cycleChoice);
            node.AddNode("NOZZLECHOICE", nozzleChoice);
            node.SetValue("isSaved", "1");
            Debug.Log("OnSave:end");
        }
        public override void CreateEngine()
        {
            Debug.Log("CreateEngine:start");
            engineSolver = new EngineDeveloping();
            //(engineSolver as EngineDeveloping).InitializeOverallEngineData(
            //    Cse_per_sqrt_t_d,
            //    Cscost, 
            //    Pcns_c,
            //    Tcns_c,
            //    Pe_c,
            //    Ae_c,
            //    maxFuelFlow_c,
            //    minFuelFlow_c
            //    );
            (engineSolver as EngineDeveloping).InitializeOverallEngineData(
                176.7246f,
                2896.138f,
                2000,
                3000,
                30f,
                1f,
                63.74f,
                0
                );
            useAtmCurve = useAtmCurve = false;
            Debug.Log("CreateEngine:end");
        }
        public override void OnAwake()
        {
            Debug.Log("OnAwake:start");
            base.OnAwake();
            nozzleAlts = new List<ConfigNode>();
            cycleAlts = new List<ConfigNode>();
            chamberAlts = new List<ConfigNode>();
            vesselTanks = new Dictionary<Part, Dictionary<string, RFTank>>();
            nozzleDefault = new ConfigNode("NOZZLEALT");
            cycleDefault = new ConfigNode("POWERCYCLEALT");
            chamberDefault = new ConfigNode("CHAMBERALT");
            /*TEMPORARY*/
            nozzleChoice = new ConfigNode("NOZZLEALT");
            /*TEMPORARY*/
            cycleChoice = new ConfigNode("POWERCYCLEALT");
            /*TEMPORARY*/
            chamberChoice = new ConfigNode("CHAMBERALT");
            Debug.Log("OnAwake:end");
        }
        public override void UpdateFlightCondition(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool oxygen)
        {
            Debug.Log("UpdateFlightCondition:start");
            Debug.Log("UpdateFlightCondition:" + ambientTherm + "-" + altitude + "-" + vel + "-" + mach + "-" + oxygen);
            Debug.Log("UpdateFlightCondition:Engine :T" + engineSolver.GetEngineTemp() + ",Isp:" + engineSolver.GetIsp() + ",S:" + engineSolver.GetStatus() + ",R:" + engineSolver.GetRunning());
            base.UpdateFlightCondition(ambientTherm, altitude, vel, mach, oxygen);
            (engineSolver as EngineDeveloping).CalculatePhysics(vessel, part, TimeWarp.fixedDeltaTime, RFFuelRatio());


            Debug.Log("UpdateFlightCondition:end");
        }

        public override void UpdateThrottle()
        {
            if (throttleLocked)
                requestedThrottle = 1f;
            if ((!EngineIgnited) && (ignitionsRemained > 0 || ignitionsRemained == -1) && (currentThrottle <= 0.01f) && requestedThrottle >= 0.01f)
            {
                if (!(engineSolver as EngineDeveloping).CheckForIgnition(RFFuelRatio()))
                { currentThrottle = actualThrottle = 0; SetFlameout(); PlayFlameoutFX(true); return; }
                else EngineIgnited = true;
                ignitionsRemained = (ignitionsRemained == -1) ? -1 : (ignitionsRemained - 1);
            }
            if (!useEngineResponseTime)
                currentThrottle = requestedThrottle * thrustPercentage * 0.01f;
            else
            {
                float requiredThrottle = requestedThrottle * thrustPercentage * 0.01f;
                float deltaT = TimeWarp.fixedDeltaTime;

                float d = requiredThrottle - currentThrottle;
                float thisTick = (d > 0 ? engineAccelerationSpeed : engineDecelerationSpeed) * d * 2 * deltaT;/*MAGIC*/
                if (Math.Abs((double)d) > thisTick)
                {
                    if (d > 0f)
                        currentThrottle += thisTick;
                    else
                        currentThrottle -= thisTick;
                }
                else
                    currentThrottle = requiredThrottle;
            }
            currentThrottle = Mathf.Max(0.01f, currentThrottle);
            actualThrottle = Mathf.RoundToInt(currentThrottle * 100f);
            //Don't need base.UpdateThrottle here
        }
        #endregion

        #region Info
        protected string GetThrustInfo()
        {
            Debug.Log("GetThrustInfo:start");
            string output = "";
            Debug.Log("GetThrustInfo:end");
            return output;
        }

        public override string GetModuleTitle()
        {
            return "Engine Development";
        }
        public override string GetPrimaryField()
        {
            return "";
        }
        public override string GetInfo()
        {
            string output = GetThrustInfo();


            return output;
        }
        #endregion

        public float GetModuleCost(float defaultCost)
        {
            return defaultCost * costMult;
        }

        public float GetModuleMass(float defaultMass)
        {
            return defaultMass * massMult;
        }
    }

}
