//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QuanLyDaoTaoNoiBo.Models.Entities;


public class NhanVien : IValidatableObject
{
    [Key]
    public int MaNhanVien { get; set; }

    [Required(ErrorMessage = "Tài khoản liên kết là bắt buộc."), Range(1, int.MaxValue, ErrorMessage = "Tài khoản không hợp lệ.")]
    public int MaTaiKhoan { get; set; }

    [Required(ErrorMessage = "Họ tên bắt buộc phải nhập."), StringLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự.")]
    public string HoTen { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phòng ban là bắt buộc."), Range(1, int.MaxValue, ErrorMessage = "Phòng ban không hợp lệ.")]
    public int MaPhongBan { get; set; }

    [Required(ErrorMessage = "Ngày sinh là bắt buộc."), DataType(DataType.Date)]
    public DateOnly NgaySinh { get; set; }

    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự."), Phone(ErrorMessage = "Số điện thoại không đúng định dạng.")]
    [RegularExpression(@"^(\+84|0)[1-9]\d{8,9}$", ErrorMessage = "Số điện thoại không hợp lệ (gồm 10-11 chữ số, bắt đầu bằng 0 hoặc +84).")]
    public string? SoDienThoai { get; set; }

    [Required(ErrorMessage = "Email bắt buộc phải nhập."), StringLength(254, ErrorMessage = "Email tối đa 254 ký tự."), EmailAddress(ErrorMessage = "Email không đúng định dạng.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Ngày vào làm là bắt buộc."), DataType(DataType.Date)]
    public DateOnly NgayVaoLam { get; set; }

    [Required]
    public bool TrangThai { get; set; } = true;

    [ForeignKey(nameof(MaTaiKhoan))]
    public TaiKhoan TaiKhoan { get; set; } = null!;

    [ForeignKey(nameof(MaPhongBan))]
    public PhongBan PhongBan { get; set; } = null!;

    public ICollection<DangKyDaoTao> DangKyDaoTaos { get; set; } = new List<DangKyDaoTao>();
    public ICollection<DiemDanh> DiemDanhs { get; set; } = new List<DiemDanh>();

    /// <summary>
    /// Ràng buộc nghiệp vụ theo mục 7.1:
    /// - Ngày vào làm không lớn hơn ngày hiện tại (giờ VN).
    /// - Ngày vào làm không trước ngày sinh.
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var homNay = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));

        if (string.IsNullOrWhiteSpace(HoTen))
        {
            yield return new ValidationResult("Họ tên bắt buộc phải nhập.", [nameof(HoTen)]);
        }

        if (NgayVaoLam > homNay)
        {
            yield return new ValidationResult("Ngày vào làm không lớn hơn ngày hiện tại.", [nameof(NgayVaoLam)]);
        }

        if (NgaySinh != default && NgayVaoLam < NgaySinh)
        {
            yield return new ValidationResult("Ngày vào làm không được trước ngày sinh.", [nameof(NgayVaoLam)]);
        }
    }
}
