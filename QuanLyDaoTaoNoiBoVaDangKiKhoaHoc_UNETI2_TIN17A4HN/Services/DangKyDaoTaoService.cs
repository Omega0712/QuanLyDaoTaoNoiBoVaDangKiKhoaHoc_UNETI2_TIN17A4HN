//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using System.Data;
using Microsoft.EntityFrameworkCore;
using QuanLyDaoTaoNoiBo.Data;
using QuanLyDaoTaoNoiBo.Models.Entities;

namespace QuanLyDaoTaoNoiBo.Services;

/// <summary>
/// Service xử lý toàn bộ nghiệp vụ Đăng ký, Duyệt, Từ chối và Hủy đào tạo của Module 3.
/// Sử dụng transaction Serializable trên SQL Server để đảm bảo an toàn tuyệt đối khi nhiều thao tác diễn ra đồng thời.
/// </summary>
public class DangKyDaoTaoService(ApplicationDbContext db)
{
    /// <summary>
    /// Lấy thời điểm hiện tại theo múi giờ Việt Nam (UTC+7).
    /// </summary>
    public static DateTime BayGioVietNam() => DateTime.UtcNow.AddHours(7);

    /// <summary>
    /// Nhân viên thực hiện đăng ký tham gia một lớp đào tạo.
    /// Kiểm tra đầy đủ: trạng thái lớp, hạn đăng ký, ngày bắt đầu, sức chứa, đăng ký trùng lớp, trùng lịch học.
    /// </summary>
    public async Task<(bool Success, string Message)> DangKyKhoaHocAsync(int maNhanVien, int maLop)
    {
        var bayGio = BayGioVietNam();
        var homNay = DateOnly.FromDateTime(bayGio);

        // Sử dụng transaction Serializable để tránh việc 2 người cùng đăng ký suất cuối cùng
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            // 1. Kiểm tra thông tin nhân viên và tài khoản
            var nhanVien = await db.NhanViens
                .Include(nv => nv.TaiKhoan)
                .SingleOrDefaultAsync(nv => nv.MaNhanVien == maNhanVien);

            if (nhanVien is null || !nhanVien.TrangThai || !nhanVien.TaiKhoan.TrangThai)
            {
                return (false, "Hồ sơ nhân viên hoặc tài khoản không tồn tại hoặc đã ngừng hoạt động.");
            }

            // 2. Kiểm tra thông tin lớp đào tạo và khóa đào tạo
            var lop = await db.LopDaoTaos
                .Include(l => l.KhoaDaoTao)
                .SingleOrDefaultAsync(l => l.MaLop == maLop);

            if (lop is null)
            {
                return (false, "Lớp đào tạo không tồn tại.");
            }

            if (!lop.KhoaDaoTao.TrangThai)
            {
                return (false, "Khóa đào tạo của lớp này đang tạm ngừng hoạt động.");
            }

            if (lop.TrangThai != "MoDangKy")
            {
                return (false, "Lớp đào tạo hiện không ở trạng thái “Đang nhận đăng ký”.");
            }

            if (lop.HanDangKy < bayGio)
            {
                return (false, "Lớp đào tạo đã hết hạn đăng ký.");
            }

            if (lop.NgayBatDau <= homNay)
            {
                return (false, "Lớp đào tạo đã hoặc sắp bắt đầu hôm nay, không thể đăng ký mới.");
            }

            // 3. Kiểm tra số lượng chỗ còn lại (chỉ tính những học viên Đã duyệt)
            var soDaDuyet = await db.DangKyDaoTaos
                .CountAsync(dk => dk.MaLop == maLop && dk.TrangThai == "DaDuyet");

            if (lop.SoLuongToiDa - soDaDuyet <= 0)
            {
                return (false, "Lớp đào tạo đã hết chỗ.");
            }

            // 4. Kiểm tra xem nhân viên đã từng có bản ghi đăng ký lớp này chưa
            // Quy định đề bài: một nhân viên chỉ có 1 bản ghi cho mỗi lớp, kể cả đã hủy/từ chối, không đăng ký lại.
            var daCoBanGhi = await db.DangKyDaoTaos
                .AnyAsync(dk => dk.MaNhanVien == maNhanVien && dk.MaLop == maLop);

            if (daCoBanGhi)
            {
                return (false, "Bạn đã từng đăng ký lớp học này (kể cả đã hủy hoặc bị từ chối) nên không thể đăng ký lại.");
            }

            // 5. Kiểm tra trùng lịch với các lớp khác mà nhân viên này ĐÃ ĐƯỢC DUYỆT
            // Công thức trùng lịch theo yêu cầu: LopA.NgayBatDau <= LopB.NgayKetThuc && LopB.NgayBatDau <= LopA.NgayKetThuc
            var lopTrungLich = await db.DangKyDaoTaos
                .Where(dk => dk.MaNhanVien == maNhanVien && dk.TrangThai == "DaDuyet" && dk.MaLop != maLop)
                .Select(dk => dk.LopDaoTao)
                .Where(other => other.TrangThai != "DaHuy" &&
                                lop.NgayBatDau <= other.NgayKetThuc &&
                                other.NgayBatDau <= lop.NgayKetThuc)
                .FirstOrDefaultAsync();

            if (lopTrungLich is not null)
            {
                return (false, $"Trùng lịch học với lớp “{lopTrungLich.TenLop}” mà bạn đã được duyệt ({lopTrungLich.NgayBatDau:dd/MM/yyyy} - {lopTrungLich.NgayKetThuc:dd/MM/yyyy}).");
            }

            // 6. Tạo bản ghi đăng ký mới ở trạng thái Chờ duyệt
            var dangKyMoi = new DangKyDaoTao
            {
                MaNhanVien = maNhanVien,
                MaLop = maLop,
                NgayDangKy = bayGio,
                TrangThai = "ChoDuyet",
                NgayDuyet = null,
                GhiChu = null
            };

            db.DangKyDaoTaos.Add(dangKyMoi);
            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, "Đăng ký lớp đào tạo thành công! Đơn đăng ký đang chờ xét duyệt.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Hệ thống đang bận hoặc dữ liệu đã thay đổi đồng thời. Vui lòng tải lại trang và thử lại.");
        }
    }

    /// <summary>
    /// Admin hoặc Quản lý (HR) duyệt một đơn đăng ký đang chờ.
    /// Kiểm tra lại sức chứa, lịch học, trạng thái nhân viên/tài khoản/khóa và lớp chưa bắt đầu.
    /// </summary>
    public async Task<(bool Success, string Message)> DuyetDangKyAsync(int maDangKy)
    {
        var bayGio = BayGioVietNam();
        var homNay = DateOnly.FromDateTime(bayGio);

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var dangKy = await db.DangKyDaoTaos
                .Include(dk => dk.NhanVien).ThenInclude(nv => nv.TaiKhoan)
                .Include(dk => dk.LopDaoTao).ThenInclude(l => l.KhoaDaoTao)
                .SingleOrDefaultAsync(dk => dk.MaDangKy == maDangKy);

            if (dangKy is null)
            {
                return (false, "Không tìm thấy đơn đăng ký này.");
            }

            if (dangKy.TrangThai != "ChoDuyet")
            {
                return (false, "Chỉ có thể duyệt đơn đăng ký đang ở trạng thái “Chờ duyệt”.");
            }

            if (!dangKy.NhanVien.TrangThai || !dangKy.NhanVien.TaiKhoan.TrangThai)
            {
                return (false, "Nhân viên hoặc tài khoản này hiện đang ngừng hoạt động.");
            }

            if (!dangKy.LopDaoTao.KhoaDaoTao.TrangThai)
            {
                return (false, "Khóa đào tạo của lớp này đang tạm ngừng hoạt động.");
            }

            if (dangKy.LopDaoTao.TrangThai == "DaHuy")
            {
                return (false, "Lớp đào tạo này đã bị hủy, không thể duyệt đăng ký.");
            }

            // Quy định đề bài: lớp chưa bắt đầu (có thể duyệt sau hạn đăng ký, khi lớp đã đóng đăng ký nhưng chưa bắt đầu)
            if (dangKy.LopDaoTao.NgayBatDau <= homNay)
            {
                return (false, "Lớp đào tạo đã bắt đầu học, không thể duyệt thêm học viên.");
            }

            // Kiểm tra lại sức chứa
            var soDaDuyet = await db.DangKyDaoTaos
                .CountAsync(dk => dk.MaLop == dangKy.MaLop && dk.TrangThai == "DaDuyet");

            if (dangKy.LopDaoTao.SoLuongToiDa - soDaDuyet <= 0)
            {
                return (false, "Lớp học đã đủ số lượng học viên tối đa, không thể duyệt thêm.");
            }

            // Kiểm tra trùng lịch với các lớp khác đã được duyệt của nhân viên này
            var lopTrungLich = await db.DangKyDaoTaos
                .Where(dk => dk.MaNhanVien == dangKy.MaNhanVien && dk.TrangThai == "DaDuyet" && dk.MaLop != dangKy.MaLop)
                .Select(dk => dk.LopDaoTao)
                .Where(other => other.TrangThai != "DaHuy" &&
                                dangKy.LopDaoTao.NgayBatDau <= other.NgayKetThuc &&
                                other.NgayBatDau <= dangKy.LopDaoTao.NgayKetThuc)
                .FirstOrDefaultAsync();

            if (lopTrungLich is not null)
            {
                return (false, $"Nhân viên này đã được duyệt ở lớp “{lopTrungLich.TenLop}” có lịch học trùng ({lopTrungLich.NgayBatDau:dd/MM/yyyy} - {lopTrungLich.NgayKetThuc:dd/MM/yyyy}).");
            }

            // Cập nhật trạng thái duyệt
            dangKy.TrangThai = "DaDuyet";
            dangKy.NgayDuyet = bayGio;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, $"Đã duyệt đăng ký cho học viên {dangKy.NhanVien.HoTen}.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Xảy ra xung đột dữ liệu trong quá trình duyệt. Vui lòng tải lại và thử lại.");
        }
    }

    /// <summary>
    /// Admin hoặc Quản lý (HR) từ chối đơn đăng ký.
    /// Yêu cầu bắt buộc phải có lý do từ chối.
    /// </summary>
    public async Task<(bool Success, string Message)> TuChoiDangKyAsync(int maDangKy, string lyDoTuChoi)
    {
        var bayGio = BayGioVietNam();

        if (string.IsNullOrWhiteSpace(lyDoTuChoi))
        {
            return (false, "Vui lòng nhập lý do từ chối đăng ký.");
        }

        lyDoTuChoi = lyDoTuChoi.Trim();
        if (lyDoTuChoi.Length > 1000)
        {
            lyDoTuChoi = lyDoTuChoi[..1000];
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var dangKy = await db.DangKyDaoTaos
                .Include(dk => dk.NhanVien)
                .SingleOrDefaultAsync(dk => dk.MaDangKy == maDangKy);

            if (dangKy is null)
            {
                return (false, "Không tìm thấy đơn đăng ký này.");
            }

            if (dangKy.TrangThai != "ChoDuyet")
            {
                return (false, "Chỉ có thể từ chối đơn đăng ký đang ở trạng thái “Chờ duyệt”.");
            }

            dangKy.TrangThai = "TuChoi";
            dangKy.NgayDuyet = bayGio;
            dangKy.GhiChu = lyDoTuChoi;

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, $"Đã từ chối đơn đăng ký của học viên {dangKy.NhanVien.HoTen}.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Xảy ra lỗi khi từ chối đơn đăng ký. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Nhân viên tự hủy đơn đăng ký của chính mình.
    /// Chỉ được hủy khi đang Chờ duyệt hoặc Đã duyệt, và trước ngày bắt đầu lớp. Bắt buộc có lý do.
    /// </summary>
    public async Task<(bool Success, string Message)> HuyDangKyAsync(int maDangKy, int maNhanVien, string lyDoHuy)
    {
        var bayGio = BayGioVietNam();
        var homNay = DateOnly.FromDateTime(bayGio);

        if (string.IsNullOrWhiteSpace(lyDoHuy))
        {
            return (false, "Vui lòng nhập lý do hủy đăng ký.");
        }

        lyDoHuy = lyDoHuy.Trim();
        if (lyDoHuy.Length > 1000)
        {
            lyDoHuy = lyDoHuy[..1000];
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var dangKy = await db.DangKyDaoTaos
                .Include(dk => dk.LopDaoTao)
                .SingleOrDefaultAsync(dk => dk.MaDangKy == maDangKy && dk.MaNhanVien == maNhanVien);

            if (dangKy is null)
            {
                return (false, "Không tìm thấy đơn đăng ký hoặc bạn không có quyền hủy đơn này.");
            }

            if (dangKy.TrangThai is not ("ChoDuyet" or "DaDuyet"))
            {
                return (false, "Chỉ có thể hủy đăng ký khi đơn đang ở trạng thái “Chờ duyệt” hoặc “Đã duyệt”.");
            }

            // Quy định đề bài: chỉ được hủy trước ngày bắt đầu; không hủy từ ngày bắt đầu trở đi
            if (dangKy.LopDaoTao.NgayBatDau <= homNay)
            {
                return (false, $"Lớp đã bắt đầu học từ ngày {dangKy.LopDaoTao.NgayBatDau:dd/MM/yyyy}, không thể hủy đăng ký.");
            }

            // Cập nhật trạng thái hủy, giữ nguyên NgayDuyet cũ nếu đã từng được duyệt
            dangKy.TrangThai = "DaHuy";
            dangKy.GhiChu = string.IsNullOrWhiteSpace(dangKy.GhiChu)
                ? $"Lý do hủy: {lyDoHuy}"
                : $"{dangKy.GhiChu} | Lý do hủy: {lyDoHuy}";

            if (dangKy.GhiChu.Length > 1000)
            {
                dangKy.GhiChu = dangKy.GhiChu[..1000];
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, "Đã hủy đăng ký lớp học thành công.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Xảy ra lỗi khi hủy đăng ký. Vui lòng thử lại.");
        }
    }

    /// <summary>
    /// Admin hoặc Quản lý (HR) hủy đăng ký theo nghiệp vụ (mục 7.6).
    /// Bắt buộc ghi chú lý do nếu thao tác ảnh hưởng đến học viên/lớp học.
    /// </summary>
    public async Task<(bool Success, string Message)> AdminHuyDangKyAsync(int maDangKy, string lyDoHuy)
    {
        if (string.IsNullOrWhiteSpace(lyDoHuy))
        {
            return (false, "Vui lòng nhập lý do hủy đăng ký của quản trị viên.");
        }

        lyDoHuy = lyDoHuy.Trim();
        if (lyDoHuy.Length > 1000)
        {
            lyDoHuy = lyDoHuy[..1000];
        }

        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var dangKy = await db.DangKyDaoTaos
                .Include(dk => dk.NhanVien)
                .Include(dk => dk.LopDaoTao)
                .SingleOrDefaultAsync(dk => dk.MaDangKy == maDangKy);

            if (dangKy is null)
            {
                return (false, "Không tìm thấy đơn đăng ký này.");
            }

            if (dangKy.TrangThai is not ("ChoDuyet" or "DaDuyet"))
            {
                return (false, "Chỉ có thể hủy đơn đăng ký đang ở trạng thái “Chờ duyệt” hoặc “Đã duyệt”.");
            }

            dangKy.TrangThai = "DaHuy";
            dangKy.GhiChu = string.IsNullOrWhiteSpace(dangKy.GhiChu)
                ? $"[Quản trị hủy]: {lyDoHuy}"
                : $"{dangKy.GhiChu} | [Quản trị hủy]: {lyDoHuy}";

            if (dangKy.GhiChu.Length > 1000)
            {
                dangKy.GhiChu = dangKy.GhiChu[..1000];
            }

            await db.SaveChangesAsync();
            await tx.CommitAsync();

            return (true, $"Đã hủy đăng ký của học viên {dangKy.NhanVien.HoTen} theo nghiệp vụ quản trị.");
        }
        catch (Exception)
        {
            await tx.RollbackAsync();
            return (false, "Xảy ra lỗi khi thực hiện hủy đăng ký. Vui lòng thử lại.");
        }
    }
}
