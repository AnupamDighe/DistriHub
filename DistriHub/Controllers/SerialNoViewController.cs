using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using DistriHub.Repository;

namespace DistriHub.Controllers
{
    [AllowAnonymous]
    public class SerialNoViewController : Controller
    {
        private readonly IRepository _repo;

        public SerialNoViewController(IRepository repo)
        {
            _repo = repo;
        }

        public async Task<IActionResult> SerialNoList()
        {
            var products = await _repo.GetProductDetailsAsync();
            return View(products);
        }

        [HttpGet]
        public async Task<IActionResult> ApiGetProductDetails()
        {
            // DataTables server-side parameters
            var drawStr = Request.Query["draw"].FirstOrDefault();
            var startStr = Request.Query["start"].FirstOrDefault();
            var lengthStr = Request.Query["length"].FirstOrDefault();

            int draw = 0;
            int.TryParse(drawStr, out draw);
            int start = 0;
            int.TryParse(startStr, out start);
            int length = 10;
            int.TryParse(lengthStr, out length);

            // Custom serial filter or global search
            var serial = Request.Query["serial"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(serial))
                serial = Request.Query["search[value]"].FirstOrDefault();

            // Ordering
            var orderColStr = Request.Query["order[0][column]"].FirstOrDefault();
            var orderDir = Request.Query["order[0][dir]"].FirstOrDefault() ?? "desc";
            int orderCol = 0;
            int.TryParse(orderColStr, out orderCol);
            var orderColName = Request.Query[$"columns[{orderCol}][data]"].FirstOrDefault() ?? "uploadDate";

            var recordsTotal = await _repo.GetProductDetailsCountAsync();
            var recordsFiltered = string.IsNullOrWhiteSpace(serial)
                ? recordsTotal
                : await _repo.GetProductDetailsFilteredCountAsync(serial);

            var items = await _repo.GetProductDetailsPagedAsync(serial, start, length, orderColName, orderDir);

            // Map to camelCase keys expected by client-side DataTables columns
            var data = items.Select(p => new
            {
                serialNo = p.SerialNo,
                categoryName = p.CategoryName,
                subCategoryName = p.SubCategoryName,
                modelName = p.ModelName,
                uploadDate = p.UploadDate,
                isUsedDisplay = p.IsUsedDisplay,
                finance = p.Finance,
                distributor = p.Distributor,
                financeDate = p.FinanceDate,
                dealer = p.Dealer,
                installation = p.Installation
            });

            return Json(new
            {
                draw,
                recordsTotal,
                recordsFiltered,
                data
            });
        }
    }
}
