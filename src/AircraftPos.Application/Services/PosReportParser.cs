using AircraftPos.Application.Interfaces;
using AircraftPos.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.Application.Services
{
    public class PosReportParser : IPosReportParser
    {
        public PosReport Parse(string rawMessage, DateTime receivedAt)
        {
            if (string.IsNullOrWhiteSpace(rawMessage))
            {
                throw new FormatException("POS message could not be empty.");
            }
            var parts = rawMessage.Split('/');

            if (parts.Length != 8)
            {
                throw new FormatException("Invalid POS message format.");
            }

            if (parts[0] != "POS")
            {
                throw new FormatException("Invalid POS message prefix.");
            }

            var flightInfo = parts[1];

            var flightNumber = flightInfo.Split(".FR")[0];

            var departure = flightInfo.Split(".FR")[1].Trim();

            var destination = parts[2]
                .Replace("TO ", "")
                .Trim();

            var dateTimePart = parts[3];

            if (dateTimePart.Length != 6)
            {
                throw new FormatException("Invalid POS date/time format.");
            }

            var day = int.Parse(dateTimePart.Substring(0, 2));
            var hour = int.Parse(dateTimePart.Substring(2, 2));
            var minute = int.Parse(dateTimePart.Substring(4, 2));

            var timestamp = new DateTime(
                receivedAt.Year,
                receivedAt.Month,
                day,
                hour,
                minute,
                0,
                DateTimeKind.Utc);

            var coordinatePart = parts[4];

            var longitudeStart = coordinatePart.IndexOfAny(['E', 'W']);

            if (longitudeStart <= 0)
            {
                throw new FormatException("Invalid coordinate format.");
            }

            var latitudePart = coordinatePart[..longitudeStart];
            var longitudePart = coordinatePart[longitudeStart..];

            var latitude = ParseCoordinate(latitudePart);
            var longitude = ParseCoordinate(longitudePart);

            var groundSpeedKnots = double.Parse(parts[5]);
            var fuelOnBoardKg = double.Parse(parts[6]);
            var fuelFlowKgPerHour = double.Parse(parts[7]);

            var flightDate = timestamp.ToString("yyyyMMdd");
            var flightId = $"{flightNumber}{flightDate}{departure}{destination}";

            return new PosReport
            {
                FlightId = flightId,
                FlightNumber = flightNumber,
                Departure = departure,
                Destination = destination,
                Timestamp = timestamp,
                Latitude = latitude,
                Longitude = longitude,
                GroundSpeedKnots = groundSpeedKnots,
                FuelOnBoardKg = fuelOnBoardKg,
                FuelFlowKgPerHour = fuelFlowKgPerHour
            };
        }

        private double ParseCoordinate(string coordinate)
        {
            var hemisphere = coordinate[0];

            int degreeLength = hemisphere switch
            {
                'N' or 'S' => 2,
                'E' or 'W' => 3,
                _ => throw new FormatException("Invalid coordinate hemisphere.")
            };

            var degrees = double.Parse(
                coordinate.Substring(1, degreeLength));

            var minutes = double.Parse(
                coordinate.Substring(1 + degreeLength));

            var decimalDegrees = degrees + minutes / 60;

            if (hemisphere is 'S' or 'W')
            {
                decimalDegrees = -decimalDegrees;
            }

            return decimalDegrees;
        }
    }
}
