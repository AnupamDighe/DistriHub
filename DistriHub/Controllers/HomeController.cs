using DistriHub.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.SqlClient;
using DistriHub.Repository;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using ExcelDataReader;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;

namespace DistriHub.Controllers
{
    public class HomeController : Controller
    {
        private readonly IWebHostEnvironment _env;
        private readonly IRepository _repo;

        public HomeController(IWebHostEnvironment env, IRepository repo)
        {
            _env = env;
            _repo = repo;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult UserLogin()
        {
            // If user already logged in, redirect to home
            var current = HttpContext.Session.GetString("Username");
            if (!string.IsNullOrWhiteSpace(current))
            {
                return RedirectToAction("Index");
            }

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UserLogin(Models.LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // fetch password from users table via repository
            var storedPassword = await _repo.GetPasswordByUsernameAsync(model.Username);
            if (string.IsNullOrEmpty(storedPassword) || storedPassword != model.Password)
            {
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return View(model);
            }

            // successful login - set session
            HttpContext.Session.SetString("Username", model.Username);

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult Logout()
        {
            // Clear session and redirect to login
            try
            {
                HttpContext.Session.Remove("Username");
                HttpContext.Session.Clear();
            }
            catch
            {
                // ignore any session errors and proceed to redirect
            }

            return RedirectToAction("UserLogin");
        }

        public IActionResult FileUpload()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> FileUpload(IFormFile uploadFile, string mapping)
        {
            if (string.IsNullOrWhiteSpace(mapping) || mapping == "0")
            {
                ModelState.AddModelError(string.Empty, "Please select a mapping.");
                return View();
            }

            if (uploadFile == null || uploadFile.Length == 0)
            {
                ModelState.AddModelError(string.Empty, "Please upload a valid .xlsx file.");
                return View();
            }

            // Ensure ExcelDataReader can handle encodings
            System.Text.Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var processed = 0;
            var duplicates = new List<DuplicateSerial>();
            var missingModels = new List<(string Serial, string Model)>();
            var updated = 0;
            var missingSerials = new List<string>();
            var distributorMismatchFromExcelAndDB = new List<string>();
            var installationAlreadyDone = new List<dynamic>();
            using (var stream = uploadFile.OpenReadStream())
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var conf = new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
                };

                var ds = reader.AsDataSet(conf);

                // process based on mapping
                switch (mapping)
                {
                    case "1": // Serial No sheet - insert only
                        // Try to find a sheet named "Serial No" (case-insensitive) or fallback to any table that contains required columns
                        var dtSerialNo = ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => string.Equals(t.TableName, "Serial No", StringComparison.OrdinalIgnoreCase))
                            ?? ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => t.Columns.Contains("Model") && t.Columns.Contains("Serial No"));
                        if (dtSerialNo == null)
                            break;

                        foreach (System.Data.DataRow row in dtSerialNo.Rows)
                        {
                            var modelName = dtSerialNo.Columns.Contains("Model") ? row["Model"]?.ToString()?.Trim() : null;
                            var serial = dtSerialNo.Columns.Contains("Serial No") ? row["Serial No"]?.ToString()?.Trim() : null;

                            if (string.IsNullOrWhiteSpace(serial))
                                continue;

                            var exists = await _repo.GetProductBySerialNoAsync(serial);
                            if (exists != null)
                            {
                                var existingModel = await _repo.GetModelByIdAsync(exists.ModelId);
                                duplicates.Add(new DuplicateSerial { ModelName = existingModel?.ModelName ?? string.Empty, SerialNo = serial });
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(modelName))
                            {
                                missingModels.Add((serial, modelName ?? string.Empty));
                                continue;
                            }

                            var model = await _repo.GetModelByNameAsync(modelName);
                            if (model == null)
                            {
                                missingModels.Add((serial, modelName));
                                continue;
                            }

                            var newProd = new Models.ProductDetails
                            {
                                CategoryId = model.CategoryId,
                                SubCategoryId = model.SubCategoryId,
                                ModelId = model.ModelId,
                                SerialNo = serial,
                                UploadDate = DateTime.UtcNow,
                                IsUsed = false,
                                Finance = string.Empty,
                                Distributor = string.Empty,
                                FinanceDate = null,
                                Dealer = string.Empty,
                                Installation = string.Empty,
                                InstallationDate = null,
                                CreatedAt = DateTime.UtcNow
                            };

                            await _repo.InsertProductDetailsAsync(newProd);
                            processed++;
                        }
                        break;

                    case "2": // Distributors - update Distributor for existing serials
                        var dtDistributor = ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => string.Equals(t.TableName, "Distributors", StringComparison.OrdinalIgnoreCase))
                                              ?? ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => t.Columns.Contains("Serial No") && t.Columns.Contains("Distributor"));
                        if (dtDistributor == null)
                            break;

                        foreach (System.Data.DataRow row in dtDistributor.Rows)
                        {
                            var serialNo = dtDistributor.Columns.Contains("Serial No") ? row["Serial No"]?.ToString()?.Trim() : null;
                            var distributor = dtDistributor.Columns.Contains("Distributor") ? row["Distributor"]?.ToString()?.Trim() : null;
                            if (string.IsNullOrWhiteSpace(serialNo)) continue;
                            var existing = await _repo.GetProductBySerialNoAsync(serialNo);
                            if (existing != null)
                            {
                                existing.Distributor = distributor ?? string.Empty;
                                existing.UpdatedAt = DateTime.UtcNow;
                                await _repo.UpdateProductDetailsDistributorsColsAsync(existing);
                                updated++;
                            }
                            else
                            {
                                missingSerials.Add(serialNo);
                            }
                        }

                        break;

                    case "3": // Dealer - update Dealer
                        var dtDealer = ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => string.Equals(t.TableName, "Dealers", StringComparison.OrdinalIgnoreCase))
                                           ?? ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => t.Columns.Contains("Serial No") && t.Columns.Contains("Distributor") && t.Columns.Contains("Dealer"));
                        if (dtDealer == null)
                            break;

                        foreach (System.Data.DataRow row in dtDealer.Rows)
                        {
                            var serial = dtDealer.Columns.Contains("Serial No") ? row["Serial No"]?.ToString()?.Trim() : null;
                            var dist = dtDealer.Columns.Contains("Distributor") ? row["Distributor"]?.ToString()?.Trim() : null;
                            var dealer = dtDealer.Columns.Contains("Dealer") ? row["Dealer"]?.ToString()?.Trim() : null;

                            if (string.IsNullOrWhiteSpace(serial))
                            {
                                continue;
                            }

                            var existing = await _repo.GetProductBySerialNoAsync(serial);
                            if (existing != null)
                            {
                                // Update Dealer only when Distributor matches (case-insensitive). If distributor in sheet is empty, treat as empty string.
                                var existingDist = existing.Distributor ?? string.Empty;
                                var sheetDist = dist ?? string.Empty;
                                if (string.Equals(existingDist.Trim(), sheetDist.Trim(), StringComparison.OrdinalIgnoreCase))
                                {
                                    existing.Dealer = dealer ?? string.Empty;
                                    existing.UpdatedAt = DateTime.UtcNow;
                                    await _repo.UpdateProductDetailsDealerColsAsync(existing);
                                    updated++;
                                }
                                else
                                {
                                    // distributor mismatch from Excel and Database
                                    distributorMismatchFromExcelAndDB.Add(serial);
                                }
                            }
                            else
                            {
                                missingSerials.Add(serial);
                            }
                        }
                        break;

                    case "4": // Installation - update Installation
                        {
                            var table = ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => string.Equals(t.TableName, "Installation", StringComparison.OrdinalIgnoreCase))
                                ?? ds.Tables.Cast<System.Data.DataTable>().FirstOrDefault(t => t.Columns.Contains("Serial No") && t.Columns.Contains("Installation") && t.Columns.Contains("Installation Date"));
                            if (table == null)
                                break;

                            foreach (System.Data.DataRow row in table.Rows)
                            {
                                var serial = table.Columns.Contains("Serial No") ? row["Serial No"]?.ToString()?.Trim() : null;
                                var inst = table.Columns.Contains("Installation") ? row["Installation"]?.ToString()?.Trim() : null;
                                var instDateObj = table.Columns.Contains("Installation Date") ? row["Installation Date"] : null;

                                var hasInstDateInSheet = instDateObj != null && instDateObj != DBNull.Value && !string.IsNullOrWhiteSpace(instDateObj.ToString());

                                if (string.IsNullOrWhiteSpace(serial))
                                {
                                    continue;
                                }

                                var existing = await _repo.GetProductBySerialNoAsync(serial);
                                if (existing != null)
                                {
                                    // If DB already has InstallationDate -> treat as Installation Already Done
                                    if (existing.InstallationDate.HasValue)
                                    {
                                        // Capture sheet values as well per request: Serial No, Installation, Installation Date
                                        string sheetDateStr = string.Empty;
                                        if (instDateObj != null && instDateObj != DBNull.Value)
                                        {
                                            if (instDateObj is DateTime dt2)
                                            {
                                                // format date for display (YYYY-MM-DD)
                                                sheetDateStr = dt2.ToString("yyyy-MM-dd");
                                            }
                                            else
                                            {
                                                var raw = instDateObj.ToString()?.Trim();
                                                if (!string.IsNullOrWhiteSpace(raw) && DateTime.TryParse(raw, out var parsedRaw))
                                                {
                                                    sheetDateStr = parsedRaw.ToString("yyyy-MM-dd");
                                                }
                                                else
                                                {
                                                    sheetDateStr = raw ?? string.Empty;
                                                }
                                            }
                                        }
                                        installationAlreadyDone.Add(new { Serial = serial, Installation = inst ?? string.Empty, InstallationDate = sheetDateStr });
                                    }
                                    else
                                    {
                                        // DB has no installation date yet
                                        if (hasInstDateInSheet)
                                        {
                                            // Sheet provides an installation date -> write it to DB
                                            DateTime? sheetDate = null;
                                            if (instDateObj is DateTime dt) sheetDate = dt;
                                            else
                                            {
                                                var instDateStr = instDateObj?.ToString()?.Trim();
                                                if (!string.IsNullOrWhiteSpace(instDateStr) && DateTime.TryParse(instDateStr, out var parsed)) sheetDate = parsed;
                                            }

                                            existing.IsUsed = true;
                                            existing.Installation = inst ?? string.Empty;
                                            existing.InstallationDate = sheetDate;
                                            existing.UpdatedAt = DateTime.UtcNow;
                                            await _repo.UpdateProductDetailsAsync(existing);
                                            updated++;
                                        }
                                        else
                                        {
                                            // Sheet's Installation Date is NULL -> clear InstallationDate in DB and set Installation value
                                            existing.IsUsed = true;
                                            existing.Installation = inst ?? string.Empty;
                                            existing.InstallationDate = null;
                                            existing.UpdatedAt = DateTime.UtcNow;
                                            await _repo.UpdateProductDetailsAsync(existing);
                                            updated++;
                                        }
                                    }
                                }
                                else
                                {
                                    missingSerials.Add(serial);
                                }
                            }
                        }
                        break;

                    default:
                        ModelState.AddModelError(string.Empty, "Unknown mapping selected.");
                        return View();
                }
            }

            // Custom success messages per mapping
            switch (mapping)
            {
                case "1": // Serial No
                    ViewBag.Message = $"Inserted: {processed}. Duplicates: {duplicates.Count}.";
                    break;
                case "2": // Distributor
                    ViewBag.Message = $"Updated: {updated}. Duplicates: {duplicates.Count}.";
                    break;
                case "3": // Dealer
                    ViewBag.Message = $"Updated: {updated}. Duplicates: {duplicates.Count}.";
                    break;
                case "4": // Installation
                    ViewBag.Message = $"Installed: {updated}. Duplicates: {duplicates.Count}. InstallationAlreadyDone: {installationAlreadyDone.Count}.";
                    break;
                default:
                    ViewBag.Message = mapping == "1"
                        ? $"Inserted: {processed}. Duplicates: {duplicates.Count}. MissingModels: {missingModels.Count}."
                        : $"Updated: {updated}. MissingSerials: {missingSerials.Count}.";
                    break;
            }
            ViewBag.Duplicates = duplicates;
            ViewBag.MissingModels = missingModels;
            ViewBag.MissingSerials = missingSerials;
            ViewBag.InstallationAlreadyDone = installationAlreadyDone;
            ViewBag.DistributorMismatchFromExcelAndDB = distributorMismatchFromExcelAndDB;
            ViewBag.Mapping = mapping;
            return View();
        }
    }
}
