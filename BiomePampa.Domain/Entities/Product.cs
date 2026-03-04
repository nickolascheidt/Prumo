using BiomePampa.Domain.Common;
using BiomePampa.Domain.Enums;

namespace BiomePampa.Domain.Entities
{
    public class Product : EntityBase
    {
        //Dados do produto
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        
        //Dados de estoque
        public string SKU { get; set; } = string.Empty;
        public OliveOilType OliveOilType { get; set; }
        public decimal Volume { get; set; } // in liters
        public string? Barcode { get; set; }
        public decimal MinimumStock { get; set; }
        public decimal MaximumStock { get; set; }
        public decimal UnitPrice { get; set; }

        public ICollection<Batch> Batches { get; set; } = new List<Batch>();
        public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
    }
}
