using System;
using System.Linq;
using System.Threading.Tasks;
using DistriHub.Models;
using DistriHub.Repository;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace DistriHub.Controllers
{
    public class ModelController : Controller
    {
        private readonly IRepository _repository;

        public ModelController(IRepository repository)
        {
            _repository = repository;
        }

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var categories = await _repository.GetAllCategoriesAsync();
            ViewData["Categories"] = categories.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName }).ToList();
            ViewData["SubCategories"] = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            return View(new ModelDto());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ModelDto dto)
        {
            var categories = await _repository.GetAllCategoriesAsync();
            ViewData["Categories"] = categories.Select(c => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = c.CategoryId.ToString(), Text = c.CategoryName }).ToList();
            var subcats = await _repository.GetSubCategoriesByCategoryIdAsync(dto.CategoryId);
            ViewData["SubCategories"] = subcats.Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem { Value = s.SubCategoryId.ToString(), Text = s.SubCategoryName }).ToList();

            if (!ModelState.IsValid)
                return View(dto);

            var existing = await _repository.GetModelByNameAndSubCategoryAsync(dto.ModelName ?? string.Empty, dto.SubCategoryId);
            if (existing != null)
            {
                ModelState.AddModelError(string.Empty, "Model with the same name already exists for the selected subcategory.");
                return View(dto);
            }

            var model = new Models.Model
            {
                CategoryId = dto.CategoryId,
                SubCategoryId = dto.SubCategoryId,
                ModelName = dto.ModelName ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                await _repository.AddModelAsync(model);
                ModelState.Clear();
                ViewData["SuccessMessage"] = "Model added successfully.";
                ViewData["SubCategories"] = Array.Empty<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
                return View(new ModelDto());
            }
            catch (SqlException sqlEx)
            {
                ModelState.AddModelError(string.Empty, "Unable to save model: " + sqlEx.Message);
                return View(dto);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, "An unexpected error occurred: " + ex.Message);
                return View(dto);
            }
        }

        [HttpGet]
        public async Task<JsonResult> GetSubCategories(int categoryId)
        {
            var subcats = await _repository.GetSubCategoriesByCategoryIdAsync(categoryId);
            var list = subcats.Select(s => new { id = s.SubCategoryId, text = s.SubCategoryName });
            return Json(list);
        }

        [AcceptVerbs("GET", "POST")]
        public async Task<IActionResult> ValidateModelName(string ModelName, int CategoryId, int SubCategoryId)
        {
            if (string.IsNullOrWhiteSpace(ModelName))
                return Json(true);

            var existing = await _repository.GetModelByNameAndSubCategoryAsync(ModelName.Trim(), SubCategoryId);
            if (existing == null) return Json(true);
            return Json("Model with this name already exists for the selected subcategory.");
        }

        [HttpGet]
        public async Task<JsonResult> Exists(string name, int subCategoryId)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Json(new { exists = false });

            var existing = await _repository.GetModelByNameAndSubCategoryAsync(name.Trim(), subCategoryId);
            return Json(new { exists = existing != null });
        }
    }
}
