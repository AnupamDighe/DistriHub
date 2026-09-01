using System;
using System.ComponentModel.DataAnnotations;

namespace DistriHub.Models
{
    public class ProductDetails
    {
        public int ProductId { get; set; }
        public int CategoryId { get; set; }
        public int SubCategoryId { get; set; }
        public int ModelId { get; set; }

        // Display names joined from related tables
        public string? CategoryName { get; set; }
        public string? SubCategoryName { get; set; }
        public string? ModelName { get; set; }

        [Required]
        [StringLength(100)]
        public string? SerialNo { get; set; }

        public DateTime UploadDate { get; set; }
        // Original boolean value for logic
        public bool IsUsed { get; set; }
        // Display version coming from SQL (e.g., "Yes"/"No")
        public string? IsUsedDisplay { get; set; }
        public string? Finance { get; set; }
        public string? Distributor { get; set; }
        public DateTime? FinanceDate { get; set; }
        public string? Dealer { get; set; }
        public string? Installation { get; set; }
        public DateTime? InstallationDate { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
