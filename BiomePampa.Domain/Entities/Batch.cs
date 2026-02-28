using BiomePampa.Domain.Common;

namespace BiomePampa.Domain.Entities
{
    public class Batch : EntityBase
    {
        public string BatchNumber { get; set; } = string.Empty;
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public DateTime ProductionDate { get; set; }
        public DateTime ExpirationDate { get; set; }
        public decimal InitialQuantity { get; set; }
        public decimal CurrentQuantity { get; set; }
        public Guid? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public string? Notes { get; set; }
    }
}
