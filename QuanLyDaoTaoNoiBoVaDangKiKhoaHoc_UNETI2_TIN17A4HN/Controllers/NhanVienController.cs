//Họ và tên : Phí Bảo Nam - 23103100236
//Module 3: Quản lý nhân viên – Đăng ký – Duyệt đào tạo.
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using QuanLyDaoTaoNoiBo.Data;
using QuanLyDaoTaoNoiBo.Filters;
using QuanLyDaoTaoNoiBo.Models.Entities;
using QuanLyDaoTaoNoiBo.Models.ViewModels;

namespace QuanLyDaoTaoNoiBo.Controllers;

/// <summary>
/// Quản lý hồ sơ nhân viên: xem danh sách, chi tiết, thêm mới, sửa và xóa hồ sơ.
/// Phân quyền: Dành riêng cho Quản trị viên (QuanTri) và Nhân sự - Đào tạo (QuanLy).
/// </summary>
[SessionRoles("QuanTri", "QuanLy")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NhanVienController(ApplicationDbContext db) : Controller
{
    /// <summary>
    /// Hiển thị danh sách nhân viên kèm tìm kiếm, lọc theo phòng ban, trạng thái và phân trang.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index(string? searchTerm, int? maPhongBan, string? trangThai,
        string sortOrder = "name_asc", int pageIndex = 1)
    {
        searchTerm = searchTerm?.Trim();
        if (trangThai is not ("hoatdong" or "ngung")) trangThai = null;
        if (!NhanVienListViewModel.SortOptions.ContainsKey(sortOrder ?? "")) sortOrder = "name_asc";
        if (maPhongBan <= 0) maPhongBan = null;

        var query = db.NhanViens.AsNoTracking()
            .Include(nv => nv.PhongBan)
            .Include(nv => nv.TaiKhoan)
            .AsQueryable();

        // Tìm kiếm theo: họ tên, email, số điện thoại, tên phòng ban hoặc tên đăng nhập
        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            query = query.Where(nv => nv.HoTen.Contains(searchTerm) ||
                                      nv.Email.Contains(searchTerm) ||
                                      (nv.SoDienThoai != null && nv.SoDienThoai.Contains(searchTerm)) ||
                                      nv.PhongBan.TenPhongBan.Contains(searchTerm) ||
                                      nv.TaiKhoan.TenDangNhap.Contains(searchTerm));
        }

        // Lọc theo phòng ban và trạng thái
        if (maPhongBan.HasValue)
        {
            query = query.Where(nv => nv.MaPhongBan == maPhongBan.Value);
        }

        if (trangThai == "hoatdong")
        {
            query = query.Where(nv => nv.TrangThai);
        }
        else if (trangThai == "ngung")
        {
            query = query.Where(nv => !nv.TrangThai);
        }

        // Đếm tổng số lượng bản ghi để phân trang
        var total = await query.CountAsync();
        const int pageSize = 10;
        pageIndex = Math.Clamp(pageIndex, 1, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));

        // Sắp xếp dữ liệu
        var sorted = sortOrder switch
        {
            "name_desc" => query.OrderByDescending(nv => nv.HoTen),
            "date_asc" => query.OrderBy(nv => nv.NgayVaoLam),
            "date_desc" => query.OrderByDescending(nv => nv.NgayVaoLam),
            _ => query.OrderBy(nv => nv.HoTen)
        };

        // Phân trang bằng Skip/Take kết hợp ThenBy khóa chính để đảm bảo ổn định
        var rows = await sorted.ThenBy(nv => nv.MaNhanVien)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return View(new NhanVienListViewModel
        {
            NhanViens = rows,
            SearchTerm = searchTerm,
            MaPhongBan = maPhongBan,
            TrangThai = trangThai,
            SortOrder = sortOrder ?? "name_asc",
            PageIndex = pageIndex,
            PageSize = pageSize,
            TotalCount = total,
            PhongBans = await db.PhongBans.AsNoTracking()
                .OrderBy(pb => pb.TenPhongBan)
                .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan.ToString()))
                .ToListAsync()
        });
    }

    /// <summary>
    /// Xem chi tiết hồ sơ nhân viên, thông tin tài khoản và số liệu đào tạo tham gia.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var entity = await db.NhanViens.AsNoTracking()
            .Include(nv => nv.TaiKhoan)
            .Include(nv => nv.PhongBan)
            .Include(nv => nv.DangKyDaoTaos)
            .Include(nv => nv.DiemDanhs)
            .SingleOrDefaultAsync(nv => nv.MaNhanVien == id);

        if (entity is null) return NotFound();

        var model = new NhanVienViewModel
        {
            MaNhanVien = entity.MaNhanVien,
            MaTaiKhoan = entity.MaTaiKhoan,
            HoTen = entity.HoTen,
            MaPhongBan = entity.MaPhongBan,
            NgaySinh = entity.NgaySinh,
            SoDienThoai = entity.SoDienThoai,
            Email = entity.Email,
            NgayVaoLam = entity.NgayVaoLam,
            TrangThai = entity.TrangThai,
            TenDangNhap = entity.TaiKhoan.TenDangNhap,
            VaiTroTaiKhoan = entity.TaiKhoan.VaiTro,
            TenPhongBan = entity.PhongBan.TenPhongBan,
            SoKhoaDaDangKy = entity.DangKyDaoTaos.Count,
            SoBuoiDaDiemDanh = entity.DiemDanhs.Count
        };

        return View(model);
    }

    /// <summary>
    /// Hiển thị form tạo hồ sơ nhân viên mới.
    /// Chỉ cho phép liên kết tài khoản đã được cấp nhưng chưa gán cho nhân viên khác.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new NhanVienViewModel
        {
            NgaySinh = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7).AddYears(-22)),
            NgayVaoLam = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7))
        };
        await NapDanhSachChon(model);
        return View(model);
    }

    /// <summary>
    /// Lưu hồ sơ nhân viên mới vào database.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(NhanVienViewModel model)
    {
        await KiemTraDuLieu(model);

        if (ModelState.IsValid)
        {
            var entity = new NhanVien
            {
                MaTaiKhoan = model.MaTaiKhoan,
                HoTen = model.HoTen.Trim(),
                MaPhongBan = model.MaPhongBan,
                NgaySinh = model.NgaySinh,
                SoDienThoai = model.SoDienThoai?.Trim(),
                Email = model.Email.Trim(),
                NgayVaoLam = model.NgayVaoLam,
                TrangThai = model.TrangThai
            };

            db.NhanViens.Add(entity);
            await db.SaveChangesAsync();

            TempData["Success"] = $"Đã tạo hồ sơ nhân viên cho {entity.HoTen} thành công.";
            return RedirectToAction(nameof(Details), new { id = entity.MaNhanVien });
        }

        await NapDanhSachChon(model);
        return View(model);
    }

    /// <summary>
    /// Hiển thị form chỉnh sửa hồ sơ nhân viên.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await db.NhanViens.Include(nv => nv.TaiKhoan).SingleOrDefaultAsync(nv => nv.MaNhanVien == id);
        if (entity is null) return NotFound();

        var model = new NhanVienViewModel
        {
            MaNhanVien = entity.MaNhanVien,
            MaTaiKhoan = entity.MaTaiKhoan,
            HoTen = entity.HoTen,
            MaPhongBan = entity.MaPhongBan,
            NgaySinh = entity.NgaySinh,
            SoDienThoai = entity.SoDienThoai,
            Email = entity.Email,
            NgayVaoLam = entity.NgayVaoLam,
            TrangThai = entity.TrangThai,
            TenDangNhap = entity.TaiKhoan.TenDangNhap
        };

        await NapDanhSachChon(model, currentMaTaiKhoan: entity.MaTaiKhoan);
        return View(model);
    }

    /// <summary>
    /// Lưu thông tin cập nhật hồ sơ nhân viên.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, NhanVienViewModel model)
    {
        if (id != model.MaNhanVien) return BadRequest();

        var entity = await db.NhanViens.FindAsync(id);
        if (entity is null) return NotFound();

        await KiemTraDuLieu(model, currentMaNhanVien: id);

        if (ModelState.IsValid)
        {
            entity.MaTaiKhoan = model.MaTaiKhoan;
            entity.HoTen = model.HoTen.Trim();
            entity.MaPhongBan = model.MaPhongBan;
            entity.NgaySinh = model.NgaySinh;
            entity.SoDienThoai = model.SoDienThoai?.Trim();
            entity.Email = model.Email.Trim();
            entity.NgayVaoLam = model.NgayVaoLam;
            entity.TrangThai = model.TrangThai;

            try
            {
                await db.SaveChangesAsync();
                TempData["Success"] = "Đã cập nhật hồ sơ nhân viên thành công.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (DbUpdateConcurrencyException)
            {
                return NotFound();
            }
        }

        await NapDanhSachChon(model, currentMaTaiKhoan: entity.MaTaiKhoan);
        return View(model);
    }

    /// <summary>
    /// Xóa hồ sơ nhân viên.
    /// Quy định đề bài: Xóa hồ sơ không xóa tài khoản.
    /// Nếu hồ sơ đã có đăng ký đào tạo hoặc điểm danh thì chỉ chuyển sang ngừng hoạt động.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await db.NhanViens.FindAsync(id);
        if (entity is null) return NotFound();

        // Kiểm tra xem nhân viên đã có dữ liệu đăng ký hoặc điểm danh hay chưa
        var coDangKy = await db.DangKyDaoTaos.AnyAsync(dk => dk.MaNhanVien == id);
        var coDiemDanh = await db.DiemDanhs.AnyAsync(dd => dd.MaNhanVien == id);

        if (coDangKy || coDiemDanh)
        {
            // Chuyển sang trạng thái ngừng hoạt động để bảo toàn lịch sử
            entity.TrangThai = false;
            await db.SaveChangesAsync();
            TempData["Success"] = "Hồ sơ nhân viên đã có dữ liệu đào tạo/điểm danh nên đã được chuyển sang trạng thái “Ngừng hoạt động” để giữ lịch sử.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Nếu chưa từng có dữ liệu liên quan thì tiến hành xóa bản ghi hồ sơ
        db.NhanViens.Remove(entity);
        try
        {
            await db.SaveChangesAsync();
            TempData["Success"] = "Đã xóa hồ sơ nhân viên thành công.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Không thể xóa hồ sơ nhân viên vì đang có dữ liệu tham chiếu.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    /// <summary>
    /// Nạp danh sách phòng ban hoạt động và tài khoản hợp lệ vào SelectList.
    /// </summary>
    private async Task NapDanhSachChon(NhanVienViewModel model, int? currentMaTaiKhoan = null)
    {
        // Chỉ chọn phòng ban đang hoạt động (hoặc phòng ban hiện tại của nhân viên)
        model.PhongBans = await db.PhongBans.AsNoTracking()
            .Where(pb => pb.TrangThai || pb.MaPhongBan == model.MaPhongBan)
            .OrderBy(pb => pb.TenPhongBan)
            .Select(pb => new SelectListItem(pb.TenPhongBan, pb.MaPhongBan.ToString()))
            .ToListAsync();

        // Tài khoản: chỉ lấy các tài khoản chưa liên kết với nhân viên nào (hoặc tài khoản hiện tại khi Sửa)
        var taiKhoanDaDung = await db.NhanViens.AsNoTracking()
            .Where(nv => !currentMaTaiKhoan.HasValue || nv.MaTaiKhoan != currentMaTaiKhoan.Value)
            .Select(nv => nv.MaTaiKhoan)
            .ToListAsync();

        model.TaiKhoans = await db.TaiKhoans.AsNoTracking()
            .Where(tk => !taiKhoanDaDung.Contains(tk.MaTaiKhoan))
            .OrderBy(tk => tk.TenDangNhap)
            .Select(tk => new SelectListItem(
                $"{tk.TenDangNhap} ({tk.HoTen} - {tk.VaiTro})",
                tk.MaTaiKhoan.ToString()))
            .ToListAsync();
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của tài khoản liên kết và phòng ban.
    /// </summary>
    private async Task KiemTraDuLieu(NhanVienViewModel model, int? currentMaNhanVien = null)
    {
        // Kiểm tra phòng ban có tồn tại
        if (!await db.PhongBans.AnyAsync(pb => pb.MaPhongBan == model.MaPhongBan))
        {
            ModelState.AddModelError(nameof(model.MaPhongBan), "Phòng ban được chọn không tồn tại.");
        }

        // Kiểm tra tài khoản có tồn tại
        var taiKhoan = await db.TaiKhoans.FindAsync(model.MaTaiKhoan);
        if (taiKhoan is null)
        {
            ModelState.AddModelError(nameof(model.MaTaiKhoan), "Tài khoản được chọn không tồn tại.");
        }
        else
        {
            // Kiểm tra tài khoản đã liên kết với nhân viên khác chưa
            var daGan = await db.NhanViens.AnyAsync(nv => nv.MaTaiKhoan == model.MaTaiKhoan &&
                                                          (!currentMaNhanVien.HasValue || nv.MaNhanVien != currentMaNhanVien.Value));
            if (daGan)
            {
                ModelState.AddModelError(nameof(model.MaTaiKhoan), "Tài khoản này đã được liên kết với một hồ sơ nhân viên khác.");
            }
        }
    }
}
