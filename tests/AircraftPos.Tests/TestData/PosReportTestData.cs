using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.Tests.TestData
{
    public static class PosReportTestData
    {
        public const string ValidMessage =
            "POS/UL604.FR RGN/TO BKK/121021/N1642.3E09612.5/550/12500/2800";

        public static readonly DateTime ReceivedAt =
            new(2026, 9, 12, 10, 30, 0, DateTimeKind.Utc);
    }
}
