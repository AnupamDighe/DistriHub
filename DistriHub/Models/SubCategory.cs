using System;
using System.ComponentModel.DataAnnotations;

namespace DistriHub.Models
{
    public class SubCategory
    {
        public int SubCategoryId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Subcategory name is required")]
        [StringLength(100, ErrorMessage = "Subcategory name cannot exceed 100 characters")]
        public string SubCategoryName { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
