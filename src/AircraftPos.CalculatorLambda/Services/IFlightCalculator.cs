using AircraftPos.CalculatorLambda.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AircraftPos.CalculatorLambda.Services
{
    public interface IFlightCalculator
    {
        CalculationResult CalculateFlightResult(ParsedPosReport posReport);
    }
}
