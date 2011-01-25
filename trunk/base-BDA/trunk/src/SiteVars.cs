//  Copyright 2005-2010 Portland State University, University of Wisconsin
//  Authors:  Robert M. Scheller,   James B. Domingo
//  BDA originally programmed by Wei (Vera) Li at University of Missouri-Columbia in 2004.

using Landis.Core;
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
        private static ISiteVar<int> timeOfLastFire;
        private static ISiteVar<int> timeOfLastWind;
        private static ISiteVar<int> timeOfLastHarvest;
        private static ISiteVar<double> neighborResourceDom;
        private static ISiteVar<double> siteResourceDomMod;
        private static ISiteVar<double> siteResourceDom;
        private static ISiteVar<double> vulnerability;
        private static ISiteVar<bool> disturbed;
        private static ISiteVar<Dictionary<int,int>> numberCFSconifersKilled;
        private static ISiteVar<ISiteCohorts> cohorts;

        //---------------------------------------------------------------------

        public static void Initialize(ICore modelCore)
        {
            timeOfLastBDA  = modelCore.Landscape.NewSiteVar<int>();
            neighborResourceDom = modelCore.Landscape.NewSiteVar<double>();
            siteResourceDomMod = modelCore.Landscape.NewSiteVar<double>();
            siteResourceDom = modelCore.Landscape.NewSiteVar<double>();
            vulnerability = modelCore.Landscape.NewSiteVar<double>();
            disturbed = modelCore.Landscape.NewSiteVar<bool>();
            numberCFSconifersKilled = modelCore.Landscape.NewSiteVar<Dictionary<int, int>>();

            SiteVars.TimeOfLastEvent.ActiveSiteValues = -10000;
            SiteVars.NeighborResourceDom.ActiveSiteValues = 0.0;
            SiteVars.SiteResourceDomMod.ActiveSiteValues = 0.0;
            SiteVars.SiteResourceDom.ActiveSiteValues = 0.0;
            SiteVars.Vulnerability.ActiveSiteValues = 0.0;

            cohorts = PlugIn.ModelCore.GetSiteVar<ISiteCohorts>("Succession.AgeCohorts");

            foreach(ActiveSite site in modelCore.Landscape)
                SiteVars.NumberCFSconifersKilled[site] = new Dictionary<int, int>();

            // Added for v1.1 to enable interactions with CFS fuels extension.
            modelCore.RegisterSiteVar(SiteVars.NumberCFSconifersKilled, "BDA.NumCFSConifers");
            modelCore.RegisterSiteVar(SiteVars.TimeOfLastEvent, "BDA.TimeOfLastEvent");

        }

        //---------------------------------------------------------------------

        public static void InitializeTimeOfLastDisturbances()
        {
            timeOfLastWind  = PlugIn.ModelCore.GetSiteVar<int>("Wind.TimeOfLastEvent");
            timeOfLastFire  = PlugIn.ModelCore.GetSiteVar<int>("Fire.TimeOfLastEvent");
            timeOfLastHarvest  = PlugIn.ModelCore.GetSiteVar<int>("Harvest.TimeOfLastEvent");

        }
        //---------------------------------------------------------------------
        public static ISiteVar<int> TimeOfLastEvent
        {
            get {
                return timeOfLastBDA;
            }
        }

        public static ISiteVar<int> TimeOfLastFire
        {
            get {
                return timeOfLastFire;
            }
        }

        public static ISiteVar<int> TimeOfLastWind
        {
            get {
                return timeOfLastWind;
            }
        }
        public static ISiteVar<int> TimeOfLastHarvest
        {
            get {
                return timeOfLastHarvest;
            }
        }
        //---------------------------------------------------------------------


        public static ISiteVar<double> SiteResourceDom
        {
            get {
                return siteResourceDom;
            }
        }
        public static ISiteVar<double> NeighborResourceDom
        {
            get {
                return neighborResourceDom;
            }
        }
        public static ISiteVar<double> SiteResourceDomMod
        {
            get {
                return siteResourceDomMod;
            }
        }

        public static ISiteVar<double> Vulnerability
        {
            get {
                return vulnerability;
            }
        }
        //---------------------------------------------------------------------

        public static ISiteVar<bool> Disturbed
        {
            get {
                return disturbed;
            }
        }

        public static ISiteVar<Dictionary<int,int>> NumberCFSconifersKilled
        {
            get {
                return numberCFSconifersKilled;
            }
            set {
                numberCFSconifersKilled = value;
            }
        }

        public static ISiteVar<ISiteCohorts> Cohorts
        {
            get
            {
                return cohorts;
            }

        }
    }
}
