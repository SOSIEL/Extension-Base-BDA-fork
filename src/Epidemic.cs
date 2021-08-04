//  Copyright 2005-2010 Portland State University, University of Wisconsin
//  Authors:  Robert M. Scheller,   James B. Domingo

using Landis.Core;
using Landis.Library.AgeOnlyCohorts;
using Landis.SpatialModeling;

namespace Landis.Extension.BaseBDA
{
    public class Epidemic : ICohortDisturbance
    {
        public class KillResult
        {
            public readonly int CohortsKilled;
            public readonly int CFSConifersKilled;
            public readonly long BiomassKilled;

            public KillResult(int cohortsKilled, int cfsConifersKilled, long biomassKilled)
            {
                CohortsKilled = cohortsKilled;
                CFSConifersKilled = cfsConifersKilled;
                BiomassKilled = biomassKilled;
            }
        }

        uint[] selectedManagementAreas;
        private static IEcoregionDataset ecoregions;
        private IAgent epidemicParms;
        private int totalSitesDamaged;
        private int totalCohortsKilled;
        private double meanSeverity;
        private long totalBiomassKilled;
        private int[] cohortsKilledInSelectedManagementAreas;
        private int[] totalSitesDamagedInSelectedManagementAreas;
        private long[] totalBiomassKilledInSelectedManagementAreas;
        private int siteSeverity;
        private double random;
        private double siteVulnerability;
        //private int advRegenAgeCutoff;
        private int siteCohortsKilled;
        private int siteCFSConifersKilled;
        private long biomassKilled;
        private long biomassCohortCount;
        private int[] sitesInEvent;

        private ActiveSite currentSite; // current site where cohorts are being damaged

        private enum TempPattern        {random, cyclic};
        private enum NeighborShape      {uniform, linear, gaussian};
        private enum InitialCondition   {map, none};
        private enum SRDMode            {SRDmax, SRDmean};


        //---------------------------------------------------------------------

        public int[] SitesInEvent
        {
            get
            {
                return sitesInEvent;
            }
        }

        //---------------------------------------------------------------------

        public int CohortsKilled
        {
            get
            {
                return totalCohortsKilled;
            }
        }

        //---------------------------------------------------------------------

        public long TotalBiomassKilled
        {
            get
            {
                return totalBiomassKilled;
            }
        }

        public int[] CohortsKilledInSelectedManagementAreas
        {
            get
            {
                return cohortsKilledInSelectedManagementAreas;
            }
        }

        public int[] TotalSitesDamagedInSelectedManagementAreas
        {
            get
            {
                return totalSitesDamagedInSelectedManagementAreas;
            }
        }

        public long[] TotalBiomassKilledInSelectedManagementAreas
        {
            get
            {
                return totalBiomassKilledInSelectedManagementAreas;
            }
        }

        //---------------------------------------------------------------------

        public double MeanSeverity
        {
            get
            {
                return meanSeverity;
            }
        }

        //---------------------------------------------------------------------

        public int TotalSitesDamaged
        {
            get
            {
                return totalSitesDamaged;
            }
        }
        //---------------------------------------------------------------------

        ExtensionType IDisturbance.Type
        {
            get
            {
                return PlugIn.type;
            }
        }

        //---------------------------------------------------------------------

        ActiveSite IDisturbance.CurrentSite
        {
            get
            {
                return currentSite;
            }
        }
        //---------------------------------------------------------------------

        IAgent EpidemicParameters
        {
            get
            {
                return epidemicParms;
            }
        }

        //---------------------------------------------------------------------
        ///<summary>
        ///Initialize an Epidemic - defined as an agent outbreak for an entire landscape
        ///at a single BDA timestep.  One epidemic per agent per BDA timestep
        ///</summary>

        public static void Initialize(IAgent agent)
        {
            PlugIn.ModelCore.UI.WriteLine("   Initializing agent {0}.", agent.AgentName);

            ecoregions = PlugIn.ModelCore.Ecoregions;

            //.ActiveSiteValues allows you to reset all active site at once.
            SiteVars.NeighborResourceDom.ActiveSiteValues = 0;
            SiteVars.Vulnerability.ActiveSiteValues = 0;
            SiteVars.SiteResourceDomMod.ActiveSiteValues = 0;
            SiteVars.SiteResourceDom.ActiveSiteValues = 0;

            foreach (ActiveSite site in PlugIn.ModelCore.Landscape)
            {
                if(agent.OutbreakZone[site] == Zone.Newzone)
                    agent.OutbreakZone[site] = Zone.Lastzone;
                else
                    agent.OutbreakZone[site] = Zone.Nozone;
            }
        }

        //---------------------------------------------------------------------
        ///<summary>
        ///Simulate an Epidemic - This is the controlling function that calls the
        ///subsequent function.  The basic logic of an epidemic resides here.
        ///</summary>
        public static Epidemic Simulate(IAgent agent, int timestep, int ROS, uint[] selectedManagementAreas)
        {
            Epidemic CurrentEpidemic = new Epidemic(agent, selectedManagementAreas);
            PlugIn.ModelCore.UI.WriteLine("   New BDA Epidemic Activated.");

            //SiteResources.SiteResourceDominance(agent, ROS, SiteVars.Cohorts);
            SiteResources.SiteResourceDominance(agent, ROS);
            SiteResources.SiteResourceDominanceModifier(agent); 

            if(agent.Dispersal)
            {
                //Asynchronous - Simulate Agent Dispersal
                // Calculate Site Vulnerability without considering the Neighborhood
                // If neither disturbance modifiers nor ecoregion modifiers are active,
                //  Vulnerability will equal SiteReourceDominance.
                SiteResources.SiteVulnerability(agent, ROS, false);
                Epicenters.NewEpicenters(agent, timestep);
            }
            else
            {
                //Synchronous:  assume that all Active sites can potentially be
                //disturbed without regard to initial locations.
                foreach (ActiveSite site in PlugIn.ModelCore.Landscape)
                    agent.OutbreakZone[site] = Zone.Newzone;
            }

            //Consider the Neighborhood if requested:
            if (agent.NeighborFlag)
                SiteResources.NeighborResourceDominance(agent);

            //Recalculate Site Vulnerability considering neighbors if necessary:
            SiteResources.SiteVulnerability(agent, ROS, agent.NeighborFlag);

            CurrentEpidemic.DisturbSites(agent);

            return CurrentEpidemic;
        }

        //---------------------------------------------------------------------
        // Epidemic Constructor
        private Epidemic(IAgent agent, uint[] selectedManagementAreas)
        {
            sitesInEvent = new int[ecoregions.Count];
            foreach(IEcoregion ecoregion in ecoregions)
                sitesInEvent[ecoregion.Index] = 0;
            epidemicParms = agent;
            totalCohortsKilled = 0;
            meanSeverity = 0.0;
            totalSitesDamaged = 0;
            biomassCohortCount = 0;

            this.selectedManagementAreas = selectedManagementAreas;
            int selectedManagementAreaCount = (selectedManagementAreas != null) ? selectedManagementAreas.Length : 0;
            cohortsKilledInSelectedManagementAreas = new int[selectedManagementAreaCount];
            totalSitesDamagedInSelectedManagementAreas = new int[selectedManagementAreaCount];
            totalBiomassKilledInSelectedManagementAreas = new long[selectedManagementAreaCount];

            //PlugIn.ModelCore.Log.WriteLine("New Agent event");
        }

        //---------------------------------------------------------------------
        //Go through all active sites and damage them according to the
        //Site Vulnerability.
        private void DisturbSites(IAgent agent)
        {
            totalBiomassKilled = 0;
            int totalSiteSeverity = 0;
            //this.advRegenAgeCutoff = agent.AdvRegenAgeCutoff;

            foreach (ActiveSite site in PlugIn.ModelCore.Landscape)
            {
                siteSeverity = 0;
                random = 0;

                double myRand = PlugIn.ModelCore.GenerateUniform();

                if (agent.OutbreakZone[site] == Zone.Newzone && SiteVars.Vulnerability[site] > myRand)
                {
                    //PlugIn.ModelCore.Log.WriteLine("Zone={0}, agent.OutbreakZone={1}", Zone.Newzone.ToString(), agent.OutbreakZone[site]);
                    //PlugIn.ModelCore.Log.WriteLine("Vulnerability={0}, Randnum={1}", SiteVars.Vulnerability[site], PlugIn.ModelCore.GenerateUniform());

                    double vulnerability = SiteVars.Vulnerability[site];
                    if (vulnerability >= 0) siteSeverity = 1;
                    if (vulnerability >= agent.Class2_SV) siteSeverity = 2;
                    if (vulnerability >= agent.Class3_SV) siteSeverity = 3;

                    random = myRand;
                    siteVulnerability = SiteVars.Vulnerability[site];

                    if (siteSeverity > 0)
                    {
                        var killResult = KillSiteCohorts(site);

                        if (SiteVars.NumberCFSConifersKilled[site].ContainsKey(PlugIn.ModelCore.CurrentTime))
                        {
                            int prevKilled = SiteVars.NumberCFSConifersKilled[site][PlugIn.ModelCore.CurrentTime];
                            SiteVars.NumberCFSConifersKilled[site][PlugIn.ModelCore.CurrentTime] =
                                prevKilled + killResult.CFSConifersKilled;
                        }
                        else
                        {
                            SiteVars.NumberCFSConifersKilled[site].Add(
                                PlugIn.ModelCore.CurrentTime, killResult.CFSConifersKilled);
                        }

                        if (killResult.CohortsKilled > 0)
                        {
                            totalCohortsKilled += killResult.CohortsKilled;
                            totalBiomassKilled += killResult.BiomassKilled;
                            ++totalSitesDamaged;

                            if (selectedManagementAreas != null)
                            {
                                var managementArea = SiteVars.ManagementArea[site];
                                var mapCode = managementArea != null ? managementArea.MapCode : uint.MaxValue;
                                // PlugIn.ModelCore.UI.WriteLine("BaseBDA: >>> Distrubed site at "
                                //     + $"({site.Location.Row}, {site.Location.Column}) in the MA-{mapCode}");
                                if (managementArea != null)
                                {
                                    for (int i = 0; i < selectedManagementAreas.Length; ++i)
                                    {
                                        if (selectedManagementAreas[i] == managementArea.MapCode)
                                        {
                                            cohortsKilledInSelectedManagementAreas[i] += killResult.CohortsKilled;
                                            totalBiomassKilledInSelectedManagementAreas[i] += killResult.BiomassKilled;
                                            ++totalSitesDamagedInSelectedManagementAreas[i];
                                        }
                                    }
                                }
                            }

                            totalSiteSeverity += siteSeverity;
                            SiteVars.Disturbed[site] = true;
                            SiteVars.TimeOfLastEvent[site] = PlugIn.ModelCore.CurrentTime;
                            SiteVars.AgentName[site] = agent.AgentName;
                        }
                        else
                            siteSeverity = 0;
                    }
                }
                agent.Severity[site] = (byte) siteSeverity;
            }
            if (totalSitesDamaged > 0)
                meanSeverity = (double)totalSiteSeverity / totalSitesDamaged;
        }

        //---------------------------------------------------------------------
        //A small helper function for going through list of cohorts at a site
        //and checking them with the filter provided by RemoveMarkedCohort(ICohort).
        private KillResult KillSiteCohorts(ActiveSite site)
        {
            siteCohortsKilled = 0;
            siteCFSConifersKilled = 0;
            biomassKilled = 0;
            biomassCohortCount = 0;
            currentSite = site;
            var siteCohorts = SiteVars.Cohorts[site];
            siteCohorts.RemoveMarkedCohorts(this);
            // PlugIn.ModelCore.UI.WriteLine($"BaseBDA: Killed biomass {biomassKilled} in {biomassCohortCount} cohorts");
            return new KillResult(siteCohortsKilled, siteCFSConifersKilled, biomassKilled);
        }

        //---------------------------------------------------------------------
        // MarkCohortForDeath is a filter to determine which cohorts are removed.
        // Each cohort is passed into the function and tested whether it should
        // be killed.
        bool ICohortDisturbance.MarkCohortForDeath(ICohort cohort)
        {
            //PlugIn.ModelCore.Log.WriteLine("Cohort={0}, {1}, {2}.", cohort.Species.Name, cohort.Age, cohort.Species.Index);
            
            bool killCohort = false;
           // bool advRegenSpp = false;

            ISppParameters sppParms = epidemicParms.SppParameters[cohort.Species.Index];

            //foreach (ISpecies mySpecies in epidemicParms.AdvRegenSppList)
            //{
            //   if (cohort.Species == mySpecies)
            //   {
            //        advRegenSpp = true;
            //        break;
            //    }
            //}

            if (cohort.Age >= sppParms.ResistantHostAge)
            {
                if (random <= siteVulnerability * sppParms.ResistantHostVuln)
                {
                    //if (advRegenSpp && cohort.Age <= this.advRegenAgeCutoff)
                    //    killCohort = false;
                    //else
                        killCohort = true;
                }
            }

            if (cohort.Age >= sppParms.TolerantHostAge)
            {
                if (random <= siteVulnerability * sppParms.TolerantHostVuln)
                {
                    //if (advRegenSpp && cohort.Age <= this.advRegenAgeCutoff)
                     //   killCohort = false;
                    //else
                        killCohort = true;
                }
            }

            if (cohort.Age >= sppParms.VulnerableHostAge)
            {
                if (random <= siteVulnerability * sppParms.VulnerableHostVuln)
                {
                    //if (advRegenSpp && cohort.Age <= this.advRegenAgeCutoff)
                     //   killCohort = false;
                    //else
                        killCohort = true;
                }
            }
            

            if (killCohort)
            {
                siteCohortsKilled++;
                if (sppParms.CFSConifer)
                    siteCFSConifersKilled++;

                if (cohort is Landis.Library.BiomassCohorts.ICohort)
                {
                    var biomassCohort = cohort as Landis.Library.BiomassCohorts.ICohort;
                    biomassKilled += biomassCohort.Biomass;
                    ++biomassCohortCount;
                }
            }

            return killCohort;
        }
    }
}
