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
            //Debug.Log("UpdateRF");
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
            //Debug.Log("RFIsPressurized");
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

        [KSPField(isPersistant = true, guiActive = true)]
        public string stabilityState = "ED";

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
            //Debug.Log("Act");
            if (!allowRestart && engineShutdown)
            {
                return; // If the engines were shutdown previously and restarting is not allowed, prevent restart of engines
            }
            if (noShieldedStart && part.ShieldedFromAirstream)
            {
                ScreenMessages.PostScreenMessage("<color=orange>[" + part.partInfo.title + "]: Cannot activate while stowed!</color>", 6f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }
            engineShutdown = false;
            if (vessel.ctrlState.mainThrottle <= 0&&ignitionsRemained!=0)
            {
                //Debug.Log("Act:zero");
                if (allowShutdown) Events["Shutdown"].active = true;
                else Events["Shutdown"].active = false;
                Events["Activate"].active = false;
                EngineIgnited = true;
                return;
            }
            currentThrottle = actualThrottle = 0;
            requestedThrottle = vessel.ctrlState.mainThrottle;//Need to check in the Throttle before trying to ignite
            UpdateThrottle();
            if (combusting)
            {
                //Debug.Log("Act:suc");
                if (allowShutdown) Events["Shutdown"].active = true;
                else Events["Shutdown"].active = false;
                Events["Activate"].active = false;
                EngineIgnited = true;
            }
            else
            {
                //Debug.Log("Act:fail");
                Shutdown();
            }

        }
        new public virtual void Shutdown() {
            //Debug.Log("Shutdown");
            requestedThrottle = currentThrottle = actualThrottle = 0;
            base.Shutdown();
            UpdateThrottle();
            combusting = false;
            EngineIgnited = false;
            engineShutdown = true;
        }
        public override void CreateEngine()
        {
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
        }
        public override void FixedUpdate()
        {
            string debugstring = "";
            debugstring += "isIgn:" + EngineIgnited + ":::isShut:" + engineShutdown + ":::reqTh:" + requestedThrottle + ":::curTh:" + currentThrottle + "\n";
            debugstring += "comb:" + combusting + ":::ignRem:" + ignitionsRemained + ":::flameout:" + flameout;
            //Debug.Log(debugstring);
            base.FixedUpdate();
            if (!HighLogic.LoadedSceneIsEditor && !HighLogic.LoadedSceneIsFlight)
                return;

            if (HighLogic.LoadedSceneIsEditor)
            {
                // nothing to see here
                return;
            }
            (engineSolver as EngineDeveloping).CalculatePhysics(vessel, part, TimeWarp.fixedDeltaTime, RFFuelRatio(),jerkTolerance);
            if ((engineSolver as EngineDeveloping).stability >= 0.996f)
                stabilityState = "Very Stable";
            else if ((engineSolver as EngineDeveloping).stability >= 0.95f)
                stabilityState = "Stable";
            else if ((engineSolver as EngineDeveloping).stability >= 0.75f)
                stabilityState = "Risky";
            else if ((engineSolver as EngineDeveloping).stability >= 0.50f)
                stabilityState = "Very Risky";
            else if ((engineSolver as EngineDeveloping).stability >= 0.30f)
                stabilityState = "Unstable";
            else
                stabilityState = "Very Unstable";
            Fields["statusL2"].guiName = "  ";
            Fields["statusL2"].guiActive = true;
            statusL2 = engineSolver.statusString;
        }
        new virtual public void OnAction(KSPActionParam KSPAP) {
            if (!EngineIgnited)
            {
                Activate();
            }
            else {
                Shutdown();
            }
        }
        public override void UpdateThrottle()
        {
            if (throttleLocked)
                requestedThrottle = 1f;
            if (!(HighLogic.LoadedSceneIsFlight && vessel != null)||engineShutdown) return;
            if (currentThrottle <= 0.00f)
            {
                combusting = false;
                currentThrottle = actualThrottle = 0;
            }
            else {
                EngineIgnited = true;
            }
            if ((!combusting) && (ignitionsRemained > 0 || ignitionsRemained == -1) && (currentThrottle <= 0.00f) && requestedThrottle > 0.000001f)
            {
                ignitionsRemained = (ignitionsRemained == -1) ? -1 : (ignitionsRemained - 1);
                if (!(engineSolver as EngineDeveloping).CheckForIgnition(RFFuelRatio(),jerkTolerance))
                {
                    ScreenMessages.PostScreenMessage("<color=orange>Ignition Failed:" + part.partInfo.title + "</color>", 6f, ScreenMessageStyle.UPPER_LEFT);
                    combusting = false;
                    currentThrottle = actualThrottle = 0;
                    EngineIgnited = false;
                    SetFlameout();
                    PlayFlameoutFX(true);
                    Shutdown();
                    return;
                }
                combusting = true;
                EngineIgnited = true;
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
        protected string GetStaticThrustInfo(bool primaryField)
        {
            string output = "";
            //From AJE
            if (engineSolver == null || !(engineSolver is EngineDeveloping))
                CreateEngine();

            // get stats
            double pressure = 101.325d, temperature = 288.15d, density = 1.225d;
            if (Planetarium.fetch != null)
            {
                CelestialBody home = Planetarium.fetch.Home;
                if (home != null)
                {
                    pressure = home.GetPressure(0d);
                    temperature = home.GetTemperature(0d);
                    density = home.GetDensity(pressure, temperature);
                }
            }
            ambientTherm = new EngineThermodynamics();
            ambientTherm.FromAmbientConditions(pressure, temperature, density);
            areaRatio = 1d;
            lastPropellantFraction = 1d;
            bool oldE = EngineIgnited;
            bool oldC = combusting;
            float oldT = currentThrottle;
            EngineIgnited = true;
            combusting = true;
            currentThrottle = 1f;
            UpdateFlightCondition(ambientTherm, 0d, Vector3d.zero, 0d, true);
            double thrust_atm = (engineSolver.GetThrust() * 0.001d);
            double Isp_atm = engineSolver.GetIsp();
            double Cstar_atm = (engineSolver as EngineDeveloping).Cstar;
            double Ct_atm = (engineSolver as EngineDeveloping).Ct;
            ambientTherm = new EngineThermodynamics();
            ambientTherm.FromAmbientConditions(0d, 4d, 0d);
            UpdateFlightCondition(ambientTherm, 0d, Vector3d.zero, 0d, true);
            double thrust_vac = (engineSolver.GetThrust() * 0.001d);
            double Isp_vac = engineSolver.GetIsp();
            double Cstar_vac = (engineSolver as EngineDeveloping).Cstar;
            double Ct_vac = (engineSolver as EngineDeveloping).Ct;

            output += "<b>Max. Thrust(ASL): </b>" + thrust_atm.ToString("N2") + " kN\n";
            output += "<b>Max. Thrust(Vac.): </b>" + thrust_vac.ToString("N2") + " kN\n";
            output += "<b><color=#0099ff>Ignitions Available: </color></b>" + ignitionsAvailable + "\n";

            output += "<b>Isp(ASL): </b>" + Isp_atm.ToString("N2") + " s\n";
            output += "<b>Isp(Vac.): </b>" + Isp_vac.ToString("N2") + " s\n";
            if (!primaryField) { 
                output += "<b>C*(ASL):</b> " + Cstar_atm.ToString("N2") + "m/s\n";
                output += "<b>Ct(ASL):</b> " + Ct_atm.ToString("N2") + "\n";
                output += "<b>C*(Vac):</b> " + Cstar_vac.ToString("N2") + "m/s\n";
                output += "<b>Ct(Vac):</b> " + Ct_vac.ToString("N2") + "\n";
            }

            EngineIgnited = oldE;
            combusting = oldC;
            currentThrottle = oldT;
            return output;
        }

        public override string GetModuleTitle()
        {
            return "Engine Development";
        }
        public override string GetPrimaryField()
        {
            return GetStaticThrustInfo(true);
        }
        public override string GetInfo()
        {
            string output = GetStaticThrustInfo(false);

            output += "\n<b><color=#99ff00ff>Propellants:</color></b>\n";
            Propellant p;
            string pName;
            for (int i = 0; i < propellants.Count; ++i)
            {
                p = propellants[i];
                pName = KSPUtil.PrintModuleName(p.name);

                output += "- <b>" + pName + "</b>: " + getMaxFuelFlow(p).ToString("0.0##") + "/sec. Max.\n";
                output += p.GetFlowModeDescription();
            }
            output += "<b>Chamber Pressure:</b>"+Pcnsc+ "kPa, <b>Chamber Temperature:</b>" +Tcnsc+ "K\n";
            output += "<b>Nozzle Exit Pressure:</b>" + Pec + "kPa, <b>Nozzle Exit Area:</b>" + Aec + "m^2\n";

            output += "<b>Flameout under: </b>" + (ignitionThreshold * 100f).ToString("0.#") + "%\n";
            if (!allowShutdown) output += "\n" + "<b><color=orange>Engine cannot be shut down!</color></b>";
            if (!allowRestart) output += "\n" + "<b><color=orange>If shutdown, engine cannot restart.</color></b>";

            currentThrottle = 0f;

            return output;
        }
        #endregion
    }

}
