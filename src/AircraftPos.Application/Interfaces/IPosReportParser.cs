using AircraftPos.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.Application.Interfaces
{
    public interface IPosReportParser
    {
        PosReport Parse(string rawMessage, DateTime receivedAt);
    }
}
