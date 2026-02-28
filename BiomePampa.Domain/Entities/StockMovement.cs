using BiomePampa.Domain.Common;
using BiomePampa.Domain.Enums;

namespace BiomePampa.Domain.Entities
{
    public class StockMovement : EntityBase
    {
        public Guid ProductId { get; set; }
        public Product Product { get; set; } = null!;
        public Guid? BatchId { get; set; }
        public Batch? Batch { get; set; }
        public MovementType MovementType { get; set; }
        public decimal Quantity { get; set; }
        public DateTime MovementDate { get; set; }
        public string? ResponsiblePerson { get; set; }
        public string? Notes { get; set; }
        public Guid? SupplierId { get; set; }
        public Supplier? Supplier { get; set; }
        public Guid? CustomerId { get; set; }
        public Customer? Customer { get; set; }
    }
}
