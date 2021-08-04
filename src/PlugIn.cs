//  Copyright 2005-2010 Portland State University, University of Wisconsin
//  Authors:  Robert M. Scheller,   James B. Domingo
//  BDA originally programmed by Wei (Vera) Li at University of Missouri-Columbia in 2004.
//  Modified for budworm-BDA version by Brian Miranda, 2012

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Landis.Core;
using Landis.Library.Metadata;
using Landis.SpatialModeling;

namespace Landis.Extension.BaseBDA
{
    ///<summary>
    /// A disturbance plug-in that simulates Biological Agents.
    /// </summary>

    public class PlugIn
        : ExtensionMain
    {
        public static readonly ExtensionType type = new ExtensionType("disturbance:bda");
        public static readonly string ExtensionName = "Base BDA";
        public static MetadataTable<EventsLog> EventLog;

        private string mapNameTemplate;
        private string srdMapNames;
        private string nrdMapNames;
        private string vulnMapNames;
        //private StreamWriter log;
        private IEnumerable<IAgent> manyAgentParameters;
        private static IInputParameters parameters;
        private static ICore modelCore;
        private bool reinitialized;
        private readonly List<string> eventLogExtraColumns;

        //---------------------------------------------------------------------

        public PlugIn()
            : base(ExtensionName, type)
        {
            eventLogExtraColumns = new List<string>();
        }

        //---------------------------------------------------------------------

        public override void LoadParameters(string dataFile,
                                            ICore mCore)
        {
            modelCore = mCore;
            InputParameterParser parser = new InputParameterParser();
            parameters = Landis.Data.Load<IInputParameters>(dataFile, parser);
        }

        //---------------------------------------------------------------------

        public static ICore ModelCore
        {
            get
            {
                return modelCore;
            }
        }

        //---------------------------------------------------------------------


        /// <summary>
        /// Initializes the extension with a data file.
        /// </summary>
        public override void Initialize()
        {
            reinitialized = false;

            if (parameters.SelectedManagementAreas != null)
            {
                eventLogExtraColumns.AddRange(
                    parameters.SelectedManagementAreas.Select(mapCode => mapCode.ToString()));
            }
            ExtensionMetadata.ColumnNames = new List<string>(eventLogExtraColumns);

            MetadataHandler.InitializeMetadata(parameters.Timestep,
               parameters.MapNamesTemplate,
               parameters.SRDMapNames,
               parameters.NRDMapNames,
               parameters.LogFileName,
               parameters.ManyAgentParameters,
               ModelCore);

            Timestep = parameters.Timestep;
            mapNameTemplate = parameters.MapNamesTemplate;
            srdMapNames = parameters.SRDMapNames;
            nrdMapNames = parameters.NRDMapNames;
            vulnMapNames = parameters.BDPMapNames;

            SiteVars.Initialize(modelCore);

            manyAgentParameters = parameters.ManyAgentParameters;
            foreach (IAgent activeAgent in manyAgentParameters)
            {
                if (activeAgent == null)
                    ModelCore.UI.WriteLine("Agent Parameters NOT loading correctly.");
                activeAgent.TimeToNextEpidemic = TimeToNext(activeAgent, Timestep) + activeAgent.StartYear;
                int timeOfNext = ModelCore.CurrentTime
                    + activeAgent.TimeToNextEpidemic
                    - activeAgent.TimeSinceLastEpidemic;
                if (timeOfNext < Timestep)
                    timeOfNext = Timestep;
                if (timeOfNext < activeAgent.StartYear)
                    timeOfNext = activeAgent.StartYear;
                SiteVars.TimeOfNext.ActiveSiteValues = timeOfNext;

                activeAgent.DispersalNeighbors = GetDispersalNeighborhood(activeAgent, Timestep);
                if (activeAgent.DispersalNeighbors != null)
                {
                    ModelCore.UI.WriteLine("Dispersal Neighborhood = {0} neighbors.",
                        activeAgent.DispersalNeighbors.Count());
                }

                activeAgent.ResourceNeighbors = GetResourceNeighborhood(activeAgent);
                if (activeAgent.ResourceNeighbors != null)
                {
                    ModelCore.UI.WriteLine("Resource Neighborhood = {0} neighbors.",
                        activeAgent.ResourceNeighbors.Count());
                }
            }

            //string logFileName = parameters.LogFileName;
            //ModelCore.UI.WriteLine("Opening BDA log file \"{0}\" ...", logFileName);
            //log = ModelCore.CreateTextFile(logFileName);
            //log.AutoFlush = true;
            //log.Write("CurrentTime, ROS, AgentName, NumCohortsKilled, NumSitesDamaged, MeanSeverity");
            //log.WriteLine("");
        }

        public override void InitializePhase2()
        {
            SiteVars.InitializeExternalVars();

            if (parameters.SelectedManagementAreas != null && SiteVars.ManagementArea == null)
            {
                throw new ApplicationException("BaseBDA: OutputKilledBiomassByManagementArea specified," +
                    " but required management area information is not available. Please make sure that you have" +
                    " proper version of the Biomass Harvest extension abd it is active"
                    );
            }

            reinitialized = true;
        }

        //---------------------------------------------------------------------
        ///<summary>
        /// Run the BDA extension at a particular timestep.
        ///</summary>
        public override void Run()
        {
            ModelCore.UI.WriteLine("   Processing landscape for BDA events ...");
            if(!reinitialized)
                InitializePhase2();

            //SiteVars.Epidemic.SiteValues = null;

            int eventCount = 0;

            foreach(IAgent activeAgent in manyAgentParameters)
            {

                activeAgent.TimeSinceLastEpidemic += Timestep;

                int ROS = RegionalOutbreakStatus(activeAgent, Timestep);

                if(ROS > 0)
                {
                    Epidemic.Initialize(activeAgent);
                    Epidemic currentEpic = Epidemic.Simulate(
                        activeAgent, Timestep, ROS, parameters.SelectedManagementAreas);
                    //activeAgent.TimeSinceLastEpidemic = activeAgent.TimeSinceLastEpidemic + Timestep;

                    if (currentEpic != null)
                    {
                        LogEvent(ModelCore.CurrentTime, currentEpic, ROS, activeAgent);

                        {
                            //----- Write BDA severity maps --------
                            string path = MapNames.ReplaceTemplateVars(mapNameTemplate, activeAgent.AgentName, ModelCore.CurrentTime);
                            //IOutputRaster<SeverityPixel> map = CreateMap(ModelCore.CurrentTime, activeAgent.AgentName);
                            //using (map) {
                            //    SeverityPixel pixel = new SeverityPixel();
                            using (IOutputRaster<ShortPixel> outputRaster = modelCore.CreateRaster<ShortPixel>(path, modelCore.Landscape.Dimensions))
                            {
                                ShortPixel pixel = outputRaster.BufferPixel;
                                foreach (Site site in ModelCore.Landscape.AllSites)
                                {
                                    if (site.IsActive)
                                    {
                                        if (SiteVars.Disturbed[site])
                                            pixel.MapCode.Value = (short)(activeAgent.Severity[site] + 1);
                                        else
                                            pixel.MapCode.Value = 1;
                                    }
                                    else
                                    {
                                        //  Inactive site
                                        pixel.MapCode.Value = 0;
                                    }
                                    outputRaster.WriteBufferPixel();
                                }
                            }
                        }

                        if (!(srdMapNames == null))
                        {
                            //----- Write BDA SRD maps --------
                            string path2 = MapNames.ReplaceTemplateVars(srdMapNames, activeAgent.AgentName, ModelCore.CurrentTime);
                            using (IOutputRaster<ShortPixel> outputRaster = modelCore.CreateRaster<ShortPixel>(path2, modelCore.Landscape.Dimensions))
                            {
                                ShortPixel pixel = outputRaster.BufferPixel;
                                foreach (Site site in ModelCore.Landscape.AllSites)
                                {
                                    if (site.IsActive)
                                    {
                                        pixel.MapCode.Value = (short) Math.Round(SiteVars.SiteResourceDom[site] * 100.00);
                                    }
                                    else
                                    {
                                        //  Inactive site
                                        pixel.MapCode.Value = 0;
                                    }
                                    outputRaster.WriteBufferPixel();
                                }
                            }
                        }

                        if (!(nrdMapNames == null))
                        {
                            //----- Write BDA NRD maps --------
                            string path3 = MapNames.ReplaceTemplateVars(nrdMapNames, activeAgent.AgentName,ModelCore.CurrentTime);
                            using (IOutputRaster<ShortPixel> outputRaster = modelCore.CreateRaster<ShortPixel>(path3, modelCore.Landscape.Dimensions))
                            {
                                ShortPixel pixel = outputRaster.BufferPixel;

                                foreach (Site site in ModelCore.Landscape.AllSites)
                                {
                                    if (site.IsActive)
                                    {
                                        pixel.MapCode.Value = (short)Math.Round(SiteVars.NeighborResourceDom[site] * 100.00);
                                    }
                                    else
                                    {
                                        //  Inactive site
                                        pixel.MapCode.Value = 0;
                                    }
                                    outputRaster.WriteBufferPixel();
                                }
                            }
                        }

                        if (!(vulnMapNames == null))
                        {
                            //----- Write BDA Vulnerability maps --------
                            string path4 = MapNames.ReplaceTemplateVars(vulnMapNames, activeAgent.AgentName, ModelCore.CurrentTime);
                            using (IOutputRaster<ShortPixel> outputRaster = modelCore.CreateRaster<ShortPixel>(path4, modelCore.Landscape.Dimensions))
                            {
                                ShortPixel pixel = outputRaster.BufferPixel;

                                foreach (Site site in ModelCore.Landscape.AllSites)
                                {
                                    if (site.IsActive)
                                    {
                                        pixel.MapCode.Value = (short)Math.Round(SiteVars.Vulnerability[site] * 100.00);
                                    }
                                    else
                                    {
                                        //  Inactive site
                                        pixel.MapCode.Value = 0;
                                    }
                                    outputRaster.WriteBufferPixel();
                                }
                            }
                        }

                        eventCount++;
                    }
                }
            }
        }

        //---------------------------------------------------------------------
        /*private void LogEvent(int   currentTime,
                              Epidemic CurrentEvent,
                              int ROS, IAgent agent)
        {
            log.Write("{0},{1},{2},{3},{4},{5:0.0}",
                      currentTime,
                      ROS,
                      agent.AgentName,
                      CurrentEvent.CohortsKilled,
                      CurrentEvent.TotalSitesDamaged,
                      CurrentEvent.MeanSeverity);
            log.WriteLine("");
        }
        */
        //---------------------------------------------------------------------

        private void LogEvent(int currentTime,
                              Epidemic CurrentEvent,
                              int ROS, IAgent agent)
        {
            ModelCore.UI.WriteLine("   Writing log.");
            EventLog.Clear();
            EventsLog el = new EventsLog();
            el.Time = currentTime;
            el.ROS = ROS;
            el.AgentName = agent.AgentName;
            el.DamagedSites = CurrentEvent.TotalSitesDamaged;
            el.CohortsKilled = CurrentEvent.CohortsKilled;
            el.MeanSeverity = CurrentEvent.MeanSeverity;
            el.TotalBiomassKilled = CurrentEvent.TotalBiomassKilled;
            el.DamagedSitesInMA = CurrentEvent.TotalSitesDamagedInSelectedManagementAreas.Select(v => (double)v).ToArray();
            el.CohortsKilledInMA = CurrentEvent.CohortsKilledInSelectedManagementAreas.Select(v => (double)v).ToArray();
            el.TotalBiomassKilledInMA = CurrentEvent.TotalBiomassKilledInSelectedManagementAreas.Select(v => (double)v).ToArray();
            //ExtensionMetadata.ColumnNames = new List<string>(eventLogExtraColumns);
            EventLog.AddObject(el);
            //Debugger.Launch();
            EventLog.WriteToFile();
        }

        //---------------------------------------------------------------------
        /*private IOutputRaster<ShortPixel> CreateMap(int currentTime, string agentName)
        {
            string path = MapNames.ReplaceTemplateVars(mapNameTemplate, agentName, currentTime);
            ModelCore.Log.WriteLine("   Writing BDA severity map to {0} ...", path);
            return PlugIn.modelCore.CreateRaster<ShortPixel>(path, PlugIn.modelCore.Landscape.Dimensions);
        }*/

        /*private IOutputRaster<ShortPixel> CreateSRDMap(int currentTime, string agentName)
        {
            string path = MapNames.ReplaceTemplateVars(srdMapNames, agentName, currentTime);
            ModelCore.Log.WriteLine("   Writing BDA SRD map to {0} ...", path);
            return PlugIn.modelCore.CreateRaster<ShortPixel>(path, PlugIn.modelCore.Landscape.Dimensions);
        }*/

        /*private IOutputRaster<ShortPixel> CreateNRDMap(int currentTime, string agentName)
        {
            string path = MapNames.ReplaceTemplateVars(nrdMapNames, agentName, currentTime);
            ModelCore.Log.WriteLine("   Writing BDA NRD map to {0} ...", path);
            return PlugIn.modelCore.CreateRaster<ShortPixel>(path, PlugIn.modelCore.Landscape.Dimensions);
        }*/
        //---------------------------------------------------------------------
        private static int TimeToNext(IAgent activeAgent, int Timestep)
        {
            int timeToNext = 0;
            if (activeAgent.RandFunc == OutbreakPattern.CyclicUniform)
            {
                int MaxI = (int)Math.Round(activeAgent.MaxInterval);
                int MinI = (int)Math.Round(activeAgent.MinInterval);
                double randNum = ModelCore.GenerateUniform();
                timeToNext = (MinI) + (int)(randNum * (MaxI - MinI));
            }
            else if (activeAgent.RandFunc == OutbreakPattern.CyclicNormal)
            {
                int randNum = (int)activeAgent.NormMean;
                if (activeAgent.NormStDev != 0)
                {
                    //ModelCore.UI.WriteLine(
                    //    $"Agent '{activeAgent.AgentName}': NormMean={activeAgent.NormMean} NormStDev={activeAgent.NormStDev}");
                    ModelCore.NormalDistribution.Mu = activeAgent.NormMean;
                    ModelCore.NormalDistribution.Sigma = activeAgent.NormStDev;
                    randNum = (int)ModelCore.NormalDistribution.NextDouble();
                    randNum = (int)ModelCore.NormalDistribution.NextDouble();
                }
                timeToNext = randNum;

                // Interval times are always rounded up to the next time step increment.
                // This bias can be removed by reducing times by half the time step.
                timeToNext = timeToNext - (Timestep / 2);

                if (timeToNext < 0) timeToNext = 0;
            }
            return timeToNext;
        }

        //---------------------------------------------------------------------
        //Calculate the Regional Outbreak Status (ROS) - the landscape scale intensity
        //of an outbreak or epidemic.
        //Units are from 0 (no outbreak) to 3 (most intense outbreak)

        private static int RegionalOutbreakStatus(IAgent activeAgent, int BDAtimestep)
        {
            int ROS = 0;
            bool activeOutbreak = false;

            if (activeAgent.TimeToNextEpidemic <= activeAgent.TimeSinceLastEpidemic && ModelCore.CurrentTime <= activeAgent.EndYear)
            {
                activeAgent.TimeToNextEpidemic = TimeToNext(activeAgent, BDAtimestep);
                int timeOfNext = ModelCore.CurrentTime + activeAgent.TimeToNextEpidemic;
                SiteVars.TimeOfNext.ActiveSiteValues = timeOfNext;
                activeOutbreak = true;
            }
            
            if(activeOutbreak)
            {
                activeAgent.TimeSinceLastEpidemic = 0;
                //calculate ROS
                if (activeAgent.TempType == TemporalType.pulse)
                    ROS = activeAgent.MaxROS;
                else if (activeAgent.TempType == TemporalType.variablepulse)
                {
                    //randomly select an ROS netween ROSmin and ROSmax
                    //ROS = (int) (Landis.Util.Random.GenerateUniform() *
                    //      (double) (activeAgent.MaxROS - activeAgent.MinROS + 1)) +
                    //      activeAgent.MinROS;

                    // Correction suggested by Brian Miranda, March 2008
                    ROS = (int) (ModelCore.GenerateUniform() *
                          (activeAgent.MaxROS - activeAgent.MinROS)) + 1 +
                          activeAgent.MinROS;
                }
            }
            else
            {
                //activeAgent.TimeSinceLastEpidemic += BDAtimestep;
                ROS = activeAgent.MinROS;
            }
            return ROS;

        }

        //---------------------------------------------------------------------
        //Generate a Relative Location array (with WEIGHTS) of neighbors.
        //Check each cell within a block surrounding the center point.  This will
        //create a set of POTENTIAL neighbors.  These potential neighbors
        //will need to be later checked to ensure that they are within the landscape
        // and active.

        private static IEnumerable<RelativeLocationWeighted> GetResourceNeighborhood(IAgent agent)
        {
            float CellLength = ModelCore.CellLength;
            ModelCore.UI.WriteLine("Creating Neighborhood List.");
            int neighborRadius = agent.NeighborRadius;
            int numCellRadius = (int) ((double) neighborRadius / CellLength) ;
            ModelCore.UI.WriteLine("NeighborRadius={0}, CellLength={1}, numCellRadius={2}",
                        neighborRadius, CellLength, numCellRadius);

            List<RelativeLocationWeighted> neighborhood = new List<RelativeLocationWeighted>();

            for (int row=(numCellRadius * -1); row<=numCellRadius; row++)
            {
                for (int col=(numCellRadius * -1); col<=numCellRadius; col++)
                {
                    double neighborWeight = 0;
                    double centroidDistance = DistanceFromCenter(row ,col);
                    //ModelCore.Log.WriteLine("Centroid Distance = {0}.", centroidDistance);
                    if(centroidDistance  <= neighborRadius && centroidDistance > 0)
                    {

                        if(agent.ShapeOfNeighbor == NeighborShape.uniform)
                            neighborWeight = 1.0;
                        if(agent.ShapeOfNeighbor == NeighborShape.linear)
                        {
                            //neighborWeight = (neighborRadius - centroidDistance + (cellLength/2)) / (double) neighborRadius;
                            neighborWeight = 1.0 - (centroidDistance / (double) neighborRadius);
                        }
                        if(agent.ShapeOfNeighbor == NeighborShape.gaussian)
                        {
                            double halfRadius = neighborRadius / 2;
                            neighborWeight = (float)
                                Math.Exp(-1 * Math.Pow(centroidDistance, 2) / Math.Pow(halfRadius, 2));
                        }

                        RelativeLocation reloc = new RelativeLocation(row, col);
                        neighborhood.Add(new RelativeLocationWeighted(reloc, neighborWeight));
                    }
                }
            }
            return neighborhood;
        }

        //---------------------------------------------------------------------
        // Generate a Relative Location array of neighbors.
        // Check each cell within a circle surrounding the center point.  This will
        // create a set of POTENTIAL neighbors.  These potential neighbors
        // will need to be later checked to ensure that they are within the landscape
        // and active.

        private static IEnumerable<RelativeLocation> GetDispersalNeighborhood(IAgent agent, int timestep)
        {
            double CellLength = ModelCore.CellLength;
            ModelCore.UI.WriteLine("Creating Dispersal Neighborhood List.");

            List<RelativeLocation> neighborhood = new List<RelativeLocation>();

            if(agent.DispersalTemp == DispersalTemplate.N4)
                neighborhood = GetNeighbors(4);

            else if(agent.DispersalTemp == DispersalTemplate.N8)
                neighborhood = GetNeighbors(8);

            else if(agent.DispersalTemp == DispersalTemplate.N12)
                neighborhood = GetNeighbors(12);

            else if(agent.DispersalTemp == DispersalTemplate.N24)
                neighborhood = GetNeighbors(24);

            else if(agent.DispersalTemp == DispersalTemplate.MaxRadius)
            {
                int neighborRadius = agent.DispersalRate * timestep;
                int numCellRadius = (int) (neighborRadius / CellLength);
                ModelCore.UI.WriteLine("NeighborRadius={0}, CellLength={1}, numCellRadius={2}",
                        neighborRadius, CellLength, numCellRadius);

                for(int row=(numCellRadius * -1); row<=numCellRadius; row++)
                {
                    for(int col=(numCellRadius * -1); col<=numCellRadius; col++)
                    {
                        double centroidDistance = DistanceFromCenter(row, col);

                        //ModelCore.Log.WriteLine("Centroid Distance = {0}.", centroidDistance);
                        if(centroidDistance  <= neighborRadius)
                        {
                            neighborhood.Add(new RelativeLocation(row,  col));
                        }
                    }
                }
            }
            return neighborhood;
        }

        //-------------------------------------------------------
        //Calculate the distance from a location to a center
        //point (row and column = 0).
        private static double DistanceFromCenter(double row, double column)
        {
            double CellLength = ModelCore.CellLength;
            row = Math.Abs(row) * CellLength;
            column = Math.Abs(column) * CellLength;
            double aSq = Math.Pow(column,2);
            double bSq = Math.Pow(row,2);
            return Math.Sqrt(aSq + bSq);
        }


        //---------------------------------------------------------------------
        //Generate List of 4,8,12, or 24 nearest neighbors.
        //---------------------------------------------------------------------
        private static List<RelativeLocation> GetNeighbors(int numNeighbors)
        {

            RelativeLocation[] neighborhood4 = new RelativeLocation[] {
                new RelativeLocation( 0,  1),   // east
                new RelativeLocation( 1,  0),   // south
                new RelativeLocation( 0, -1),   // west
                new RelativeLocation(-1,  0),   // north
            };

            RelativeLocation[] neighborhood8 = new RelativeLocation[] {
                new RelativeLocation(-1,  1),   // northeast
                new RelativeLocation( 1,  1),   // southeast
                new RelativeLocation( 1,  -1),  // southwest
                new RelativeLocation( -1, -1),  // northwest
            };

            RelativeLocation[] neighborhood12 = new RelativeLocation[] {
                new RelativeLocation(-2,  0),   // north north
                new RelativeLocation( 0,  2),   // east east
                new RelativeLocation( 2,  0),   // south south
                new RelativeLocation( 0, -2),   // west west
            };

            RelativeLocation[] neighborhood24 = new RelativeLocation[] {
                new RelativeLocation(-2,  -2),  // northwest northwest
                new RelativeLocation( -2,  -1),  // northwest south
                new RelativeLocation( -1,  -2),   // northwest east
                new RelativeLocation( -2,  2),   // northeast northeast
                new RelativeLocation( -2,  1),  // northeast west
                new RelativeLocation( -1, 2),   // northeast south
                new RelativeLocation( 2, 2),  // southeast southeast
                new RelativeLocation(1,  2),   // southeast north
                new RelativeLocation(2,  1),   //southeast west
                new RelativeLocation( 2,  -2),   // southwest southwest
                new RelativeLocation( 2,  -1),   // southwest east
                new RelativeLocation( 1, -2),   // southwest north
            };

            //Progressively add neighbors as necessary:
            List<RelativeLocation> neighbors = new List<RelativeLocation>();
            foreach (RelativeLocation relativeLoc in neighborhood4)
                neighbors.Add(relativeLoc);
            if(numNeighbors <= 4)  return neighbors;

            foreach (RelativeLocation relativeLoc in neighborhood8)
                neighbors.Add(relativeLoc);
            if(numNeighbors <= 8)  return neighbors;

            foreach (RelativeLocation relativeLoc in neighborhood12)
                neighbors.Add(relativeLoc);
            if(numNeighbors <= 12)  return neighbors;

            foreach (RelativeLocation relativeLoc in neighborhood24)
                neighbors.Add(relativeLoc);

            return neighbors;

        }
    }
}
