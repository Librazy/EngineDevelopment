using System;
using SolverEngines;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;

namespace EngineDevelopment
{
    class ModuleEngineDev : ModuleEnginesSolver, IPartCostModifier, IPartMassModifier
    {
        public List<ConfigNode> nozzleAlts;
        public List<ConfigNode> cycleAlts;
        public List<ConfigNode> chamberAlts;
        //#region Fields
        [KSPField(isPersistant = true, guiActive = false)]
        public int maxBurnTime = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int reliability = 0;
        [KSPField(isPersistant = true, guiActive = true)]
        public int ignitionsAvailable = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public int jerkResistance = 0;

        protected bool instantThrottle = false;
        protected float throttleResponseRate;
        protected EngineDeveloping engDevSolver = null;
        //#endregion
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
        private ConfigNode cycleDefault = null;
        private ConfigNode nozzleDefault = null;
        private ConfigNode chamberDefault = null;
        public override void OnLoad(ConfigNode node)
        {
            Debug.Log("OnLoad:start");
            base.OnLoad(node);

            //Load the setting nodes
            foreach (ConfigNode subNode in node.GetNodes("NOZZLEALT"))
            {
                ConfigNode newNode = new ConfigNode("NOZZLEALT");
                subNode.CopyTo(newNode);
                nozzleAlts.Add(newNode);
                if (newNode.GetValue("isDefault") == "1") subNode.CopyTo(nozzleDefault);
                Debug.Log("OnLoad:Nozzle" + newNode.GetValue("name"));
            }
            foreach (ConfigNode subNode in node.GetNodes("POWERCYCLEALT"))
            {
                ConfigNode newNode = new ConfigNode("POWERCYCLEALT");
                subNode.CopyTo(newNode);
                cycleAlts.Add(newNode);
                if (newNode.GetValue("isDefault") == "1") subNode.CopyTo(cycleDefault);
                Debug.Log("OnLoad:PC" + newNode.GetValue("name"));
            }
            foreach (ConfigNode subNode in node.GetNodes("CHAMBERALT"))
            {
                ConfigNode newNode = new ConfigNode("CHAMBERALT");
                subNode.CopyTo(newNode);
                chamberAlts.Add(newNode);
                if (newNode.GetValue("isDefault") == "1") subNode.CopyTo(chamberDefault);
                Debug.Log("OnLoad:Chamber" + newNode.GetValue("name"));
            }
            float outMaxFF, outMinFF;
            if (float.TryParse(cycleDefault.GetValue("maxMassFlow"), out outMaxFF))maxFuelFlow = outMaxFF;
            if (float.TryParse(cycleDefault.GetValue("minMassFlow"), out outMinFF))minFuelFlow = outMinFF;

            
            maxEngineTemp = 10000;
            Debug.Log("OnLoad:end");
        }
        #region Overrides
        public override void CreateEngine()
        {
            Debug.Log("CreateEngine:start");
            engDevSolver = new EngineDeveloping();
            engineSolver = engDevSolver;
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
            Debug.Log("OnAwake:end");
        }
        public override void OnStart(StartState state)
        {
            Debug.Log("OnStart:start");
            base.OnStart(state);
            Debug.Log("OnStart:end");
        }
        public override void UpdateFlightCondition(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool oxygen)
        {
            Debug.Log("UpdateFlightCondition:start");
            Debug.Log("UpdateFlightCondition:" + ambientTherm + "-" + altitude + "-" + vel + "-" + mach + "-" + oxygen);
            Debug.Log("UpdateFlightCondition:Engine :T" + engDevSolver.GetEngineTemp() + ",Isp:" + engDevSolver.GetIsp() + ",S:" + engDevSolver.GetStatus() + ",R:" + engDevSolver.GetRunning());
            base.UpdateFlightCondition(ambientTherm, altitude, vel, mach, oxygen);
            engDevSolver.CalculatePhysics(vessel, part, TimeWarp.fixedDeltaTime, RFFuelRatio());


            Debug.Log("UpdateFlightCondition:end");
        }
        #endregion

        #region Info
        protected string ThrottleString()
        {
            string output = "";

            return output;
        }
        protected string GetThrustInfo()
        {
            Debug.Log("GetThrustInfo:start");
            string output = "";
            if (engDevSolver != null) output = engDevSolver.physicsSimulator.GetJerkDamage().ToString();
            Debug.Log("GetThrustInfo:end");
            return output;
        }

        public override string GetModuleTitle()
        {
            return "Engine Development";
        }
        public override string GetPrimaryField()
        {
            return GetThrustInfo();
        }

        public override string GetInfo()
        {
            string output = GetThrustInfo();


            return output;
        }
        #endregion

        public float GetModuleCost(float defaultCost)
        {
            return defaultCost;
        }

        public float GetModuleMass(float defaultMass)
        {
            return defaultMass;
        }
    }

}
