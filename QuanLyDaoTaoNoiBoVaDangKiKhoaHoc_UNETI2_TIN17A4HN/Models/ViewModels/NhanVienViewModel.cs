//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using QuanLyDaoTaoNoiBo.Models.Entities;

namespace QuanLyDaoTaoNoiBo.Models.ViewModels;

/// <summary>
/// ViewModel dùng cho form Thêm, Sửa và hiển thị Chi tiết hồ sơ nhân viên.
/// </summary>
public class NhanVienViewModel : IValidatableObject
{
    public int MaNhanVien { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn tài khoản liên kết."), Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn tài khoản hợp lệ.")]
    [Display(Name = "Tài khoản liên kết")]
    public int MaTaiKhoan { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ và tên nhân viên."), StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn phòng ban."), Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng ban hợp lệ.")]
    [Display(Name = "Phòng ban")]
    public int MaPhongBan { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn ngày sinh."), DataType(DataType.Date)]
    [Display(Name = "Ngày sinh")]
    public DateOnly NgaySinh { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự."), Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
    [Display(Name = "Số điện thoại")]
    public string? SoDienThoai { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập email."), StringLength(254, ErrorMessage = "Email tối đa 254 ký tự."), EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    [Display(Name = "Email nhân viên")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn ngày vào làm."), DataType(DataType.Date)]
    [Display(Name = "Ngày vào làm")]
    public DateOnly NgayVaoLam { get; set; }

    [Display(Name = "Trạng thái hoạt động")]
    public bool TrangThai { get; set; } = true;

    // Các thuộc tính chỉ hiển thị thông tin kèm theo
    [ValidateNever]
    public string TenDangNhap { get; set; } = string.Empty;

    [ValidateNever]
    public string VaiTroTaiKhoan { get; set; } = string.Empty;

    [ValidateNever]
    public string TenPhongBan { get; set; } = string.Empty;

    [ValidateNever]
    public int SoKhoaDaDangKy { get; set; }

    [ValidateNever]
    public int SoBuoiDaDiemDanh { get; set; }

    // Danh sách chọn tài khoản và phòng ban
    [ValidateNever]
    public IEnumerable<SelectListItem> TaiKhoans { get; set; } = [];

    [ValidateNever]
    public IEnumerable<SelectListItem> PhongBans { get; set; } = [];

    public string TenTrangThai => TrangThai ? "Đang làm việc" : "Ngừng hoạt động";
    public string TrangThaiCss => TrangThai ? "status-on" : "status-off";

    /// <summary>
    /// Kiểm tra nghiệp vụ ngày sinh và ngày vào làm theo quy định đề bài:
    /// - Ngày vào làm không sau hôm nay (theo giờ VN).
    /// - Ngày vào làm không được trước ngày sinh.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var homNay = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        if (NgaySinh == default)
        {
            yield return new ValidationResult("Vui lòng nhập ngày sinh hợp lệ.", [nameof(NgaySinh)]);
        }
        else if (NgaySinh > homNay)
        {
            yield return new ValidationResult("Ngày sinh không được ở tương lai.", [nameof(NgaySinh)]);
        }

        if (NgayVaoLam == default)
        {
            yield return new ValidationResult("Vui lòng nhập ngày vào làm hợp lệ.", [nameof(NgayVaoLam)]);
        }
        else
        {
            if (NgayVaoLam > homNay)
            {
                yield return new ValidationResult("Ngày vào làm không được sau ngày hiện tại (giờ Việt Nam).", [nameof(NgayVaoLam)]);
            }

            if (NgaySinh != default && NgayVaoLam < NgaySinh)
            {
                yield return new ValidationResult("Ngày vào làm không được trước ngày sinh.", [nameof(NgayVaoLam)]);
            }
        }
    }
}

/// <summary>
/// ViewModel cho màn hình Danh sách nhân viên (tìm kiếm, lọc, phân trang).
/// </summary>
public class NhanVienListViewModel
{
    public List<NhanVien> NhanViens { get; set; } = [];

    public string? SearchTerm { get; set; }
    public int? MaPhongBan { get; set; }
    public string? TrangThai { get; set; }
    public string SortOrder { get; set; } = "name_asc";

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public IEnumerable<SelectListItem> PhongBans { get; set; } = [];

    public static IReadOnlyDictionary<string, string> SortOptions { get; } = new Dictionary<string, string>
    {
        ["name_asc"] = "Họ tên (A → Z)",
        ["name_desc"] = "Họ tên (Z → A)",
        ["date_desc"] = "Ngày vào làm (Mới nhất)",
        ["date_asc"] = "Ngày vào làm (Cũ nhất)"
    };

    public Dictionary<string, string?> RouteForPage(int page) => new()
    {
        ["searchTerm"] = SearchTerm,
        ["maPhongBan"] = MaPhongBan?.ToString(),
        ["trangThai"] = TrangThai,
        ["sortOrder"] = SortOrder,
        ["pageIndex"] = page.ToString()
    };
}
