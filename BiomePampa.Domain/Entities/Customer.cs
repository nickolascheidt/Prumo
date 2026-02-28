using BiomePampa.Domain.Common;

namespace BiomePampa.Domain.Entities
{
    public class Customer : EntityBase
    {
        public string Name { get; set; } = string.Empty;
        public string? CompanyName { get; set; }
        public string TaxId { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public string? Address { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? ZipCode { get; set; }
        public string? Notes { get; set; }

        public ICollection<StockMovement> Movements { get; set; } = new List<StockMovement>();
    }
}
