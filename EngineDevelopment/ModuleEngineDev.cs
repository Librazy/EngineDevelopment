using System;
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

        [KSPField(isPersistant = true, guiActive = false)]
        public float Pe_d = -1, Ae_d = -1, maxFuelFlow_d = -1, minFuelFlow_d = -1, Tcns_d = -1, Pcns_d = -1;
        [KSPField(isPersistant = true, guiActive = false)]
        public float Pe_c = -1, Ae_c = -1, maxFuelFlow_c = -1, minFuelFlow_c = -1, Tcns_c = -1, Pcns_c = -1;

        #region Overrides
        public override void CreateEngine()
        {
            Debug.Log("CreateEngine:start");
            engineSolver = new EngineDeveloping();
            //(engineSolver as EngineDeveloping).InitializeOverallEngineData(
            //    Pcns_c,
            //    Tcns_c,
            //    Pe_c,
            //    Ae_c,
            //    maxFuelFlow_c,
            //    minFuelFlow_c
            //    );
            (engineSolver as EngineDeveloping).InitializeDefaultEngineData(
                atmosphereCurve.Evaluate(0),
                atmosphereCurve.Evaluate(1),
                maxThrust,
                2000,
                3000,
                50f,
                0.7f,
                63.74f,
                0f,
                1f
            );
            (engineSolver as EngineDeveloping).InitializeOverallEngineData(
                2500,
                3300,
                20f,//Pe
                1f,//Ae
                63.74f,
                0f,
                1f
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
            currentThrottle = Mathf.Max(0.000f, currentThrottle);
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
