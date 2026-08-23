using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace DistriHub.Models
{
    // DTO used for view rendering and binding
    public class CategoryDto
    {
        [Required(ErrorMessage = "Category name is required")]
        [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters")]
        [Display(Name = "Category Name")]
        [Remote(action: "ValidateName", controller: "Category", ErrorMessage = "Category with this name already exists.")]
        public string? CategoryName { get; set; }
    }
}
