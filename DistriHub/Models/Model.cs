using System;
using System.ComponentModel.DataAnnotations;

namespace DistriHub.Models
{
    public class Model
    {
        public int ModelId { get; set; }

        [Required]
        public int CategoryId { get; set; }

        [Required]
        public int SubCategoryId { get; set; }

        [Required(ErrorMessage = "Model name is required")]
        [StringLength(100, ErrorMessage = "Model name cannot exceed 100 characters")]
        public string? ModelName { get; set; }

        [Required]
        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
