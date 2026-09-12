using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.CalculatorLambda.Models
{
    public class ParsedRecord
    {
        public string FlightId { get; set; } = string.Empty;

        public DateTime Timestamp { get; set; }

        public string Attachment { get; set; } = string.Empty;
    }
}
