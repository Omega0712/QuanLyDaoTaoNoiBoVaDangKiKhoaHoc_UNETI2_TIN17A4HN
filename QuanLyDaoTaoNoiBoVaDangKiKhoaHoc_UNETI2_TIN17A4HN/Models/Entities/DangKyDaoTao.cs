//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyDaoTaoNoiBo.Models.Entities;

/// <summary>
/// Entity DangKyDaoTao (Mục 7.2)
/// Quản lý thông tin đăng ký đào tạo của nhân viên:
/// - MaDangKy
/// - MaNhanVien
/// - MaLop
/// - NgayDangKy
/// - TrangThai (tối thiểu: ChoDuyet, DaDuyet, TuChoi, DaHuy)
/// - NgayDuyet
/// - GhiChu
/// </summary>
public class DangKyDaoTao : IValidatableObject
{
    [Key]
    public int MaDangKy { get; set; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "Nhân viên không hợp lệ.")]
    public int MaNhanVien { get; set; }

    [Required, Range(1, int.MaxValue, ErrorMessage = "Lớp đào tạo không hợp lệ.")]
    public int MaLop { get; set; }

    [Required]
    public DateTime NgayDangKy { get; set; }

    /// <summary>
    /// Trạng thái đăng ký theo mục 7.2 (tối thiểu: ChoDuyet, DaDuyet, TuChoi, DaHuy).
    /// </summary>
    [Required, StringLength(30)]
    [RegularExpression("^(ChoDuyet|DaDuyet|TuChoi|DaHuy)$", ErrorMessage = "Trạng thái đăng ký không hợp lệ.")]
    public string TrangThai { get; set; } = TrangThaiDangKyConst.ChoDuyet;

    public DateTime? NgayDuyet { get; set; }

    [StringLength(1000, ErrorMessage = "Ghi chú/Lý do tối đa 1000 ký tự.")]
    public string? GhiChu { get; set; }

    [ForeignKey(nameof(MaNhanVien))]
    public NhanVien NhanVien { get; set; } = null!;

    [ForeignKey(nameof(MaLop))]
    public LopDaoTao LopDaoTao { get; set; } = null!;

    public KetQuaDaoTao? KetQuaDaoTao { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (NgayDuyet.HasValue && NgayDuyet.Value < NgayDangKy)
        {
            yield return new ValidationResult("Ngày duyệt không được trước ngày đăng ký.", [nameof(NgayDuyet)]);
        }
    }
}

/// <summary>
/// Các hằng số trạng thái đăng ký đào tạo (Mục 7.2)
/// </summary>
public static class TrangThaiDangKyConst
{
    public const string ChoDuyet = "ChoDuyet";
    public const string DaDuyet = "DaDuyet";
    public const string TuChoi = "TuChoi";
    public const string DaHuy = "DaHuy";

    public static readonly IReadOnlyDictionary<string, string> TenTrangThai = new Dictionary<string, string>
    {
        [ChoDuyet] = "Chờ duyệt",
        [DaDuyet] = "Đã duyệt",
        [TuChoi] = "Từ chối",
        [DaHuy] = "Đã hủy"
    };
}
