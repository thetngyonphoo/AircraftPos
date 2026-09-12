using AircraftPos.Application.Services;
using AircraftPos.Tests.TestData;

namespace AircraftPos.Tests.Parser
{
    public class PosReportParserTests
    {
        private readonly PosReportParser _parser = new();

        [Fact]
        public void Parse_ValidMessage_ReturnsCorrectPosReport()
        {
            // Act
            var result = _parser.Parse(
                PosReportTestData.ValidMessage,
                PosReportTestData.ReceivedAt);

            // Assert
            Assert.Equal("UL60420260912RGNBKK", result.FlightId);
            Assert.Equal("UL604", result.FlightNumber);
            Assert.Equal("RGN", result.Departure);
            Assert.Equal("BKK", result.Destination);

            Assert.Equal(
                new DateTime(2026, 9, 12, 10, 21, 0, DateTimeKind.Utc),
                result.Timestamp);

            Assert.Equal(16.705, result.Latitude, 3);
            Assert.Equal(96.208333, result.Longitude, 3);

            Assert.Equal(550, result.GroundSpeedKnots);
            Assert.Equal(12500, result.FuelOnBoardKg);
            Assert.Equal(2800, result.FuelFlowKgPerHour);
        }

        [Fact]
        public void Parse_EmptyMessage_ThrowsFormatException()
        {
            // Arrange
            var receivedAt = DateTime.UtcNow;

            // Act
            var exception = Assert.Throws<FormatException>(() =>
                _parser.Parse("", receivedAt));

            // Assert
            Assert.Equal(
                "POS message could not be empty.",
                exception.Message);
        }

        [Fact]
        public void Parse_InvalidNumberOfParts_ThrowsFormatException()
        {
            // Arrange
            var rawMessage = "POS/UL604.FR RGN/TO BKK/121021/N1642.3E09612.5";

            // Act
            var exception = Assert.Throws<FormatException>(() =>
                _parser.Parse(rawMessage, DateTime.UtcNow));

            // Assert
            Assert.Equal(
                "Invalid POS message format.",
                exception.Message);
        }

        [Fact]
        public void Parse_InvalidPrefix_ThrowsFormatException()
        {
            // Arrange
            var rawMessage =
                "INVALID/UL604.FR RGN/TO BKK/121021/N1642.3E09612.5/550/12500/2800";

            // Act
            var exception = Assert.Throws<FormatException>(() =>
                _parser.Parse(rawMessage, DateTime.UtcNow));

            // Assert
            Assert.Equal(
                "Invalid POS message prefix.",
                exception.Message);
        }

    }
}