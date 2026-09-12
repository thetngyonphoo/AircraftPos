using AircraftPos.CalculatorLambda.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.CalculatorLambda.Services
{
    public class FlightCalculator : IFlightCalculator
    {
        private const double EarthRadiusNauticalMiles = 3440.065;

        private static readonly Dictionary<string, (double Latitude, double Longitude)> Airports =
            new()
            {
                ["RGN"] = (16.9073, 96.1332),
                ["BKK"] = (13.6900, 100.7501),
                ["SIN"] = (1.3644, 103.9915)
            };

        public CalculationResult CalculateFlightResult(ParsedPosReport posReport)
        {
            if (!Airports.TryGetValue(
                posReport.Destination.ToUpperInvariant(),
                out var airport))
            {
                throw new InvalidOperationException(
                    $"Airport coordinates not configured for destination '{posReport.Destination}'.");
            }

            if (posReport.GroundSpeedKnots <= 0)
            {
                throw new InvalidOperationException(
                    "Ground speed must be greater than zero.");
            }

            var distanceNm = HaversineDistanceNm(posReport.Latitude, posReport.Longitude, airport.Latitude, airport.Longitude);

            var remainingFlightTimeHours = distanceNm / posReport.GroundSpeedKnots;

            var remainingFlightTimeMinutes = (int)Math.Round(remainingFlightTimeHours * 60);

            var estimatedFuelAtArrivalKg = posReport.FuelOnBoardKg - (posReport.FuelFlowKgPerHour * remainingFlightTimeHours);

            return new CalculationResult
            {
                FlightId = posReport.FlightId,
                Timestamp = DateTime.UtcNow,

                Input = new CalculationInput
                {
                    CurrentLatitude = posReport.Latitude,
                    CurrentLongitude = posReport.Longitude,
                    Destination = posReport.Destination,
                    GroundSpeedKnots = posReport.GroundSpeedKnots,
                    FuelOnBoardKg = posReport.FuelOnBoardKg,
                    FuelFlowKgPerHour = posReport.FuelFlowKgPerHour
                },
                RemainingFlightTimeMinutes = remainingFlightTimeMinutes,
                EstimatedFuelAtArrivalKg = estimatedFuelAtArrivalKg,
                LowFuelWarning = estimatedFuelAtArrivalKg < 0
            };
        }

        private static double ToRadians(double degrees)
        {
            return degrees * Math.PI / 180.0;
        }

        private static double HaversineDistanceNm(double lat1, double lon1, double lat2, double lon2)
        {
            var dLat = ToRadians(lat2 - lat1);
            var dLon = ToRadians(lon2 - lon1);

            var a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(ToRadians(lat1))
                * Math.Cos(ToRadians(lat2))
                * Math.Sin(dLon / 2)
                * Math.Sin(dLon / 2);

            var c =
                2 * Math.Atan2(
                    Math.Sqrt(a),
                    Math.Sqrt(1 - a));

            return EarthRadiusNauticalMiles * c;
        }      
    }
}
