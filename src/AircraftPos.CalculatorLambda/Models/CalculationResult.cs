using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.CalculatorLambda.Models
{
    public class CalculationResult
    {
        public string FlightId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public CalculationInput Input { get; set; } = new();

        public int RemainingFlightTimeMinutes { get; set; }

        public double EstimatedFuelAtArrivalKg { get; set; }

        public bool LowFuelWarning { get; set; }
    }

    public class CalculationInput
    {
        public double CurrentLatitude { get; set; }

        public double CurrentLongitude { get; set; }

        public string Destination { get; set; } = string.Empty;

        public double GroundSpeedKnots { get; set; }

        public double FuelOnBoardKg { get; set; }

        public double FuelFlowKgPerHour { get; set; }
    }
}
