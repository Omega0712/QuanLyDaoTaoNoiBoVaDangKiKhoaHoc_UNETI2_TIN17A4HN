//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace QuanLyDaoTaoNoiBo.Models.ViewModels;

/// <summary>
/// Model hiển thị thông tin 1 lớp học kèm trạng thái đăng ký của nhân viên hiện tại.
/// </summary>
public class LopDangKyItemViewModel
{
    public int MaLop { get; set; }
    public string TenLop { get; set; } = string.Empty;
    public string TenKhoaDaoTao { get; set; } = string.Empty;
    public string TenLinhVuc { get; set; } = string.Empty;
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public DateTime HanDangKy { get; set; }
    public string DiaDiem { get; set; } = string.Empty;
    public string TenGiangVien { get; set; } = string.Empty;
    public int SoLuongToiDa { get; set; }
    public int SoDaDuyet { get; set; }
    public int SoChoCon => SoLuongToiDa - SoDaDuyet;
    public string TrangThaiLop { get; set; } = string.Empty;

    // Trạng thái đăng ký của nhân viên hiện tại đối với lớp này
    public bool DaTungDangKy { get; set; }
    public int? MaDangKy { get; set; }
    public string? TrangThaiDangKy { get; set; }
    public bool CoTheDangKy { get; set; }
    public string? LyDoKhongTheDangKy { get; set; }

    public static IReadOnlyDictionary<string, string> TrangThaiDangKyDict { get; } = new Dictionary<string, string>
    {
        ["ChoDuyet"] = "Chờ duyệt",
        ["DaDuyet"] = "Đã duyệt",
        ["TuChoi"] = "Từ chối",
        ["DaHuy"] = "Đã hủy"
    };

    public string TenTrangThaiDangKy => TrangThaiDangKy != null && TrangThaiDangKyDict.TryGetValue(TrangThaiDangKy, out var val)
        ? val : "Chưa đăng ký";

    public string TrangThaiDangKyCss => TrangThaiDangKy switch
    {
        "DaDuyet" => "status-on",
        "ChoDuyet" => "status-warn",
        "TuChoi" => "status-off",
        "DaHuy" => "status-muted",
        _ => "status-muted"
    };
}

/// <summary>
/// ViewModel cho màn hình Nhân viên duyệt tìm và đăng ký lớp đào tạo (/DangKyDaoTao/Index).
/// </summary>
public class DangKyDaoTaoListViewModel
{
    public List<LopDangKyItemViewModel> Lops { get; set; } = [];

    public string? SearchTerm { get; set; }
    public int? MaLinhVuc { get; set; }
    public string? TinhTrangDangKy { get; set; } // "chuadk", "dadk", null

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public IEnumerable<SelectListItem> LinhVucs { get; set; } = [];

    public bool ChuaCoHoSoNhanVien { get; set; }
    public bool NhanVienNgungHoatDong { get; set; }

    public Dictionary<string, string?> RouteForPage(int page) => new()
    {
        ["searchTerm"] = SearchTerm,
        ["maLinhVuc"] = MaLinhVuc?.ToString(),
        ["tinhTrangDangKy"] = TinhTrangDangKy,
        ["pageIndex"] = page.ToString()
    };
}

/// <summary>
/// Model hiển thị từng dòng trong Lịch sử đăng ký của nhân viên.
/// </summary>
public class LichSuDangKyItemViewModel
{
    public int MaDangKy { get; set; }
    public int MaLop { get; set; }
    public string TenLop { get; set; } = string.Empty;
    public string TenKhoaDaoTao { get; set; } = string.Empty;
    public string TenLinhVuc { get; set; } = string.Empty;
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public string DiaDiem { get; set; } = string.Empty;
    public DateTime NgayDangKy { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime? NgayDuyet { get; set; }
    public string? GhiChu { get; set; }

    public static IReadOnlyDictionary<string, string> TrangThais { get; } = new Dictionary<string, string>
    {
        ["ChoDuyet"] = "Chờ duyệt",
        ["DaDuyet"] = "Đã duyệt",
        ["TuChoi"] = "Từ chối",
        ["DaHuy"] = "Đã hủy"
    };

    public string TenTrangThai => TrangThais.GetValueOrDefault(TrangThai, TrangThai);

    public string TrangThaiCss => TrangThai switch
    {
        "DaDuyet" => "status-on",
        "ChoDuyet" => "status-warn",
        "TuChoi" => "status-off",
        "DaHuy" => "status-muted",
        _ => "status-muted"
    };

    // Điều kiện hủy: Chỉ hủy khi trạng thái là Chờ duyệt hoặc Đã duyệt VÀ trước ngày bắt đầu lớp.
    public bool CoTheHuy { get; set; }
    public string? LyDoKhongTheHuy { get; set; }
}

/// <summary>
/// ViewModel cho màn hình Lịch sử đăng ký của nhân viên (/DangKyDaoTao/LichSuDangKy).
/// </summary>
public class LichSuDangKyListViewModel
{
    public List<LichSuDangKyItemViewModel> DanhSach { get; set; } = [];
    public bool ChuaCoHoSoNhanVien { get; set; }
    public string? SearchTerm { get; set; }
    public string? TrangThai { get; set; }
}

/// <summary>
/// Model hiển thị thông tin đăng ký cho Admin/HR duyệt (/DangKyDaoTao/DuyetDangKy).
/// </summary>
public class DuyetDangKyItemViewModel
{
    public int MaDangKy { get; set; }
    public int MaNhanVien { get; set; }
    public string HoTenNhanVien { get; set; } = string.Empty;
    public string EmailNhanVien { get; set; } = string.Empty;
    public string TenPhongBan { get; set; } = string.Empty;

    public int MaLop { get; set; }
    public string TenLop { get; set; } = string.Empty;
    public string TenKhoaDaoTao { get; set; } = string.Empty;
    public DateOnly NgayBatDau { get; set; }
    public DateOnly NgayKetThuc { get; set; }
    public int SoLuongToiDa { get; set; }
    public int SoDaDuyet { get; set; }
    public int SoChoCon => SoLuongToiDa - SoDaDuyet;
    public string TrangThaiLop { get; set; } = string.Empty;

    public DateTime NgayDangKy { get; set; }
    public string TrangThai { get; set; } = string.Empty;
    public DateTime? NgayDuyet { get; set; }
    public string? GhiChu { get; set; }

    public static IReadOnlyDictionary<string, string> TrangThais { get; } = new Dictionary<string, string>
    {
        ["ChoDuyet"] = "Chờ duyệt",
        ["DaDuyet"] = "Đã duyệt",
        ["TuChoi"] = "Từ chối",
        ["DaHuy"] = "Đã hủy"
    };

    public string TenTrangThai => TrangThais.GetValueOrDefault(TrangThai, TrangThai);

    public string TrangThaiCss => TrangThai switch
    {
        "DaDuyet" => "status-on",
        "ChoDuyet" => "status-warn",
        "TuChoi" => "status-off",
        "DaHuy" => "status-muted",
        _ => "status-muted"
    };

    // Chỉ bản ghi Chờ duyệt mới hiển thị nút Duyệt/Từ chối
    public bool CoTheDuyet => TrangThai == "ChoDuyet";

    // Quản trị viên có thể hủy đăng ký nếu đang ở trạng thái Chờ duyệt hoặc Đã duyệt
    public bool CoTheHuyBoiAdmin => TrangThai is "ChoDuyet" or "DaDuyet";
}

/// <summary>
/// ViewModel cho màn hình Quản lý / Duyệt đăng ký đào tạo của Admin và HR.
/// </summary>
public class DuyetDangKyListViewModel
{
    public List<DuyetDangKyItemViewModel> DanhSach { get; set; } = [];

    public string? SearchTerm { get; set; }
    public int? MaLop { get; set; }
    public int? MaPhongBan { get; set; }
    public string TrangThai { get; set; } = "ChoDuyet"; // Mặc định chỉ hiển thị danh sách Chờ duyệt
    public string SortOrder { get; set; } = "date_asc"; // Mặc định sắp xếp theo ngày đăng ký (cũ nhất trước để duyệt tuần tự)

    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public int TotalCount { get; set; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageIndex > 1;
    public bool HasNextPage => PageIndex < TotalPages;

    public IEnumerable<SelectListItem> Lops { get; set; } = [];
    public IEnumerable<SelectListItem> PhongBans { get; set; } = [];

    public static IReadOnlyDictionary<string, string> SortOptions { get; } = new Dictionary<string, string>
    {
        ["date_asc"] = "Ngày đăng ký (Cũ nhất trước - Ưu tiên duyệt)",
        ["date_desc"] = "Ngày đăng ký (Mới nhất trước)"
    };

    public Dictionary<string, string?> RouteForPage(int page) => new()
    {
        ["searchTerm"] = SearchTerm,
        ["maLop"] = MaLop?.ToString(),
        ["maPhongBan"] = MaPhongBan?.ToString(),
        ["trangThai"] = TrangThai,
        ["sortOrder"] = SortOrder,
        ["pageIndex"] = page.ToString()
    };
}
