using System;
using System.Linq;
using System.Threading.Tasks;
using DistriHub.Models;
using DistriHub.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DistriHub.Controllers
{
    public class SubCategoryController : Controller
    {
        private readonly IRepository _repository;

        public SubCategoryController(IRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await _repository.GetAllCategoriesAsync();
            ViewData["Categories"] = categories.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName }).ToList();
            return View(new SubCategoryDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SubCategoryDto dto)
        {
            var categories = await _repository.GetAllCategoriesAsync();
            ViewData["Categories"] = categories.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName }).ToList();

            if (!ModelState.IsValid)
                return View(dto);

            var existing = await _repository.GetSubCategoryByNameAndCategoryAsync(dto.SubCategoryName ?? string.Empty, dto.CategoryId);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "Subcategory with the same name already exists for the selected category.");
                return View(dto);
            }

            var sub = new SubCategory
            {
                CategoryId = dto.CategoryId,
                SubCategoryName = dto.SubCategoryName ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _repository.AddSubCategoryAsync(sub);
                ModelState.Clear();
                ViewData["SuccessMessage"] = "Subcategory added successfully.";
                return View(new SubCategoryDto());
            }
            catch (SqlException sqlEx)
            {
                ModelState.AddModelError(string.Empty, "Unable to save subcategory: " + sqlEx.Message);
                return View(dto);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                return View(dto);
            }
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> ValidateSubName(string SubCategoryName, int CategoryId)
        {
            if (string.IsNullOrWhiteSpace(SubCategoryName))
                return Json(true);

            var existing = await _repository.GetSubCategoryByNameAndCategoryAsync(SubCategoryName.Trim(), CategoryId);
            if (existing == null) return Json(true);
            return Json("Subcategory with this name already exists for the selected category.");
        }

        [HttpGet]
        public async Task<JsonResult> Exists(string name, int categoryId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { exists = false });

            var existing = await _repository.GetSubCategoryByNameAndCategoryAsync(name.Trim(), categoryId);
            return Json(new { exists = existing != null });
        }
    }
}
