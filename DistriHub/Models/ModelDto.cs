using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DistriHub.Models
{
    public class ModelDto
    {
        [Required(ErrorMessage = "Please select a category")]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "Please select a subcategory")]
        [Display(Name = "SubCategory")]
        public int SubCategoryId { get; set; }

        [Required(ErrorMessage = "Model name is required")]
        [StringLength(100, ErrorMessage = "Model name cannot exceed 100 characters")]
        [Display(Name = "Model Name")]
        [Remote(action: "ValidateModelName", controller: "Model", AdditionalFields = "CategoryId,SubCategoryId", ErrorMessage = "Model with this name already exists for the selected category/subcategory.")]
        public string? ModelName { get; set; }
    }
}
