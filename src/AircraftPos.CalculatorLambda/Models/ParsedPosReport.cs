using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.CalculatorLambda.Models
{
    public class ParsedPosReport
    {
        public string FlightId { get; set; } = string.Empty;

        public string FlightNumber { get; set; } = string.Empty;

        public string Departure { get; set; } = string.Empty;

        public string Destination { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public double Latitude { get; set; }

        public double Longitude { get; set; }

        public double GroundSpeedKnots { get; set; }

        public double FuelOnBoardKg { get; set; }

        public double FuelFlowKgPerHour { get; set; }
    }
}
