namespace ForgeBorn.Domain.Material
{
    /// <summary>
    /// Game-model material parameters (SI). Physically informed approximation, not engineering data.
    /// Further fields (specific heat, conductivity, flow stress curves) arrive with M2/M3.
    /// </summary>
    public sealed class MaterialData
    {
        public string Id { get; }
        public double DensityKgPerM3 { get; }

        /// <summary>Relative grinding resistance; removal depth scales with 1/GrindResistance. 1.0 = baseline.</summary>
        public double GrindResistance { get; }

        public MaterialData(string id, double densityKgPerM3, double grindResistance)
        {
            Id = id;
            DensityKgPerM3 = densityKgPerM3;
            GrindResistance = grindResistance;
        }

        public static readonly MaterialData SteelBasicV1 = new MaterialData("steel_basic_v1", 7850.0, 1.0);

        public static MaterialData ById(string id)
        {
            return id == SteelBasicV1.Id ? SteelBasicV1 : null;
        }
    }
}
