using System;
using System.Threading.Tasks;
using DistriHub.Models;
using DistriHub.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DistriHub.Controllers
{
    public class CategoryController : Controller
    {
        private readonly IRepository _repository;

        public CategoryController(IRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View(new CategoryDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CategoryDto dto)
        {
            if (!ModelState.IsValid)
                return View(dto);

            // Check if category already exists (case-insensitive check can be done in DB if needed)
            var existing = await _repository.GetCategoryByNameAsync(dto.CategoryName);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "Category with the same name already exists.");
                return View(dto);
            }

            var category = new Category
            {
                CategoryName = dto.CategoryName,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _repository.AddCategoryAsync(category);
                // Show success message on the same page without redirecting
                ModelState.Clear();
                ViewData["SuccessMessage"] = "Category added successfully.";
                return View(new CategoryDto());
            }
            catch (SqlException sqlEx)
            {
                // Handle SQL errors (e.g. unique constraint violation)
                ModelState.AddModelError(string.Empty, "Unable to save category: " + sqlEx.Message);
                return View(dto);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                return View(dto);
            }
        }
        [HttpGet]
        public async Task<JsonResult> Exists(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { exists = false });

            var existing = await _repository.GetCategoryByNameAsync(name.Trim());
            return Json(new { exists = existing != null });
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> ValidateName(string CategoryName)
        {
            if (string.IsNullOrWhiteSpace(CategoryName))
                return Json(true);

            var existing = await _repository.GetCategoryByNameAsync(CategoryName.Trim());
            // jQuery Validate remote expects true for valid, or a string error message for invalid
            if (existing == null) return Json(true);
            return Json("Category with this name already exists.");
        }
    }
}
