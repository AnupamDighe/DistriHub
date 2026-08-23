using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DistriHub.Models
{
    public class SubCategoryDto
    {
        [Required(ErrorMessage = "Please select a category")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Subcategory name is required")]
        [StringLength(100, ErrorMessage = "Subcategory name cannot exceed 100 characters")]
        [Display(Name = "SubCategory Name")]
        [Remote(action: "ValidateSubName", controller: "SubCategory", AdditionalFields = "CategoryId", ErrorMessage = "Subcategory with this name already exists for the selected category.")]
        public string? SubCategoryName { get; set; }
    }
}
