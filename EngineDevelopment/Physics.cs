using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace EngineDevelopment
{
	public class PhysicsSimulator
	{
		public static float s_NaturalDiffusionRateX = 0.02f;
		public static float s_NaturalDiffusionRateY = 0.03f;

		public static float s_TranslateAxialCoefficientX = 0.06f;
		public static float s_TranslateAxialCoefficientY = 0.06f;

		public static float s_TranslateSidewayCoefficientX = 0.04f;
		public static float s_TranslateSidewayCoefficientY = 0.02f;

		public static float s_RotateYawPitchCoefficientX = 0.003f;
		public static float s_RotateYawPitchCoefficientY = 0.004f;

		public static float s_RotateRollCoefficientX = 0.005f;
		public static float s_RotateRollCoefficientY = 0.006f;

		public static float s_VentingVelocity = 100.0f;
		public static float s_VentingAccThreshold = 0.00000004f;


		float ullageHeightMin, ullageHeightMax;
		float ullageRadialMin, ullageRadialMax;
        float jerk=0,angularJerk=0;
		string fuelFlowState = "";
        private Vector3 localAcceleration = new Vector3();
        private Vector3 lastAcceleration = new Vector3();
        private Vector3 lastRotation = new Vector3();
        private Queue<float> jerkAccum = new Queue<float>();
        float jerkAccumAmount = 0;
        float maxJerkAccumAmount = 0;
        public string FuelFlowState
        {
            get
            {
                return fuelFlowState;
            }

            private set
            {
                fuelFlowState = value;
            }
        }


        public void Reset()
		{
			ullageHeightMin = 0.05f; ullageHeightMax = 0.95f;
			ullageRadialMin = 0.0f; ullageRadialMax = 0.95f;
            localAcceleration = lastAcceleration = lastRotation = new Vector3(0, 0, 0);
            jerkAccum.Clear();
            jerkAccumAmount  = 0;
        }
        public void Update(Vessel vessel, Part engine, float deltaTime,  float fuelRatio) {
            Debug.Log("Update:start");
            localAcceleration = engine.transform.InverseTransformDirection(vessel.acceleration - FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()));
            UpdateUllage(vessel, engine, deltaTime, fuelRatio);
            UpdateJerk(vessel, engine, deltaTime);
            lastAcceleration = localAcceleration;
            //GetJerkDamage();
            Debug.Log("Update:end");
        }
        protected void UpdateUllage(Vessel vessel, Part engine, float deltaTime, float fuelRatio)
		{
            Debug.Log("UpdateUllage:start");
            if (vessel.isActiveVessel == false) return;

			float fuelRatioFactor = (0.5f + fuelRatio) / 1.4f;
			float invFuelRatioFactor = 1.0f / fuelRatioFactor;

			//if (ventingAcc != 0.0f) Debug.Log("BoilOffAcc: " + ventingAcc.ToString("F8"));
			//else Debug.Log("BoilOffAcc: No boiloff.");
			Vector3 localAccelerationAmount = localAcceleration * deltaTime;
			Vector3 rotationAmount = new Vector3();
            Vector3 rotation = new Vector3();
            if (engine.rigidbody != null && engine.rigidbody.angularVelocity != null)
			{
				rotationAmount = rotation * deltaTime;
			}

			if ((TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)&&(vessel.LandedOrSplashed == true))
			{
				localAcceleration = engine.transform.InverseTransformDirection(-FlightGlobals.getGeeForceAtPosition(vessel.GetWorldPos3D()));
                localAccelerationAmount = localAcceleration * deltaTime;
                rotation.Set(0.0f, 0.0f, 0.0f);
			}

			Debug.Log("Ullage: dt: " + deltaTime.ToString("F2") + " localAcc: " + localAcceleration.ToString() + " rotateRate: " + rotation.ToString());
			

			// Translate forward/backward.
			ullageHeightMin = Mathf.Clamp(ullageHeightMin + localAccelerationAmount.y * s_TranslateAxialCoefficientY * fuelRatioFactor, 0.0f, 0.9f);
			ullageHeightMax = Mathf.Clamp(ullageHeightMax + localAccelerationAmount.y * s_TranslateAxialCoefficientY * fuelRatioFactor, 0.1f, 1.0f);
			ullageRadialMin = Mathf.Clamp(ullageRadialMin - Mathf.Abs(localAccelerationAmount.y) * s_TranslateAxialCoefficientX * fuelRatioFactor, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax + Mathf.Abs(localAccelerationAmount.y) * s_TranslateAxialCoefficientX * fuelRatioFactor, 0.1f, 1.0f);

			// Translate up/down/left/right.
			Vector3 sideAcc = new Vector3(localAccelerationAmount.x, 0.0f, localAccelerationAmount.z);
			ullageHeightMin = Mathf.Clamp(ullageHeightMin - sideAcc.magnitude * s_TranslateSidewayCoefficientY * fuelRatioFactor, 0.0f, 0.9f);
			ullageHeightMax = Mathf.Clamp(ullageHeightMax + sideAcc.magnitude * s_TranslateSidewayCoefficientY * fuelRatioFactor, 0.1f, 1.0f);
			ullageRadialMin = Mathf.Clamp(ullageRadialMin + sideAcc.magnitude * s_TranslateSidewayCoefficientX * fuelRatioFactor, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax + sideAcc.magnitude * s_TranslateSidewayCoefficientX * fuelRatioFactor, 0.1f, 1.0f);

			// Rotate yaw/pitch.
			Vector3 rotateYawPitch = new Vector3(rotation.x, 0.0f, rotation.z);
			if(ullageHeightMin < 0.45)
				ullageHeightMin = Mathf.Clamp(ullageHeightMin + rotateYawPitch.magnitude * s_RotateYawPitchCoefficientY, 0.0f, 0.45f);
			else
				ullageHeightMin = Mathf.Clamp(ullageHeightMin - rotateYawPitch.magnitude * s_RotateYawPitchCoefficientY, 0.45f, 0.9f);

			if (ullageHeightMax < 0.55)
				ullageHeightMax = Mathf.Clamp(ullageHeightMax + rotateYawPitch.magnitude * s_RotateYawPitchCoefficientY, 0.1f, 0.55f);
			else
				ullageHeightMax = Mathf.Clamp(ullageHeightMax - rotateYawPitch.magnitude * s_RotateYawPitchCoefficientY, 0.55f, 1.0f);

			ullageRadialMin = Mathf.Clamp(ullageRadialMin - rotateYawPitch.magnitude * s_RotateYawPitchCoefficientX, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax + rotateYawPitch.magnitude * s_RotateYawPitchCoefficientX, 0.1f, 1.0f);
			
			// Rotate roll.
			ullageHeightMin = Mathf.Clamp(ullageHeightMin - Mathf.Abs(rotation.y) * s_RotateRollCoefficientY * fuelRatioFactor, 0.0f, 0.9f);
			ullageHeightMax = Mathf.Clamp(ullageHeightMax + Mathf.Abs(rotation.y) * s_RotateRollCoefficientY * fuelRatioFactor, 0.1f, 1.0f);
			ullageRadialMin = Mathf.Clamp(ullageRadialMin - Mathf.Abs(rotation.y) * s_RotateRollCoefficientX * fuelRatioFactor, 0.0f, 0.9f);
			ullageRadialMax = Mathf.Clamp(ullageRadialMax - Mathf.Abs(rotation.y) * s_RotateRollCoefficientX * fuelRatioFactor, 0.1f, 1.0f);

			Debug.Log("Ullage: Height: (" + ullageHeightMin.ToString("F2") + " - " + ullageHeightMax.ToString("F2") + ") Radius: (" + ullageRadialMin.ToString("F2") + " - " + ullageRadialMax.ToString("F2") + ")");
            Debug.Log("UpdateUllage:end");

        }
        protected void UpdateJerk(Vessel vessel, Part engine, float deltaTime)
        {
            Debug.Log("UpdateJerk:start");
            if (TimeWarp.WarpMode == TimeWarp.Modes.HIGH && TimeWarp.CurrentRate > TimeWarp.MaxPhysicsRate)return;
            jerk = (( localAcceleration - lastAcceleration)/deltaTime).magnitude;
            jerkAccumAmount += jerk;
            jerkAccum.Enqueue(jerk);
            if (jerkAccum.Count > (1 / deltaTime)) {
                jerkAccumAmount -= jerkAccum.Dequeue();
            }
            maxJerkAccumAmount = maxJerkAccumAmount > jerkAccumAmount ? maxJerkAccumAmount : jerkAccumAmount;
            Debug.Log("UpdateJerk:end");
        }
		public float GetFuelFlowStability(float fuelRatio)
		{
            Debug.Log("GetFuelFlowStability:start");
            float bLevel = Mathf.Clamp((ullageHeightMax - ullageHeightMin) * (ullageRadialMax - ullageRadialMin) / 0.1f * Mathf.Clamp(8.2f - 8 * fuelRatio, 0.0f, 8.2f) - 1.0f, 0.0f, 15.0f);
			Debug.Log("Ullage: bLevel: " + bLevel.ToString("F3"));
	
			float pVertical = 1.0f;
			pVertical = 1.0f - (ullageHeightMin - 0.1f) / 0.2f;
			pVertical = Mathf.Clamp01(pVertical);
			Debug.Log("Ullage: pVertical: " + pVertical.ToString("F3"));
	
			float pHorizontal = 1.0f;
			pHorizontal = 1.0f - (ullageRadialMin - 0.1f) / 0.2f;
			pHorizontal = Mathf.Clamp01(pHorizontal);
			Debug.Log("Ullage: pHorizontal: " + pHorizontal.ToString("F3"));
			
			float successProbability = Mathf.Max(0.0f, 1.0f - (pVertical * pHorizontal * (0.75f + Mathf.Sqrt(bLevel))));
			
			if (successProbability >= 0.996f)
				fuelFlowState = "Very Stable";
			else if (successProbability >= 0.95f)
				fuelFlowState = "Stable";
			else if (successProbability >= 0.75f)
				fuelFlowState = "Risky";
			else if (successProbability >= 0.50f)
				fuelFlowState = "Very Risky";
			else if (successProbability >= 0.30f)
				fuelFlowState = "Unstable";
			else
				fuelFlowState = "Very Unstable";
            Debug.Log("GetFuelFlowStability:end");
            return successProbability; 
		}
        public float GetJerkDamage() {
            Debug.Log("Jerk: " + jerk);
            Debug.Log("JerkAccumAmount: " + jerkAccumAmount + "::" + maxJerkAccumAmount);
            Debug.Log("AngularJerk: " + angularJerk);
            return jerkAccumAmount;
        }
    }
}
