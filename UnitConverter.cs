
using System.Collections.Generic;
using Rhino;

namespace fSpyFileImport
{
  

    public static class UnitConverter
    {
        private static readonly Dictionary<string, double> ToMeters = new Dictionary<string, double>
        {
            { "Millimeters", 0.001 },
            { "Centimeters", 0.01 },
            { "Meters", 1.0 },
            { "Kilometers", 1000.0 },
            { "Inches", 0.0254 },
            { "Feet", 0.3048 },
            { "Miles", 1609.34 }
        };

        public static double GetImportToModelScale(string importUnitName, RhinoDoc doc)
        {
            
            if (!ToMeters.TryGetValue(importUnitName, out double importToMeters))
                throw new System.Exception($"Unsupported import unit: {importUnitName}");

            double modelToMeters = GetUnitToMeterFactor(doc.ModelUnitSystem);

            return importToMeters / modelToMeters;
        }

        private static double GetUnitToMeterFactor(UnitSystem unit)
        {
            switch (unit)
            {
                case UnitSystem.Millimeters: return 0.001;
                case UnitSystem.Centimeters: return 0.01;
                case UnitSystem.Meters: return 1.0;
                case UnitSystem.Kilometers: return 1000.0;
                case UnitSystem.Inches: return 0.0254;
                case UnitSystem.Feet: return 0.3048;
                case UnitSystem.Miles: return 1609.34;
                default: return 1.0; // fallback
            }
        }
    }

}