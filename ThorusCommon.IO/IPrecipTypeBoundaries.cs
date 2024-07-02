using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ThorusCommon.IO
{
    public interface IPrecipTypeBoundaries
    {
        float MaxTeForSolidPrecip { get; }
        float MinTsForMelting { get; }
        float MinTeForLiquidPrecip { get; }
        float MaxTsForFreezing { get; }
        float MaxFreezingRainDelta { get; }
    }
}