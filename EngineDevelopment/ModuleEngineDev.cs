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
    class ModuleEngineDev : ModuleEnginesSolver
    {
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

        #region Fields
        [KSPField(isPersistant = false, guiActive = false)]
        public int maxBurnTime = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int burningTime = 0;
        [KSPField(isPersistant = true, guiActive = false)]
        public int reliability = 0;
        [KSPField(isPersistant = false)]
        public int ignitionsAvailable = -1;
        [KSPField(isPersistant = true, guiActive = true)]
        public int ignitionsRemained = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public int jerkTolerance = 0;
        [KSPField(isPersistant = false, guiActive = false)]
        public int multiChamber = 0;

        #endregion

        private float massMult = 1;
        private float costMult = 1;
        //TEMPORARY
        [KSPField(isPersistant = false, guiActive = false)]
        public float Ped = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Aed = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float maxFuelFlowd = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float minFuelFlowd = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Tcnsd = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Pcnsd = -1;



        [KSPField(isPersistant = false, guiActive = false)]
        public float Pec = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Aec = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float maxFuelFlowc = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float minFuelFlowc = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Tcnsc = -1;
        [KSPField(isPersistant = false, guiActive = false)]
        public float Pcnsc = -1;

        public bool combusting = false;
        #region Overrides
        public override void OnStart(StartState state)
        {
            base.OnStart(state);
            if (state == StartState.Editor)
            {
                ignitionsRemained = ignitionsAvailable;
            }
        }
        public override void Activate()
        {
            Debug.Log("Activate:start");
            if (!allowRestart && engineShutdown)
            {
                return; // If the engines were shutdown previously and restarting is not allowed, prevent restart of engines
            }
            if (noShieldedStart && part.ShieldedFromAirstream)
            {
                ScreenMessages.PostScreenMessage("<color=orange>[" + part.partInfo.title + "]: Cannot activate while stowed!</color>", 6f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }
            if (vessel.ctrlState.mainThrottle <= 0 && requestedThrottle <= 0) {
                if (allowShutdown) Events["Shutdown"].active = true;
                else Events["Shutdown"].active = false;
                Events["Activate"].active = false;
                EngineIgnited = true;
                Debug.Log("Activate::ZeroThrottle");
                return;
            }
            currentThrottle = actualThrottle = 0;
            requestedThrottle = vessel.ctrlState.mainThrottle;//Need to check in the Throttle before trying to ignite
            Debug.Log("Activate::" + combusting + " " + requestedThrottle);
            UpdateThrottle();
            Debug.Log("Activate:::" + combusting);
            if (combusting)
            {
                if (allowShutdown) Events["Shutdown"].active = true;
                else Events["Shutdown"].active = false;
                Events["Activate"].active = false;
                EngineIgnited = true;
                Debug.Log("Activate::succeeded");
            }
            else
            {
                requestedThrottle = currentThrottle = actualThrottle = 0;
                Shutdown();
                Debug.Log("Activate::failed");
            }

        }
        public override void CreateEngine()
        {
            Debug.Log("CreateEngine:start");
            engineSolver = new EngineDeveloping();
            (engineSolver as EngineDeveloping).InitializeDefaultEngineData(
                atmosphereCurve.Evaluate(0),
                atmosphereCurve.Evaluate(1),
                maxThrust,
                Pcnsd,
                Tcnsd,
                Ped,
                Aed,
                maxFuelFlowd,
                minFuelFlowd,
                1f
            );
            (engineSolver as EngineDeveloping).InitializeOverallEngineData(
                Pcnsc,
                Tcnsc,
                Pec,
                Aec,
                maxFuelFlowc,
                minFuelFlowc,
                1f
            );
            useAtmCurve = useAtmCurve = false;
            Debug.Log("CreateEngine:" + ignitionsAvailable);
            Debug.Log("CreateEngine:end");
        }
        public override void FixedUpdate()
        {
            Debug.Log("FixedUpdate");
            base.FixedUpdate();
            (engineSolver as EngineDeveloping).CalculatePhysics(vessel, part, TimeWarp.fixedDeltaTime, RFFuelRatio());
        }
        public override void UpdateFlightCondition(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool oxygen)
        {

            Debug.Log("UpdateFlightCondition:start");
            Debug.Log("UpdateFlightCondition:" + ambientTherm + "-" + altitude + "-" + vel + "-" + mach + "-" + oxygen);
            Debug.Log("UpdateFlightCondition:Engine :T" + engineSolver.GetEngineTemp() + ",Isp:" + engineSolver.GetIsp() + ",S:" + engineSolver.GetStatus() + ",R:" + engineSolver.GetRunning());
            base.UpdateFlightCondition(ambientTherm, altitude, vel, mach, oxygen);
            Debug.Log("UpdateFlightCondition:end::");
        }

        public override void UpdateThrottle()
        {

            Debug.Log("UpdateThrottle:start:cur::" + currentThrottle + "::req:::" + requestedThrottle + "::comb::" + combusting);
            if (throttleLocked)
                requestedThrottle = 1f;
            if (!(!HighLogic.LoadedSceneIsEditor && !(HighLogic.LoadedSceneIsFlight && vessel != null && vessel.situation == Vessel.Situations.PRELAUNCH))) return;
            if (currentThrottle <= 0.00f)
            {
                combusting = false;
                currentThrottle = actualThrottle = 0;
            }

            if ((!combusting) && (ignitionsRemained > 0 || ignitionsRemained == -1) && (currentThrottle <= 0.00f) && requestedThrottle > 0.000001f)
            {
                Debug.Log("UpdateThrottle:try" + combusting + "::" + ignitionsRemained);
                ignitionsRemained = (ignitionsRemained == -1) ? -1 : (ignitionsRemained - 1);
                if (!(engineSolver as EngineDeveloping).CheckForIgnition(RFFuelRatio(),jerkTolerance))
                {
                    combusting = false;
                    currentThrottle = actualThrottle = 0;
                    SetFlameout();
                    PlayFlameoutFX(true);
                    Debug.Log("UpdateThrottle:failed" + combusting + "::" + ignitionsRemained);
                    return;
                }
                combusting = true;
                Debug.Log("UpdateThrottle:succeeded" + combusting + "::" + ignitionsRemained);
                SetUnflameout();
                PlayEngageFX();
            }
            if (!combusting)
            {
                currentThrottle = actualThrottle = 0;
                return;
            }
            if (!useEngineResponseTime)
            {
                currentThrottle = requestedThrottle * thrustPercentage * 0.01f;
            }
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
    }

}
