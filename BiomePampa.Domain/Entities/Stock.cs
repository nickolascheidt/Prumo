using BiomePampa.Domain.Common;

namespace BiomePampa.Domain.Entities
{
    public class Stock : EntityBase
    {
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public decimal AvailableQuantity { get; set; }
        public decimal ReservedQuantity { get; set; }
        public string? Location { get; set; } // Sector or warehouse address
        public DateTime LastUpdate { get; set; }
    }
}
