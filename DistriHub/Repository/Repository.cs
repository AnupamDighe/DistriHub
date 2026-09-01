using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using DistriHub.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace DistriHub.Repository
{
    public class Repository : IRepository
    {
        private readonly string _connectionString;
        private readonly Microsoft.Extensions.Logging.ILogger<Repository> _logger;

        public Repository(IConfiguration configuration, Microsoft.Extensions.Logging.ILogger<Repository> logger)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
            _logger = logger;
        }

        public async Task<IEnumerable<SubCategory>> GetSubCategoriesByCategoryIdAsync(int categoryId)
        {
            var list = new List<SubCategory>();
            const string sql = "SELECT SubCategoryId, CategoryId, SubCategoryName, CreatedAt, UpdatedAt FROM [dbo].[SubCategory] WHERE CategoryId = @CategoryId ORDER BY SubCategoryName;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new SubCategory
                {
                    SubCategoryId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryId = row.IsNull(1) ? 0 : row.Field<int>(1),
                    SubCategoryName = row.IsNull(2) ? null : row.Field<string>(2),
                    CreatedAt = row.IsNull(3) ? default : row.Field<DateTime>(3),
                    UpdatedAt = row.IsNull(4) ? (DateTime?)null : row.Field<DateTime>(4)
                });
            }

            return list;
        }

        public async Task<int> GetProductDetailsCountAsync()
        {
            const string sql = "SELECT COUNT(1) FROM [dbo].[ProductDetails];";
            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<int> GetProductDetailsFilteredCountAsync(string? serialFilter)
        {
            string sql;
            if (string.IsNullOrWhiteSpace(serialFilter))
            {
                sql = "SELECT COUNT(1) FROM [dbo].[ProductDetails];";
                await using var conn0 = new SqlConnection(_connectionString);
                await using var cmd0 = new SqlCommand(sql, conn0);
                await conn0.OpenAsync();
                var res0 = await cmd0.ExecuteScalarAsync();
                return Convert.ToInt32(res0);
            }

            sql = "SELECT COUNT(1) FROM [dbo].[ProductDetails] WHERE LOWER(SerialNo) LIKE '%' + LOWER(@Serial) + '%';";
            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Serial", SqlDbType.NVarChar, 100).Value = serialFilter.Trim();
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<IEnumerable<Models.ProductDetails>> GetProductDetailsPagedAsync(string? serialFilter, int start, int length, string sortColumn, string sortDir)
        {
            var list = new List<Models.ProductDetails>();

            // Validate sort column and direction to prevent SQL injection
            var allowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "SerialNo", "CategoryName", "SubCategoryName", "ModelName", "UploadDate", "IsUsed", "Finance", "Distributor", "FinanceDate", "Dealer", "Installation"
            };
            if (!allowedColumns.Contains(sortColumn))
                sortColumn = "UploadDate";
            sortDir = string.Equals(sortDir, "asc", StringComparison.OrdinalIgnoreCase) ? "ASC" : "DESC";

            // Handle length -1 (DataTables uses -1 to indicate all records)
            if (length <= 0) length = int.MaxValue;

            string where = string.IsNullOrWhiteSpace(serialFilter)
                ? string.Empty
                : "WHERE LOWER(SerialNo) LIKE '%' + LOWER(@Serial) + '%'";

            string sql = $"SELECT a.ProductId, c.CategoryName, e.SubCategoryName, b.ModelName, a.SerialNo, a.UploadDate, CASE WHEN a.IsUsed=1 THEN 'Yes' ELSE 'No' END AS IsUsed, a.Finance, a.Distributor, a.FinanceDate, a.Dealer, a.Installation, a.InstallationDate, a.CreatedAt, a.UpdatedAt FROM [dbo].[ProductDetails] a LEFT JOIN [Model] b ON a.ModelId=b.ModelId LEFT JOIN Category c ON a.CategoryId=c.CategoryId LEFT JOIN SubCategory e ON a.SubCategoryId=e.SubCategoryId {where} ORDER BY {sortColumn} {sortDir} OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            if (!string.IsNullOrWhiteSpace(serialFilter))
                cmd.Parameters.Add("@Serial", SqlDbType.NVarChar, 100).Value = serialFilter.Trim();
            cmd.Parameters.Add("@Start", SqlDbType.Int).Value = start;
            cmd.Parameters.Add("@Length", SqlDbType.Int).Value = length;

            await conn.OpenAsync();
            // Use DataTable to load results instead of a DataReader
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);

            foreach (DataRow row in dt.Rows)
            {
                list.Add(new Models.ProductDetails
                {
                    ProductId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryName = row.IsNull(1) ? null : row.Field<string>(1),
                    SubCategoryName = row.IsNull(2) ? null : row.Field<string>(2),
                    ModelName = row.IsNull(3) ? null : row.Field<string>(3),
                    SerialNo = row.IsNull(4) ? null : row.Field<string>(4),
                    UploadDate = row.IsNull(5) ? default : row.Field<DateTime>(5),
                    IsUsedDisplay = row.IsNull(6) ? null : row.Field<string>(6),
                    IsUsed = row.IsNull(6) ? false : string.Equals(row.Field<string>(6), "Yes", StringComparison.OrdinalIgnoreCase),
                    Finance = row.IsNull(7) ? null : row.Field<string>(7),
                    Distributor = row.IsNull(8) ? null : row.Field<string>(8),
                    FinanceDate = row.IsNull(9) ? (DateTime?)null : row.Field<DateTime>(9),
                    Dealer = row.IsNull(10) ? null : row.Field<string>(10),
                    Installation = row.IsNull(11) ? null : row.Field<string>(11),
                    InstallationDate = row.IsNull(12) ? (DateTime?)null : row.Field<DateTime>(12),
                    CreatedAt = row.IsNull(13) ? default : row.Field<DateTime>(13),
                    UpdatedAt = row.IsNull(14) ? (DateTime?)null : row.Field<DateTime>(14)
                });
            }

            return list;
        }

        public async Task<int> AddModelAsync(Models.Model model)
        {
            const string sql = @"INSERT INTO [dbo].[Model] (CategoryId, SubCategoryId, ModelName, CreatedAt, UpdatedAt)
            VALUES (@CategoryId, @SubCategoryId, @ModelName, @CreatedAt, @UpdatedAt);
            SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = model.CategoryId;
            cmd.Parameters.Add("@SubCategoryId", SqlDbType.Int).Value = model.SubCategoryId;
            cmd.Parameters.Add("@ModelName", SqlDbType.NVarChar, 100).Value = model.ModelName;
            cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = model.CreatedAt;
            cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = (object?)model.UpdatedAt ?? DBNull.Value;

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<Models.Model?> GetModelByNameAndSubCategoryAsync(string name, int subCategoryId)
        {
            const string sql = "SELECT ModelId, CategoryId, SubCategoryId, ModelName, CreatedAt, UpdatedAt FROM [dbo].[Model] WHERE SubCategoryId = @SubCategoryId AND LOWER(ModelName) = LOWER(@Name);";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@SubCategoryId", SqlDbType.Int).Value = subCategoryId;
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                return new Models.Model
                {
                    ModelId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryId = row.IsNull(1) ? 0 : row.Field<int>(1),
                    SubCategoryId = row.IsNull(2) ? 0 : row.Field<int>(2),
                    ModelName = row.IsNull(3) ? null : row.Field<string>(3),
                    CreatedAt = row.IsNull(4) ? default : row.Field<DateTime>(4),
                    UpdatedAt = row.IsNull(5) ? (DateTime?)null : row.Field<DateTime>(5)
                };
            }

            return null;
        }

        // Serial number validation - returns status codes as described in controller spec
        // 0 = Valid Serial No (and marks IsUsed = true)
        // -1 = Invalid Serial Number
        // -2 = Mismatch in model and serial number
        // -3 = Serial Number Already Validated
        // -4 = Invalid Material code
        // -5 = Invalid Access Code
        public async Task<int> ValidateSerialAsync(string materialCode, string serialNumber, string source)
        {
            // source is the username extracted from the authenticated JWT token; ensure it exists in UserDetails
            if (!await IsSourceValidAsync(source))
                return -5;

            if (!IsMaterialCodeValid(materialCode))
                return -4;

            if (!IsSerialNumberPatternValid(serialNumber))
                return -1;

            if (!DoesModelMatchSerial(materialCode, serialNumber))
                return -2;

            var prod = await GetProductBySerialNoAsync(serialNumber);
            if (prod == null)
                return -1;

            if (prod.IsUsed)
                return -3;

            prod.IsUsed = true;
            prod.UpdatedAt = DateTime.UtcNow;
            await UpdateProductDetailsAsync(prod);
            return 0;
        }

        public async Task<int> UnfreezeSerialAsync(string materialCode, string serialNumber, string source)
        {
            if (!await IsSourceValidAsync(source))
                return -5;

            if (!IsMaterialCodeValid(materialCode))
                return -4;

            if (!IsSerialNumberPatternValid(serialNumber))
                return -1;

            if (!DoesModelMatchSerial(materialCode, serialNumber))
                return -2;

            var prod = await GetProductBySerialNoAsync(serialNumber);
            if (prod == null)
                return -1;

            if (!prod.IsUsed)
                return -1;

            prod.IsUsed = false;
            prod.UpdatedAt = DateTime.UtcNow;
            await UpdateProductDetailsAsync(prod);
            return 0;
        }

        #region Serial validation helpers
        private async Task<bool> IsSourceValidAsync(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
                return false;

            var pwd = await GetPasswordByUsernameAsync(source.Trim());
            return pwd != null;
        }

        public async Task<string?> GetPasswordByUsernameAsync(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return null;

            const string sql = "SELECT Password FROM dbo.Users WHERE Username = @Username";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 200).Value = username;
            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToString(result);
        }

        public async Task SetRefreshTokenAsync(string username, string? refreshToken, DateTime expiry)
        {
            const string sql = "UPDATE dbo.UserDetails SET RefreshToken = @RefreshToken, RefreshTokenExpiry = @Expiry WHERE Username = @Username";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@RefreshToken", SqlDbType.NVarChar, 200).Value = (object?)refreshToken ?? DBNull.Value;
            cmd.Parameters.Add("@Expiry", SqlDbType.DateTime2).Value = expiry;
            cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 200).Value = username;
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<(string? RefreshToken, DateTime? Expiry)> GetRefreshTokenAsync(string username)
        {
            const string sql = "SELECT RefreshToken, RefreshTokenExpiry FROM dbo.UserDetails WHERE Username = @Username";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Username", SqlDbType.NVarChar, 200).Value = username;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                var token = row.IsNull(0) ? null : row.Field<string>(0);
                var expiry = row.IsNull(1) ? (DateTime?)null : row.Field<DateTime>(1);
                return (token, expiry);
            }

            return (null, null);
        }

        private static bool IsMaterialCodeValid(string materialCode)
        {
            if (string.IsNullOrWhiteSpace(materialCode))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(materialCode.Trim(), "^\\(M\\d+\\).+");
        }

        private static bool IsSerialNumberPatternValid(string serial)
        {
            if (string.IsNullOrWhiteSpace(serial))
                return false;

            return System.Text.RegularExpressions.Regex.IsMatch(serial.Trim(), "^M\\d+Y\\d+$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        private bool DoesModelMatchSerial(string materialCode, string serial)
        {
            try
            {
                var matMatch = System.Text.RegularExpressions.Regex.Match(materialCode ?? string.Empty, "\\(M(\\d+)\\)");
                var serMatch = System.Text.RegularExpressions.Regex.Match(serial ?? string.Empty, "^M(\\d+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

                if (!matMatch.Success || !serMatch.Success)
                    return false;

                var matDigits = matMatch.Groups[1].Value;
                var serDigits = serMatch.Groups[1].Value;

                return string.Equals(matDigits, serDigits, StringComparison.Ordinal);
            }
            catch (Exception ex)
            {
                // If any unexpected error occurs while validating pattern, treat as mismatch.
                // Log the exception for diagnostics.
                try { _logger?.LogError(ex, "Error while validating serial/model pattern for material '{MaterialCode}' serial '{Serial}'", materialCode, serial); } catch { }
                return false;
            }
        }

        #endregion

        public async Task<Models.Model?> GetModelByIdAsync(int id)
        {
            const string sql = "SELECT ModelId, CategoryId, SubCategoryId, ModelName, CreatedAt, UpdatedAt FROM [dbo].[Model] WHERE ModelId = @Id;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                return new Models.Model
                {
                    ModelId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryId = row.IsNull(1) ? 0 : row.Field<int>(1),
                    SubCategoryId = row.IsNull(2) ? 0 : row.Field<int>(2),
                    ModelName = row.IsNull(3) ? null : row.Field<string>(3),
                    CreatedAt = row.IsNull(4) ? default : row.Field<DateTime>(4),
                    UpdatedAt = row.IsNull(5) ? (DateTime?)null : row.Field<DateTime>(5)
                };
            }

            return null;
        }

        public async Task<Models.Model?> GetModelByNameAsync(string name)
        {
            const string sql = "SELECT ModelId, CategoryId, SubCategoryId, ModelName, CreatedAt, UpdatedAt FROM [dbo].[Model] WHERE LOWER(ModelName) = LOWER(@Name);";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Models.Model
                {
                    ModelId = reader.GetInt32(0),
                    CategoryId = reader.GetInt32(1),
                    SubCategoryId = reader.GetInt32(2),
                    ModelName = reader.GetString(3),
                    CreatedAt = reader.GetDateTime(4),
                    UpdatedAt = reader.IsDBNull(5) ? (DateTime?)null : reader.GetDateTime(5)
                };
            }

            return null;
        }

        public async Task<Models.ProductDetails?> GetProductBySerialNoAsync(string serialNo)
        {
            const string sql = "SELECT ProductId, CategoryId, SubCategoryId, ModelId, SerialNo, UploadDate, IsUsed, Finance, Distributor, FinanceDate, Dealer, Installation, InstallationDate, CreatedAt, UpdatedAt FROM [dbo].[ProductDetails] WHERE LOWER(SerialNo) = LOWER(@SerialNo);";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@SerialNo", SqlDbType.NVarChar, 100).Value = serialNo;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                return new Models.ProductDetails
                {
                    ProductId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryId = row.IsNull(1) ? 0 : row.Field<int>(1),
                    SubCategoryId = row.IsNull(2) ? 0 : row.Field<int>(2),
                    ModelId = row.IsNull(3) ? 0 : row.Field<int>(3),
                    SerialNo = row.IsNull(4) ? null : row.Field<string>(4),
                    UploadDate = row.IsNull(5) ? default : row.Field<DateTime>(5),
                    IsUsed = row.IsNull(6) ? false : row.Field<bool>(6),
                    Finance = row.IsNull(7) ? null : row.Field<string>(7),
                    Distributor = row.IsNull(8) ? null : row.Field<string>(8),
                    FinanceDate = row.IsNull(9) ? (DateTime?)null : row.Field<DateTime>(9),
                    Dealer = row.IsNull(10) ? null : row.Field<string>(10),
                    Installation = row.IsNull(11) ? null : row.Field<string>(11),
                    InstallationDate = row.IsNull(12) ? (DateTime?)null : row.Field<DateTime>(12),
                    CreatedAt = row.IsNull(13) ? default : row.Field<DateTime>(13),
                    UpdatedAt = row.IsNull(14) ? (DateTime?)null : row.Field<DateTime>(14)
                };
            }

            return null;
        }

        public async Task<int> InsertProductDetailsAsync(Models.ProductDetails product)
        {
            const string sql = @"INSERT INTO [dbo].[ProductDetails] (CategoryId, SubCategoryId, ModelId, SerialNo, UploadDate, IsUsed, Finance, Distributor, FinanceDate, Dealer, Installation, InstallationDate, CreatedAt, UpdatedAt)
            VALUES (@CategoryId, @SubCategoryId, @ModelId, @SerialNo, @UploadDate, @IsUsed, @Finance, @Distributor, @FinanceDate, @Dealer, @Installation, @InstallationDate, @CreatedAt, @UpdatedAt);
            SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = product.CategoryId;
            cmd.Parameters.Add("@SubCategoryId", SqlDbType.Int).Value = product.SubCategoryId;
            cmd.Parameters.Add("@ModelId", SqlDbType.Int).Value = product.ModelId;
            cmd.Parameters.Add("@SerialNo", SqlDbType.NVarChar, 100).Value = product.SerialNo;
            cmd.Parameters.Add("@UploadDate", SqlDbType.DateTime2).Value = product.UploadDate;
            cmd.Parameters.Add("@IsUsed", SqlDbType.Bit).Value = product.IsUsed;
            cmd.Parameters.Add("@Finance", SqlDbType.NVarChar, 100).Value = (object?)product.Finance ?? DBNull.Value;
            cmd.Parameters.Add("@Distributor", SqlDbType.NVarChar, 100).Value = (object?)product.Distributor ?? DBNull.Value;
            cmd.Parameters.Add("@FinanceDate", SqlDbType.DateTime2).Value = (object?)product.FinanceDate ?? DBNull.Value;
            cmd.Parameters.Add("@Dealer", SqlDbType.NVarChar, 100).Value = (object?)product.Dealer ?? DBNull.Value;
            cmd.Parameters.Add("@Installation", SqlDbType.NVarChar, 100).Value = (object?)product.Installation ?? DBNull.Value;
            cmd.Parameters.Add("@InstallationDate", SqlDbType.DateTime2).Value = (object?)product.InstallationDate ?? DBNull.Value;
            cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = product.CreatedAt;
            cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = (object?)product.UpdatedAt ?? DBNull.Value;


            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task UpdateProductDetailsAsync(Models.ProductDetails product)
        {
            const string sql = @"UPDATE [dbo].[ProductDetails]
            SET IsUsed=@IsUsed,
                Installation = @Installation,
                InstallationDate=@InstallationDate,
                UpdatedAt = @UpdatedAt
            WHERE ProductId = @ProductId;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@IsUsed", SqlDbType.Bit).Value = product.IsUsed;
            cmd.Parameters.Add("@Installation", SqlDbType.NVarChar, 100).Value = (object?)product.Installation ?? DBNull.Value;
            cmd.Parameters.Add("@InstallationDate", SqlDbType.DateTime2).Value = (object?)product.InstallationDate ?? DBNull.Value;
            cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = product.UpdatedAt ?? DateTime.UtcNow;
            cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = product.ProductId;

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateProductDetailsDistributorsColsAsync(Models.ProductDetails product)
        {
            const string sql = @"UPDATE [dbo].[ProductDetails]
            SET Distributor = @Distributor,
                UpdatedAt = @UpdatedAt
            WHERE ProductId = @ProductId;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Distributor", SqlDbType.NVarChar, 100).Value = (object?)product.Distributor ?? DBNull.Value;
            cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = product.UpdatedAt ?? DateTime.UtcNow;
            cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = product.ProductId;

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateProductDetailsDealerColsAsync(Models.ProductDetails product)
        {
            const string sql = @"UPDATE [dbo].[ProductDetails]
            SET Dealer = @Dealer,
                UpdatedAt = @UpdatedAt
            WHERE ProductId = @ProductId;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Dealer", SqlDbType.NVarChar, 100).Value = (object?)product.Dealer ?? DBNull.Value;
            cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = product.UpdatedAt ?? DateTime.UtcNow;
            cmd.Parameters.Add("@ProductId", SqlDbType.Int).Value = product.ProductId;

            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<int> AddSubCategoryAsync(SubCategory subCategory)
        {
            const string sql = @"INSERT INTO [dbo].[SubCategory] (CategoryId, SubCategoryName, CreatedAt, UpdatedAt)
            VALUES (@CategoryId, @SubCategoryName, @CreatedAt, @UpdatedAt);
            SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = subCategory.CategoryId;
            cmd.Parameters.Add("@SubCategoryName", SqlDbType.NVarChar, 100).Value = subCategory.SubCategoryName;
            cmd.Parameters.Add("@CreatedAt", SqlDbType.DateTime2).Value = subCategory.CreatedAt;
            cmd.Parameters.Add("@UpdatedAt", SqlDbType.DateTime2).Value = (object?)subCategory.UpdatedAt ?? DBNull.Value;

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<SubCategory?> GetSubCategoryByNameAndCategoryAsync(string name, int categoryId)
        {
            const string sql = "SELECT SubCategoryId, CategoryId, SubCategoryName, CreatedAt, UpdatedAt FROM [dbo].[SubCategory] WHERE CategoryId = @CategoryId AND LOWER(SubCategoryName) = LOWER(@Name);";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@CategoryId", SqlDbType.Int).Value = categoryId;
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            if (dt.Rows.Count > 0)
            {
                var row = dt.Rows[0];
                return new SubCategory
                {
                    SubCategoryId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryId = row.IsNull(1) ? 0 : row.Field<int>(1),
                    SubCategoryName = row.IsNull(2) ? null : row.Field<string>(2),
                    CreatedAt = row.IsNull(3) ? default : row.Field<DateTime>(3),
                    UpdatedAt = row.IsNull(4) ? (DateTime?)null : row.Field<DateTime>(4)
                };
            }

            return null;
        }

        public async Task<int> AddCategoryAsync(Category category)
        {
            const string sql = @"INSERT INTO [dbo].[Category] (CategoryName)
            VALUES (@CategoryName);
            SELECT SCOPE_IDENTITY();";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);

            cmd.Parameters.Add("@CategoryName", SqlDbType.NVarChar, 100).Value = category.CategoryName;

            await conn.OpenAsync();
            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(result);
        }

        public async Task<IEnumerable<Category>> GetAllCategoriesAsync()
        {
            var list = new List<Category>();
            const string sql = "SELECT CategoryId, CategoryName, CreatedAt, UpdatedAt FROM [dbo].[Category] ORDER BY CategoryName;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt = new DataTable();
            dt.Load(reader);
            foreach (DataRow row in dt.Rows)
            {
                list.Add(new Category
                {
                    CategoryId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryName = row.IsNull(1) ? null : row.Field<string>(1),
                    CreatedAt = row.IsNull(2) ? default : row.Field<DateTime>(2),
                    UpdatedAt = row.IsNull(3) ? (DateTime?)null : row.Field<DateTime>(3)
                });
            }

            return list;
        }

        public async Task<Category?> GetCategoryByIdAsync(int id)
        {
            const string sql = "SELECT CategoryId, CategoryName, CreatedAt, UpdatedAt FROM [dbo].[Category] WHERE CategoryId = @Id;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Id", SqlDbType.Int).Value = id;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt2 = new DataTable();
            dt2.Load(reader);
            if (dt2.Rows.Count > 0)
            {
                var row = dt2.Rows[0];
                return new Category
                {
                    CategoryId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryName = row.IsNull(1) ? null : row.Field<string>(1),
                    CreatedAt = row.IsNull(2) ? default : row.Field<DateTime>(2),
                    UpdatedAt = row.IsNull(3) ? (DateTime?)null : row.Field<DateTime>(3)
                };
            }

            return null;
        }

        public async Task<Category?> GetCategoryByNameAsync(string name)
        {
            // Use case-insensitive comparison by lowercasing both sides to avoid collation issues
            const string sql = "SELECT CategoryId, CategoryName, CreatedAt, UpdatedAt FROM [dbo].[Category] WHERE LOWER(CategoryName) = LOWER(@Name);";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = name;
            await conn.OpenAsync();
            await using var reader = await cmd.ExecuteReaderAsync();
            var dt3 = new DataTable();
            dt3.Load(reader);
            if (dt3.Rows.Count > 0)
            {
                var row = dt3.Rows[0];
                return new Category
                {
                    CategoryId = row.IsNull(0) ? 0 : row.Field<int>(0),
                    CategoryName = row.IsNull(1) ? null : row.Field<string>(1),
                    CreatedAt = row.IsNull(2) ? default : row.Field<DateTime>(2),
                    UpdatedAt = row.IsNull(3) ? (DateTime?)null : row.Field<DateTime>(3)
                };
            }

            return null;
        }

        public async Task<IEnumerable<Models.ProductDetails>> GetProductDetailsAsync()
        {
            var list = new List<Models.ProductDetails>();
            string sql = "SELECT a.ProductId, c.CategoryName, e.SubCategoryName, b.ModelName, a.SerialNo, a.UploadDate, CASE WHEN a.IsUsed=1 THEN 'Yes' ELSE 'No' END AS IsUsed, a.Finance, a.Distributor, a.FinanceDate, a.Dealer, a.Installation, a.InstallationDate, a.CreatedAt, a.UpdatedAt FROM [dbo].[ProductDetails] a LEFT JOIN [Model] b ON a.ModelId=b.ModelId LEFT JOIN Category c ON a.CategoryId=c.CategoryId LEFT JOIN SubCategory e ON a.SubCategoryId=e.SubCategoryId;";

            await using var conn = new SqlConnection(_connectionString);
            await using var cmd = new SqlCommand(sql, conn);
            await conn.OpenAsync();

            // Load results into a DataTable for simpler column-based access
            var table = new DataTable();
            await using (var reader = await cmd.ExecuteReaderAsync())
            {
                table.Load(reader);
            }

            foreach (DataRow row in table.Rows)
            {
                // Use column names to map correctly to the model's display properties
                var isUsedDisplay = row.IsNull("IsUsed") ? null : row.Field<string>("IsUsed");

                list.Add(new Models.ProductDetails
                {
                    ProductId = row.Field<int>("ProductId"),
                    CategoryName = row.IsNull("CategoryName") ? null : row.Field<string>("CategoryName"),
                    SubCategoryName = row.IsNull("SubCategoryName") ? null : row.Field<string>("SubCategoryName"),
                    ModelName = row.IsNull("ModelName") ? null : row.Field<string>("ModelName"),
                    SerialNo = row.IsNull("SerialNo") ? null : row.Field<string>("SerialNo"),
                    UploadDate = row.Field<DateTime>("UploadDate"),
                    IsUsedDisplay = isUsedDisplay,
                    IsUsed = !string.IsNullOrEmpty(isUsedDisplay) && string.Equals(isUsedDisplay, "Yes", StringComparison.OrdinalIgnoreCase),
                    Finance = row.IsNull("Finance") ? null : row.Field<string>("Finance"),
                    Distributor = row.IsNull("Distributor") ? null : row.Field<string>("Distributor"),
                    FinanceDate = row.IsNull("FinanceDate") ? (DateTime?)null : row.Field<DateTime>("FinanceDate"),
                    Dealer = row.IsNull("Dealer") ? null : row.Field<string>("Dealer"),
                    Installation = row.IsNull("Installation") ? null : row.Field<string>("Installation"),
                    InstallationDate = row.IsNull("InstallationDate") ? (DateTime?)null : row.Field<DateTime>("InstallationDate"),
                    CreatedAt = row.Field<DateTime>("CreatedAt"),
                    UpdatedAt = row.IsNull("UpdatedAt") ? (DateTime?)null : row.Field<DateTime>("UpdatedAt")
                });
            }

            return list;
        }
    }
}
