//  Copyright 2005-2010 Portland State University, University of Wisconsin
//  Authors:  Robert M. Scheller,   James B. Domingo
//  BDA originally programmed by Wei (Vera) Li at University of Missouri-Columbia in 2004.

using Landis.Core;
using Landis.Library.HarvestManagement;
using Landis.SpatialModeling;
using Landis.Library.AgeOnlyCohorts;
using System.Collections.Generic;

namespace Landis.Extension.BaseBDA
{
    ///<summary>
    /// Site Variables for a disturbance plug-in that simulates Biological Agents.
    /// </summary>
    public static class SiteVars
    {
        private static ISiteVar<int> timeOfLastBDA;
        private static ISiteVar<string> harvestPrescriptionName;
        private static ISiteVar<int> timeOfLastHarvest;
        private static ISiteVar<int> harvestCohortsKilled;
        private static ISiteVar<int> timeOfLastFire;
        private static ISiteVar<byte> fireSeverity;
        private static ISiteVar<int> timeOfLastWind;
        private static ISiteVar<byte> windSeverity; 
        private static ISiteVar<double> neighborResourceDom;
        private static ISiteVar<double> siteResourceDomMod;
        private static ISiteVar<double> siteResourceDom;
        private static ISiteVar<double> vulnerability;
        private static ISiteVar<bool> disturbed;
        private static ISiteVar<Dictionary<int,int>> numberCFSConifersKilled;
        private static ISiteVar<ISiteCohorts> cohorts;
        private static ISiteVar<int> timeOfNext;
        private static ISiteVar<string> agentName;
        private static ISiteVar<int> timeOfLastBiomassInsects;
        private static ISiteVar<string> biomassInsectsAgent;
        private static ISiteVar<int> biomassInsectsDefol;
        private static ISiteVar<ManagementArea> managementArea;

        //---------------------------------------------------------------------

        public static void Initialize(ICore modelCore)
        {
            timeOfLastBDA  = modelCore.Landscape.NewSiteVar<int>();
            neighborResourceDom = modelCore.Landscape.NewSiteVar<double>();
            siteResourceDomMod = modelCore.Landscape.NewSiteVar<double>();
            siteResourceDom = modelCore.Landscape.NewSiteVar<double>();
            vulnerability = modelCore.Landscape.NewSiteVar<double>();
            disturbed = modelCore.Landscape.NewSiteVar<bool>();
            numberCFSConifersKilled = modelCore.Landscape.NewSiteVar<Dictionary<int, int>>();
            timeOfNext = modelCore.Landscape.NewSiteVar<int>();
            agentName = modelCore.Landscape.NewSiteVar<string>();
            biomassInsectsAgent = modelCore.Landscape.NewSiteVar<string>();
            biomassInsectsDefol = modelCore.Landscape.NewSiteVar<int>();

            TimeOfLastEvent.ActiveSiteValues = -10000;
            NeighborResourceDom.ActiveSiteValues = 0.0;
            SiteResourceDomMod.ActiveSiteValues = 0.0;
            SiteResourceDom.ActiveSiteValues = 0.0;
            Vulnerability.ActiveSiteValues = 0.0;
            TimeOfNext.ActiveSiteValues = 9999;
            AgentName.ActiveSiteValues = "";
            BiomassInsectsDefol.ActiveSiteValues = 0;

            cohorts = PlugIn.ModelCore.GetSiteVar<ISiteCohorts>("Succession.AgeCohorts");

            foreach(ActiveSite site in modelCore.Landscape)
                SiteVars.NumberCFSConifersKilled[site] = new Dictionary<int, int>();

            // Added for v1.1 to enable interactions with CFS fuels extension.
            modelCore.RegisterSiteVar(SiteVars.NumberCFSConifersKilled, "BDA.NumCFSConifers");
            modelCore.RegisterSiteVar(SiteVars.TimeOfLastEvent, "BDA.TimeOfLastEvent");
            modelCore.RegisterSiteVar(SiteVars.AgentName, "BDA.AgentName");
            // Added to enable interactions with other extensions (Presalvage harvest)
            modelCore.RegisterSiteVar(SiteVars.TimeOfNext, "BDA.TimeOfNext");

        }

        //---------------------------------------------------------------------

        public static void InitializeExternalVars()
        {
            harvestPrescriptionName = PlugIn.ModelCore.GetSiteVar<string>("Harvest.PrescriptionName");
            timeOfLastHarvest = PlugIn.ModelCore.GetSiteVar<int>("Harvest.TimeOfLastEvent");
            harvestCohortsKilled = PlugIn.ModelCore.GetSiteVar<int>("Harvest.CohortsKilled");
            timeOfLastFire = PlugIn.ModelCore.GetSiteVar<int>("Fire.TimeOfLastEvent");
            fireSeverity = PlugIn.ModelCore.GetSiteVar<byte>("Fire.Severity");
            timeOfLastWind = PlugIn.ModelCore.GetSiteVar<int>("Wind.TimeOfLastEvent");
            windSeverity = PlugIn.ModelCore.GetSiteVar<byte>("Wind.Severity");
            timeOfLastBiomassInsects = PlugIn.ModelCore.GetSiteVar<int>("BiomassInsects.TimeOfLastEvent");
            biomassInsectsAgent = PlugIn.ModelCore.GetSiteVar<string>("BiomassInsects.InsectName");
            biomassInsectsDefol = PlugIn.ModelCore.GetSiteVar<int>("BiomassInsects.PctDefoliation");
            managementArea = PlugIn.ModelCore.GetSiteVar<ManagementArea>("BiomassHarvest.ManagementArea");
        }

        //---------------------------------------------------------------------
        public static ISiteVar<int> TimeOfLastEvent
        {
            get
            {
                return timeOfLastBDA;
            }
        }

        //---------------------------------------------------------------------

        public static ISiteVar<string> HarvestPrescriptionName
        {
            get
            {
                return harvestPrescriptionName;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<int> TimeOfLastHarvest
        {
            get
            {
                return timeOfLastHarvest;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<int> HarvestCohortsKilled
        {
            get
            {
                return harvestCohortsKilled;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<int> TimeOfLastFire
        {
            get
            {
                return timeOfLastFire;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<byte> FireSeverity
        {
            get
            {
                return fireSeverity;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<int> TimeOfLastWind
        {
            get
            {
                return timeOfLastWind;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<byte> WindSeverity
        {
            get
            {
                return windSeverity;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<double> SiteResourceDom
        {
            get
            {
                return siteResourceDom;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<double> NeighborResourceDom
        {
            get
            {
                return neighborResourceDom;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<double> SiteResourceDomMod
        {
            get
            {
                return siteResourceDomMod;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<double> Vulnerability
        {
            get
            {
                return vulnerability;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<bool> Disturbed
        {
            get
            {
                return disturbed;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<Dictionary<int,int>> NumberCFSConifersKilled
        {
            get
            {
                return numberCFSConifersKilled;
            }
            set
            {
                numberCFSConifersKilled = value;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<ISiteCohorts> Cohorts
        {
            get
            {
                return cohorts;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<int> TimeOfNext
        {
            get
            {
                return timeOfNext;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<string> AgentName
        {
            get
            {
                return agentName;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<int> TimeOfLastBiomassInsects
        {
            get
            {
                return timeOfLastBiomassInsects;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<string> BiomassInsectsAgent
        {
            get
            {
                return biomassInsectsAgent;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<int> BiomassInsectsDefol
        {
            get
            {
                return biomassInsectsDefol;
            }
        }
        //---------------------------------------------------------------------
        public static ISiteVar<ManagementArea> ManagementArea
        {
            get
            {
                return managementArea;
            }
        }
    }
}
