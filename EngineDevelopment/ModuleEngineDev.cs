using System;
using SolverEngines;
using UnityEngine;
using System.Collections.Generic;

namespace EngineDevelopment
{
    class ModuleEngineDev : ModuleEnginesSolver, IPartCostModifier, IPartMassModifier
    {
        public List<ConfigNode> nozzleAlts;
        public List<ConfigNode> cycleAlts;
        #region Fields
        //[KSPField]
        //public double chamberNominalTemp = 0d;


        protected bool instantThrottle = false;
        protected float throttleResponseRate;
        protected EngineDeveloping engDevSolver = null;
        #endregion


        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            foreach (ConfigNode subNode in node.GetNodes("NOZZLEALT"))
            {
                ConfigNode newNode = new ConfigNode("NOZZLEALT");
                subNode.CopyTo(newNode);
                nozzleAlts.Add(newNode);
            }
            foreach (ConfigNode subNode in node.GetNodes("POWERCYCLEALT"))
            {
                ConfigNode newNode = new ConfigNode("POWERCYCLEALT");
                subNode.CopyTo(newNode);
                cycleAlts.Add(newNode);
            }
            CreateEngine();
        }
        #region Overrides
        public override void CreateEngine()
        {
            engDevSolver = new EngineDeveloping();
            engineSolver = engDevSolver;
        }
        public override void OnAwake()
        {
            base.OnAwake();

        }
        public override void OnStart(PartModule.StartState state)
        {
            base.OnStart(state);

        }
        public override void UpdateThrottle()
        {
        }
        public override void UpdateFlightCondition(EngineThermodynamics ambientTherm, double altitude, Vector3d vel, double mach, bool oxygen)
        {

            base.UpdateFlightCondition(ambientTherm, altitude, vel, mach, oxygen);
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
            string output = "";

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
