namespace ThorusCommon.IO
{
    public static class PrecipTypeComputer
    {
        public delegate T PrecipTypeHandler<T>();

        public static T Compute<T>(float te, float ts, float t01,
            IPrecipTypeBoundaries boundaries,
            PrecipTypeHandler<T> snowHandler,
            PrecipTypeHandler<T> rainHandler,
            PrecipTypeHandler<T> freezingRainHandler,
            PrecipTypeHandler<T> sleetHandler
            )
        {
            if (te < boundaries.MaxTeForSolidPrecip)
            {
                // The air is cold enough to have solid precip when they hit the soil.

                if (ts > boundaries.MinTsForMelting)
                {
                    // Sleet: because the soil is too warm, some deposited snow will melt quickly
                    return sleetHandler();
                }

                // Assume snow
                return snowHandler();
            }

            if (te > boundaries.MinTeForLiquidPrecip)
            {
                // The air is warm enough to have liquid precip when they hit the soil.

                if (ts > boundaries.MinTsForMelting)
                {
                    // The soil is warm enough to keep the deposited rain in the liquid phase
                    return rainHandler();
                }
                else if (ts <= boundaries.MaxTsForFreezing && (te - t01) < boundaries.MaxFreezingRainDelta)
                {
                    // The soil is too cold to keep the liquid phase, so we have freezing rain
                    return freezingRainHandler();
                }

                // Assume rain
                return rainHandler();
            }

            // The air in soil proximity has a temperature that allows mixed precip -
            // that is, both solid and liquid phases at the same time.

            // If the surface is warm enough, then solid phase precip melt when hitting the ground.
            // We hence have sleet conditions - if the sleet has a rate big enough, 
            // it will deposit as a thin layer of melting snow.
            if (ts > boundaries.MinTsForMelting)
            {
                return sleetHandler();
            }

            // If the surface is cold enough, but the air is above the melting point,
            // then we have a very dangerous condition known as freezing rain.
            else if ((te - t01) < boundaries.MaxFreezingRainDelta)
            {
                return freezingRainHandler();
            }

            // The surface is cold enough, the air is below the melting point, 
            // however it is above the temperature that allows consistent solid phase precip.
            // This is a variation of the sleet condition.
            return sleetHandler();
        }
    }
}
