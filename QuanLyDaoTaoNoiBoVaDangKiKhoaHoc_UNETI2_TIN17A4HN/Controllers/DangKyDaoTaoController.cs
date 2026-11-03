//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyDaoTaoNoiBo.Data;
using QuanLyDaoTaoNoiBo.Filters;
using QuanLyDaoTaoNoiBo.Models.ViewModels;
using QuanLyDaoTaoNoiBo.Services;

namespace QuanLyDaoTaoNoiBo.Controllers;

/// <summary>
/// Controller xử lý các chức năng Đăng ký đào tạo cho nhân viên và Duyệt đăng ký cho Quản trị viên/HR.
/// </summary>
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DangKyDaoTaoController(ApplicationDbContext db, DangKyDaoTaoService dangKyService) : Controller
{
    // =========================================================================
    // PHẦN DÀNH CHO NHÂN VIÊN: XEM LỚP ĐĂNG KÝ VÀ LỊCH SỬ ĐÀO TẠO
    // =========================================================================

    /// <summary>
    /// Trang danh sách các lớp đào tạo mở để nhân viên lựa chọn và đăng ký.
    /// Đường dẫn: /DangKyDaoTao/Index
    /// </summary>
    [HttpGet, SessionRoles("NhanVien")]
    public async Task<IActionResult> Index(string? searchTerm, int? maLinhVuc, string? tinhTrangDangKy, int pageIndex = 1)
    {
        searchTerm = searchTerm?.Trim();
        if (maLinhVuc <= 0) maLinhVuc = null;

        var bayGio = DangKyDaoTaoService.BayGioVietNam();
        var homNay = DateOnly.FromDateTime(bayGio);

        // Lấy thông tin nhân viên liên kết với tài khoản đang đăng nhập
        var maTaiKhoan = HttpContext.Session.GetInt32("MaTaiKhoan");
        var nhanVien = maTaiKhoan.HasValue
            ? await db.NhanViens.AsNoTracking().FirstOrDefaultAsync(nv => nv.MaTaiKhoan == maTaiKhoan.Value)
            : null;

        var nhanVienDangHoatDong = nhanVien is not null && nhanVien.TrangThai;

        var model = new DangKyDaoTaoListViewModel
        {
            SearchTerm = searchTerm,
            MaLinhVuc = maLinhVuc,
            TinhTrangDangKy = tinhTrangDangKy,
            ChuaCoHoSoNhanVien = nhanVien is null,
            NhanVienNgungHoatDong = nhanVien is not null && !nhanVien.TrangThai,
            LinhVucs = await db.LinhVucDaoTaos.AsNoTracking().Where(lv => lv.TrangThai).OrderBy(lv => lv.TenLinhVuc)
                .Select(lv => new SelectListItem(lv.TenLinhVuc, lv.MaLinhVuc.ToString()))
                .ToListAsync()
        };

        if (nhanVien is null || !nhanVienDangHoatDong)
        {
            return View(model);
        }

        // Lấy danh sách các bản ghi đăng ký hiện có của nhân viên này để tra cứu trạng thái từng lớp
        var danhSachDangKyCuaToi = await db.DangKyDaoTaos.AsNoTracking()
            .Where(dk => dk.MaNhanVien == nhanVien.MaNhanVien)
            .ToDictionaryAsync(dk => dk.MaLop);

        // Lấy danh sách các lớp đã duyệt của nhân viên để kiểm tra trùng lịch
        var lopDaDuyetCuaToi = await db.DangKyDaoTaos.AsNoTracking()
            .Where(dk => dk.MaNhanVien == nhanVien.MaNhanVien && dk.TrangThai == "DaDuyet")
            .Select(dk => dk.LopDaoTao)
            .Where(l => l.TrangThai != "DaHuy")
            .ToListAsync();

        // Truy vấn lớp học kèm khóa và lĩnh vực
        var query = db.LopDaoTaos.AsNoTracking()
            .Include(l => l.KhoaDaoTao).ThenInclude(k => k.LinhVucDaoTao)
            .Include(l => l.GiangVien)
            .Include(l => l.DangKyDaoTaos)
            .AsQueryable();

        // Tìm kiếm theo tên lớp, tên khóa, tên lĩnh vực hoặc địa điểm
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(l => l.TenLop.Contains(searchTerm) ||
                                     l.KhoaDaoTao.TenKhoaDaoTao.Contains(searchTerm) ||
                                     l.KhoaDaoTao.LinhVucDaoTao.TenLinhVuc.Contains(searchTerm) ||
                                     l.DiaDiem.Contains(searchTerm));
        }

        if (maLinhVuc.HasValue)
        {
            query = query.Where(l => l.KhoaDaoTao.MaLinhVuc == maLinhVuc.Value);
        }

        // Lọc theo tình trạng đăng ký của cá nhân (đã đăng ký / chưa đăng ký)
        if (tinhTrangDangKy == "dadk")
        {
            var maLopDaDk = danhSachDangKyCuaToi.Keys.ToList();
            query = query.Where(l => maLopDaDk.Contains(l.MaLop));
        }
        else if (tinhTrangDangKy == "chuadk")
        {
            var maLopDaDk = danhSachDangKyCuaToi.Keys.ToList();
            query = query.Where(l => !maLopDaDk.Contains(l.MaLop));
        }

        var total = await query.CountAsync();
        const int pageSize = 10;
        pageIndex = Math.Clamp(pageIndex, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));

        var items = await query.OrderBy(l => l.NgayBatDau).ThenBy(l => l.MaLop)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        model.PageIndex = pageIndex;
        model.PageSize = pageSize;
        model.TotalCount = total;

        foreach (var lop in items)
        {
            var soDaDuyet = lop.DangKyDaoTaos.Count(dk => dk.TrangThai == "DaDuyet");
            var soChoCon = lop.SoLuongToiDa - soDaDuyet;
            danhSachDangKyCuaToi.TryGetValue(lop.MaLop, out var banGhiDk);

            var itemVm = new LopDangKyItemViewModel
            {
                MaLop = lop.MaLop,
                TenLop = lop.TenLop,
                TenKhoaDaoTao = lop.KhoaDaoTao.TenKhoaDaoTao,
                TenLinhVuc = lop.KhoaDaoTao.LinhVucDaoTao.TenLinhVuc,
                NgayBatDau = lop.NgayBatDau,
                NgayKetThuc = lop.NgayKetThuc,
                HanDangKy = lop.HanDangKy,
                DiaDiem = lop.DiaDiem,
                TenGiangVien = lop.GiangVien?.HoTen ?? "Chưa phân công",
                SoLuongToiDa = lop.SoLuongToiDa,
                SoDaDuyet = soDaDuyet,
                TrangThaiLop = lop.TrangThai,
                DaTungDangKy = banGhiDk is not null,
                MaDangKy = banGhiDk?.MaDangKy,
                TrangThaiDangKy = banGhiDk?.TrangThai
            };

            // Xác định điều kiện đăng ký
            if (banGhiDk is not null)
            {
                itemVm.CoTheDangKy = false;
                itemVm.LyDoKhongTheDangKy = $"Đã có bản ghi đăng ký ({itemVm.TenTrangThaiDangKy})";
            }
            else if (!lop.KhoaDaoTao.TrangThai)
            {
                itemVm.CoTheDangKy = false;
                itemVm.LyDoKhongTheDangKy = "Khóa đào tạo đang tạm ngừng hoạt động";
            }
            else if (lop.TrangThai != "MoDangKy")
            {
                itemVm.CoTheDangKy = false;
                itemVm.LyDoKhongTheDangKy = "Lớp không mở nhận đăng ký";
            }
            else if (lop.HanDangKy < bayGio)
            {
                itemVm.CoTheDangKy = false;
                itemVm.LyDoKhongTheDangKy = "Đã hết hạn đăng ký";
            }
            else if (lop.NgayBatDau <= homNay)
            {
                itemVm.CoTheDangKy = false;
                itemVm.LyDoKhongTheDangKy = "Lớp đã hoặc bắt đầu hôm nay";
            }
            else if (soChoCon <= 0)
            {
                itemVm.CoTheDangKy = false;
                itemVm.LyDoKhongTheDangKy = "Lớp đã hết chỗ";
            }
            else
            {
                // Kiểm tra trùng lịch với lớp đã duyệt
                var biTrung = lopDaDuyetCuaToi.FirstOrDefault(other =>
                    lop.NgayBatDau <= other.NgayKetThuc && other.NgayBatDau <= lop.NgayKetThuc);

                if (biTrung is not null)
                {
                    itemVm.CoTheDangKy = false;
                    itemVm.LyDoKhongTheDangKy = $"Trùng lịch với lớp “{biTrung.TenLop}” ({biTrung.NgayBatDau:dd/MM} - {biTrung.NgayKetThuc:dd/MM})";
                }
                else
                {
                    itemVm.CoTheDangKy = true;
                }
            }

            model.Lops.Add(itemVm);
        }

        return View(model);
    }

    /// <summary>
    /// Thực hiện gửi yêu cầu đăng ký lớp học cho nhân viên hiện tại.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, SessionRoles("NhanVien")]
    public async Task<IActionResult> DangKy(int maLop)
    {
        var maTaiKhoan = HttpContext.Session.GetInt32("MaTaiKhoan");
        var nhanVien = maTaiKhoan.HasValue
            ? await db.NhanViens.FirstOrDefaultAsync(nv => nv.MaTaiKhoan == maTaiKhoan.Value)
            : null;

        if (nhanVien is null)
        {
            TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ nhân viên.";
            return RedirectToAction(nameof(Index));
        }

        // Kiểm tra nghiệp vụ mục 7.1: Nhân viên nghỉ việc / ngừng hoạt động không được tạo đăng ký mới
        if (!nhanVien.TrangThai)
        {
            TempData["Error"] = "Nhân viên đã nghỉ việc hoặc ngừng hoạt động không được phép tạo đăng ký mới.";
            return RedirectToAction(nameof(Index));
        }

        var (success, message) = await dangKyService.DangKyKhoaHocAsync(nhanVien.MaNhanVien, maLop);
        if (success)
        {
            TempData["Success"] = message;
            return RedirectToAction(nameof(LichSuDangKy));
        }

        TempData["Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hiển thị lịch sử các khóa đào tạo mà nhân viên đã đăng ký.
    /// Đường dẫn: /DangKyDaoTao/LichSuDangKy
    /// </summary>
    [HttpGet, SessionRoles("NhanVien")]
    public async Task<IActionResult> LichSuDangKy(string? searchTerm, string? trangThai)
    {
        searchTerm = searchTerm?.Trim();
        var bayGio = DangKyDaoTaoService.BayGioVietNam();
        var homNay = DateOnly.FromDateTime(bayGio);

        var maTaiKhoan = HttpContext.Session.GetInt32("MaTaiKhoan");
        var nhanVien = maTaiKhoan.HasValue
            ? await db.NhanViens.AsNoTracking().FirstOrDefaultAsync(nv => nv.MaTaiKhoan == maTaiKhoan.Value && nv.TrangThai)
            : null;

        var model = new LichSuDangKyListViewModel
        {
            ChuaCoHoSoNhanVien = nhanVien is null,
            SearchTerm = searchTerm,
            TrangThai = trangThai
        };

        if (nhanVien is null)
        {
            return View(model);
        }

        var query = db.DangKyDaoTaos.AsNoTracking()
            .Where(dk => dk.MaNhanVien == nhanVien.MaNhanVien)
            .Include(dk => dk.LopDaoTao).ThenInclude(l => l.KhoaDaoTao).ThenInclude(k => k.LinhVucDaoTao)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(dk => dk.LopDaoTao.TenLop.Contains(searchTerm) ||
                                      dk.LopDaoTao.KhoaDaoTao.TenKhoaDaoTao.Contains(searchTerm));
        }

        if (!string.IsNullOrWhiteSpace(trangThai))
        {
            query = query.Where(dk => dk.TrangThai == trangThai);
        }

        var items = await query.OrderByDescending(dk => dk.NgayDangKy).ToListAsync();

        foreach (var item in items)
        {
            var coTheHuy = (item.TrangThai is "ChoDuyet" or "DaDuyet") && (homNay < item.LopDaoTao.NgayBatDau);
            string? lyDoKhongTheHuy = null;

            if (item.TrangThai is not ("ChoDuyet" or "DaDuyet"))
            {
                lyDoKhongTheHuy = item.TrangThai == "DaHuy" ? "Đã hủy" : "Đã bị từ chối";
            }
            else if (homNay >= item.LopDaoTao.NgayBatDau)
            {
                lyDoKhongTheHuy = "Lớp đã bắt đầu học";
            }

            model.DanhSach.Add(new LichSuDangKyItemViewModel
            {
                MaDangKy = item.MaDangKy,
                MaLop = item.MaLop,
                TenLop = item.LopDaoTao.TenLop,
                TenKhoaDaoTao = item.LopDaoTao.KhoaDaoTao.TenKhoaDaoTao,
                TenLinhVuc = item.LopDaoTao.KhoaDaoTao.LinhVucDaoTao.TenLinhVuc,
                NgayBatDau = item.LopDaoTao.NgayBatDau,
                NgayKetThuc = item.LopDaoTao.NgayKetThuc,
                DiaDiem = item.LopDaoTao.DiaDiem,
                NgayDangKy = item.NgayDangKy,
                TrangThai = item.TrangThai,
                NgayDuyet = item.NgayDuyet,
                GhiChu = item.GhiChu,
                CoTheHuy = coTheHuy,
                LyDoKhongTheHuy = lyDoKhongTheHuy
            });
        }

        return View(model);
    }

    /// <summary>
    /// Nhân viên hủy đơn đăng ký của mình trước ngày bắt đầu lớp. Bắt buộc nhập lý do.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, SessionRoles("NhanVien")]
    public async Task<IActionResult> HuyDangKy(int maDangKy, string lyDoHuy)
    {
        var maTaiKhoan = HttpContext.Session.GetInt32("MaTaiKhoan");
        var nhanVien = maTaiKhoan.HasValue
            ? await db.NhanViens.FirstOrDefaultAsync(nv => nv.MaTaiKhoan == maTaiKhoan.Value && nv.TrangThai)
            : null;

        if (nhanVien is null)
        {
            TempData["Error"] = "Tài khoản của bạn chưa được liên kết với hồ sơ nhân viên.";
            return RedirectToAction(nameof(LichSuDangKy));
        }

        var (success, message) = await dangKyService.HuyDangKyAsync(maDangKy, nhanVien.MaNhanVien, lyDoHuy);
        if (success)
        {
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        return RedirectToAction(nameof(LichSuDangKy));
    }

    // =========================================================================
    // PHẦN DÀNH CHO ADMIN VÀ HR (QUẢN TRỊ VIÊN VÀ QUẢN LÝ): DUYỆT ĐĂNG KÝ
    // =========================================================================

    /// <summary>
    /// Màn hình quản lý và xét duyệt các đơn đăng ký khóa học.
    /// Đường dẫn: /DangKyDaoTao/DuyetDangKy
    /// </summary>
    [HttpGet, SessionRoles("QuanTri", "QuanLy")]
    public async Task<IActionResult> DuyetDangKy(string? searchTerm, int? maLop, int? maPhongBan,
        string trangThai = "ChoDuyet", string sortOrder = "date_asc", int pageIndex = 1)
    {
        searchTerm = searchTerm?.Trim();
        if (maLop <= 0) maLop = null;
        if (maPhongBan <= 0) maPhongBan = null;
        if (!DuyetDangKyListViewModel.SortOptions.ContainsKey(sortOrder ?? "")) sortOrder = "date_asc";

        var query = db.DangKyDaoTaos.AsNoTracking()
            .Include(dk => dk.NhanVien).ThenInclude(nv => nv.PhongBan)
            .Include(dk => dk.LopDaoTao).ThenInclude(l => l.KhoaDaoTao)
            .AsQueryable();

        // Tìm kiếm theo tên nhân viên, email, mã nhân viên hoặc tên lớp
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(dk => dk.NhanVien.HoTen.Contains(searchTerm) ||
                                      dk.NhanVien.Email.Contains(searchTerm) ||
                                      dk.LopDaoTao.TenLop.Contains(searchTerm) ||
                                      dk.LopDaoTao.KhoaDaoTao.TenKhoaDaoTao.Contains(searchTerm));
        }

        if (maLop.HasValue)
        {
            query = query.Where(dk => dk.MaLop == maLop.Value);
        }

        if (maPhongBan.HasValue)
        {
            query = query.Where(dk => dk.NhanVien.MaPhongBan == maPhongBan.Value);
        }

        // Lọc theo trạng thái; mặc định là Chờ duyệt nếu không chọn "TatCa"
        if (!string.IsNullOrWhiteSpace(trangThai) && trangThai != "TatCa")
        {
            query = query.Where(dk => dk.TrangThai == trangThai);
        }

        var total = await query.CountAsync();
        const int pageSize = 10;
        pageIndex = Math.Clamp(pageIndex, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));

        // Sắp xếp theo ngày đăng ký (mục 7.7)
        var sorted = sortOrder == "date_desc"
            ? query.OrderByDescending(dk => dk.NgayDangKy)
            : query.OrderBy(dk => dk.NgayDangKy);

        var items = await sorted.ThenBy(dk => dk.MaDangKy)
            .Skip((pageIndex - 1) * pageSize).Take(pageSize)
            .ToListAsync();

        // Lấy danh sách số lượng đã duyệt của từng lớp trong trang để hiển thị chỗ còn
        var maLopsInPage = items.Select(i => i.MaLop).Distinct().ToList();
        var countDaDuyet = await db.DangKyDaoTaos.AsNoTracking()
            .Where(dk => maLopsInPage.Contains(dk.MaLop) && dk.TrangThai == "DaDuyet")
            .GroupBy(dk => dk.MaLop)
            .Select(g => new { MaLop = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.MaLop, x => x.Count);

        var model = new DuyetDangKyListViewModel
        {
            SearchTerm = searchTerm,
            MaLop = maLop,
            MaPhongBan = maPhongBan,
            TrangThai = trangThai,
            SortOrder = sortOrder ?? "date_asc",
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            Lops = await db.LopDaoTaos.AsNoTracking().OrderBy(l => l.TenLop)
                .Select(l => new SelectListItem(l.TenLop, l.MaLop.ToString()))
                .ToListAsync(),
            PhongBans = await db.PhongBans.AsNoTracking().OrderBy(pb => pb.TenPhongBan)
                .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan.ToString()))
                .ToListAsync(),
            DanhSach = items.Select(dk =>
            {
                var daDuyet = countDaDuyet.GetValueOrDefault(dk.MaLop, 0);
                return new DuyetDangKyItemViewModel
                {
                    MaDangKy = dk.MaDangKy,
                    MaNhanVien = dk.MaNhanVien,
                    HoTenNhanVien = dk.NhanVien.HoTen,
                    EmailNhanVien = dk.NhanVien.Email,
                    TenPhongBan = dk.NhanVien.PhongBan.TenPhongBan,
                    MaLop = dk.MaLop,
                    TenLop = dk.LopDaoTao.TenLop,
                    TenKhoaDaoTao = dk.LopDaoTao.KhoaDaoTao.TenKhoaDaoTao,
                    NgayBatDau = dk.LopDaoTao.NgayBatDau,
                    NgayKetThuc = dk.LopDaoTao.NgayKetThuc,
                    SoLuongToiDa = dk.LopDaoTao.SoLuongToiDa,
                    SoDaDuyet = daDuyet,
                    TrangThaiLop = dk.LopDaoTao.TrangThai,
                    NgayDangKy = dk.NgayDangKy,
                    TrangThai = dk.TrangThai,
                    NgayDuyet = dk.NgayDuyet,
                    GhiChu = dk.GhiChu
                };
            }).ToList()
        };

        return View(model);
    }

    /// <summary>
    /// Admin hoặc Quản lý bấm Duyệt đơn đăng ký.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, SessionRoles("QuanTri", "QuanLy")]
    public async Task<IActionResult> Duyet(int maDangKy)
    {
        var (success, message) = await dangKyService.DuyetDangKyAsync(maDangKy);
        if (success)
        {
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        return RedirectToAction(nameof(DuyetDangKy));
    }

    /// <summary>
    /// Admin hoặc Quản lý bấm Từ chối đơn đăng ký kèm theo lý do từ chối.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, SessionRoles("QuanTri", "QuanLy")]
    public async Task<IActionResult> TuChoi(int maDangKy, string lyDoTuChoi)
    {
        var (success, message) = await dangKyService.TuChoiDangKyAsync(maDangKy, lyDoTuChoi);
        if (success)
        {
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        return RedirectToAction(nameof(DuyetDangKy));
    }

    /// <summary>
    /// Admin hoặc Quản lý hủy đơn đăng ký theo nghiệp vụ (mục 7.6). Bắt buộc ghi chú lý do.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken, SessionRoles("QuanTri", "QuanLy")]
    public async Task<IActionResult> AdminHuy(int maDangKy, string lyDoHuy)
    {
        var (success, message) = await dangKyService.AdminHuyDangKyAsync(maDangKy, lyDoHuy);
        if (success)
        {
            TempData["Success"] = message;
        }
        else
        {
            TempData["Error"] = message;
        }

        return RedirectToAction(nameof(DuyetDangKy));
    }
}
