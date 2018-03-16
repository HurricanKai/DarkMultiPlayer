﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkMultiPlayer
{
    public class VesselUpdate
    {
        public Guid vesselID;
        public double planetTime;
        public string bodyName;
        public float[] rotation;
        public float[] angularVelocity;
        public FlightCtrlState flightState;
        public bool[] actiongroupControls;
        public bool isSurfaceUpdate;
        //Orbital parameters
        public double[] orbit;
        //Surface parameters
        //Position = lat,long,alt,ground height.
        public double[] position;
        public double[] velocity;
        public double[] acceleration;
        public float[] terrainNormal;
        //SAS
        public bool sasEnabled;
        public int autopilotMode;
        public float[] lockedRotation;
        //Private
        private VesselWorker vesselWorker;

        public VesselUpdate(VesselWorker vesselWorker)
        {
            this.vesselWorker = vesselWorker;
        }

        public static VesselUpdate CopyFromVessel(VesselWorker vesselWorker, Vessel updateVessel)
        {
            VesselUpdate returnUpdate = new VesselUpdate(vesselWorker);
            try
            {
                returnUpdate.vesselID = updateVessel.id;
                returnUpdate.planetTime = Planetarium.GetUniversalTime();
                returnUpdate.bodyName = updateVessel.mainBody.bodyName;

                returnUpdate.rotation = new float[4];
                returnUpdate.rotation[0] = updateVessel.srfRelRotation.x;
                returnUpdate.rotation[1] = updateVessel.srfRelRotation.y;
                returnUpdate.rotation[2] = updateVessel.srfRelRotation.z;
                returnUpdate.rotation[3] = updateVessel.srfRelRotation.w;

                returnUpdate.angularVelocity = new float[3];
                returnUpdate.angularVelocity[0] = updateVessel.angularVelocity.x;
                returnUpdate.angularVelocity[1] = updateVessel.angularVelocity.y;
                returnUpdate.angularVelocity[2] = updateVessel.angularVelocity.z;
                //Flight state
                returnUpdate.flightState = new FlightCtrlState();
                returnUpdate.flightState.CopyFrom(updateVessel.ctrlState);
                returnUpdate.actiongroupControls = new bool[5];
                returnUpdate.actiongroupControls[0] = updateVessel.ActionGroups[KSPActionGroup.Gear];
                returnUpdate.actiongroupControls[1] = updateVessel.ActionGroups[KSPActionGroup.Light];
                returnUpdate.actiongroupControls[2] = updateVessel.ActionGroups[KSPActionGroup.Brakes];
                returnUpdate.actiongroupControls[3] = updateVessel.ActionGroups[KSPActionGroup.SAS];
                returnUpdate.actiongroupControls[4] = updateVessel.ActionGroups[KSPActionGroup.RCS];

                if (updateVessel.altitude < 10000)
                {
                    //Use surface position under 10k
                    returnUpdate.isSurfaceUpdate = true;
                    returnUpdate.position = new double[4];
                    returnUpdate.position[0] = updateVessel.latitude;
                    returnUpdate.position[1] = updateVessel.longitude;
                    returnUpdate.position[2] = updateVessel.altitude;
                    VesselUtil.DMPRaycastPair groundRaycast = VesselUtil.RaycastGround(updateVessel.latitude, updateVessel.longitude, updateVessel.mainBody);
                    returnUpdate.position[3] = groundRaycast.altitude;
                    returnUpdate.terrainNormal = new float[3];
                    returnUpdate.terrainNormal[0] = groundRaycast.terrainNormal.x;
                    returnUpdate.terrainNormal[1] = groundRaycast.terrainNormal.y;
                    returnUpdate.terrainNormal[2] = groundRaycast.terrainNormal.z;
                    returnUpdate.velocity = new double[3];
                    Vector3d srfVel = Quaternion.Inverse(updateVessel.mainBody.bodyTransform.rotation) * updateVessel.srf_velocity;
                    returnUpdate.velocity[0] = srfVel.x;
                    returnUpdate.velocity[1] = srfVel.y;
                    returnUpdate.velocity[2] = srfVel.z;
                    returnUpdate.acceleration = new double[3];
                    Vector3d srfAcceleration = Quaternion.Inverse(updateVessel.mainBody.bodyTransform.rotation) * updateVessel.acceleration;
                    returnUpdate.acceleration[0] = srfAcceleration.x;
                    returnUpdate.acceleration[1] = srfAcceleration.y;
                    returnUpdate.acceleration[2] = srfAcceleration.z;
                }
                else
                {
                    //Use orbital positioning over 10k
                    returnUpdate.isSurfaceUpdate = false;
                    returnUpdate.orbit = new double[7];
                    returnUpdate.orbit[0] = updateVessel.orbit.inclination;
                    returnUpdate.orbit[1] = updateVessel.orbit.eccentricity;
                    returnUpdate.orbit[2] = updateVessel.orbit.semiMajorAxis;
                    returnUpdate.orbit[3] = updateVessel.orbit.LAN;
                    returnUpdate.orbit[4] = updateVessel.orbit.argumentOfPeriapsis;
                    returnUpdate.orbit[5] = updateVessel.orbit.meanAnomalyAtEpoch;
                    returnUpdate.orbit[6] = updateVessel.orbit.epoch;
                }
                returnUpdate.sasEnabled = updateVessel.Autopilot.Enabled;
                if (returnUpdate.sasEnabled)
                {
                    returnUpdate.autopilotMode = (int)updateVessel.Autopilot.Mode;
                    returnUpdate.lockedRotation = new float[4];
                    returnUpdate.lockedRotation[0] = updateVessel.Autopilot.SAS.lockedRotation.x;
                    returnUpdate.lockedRotation[1] = updateVessel.Autopilot.SAS.lockedRotation.y;
                    returnUpdate.lockedRotation[2] = updateVessel.Autopilot.SAS.lockedRotation.z;
                    returnUpdate.lockedRotation[3] = updateVessel.Autopilot.SAS.lockedRotation.w;
                }
            }
            catch (Exception e)
            {
                DarkLog.Debug("Failed to get vessel update, exception: " + e);
                returnUpdate = null;
            }
            return returnUpdate;
        }

        public void Apply(PosistionStatistics posistionStatistics, Dictionary<Guid, VesselCtrlUpdate> ctrlUpdate, VesselUpdate previousUpdate)
        {
            if (HighLogic.LoadedScene == GameScenes.LOADING)
            {
                return;
            }

            //Ignore updates to our own vessel if we are in flight and we aren't spectating
            if (!vesselWorker.isSpectating && (FlightGlobals.fetch.activeVessel != null ? FlightGlobals.fetch.activeVessel.id == vesselID : false) && HighLogic.LoadedScene == GameScenes.FLIGHT)
            {
                return;
            }
            Vessel updateVessel = FlightGlobals.fetch.vessels.FindLast(v => v.id == vesselID);
            if (updateVessel == null)
            {
                //DarkLog.Debug("ApplyVesselUpdate - Got vessel update for " + vesselID + " but vessel does not exist");
                return;
            }
            CelestialBody updateBody = FlightGlobals.Bodies.Find(b => b.bodyName == bodyName);
            if (updateBody == null)
            {
                //DarkLog.Debug("ApplyVesselUpdate - updateBody not found");
                return;
            }

            Quaternion normalRotate = Quaternion.identity;
            Vector3 oldPos = updateVessel.GetWorldPos3D();
            Vector3 oldVelocity = updateVessel.orbitDriver.orbit.GetVel();
            //Position/Velocity
            if (isSurfaceUpdate)
            {
                //Get the new position/velocity
                double altitudeFudge = 0;
                VesselUtil.DMPRaycastPair dmpRaycast = VesselUtil.RaycastGround(position[0], position[1], updateBody);
                if (dmpRaycast.altitude != -1d && position[3] != -1d)
                {

                    Vector3 theirNormal = new Vector3(terrainNormal[0], terrainNormal[1], terrainNormal[2]);
                    altitudeFudge = dmpRaycast.altitude - position[3];
                    if (Math.Abs(position[2] - position[3]) < 50f)
                    {
                        normalRotate = Quaternion.FromToRotation(theirNormal, dmpRaycast.terrainNormal);
                    }
                }

                double planetariumDifference = Planetarium.GetUniversalTime() - planetTime;

                //Velocity fudge
                Vector3d updateAcceleration = updateBody.bodyTransform.rotation * new Vector3d(acceleration[0], acceleration[1], acceleration[2]);
                Vector3d velocityFudge = Vector3d.zero;
                if (Math.Abs(planetariumDifference) < 3f)
                {
                    //Velocity = a*t
                    velocityFudge = updateAcceleration * planetariumDifference;
                }

                //Position fudge
                Vector3d updateVelocity = updateBody.bodyTransform.rotation * new Vector3d(velocity[0], velocity[1], velocity[2]) + velocityFudge;
                Vector3d positionFudge = Vector3d.zero;

                if (Math.Abs(planetariumDifference) < 3f)
                {
                    //Use the average velocity to determine the new position
                    //Displacement = v0*t + 1/2at^2.
                    positionFudge = (updateVelocity * planetariumDifference) + (0.5d * updateAcceleration * planetariumDifference * planetariumDifference);
                }
                Vector3d updatePostion = updateBody.GetWorldSurfacePosition(position[0], position[1], position[2] + altitudeFudge) + positionFudge;
                Vector3d orbitalPos = updatePostion - updateBody.position;
                Vector3d surfaceOrbitVelDiff = updateBody.getRFrmVel(updatePostion);
                Vector3d orbitalVel = updateVelocity + surfaceOrbitVelDiff;
                updateVessel.orbitDriver.orbit.UpdateFromStateVectors(orbitalPos.xzy, orbitalVel.xzy, updateBody, Planetarium.GetUniversalTime());
            }
            else
            {
                updateVessel.orbit.SetOrbit(orbit[0], orbit[1], orbit[2], orbit[3], orbit[4], orbit[5], orbit[6], updateBody);
            }
            //Updates orbit.pos/vel
            updateVessel.orbitDriver.orbit.UpdateFromOrbitAtUT(updateVessel.orbitDriver.orbit, Planetarium.GetUniversalTime(), updateBody);

            //Updates vessel pos from the orbit, as if on rails
            updateVessel.orbitDriver.updateFromParameters();

            //Rotation
            Quaternion unfudgedRotation = new Quaternion(rotation[0], rotation[1], rotation[2], rotation[3]);
            //Rotation extrapolation? :O
            if (previousUpdate != null)
            {
                double deltaUpdateT = planetTime - previousUpdate.planetTime;
                double deltaRealT = Planetarium.GetUniversalTime() - previousUpdate.planetTime;
                float scaling = (float)(deltaRealT / deltaUpdateT);
                if (Math.Abs(deltaRealT) < 3f)
                {
                    Quaternion previousRotation = new Quaternion(previousUpdate.rotation[0], previousUpdate.rotation[1], previousUpdate.rotation[2], previousUpdate.rotation[3]);
                    Quaternion rotationDelta = unfudgedRotation * Quaternion.Inverse(previousRotation);
                    Vector3 rotAxis;
                    float rotAngle;
                    rotationDelta.ToAngleAxis(out rotAngle, out rotAxis);
                    if (rotAngle > 180)
                    {
                        rotAngle -= 360;
                    }
                    rotAngle = (rotAngle * scaling) % 360;
                    unfudgedRotation = Quaternion.AngleAxis(rotAngle, rotAxis) * unfudgedRotation;
                }
            }
            Quaternion updateRotation = normalRotate * unfudgedRotation;
            //Rotational error tracking
            double rotationalError = Quaternion.Angle(updateVessel.srfRelRotation, updateRotation);
            //updateVessel.SetRotation(updateVessel.mainBody.bodyTransform.rotation * updateRotation);
            updateVessel.srfRelRotation = updateRotation;
            updateVessel.protoVessel.rotation = updateVessel.srfRelRotation;

            Vector3 angularVel = updateVessel.ReferenceTransform.rotation * new Vector3(angularVelocity[0], angularVelocity[1], angularVelocity[2]);
            if (updateVessel.parts != null && !updateVessel.packed)
            {
                for (int i = 0; i < updateVessel.parts.Count; i++)
                {
                    Part thisPart = updateVessel.parts[i];
                    thisPart.vel = updateVessel.orbit.GetVel() - Krakensbane.GetFrameVelocity();
                    if (thisPart.orbit.referenceBody.inverseRotation)
                    {
                        thisPart.vel -= updateBody.getRFrmVel(thisPart.partTransform.position);
                    }
                    if (thisPart.rb != null && thisPart.State != PartStates.DEAD)
                    {
                        thisPart.rb.velocity = thisPart.vel;
                        //Angular Vel
                        thisPart.rb.angularVelocity = angularVel;
                        if (thisPart != updateVessel.rootPart)
                        {
                            Vector3 diffPos = thisPart.rb.position - updateVessel.CoM;
                            Vector3 partVelDifference = Vector3.Cross(angularVel, diffPos);
                            thisPart.rb.velocity = thisPart.rb.velocity + partVelDifference;
                        }
                    }
                }
            }

            //Updates Vessel.CoMD, which is used for GetWorldPos3D
            updateVessel.precalc.CalculatePhysicsStats();
            updateVessel.latitude = updateBody.GetLatitude(updateVessel.GetWorldPos3D());
            updateVessel.longitude = updateBody.GetLongitude(updateVessel.GetWorldPos3D());
            updateVessel.altitude = updateBody.GetAltitude(updateVessel.GetWorldPos3D());
            updateVessel.protoVessel.latitude = updateVessel.latitude;
            updateVessel.protoVessel.longitude = updateVessel.longitude;
            updateVessel.protoVessel.altitude = updateVessel.altitude;
            double distanceError = Vector3d.Distance(oldPos, updateVessel.GetWorldPos3D());
            double velocityError = Vector3d.Distance(oldVelocity, updateVessel.orbitDriver.orbit.GetVel());
            if (ctrlUpdate != null)
            {
                if (ctrlUpdate.ContainsKey(updateVessel.id))
                {
                    updateVessel.OnFlyByWire -= ctrlUpdate[updateVessel.id].UpdateControls;
                    ctrlUpdate.Remove(updateVessel.id);
                }
                VesselCtrlUpdate vcu = new VesselCtrlUpdate(updateVessel, ctrlUpdate, planetTime + 5, flightState);
                ctrlUpdate.Add(updateVessel.id, vcu);
                updateVessel.OnFlyByWire += vcu.UpdateControls;
            }

            //Action group controls
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.Gear, actiongroupControls[0]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.Light, actiongroupControls[1]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, actiongroupControls[2]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, actiongroupControls[3]);
            updateVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, actiongroupControls[4]);

            if (sasEnabled)
            {
                updateVessel.Autopilot.SetMode((VesselAutopilot.AutopilotMode)autopilotMode);
                updateVessel.Autopilot.SAS.LockRotation(new Quaternion(lockedRotation[0], lockedRotation[1], lockedRotation[2], lockedRotation[3]));
            }
            posistionStatistics.LogError(updateVessel.id, distanceError, velocityError, rotationalError, planetTime);
        }
    }
}