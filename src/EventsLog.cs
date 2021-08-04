using Landis.Library.Metadata;

namespace Landis.Extension.BaseBDA
{
    public class EventsLog
    {
        //log.Write("CurrentTime, ROS, AgentName, NumCohortsKilled, NumSitesDamaged, MeanSeverity");

        [DataField(Unit = FieldUnits.Year, Desc = "Simulation Year")]
        public int Time { set; get; }

        [DataField(Desc = "Rate of Spread")]
        public int ROS { set; get; }

        [DataField(Desc = "Agent Name")]
        public string AgentName { set; get; }

        //[DataField(Unit = FiledUnits.None, Desc = "Total Number of Sites in Event")]
        //public int TotalSites { set; get; }

        [DataField(Unit = FieldUnits.Count, Desc = "Number of Cohorts Killed")]
        public int CohortsKilled { set; get; }

        [DataField(Unit = FieldUnits.Count, Desc = "Number of Damaged Sites in Event")]
        public int DamagedSites { set; get; }

        [DataField(Desc = "Mean Severity (1-5)", Format="0.00")]
        public double MeanSeverity { set; get; }

        [DataField(Unit = FieldUnits.Count, Desc = "Total Biomass Killed")]
        public long TotalBiomassKilled { set; get; }

        // I had to make this 'double' because metadata library doesn't understand any other array types
        [DataField(Unit = FieldUnits.Count, Desc = "Number of Cohorts Killed in the selected MAs", ColumnList = true)]
        public double[] CohortsKilledInMA { set; get; }

        // I had to make this 'double' because metadata library doesn't understand any other array types
        [DataField(Unit = FieldUnits.Count, Desc = "Number of Damaged Sites in Event in the selected MAs", ColumnList = true)]
        public double[] DamagedSitesInMA { set; get; }

        // I had to make this 'double' because metadata library doesn't understand any other array types
        [DataField(Unit = FieldUnits.Count, Desc = "Total Biomass Killed in the selected MAs", ColumnList = true)]
        public double[] TotalBiomassKilledInMA { set; get; }
    }
}
