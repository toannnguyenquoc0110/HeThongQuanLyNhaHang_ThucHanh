using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyNhaHang.Models;

namespace QuanLyNhaHang.Data;

/// <summary>Dữ liệu mẫu: 4 chi nhánh × 15 bàn, 30 nhân viên, thực đơn, kho và 14 ngày lịch sử bán hàng.</summary>
public static class SeedData
{
    public static void Run(AppDbContext db)
    {
        // Seed tài khoản demo độc lập với dữ liệu chi nhánh để vẫn có tài khoản
        // khách hàng khi database đã có dữ liệu từ lần chạy trước.
        EnsureDemoCustomer(db);
        EnsureDemoStaff(db);
        if (db.Branches.Any()) return;
        var rnd = new Random(42);
        var today = DateTime.Today;

        // ---------- Chi nhánh, khu vực, bàn ----------
        var info = new[]
        {
            ("Vị Nhà - Quận 1", "12 Nguyễn Huệ, Quận 1", "028 3822 1001"),
            ("Vị Nhà - Quận 3", "88 Võ Văn Tần, Quận 3", "028 3930 2002"),
            ("Vị Nhà - Thủ Đức", "5 Võ Văn Ngân, TP. Thủ Đức", "028 3722 3003"),
            ("Vị Nhà - Phú Nhuận", "230 Phan Xích Long, Phú Nhuận", "028 3995 4004"),
        };
        foreach (var (n, a, p) in info) db.Branches.Add(new Branch { Name = n, Address = a, Phone = p, OpenHours = "10:00 - 22:30" });
        db.SaveChanges();
        var branches = db.Branches.OrderBy(b => b.Id).ToList();

        var seats = new[] { new[] { 2, 2, 4, 4, 4, 6 }, new[] { 4, 4, 6, 8, 10 }, new[] { 4, 4, 6, 6 } };
        foreach (var b in branches)
        {
            var zones = new[] { "Tầng 1", "Tầng 2", "Sân vườn" }.Select(n => new Zone { BranchId = b.Id, Name = n }).ToList();
            db.Zones.AddRange(zones); db.SaveChanges();
            var k = 1;
            for (var i = 0; i < 3; i++)
                foreach (var s in seats[i]) db.Tables.Add(new DiningTable { BranchId = b.Id, ZoneId = zones[i].Id, Name = "Bàn " + (k++).ToString("00"), Seats = s });
        }
        db.SaveChanges();

        // ---------- Nhân sự: 4 quản lý + 14 phục vụ + 12 bếp = 30 ----------
        string[] last = { "Nguyễn", "Trần", "Lê", "Phạm", "Hoàng", "Võ", "Đặng", "Bùi" };
        string[] mid = { "Văn", "Thị", "Minh", "Quốc", "Ngọc", "Thanh", "Hữu", "Gia" };
        string[] first = { "An", "Bình", "Chi", "Dũng", "Giang", "Hà", "Hiếu", "Khoa", "Lan", "Linh", "Nam", "Phúc", "Quân", "Thảo", "Trang", "Tuấn", "Vy", "Yến" };
        var waiters = new[] { 3, 4, 4, 3 }; var cooks = new[] { 3, 3, 3, 3 };
        var idx = 0;
        Employee Make(Branch b, Role r, decimal baseSalary, decimal hourly)
        {
            idx++;
            return new Employee
            {
                BranchId = b.Id, Role = r, BaseSalary = baseSalary, HourlyRate = hourly,
                Code = "NV" + idx.ToString("000"),
                FullName = $"{last[idx % 8]} {mid[(idx / 2) % 8]} {first[idx % first.Length]}",
                Phone = "09" + rnd.Next(10000000, 99999999), HireDate = today.AddDays(-rnd.Next(60, 900)),
            };
        }
        for (var i = 0; i < branches.Count; i++)
        {
            db.Employees.Add(Make(branches[i], Role.QuanLy, 15000000, 0));
            for (var j = 0; j < waiters[i]; j++) db.Employees.Add(Make(branches[i], Role.PhucVu, 4000000, 22000));
            for (var j = 0; j < cooks[i]; j++) db.Employees.Add(Make(branches[i], Role.Bep, 5000000, 28000));
        }
        db.Shifts.AddRange(new Shift { Name = "Ca sáng", StartTime = "09:00", EndTime = "15:00" },
                           new Shift { Name = "Ca chiều", StartTime = "12:00", EndTime = "18:00" },
                           new Shift { Name = "Ca tối", StartTime = "17:00", EndTime = "23:00" });
        db.SaveChanges();
        var emps = db.Employees.ToList(); var shifts = db.Shifts.OrderBy(s => s.Id).ToList();

        // Lịch ca tuần này + tuần sau (mỗi người nghỉ 1 ngày), chấm công 28 ngày qua
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        foreach (var e in emps.Where(e => e.Role != Role.QuanLy))
        {
            for (var d = 0; d < 14; d++)
            {
                if ((d + e.Id) % 7 == 0) continue;
                db.ShiftAssignments.Add(new ShiftAssignment { EmployeeId = e.Id, ShiftId = shifts[(e.Id + d / 2) % 3].Id, Date = monday.AddDays(d) });
            }
            for (var d = 28; d >= 1; d--)
            {
                if ((d + e.Id) % 7 == 0) continue;
                var day = today.AddDays(-d); var s = 8 + (e.Id + d) % 4;
                db.Attendances.Add(new Attendance { EmployeeId = e.Id, Date = day, CheckIn = day.AddHours(s).AddMinutes(rnd.Next(0, 10)), CheckOut = day.AddHours(s + 6).AddMinutes(rnd.Next(0, 25)) });
            }
        }
        var sample = emps.Where(e => e.Role == Role.PhucVu).Take(2).ToList();
        db.LeaveRequests.Add(new LeaveRequest { EmployeeId = sample[0].Id, FromDate = today.AddDays(3), ToDate = today.AddDays(4), Reason = "Việc gia đình" });
        db.LeaveRequests.Add(new LeaveRequest { EmployeeId = sample[1].Id, FromDate = today.AddDays(-5), ToDate = today.AddDays(-5), Reason = "Khám sức khỏe", Status = "Đã duyệt" });

        // ---------- Kho: nguyên liệu, nhà cung cấp ----------
        var ingDefs = new (string, string, decimal, decimal)[]
        {
            ("Thịt bò","kg",5,260000),("Thịt heo","kg",5,120000),("Gà","kg",5,90000),("Tôm","kg",3,240000),("Cá","kg",4,150000),
            ("Mực","kg",2,200000),("Rau xanh","kg",5,30000),("Cà chua","kg",3,25000),("Gạo","kg",20,22000),("Bún phở","kg",6,28000),
            ("Trứng","quả",60,3500),("Hành tỏi","kg",2,50000),("Nước mắm","lít",3,45000),("Dầu ăn","lít",8,38000),("Sữa tươi","lít",6,32000),
            ("Bột mì","kg",5,18000),("Đường","kg",4,22000),("Trà","kg",1,200000),("Cà phê","kg",2,260000),("Trái cây","kg",5,45000),
            ("Bia","lon",48,14000),("Nước ngọt","lon",48,8000),("Phô mai","kg",1,300000),("Nấm","kg",2,90000),
        };
        foreach (var (n, u, m, c) in ingDefs) db.Ingredients.Add(new Ingredient { Name = n, Unit = u, MinLevel = m, Cost = c });
        db.Suppliers.AddRange(
            new Supplier { Name = "Thực phẩm Sạch Sài Gòn", Phone = "028 3811 2233", Address = "Chợ đầu mối Hóc Môn", Note = "Thịt, hải sản - giao 6h sáng" },
            new Supplier { Name = "Nông sản Đà Lạt Xanh", Phone = "0263 355 6677", Address = "Đà Lạt", Note = "Rau củ, trái cây" },
            new Supplier { Name = "Đại lý Gạo & Khô Minh Phát", Phone = "028 3844 9900", Address = "Quận 6", Note = "Gạo, bún, dầu ăn, gia vị" },
            new Supplier { Name = "Nhà phân phối Đồ uống Tân Việt", Phone = "028 3777 1212", Address = "Quận Tân Bình", Note = "Bia, nước ngọt, cà phê, trà" });
        db.SaveChanges();
        var ings = db.Ingredients.ToList(); var sups = db.Suppliers.ToList();

        foreach (var b in branches)
            foreach (var i in ings)
            {
                var qty = Math.Round(i.MinLevel * (decimal)(0.7 + rnd.NextDouble() * 2.6), QuanLyNhaHang.Helpers.Fmt.IsCount(i.Unit) ? 0 : 1);
                if (rnd.Next(12) == 0) qty = Math.Round(i.MinLevel * 0.4m, QuanLyNhaHang.Helpers.Fmt.IsCount(i.Unit) ? 0 : 1);   // vài món sắp hết để thấy cảnh báo
                db.BranchStocks.Add(new BranchStock { BranchId = b.Id, IngredientId = i.Id, Quantity = qty, ExpiryDate = i.Unit == "kg" && i.Cost < 100000 || i.Name == "Sữa tươi" ? today.AddDays(rnd.Next(2, 14)) : null });
                db.StockMovements.Add(new StockMovement { BranchId = b.Id, IngredientId = i.Id, Qty = qty, Type = "Tồn đầu kỳ", Ref = "SEED-OPENING", Note = "Tồn kho ban đầu của dữ liệu demo", OperationKey = $"seed-opening:{b.Id}:{i.Id}" });
            }

        // ---------- Thực đơn + công thức ----------
        var cats = new[] { "Khai vị", "Món chính", "Lẩu & nướng", "Cơm & mì", "Tráng miệng", "Đồ uống" };
        for (var i = 0; i < cats.Length; i++) db.Categories.Add(new MenuCategory { Name = cats[i], SortOrder = i + 1 });
        db.SaveChanges();
        var catIds = db.Categories.OrderBy(c => c.SortOrder).Select(c => c.Id).ToList();

        var menuDefs = new[]
        {
            "0|Gỏi cuốn tôm thịt|🥬|45000|18000|8|Hải sản|Tôm:0.05;Thịt heo:0.04;Rau xanh:0.08;Bún phở:0.05",
            "0|Chả giò chiên|🥟|55000|20000|10|Gluten, trứng|Thịt heo:0.1;Dầu ăn:0.05;Hành tỏi:0.01",
            "0|Gỏi ngó sen tôm thịt|🥗|65000|25000|8|Hải sản, đậu phộng|Tôm:0.06;Rau xanh:0.1",
            "0|Súp nấm gà|🍲|48000|15000|8|Trứng|Gà:0.06;Nấm:0.05;Trứng:1",
            "1|Bò lúc lắc|🥩|145000|62000|15||Thịt bò:0.2;Hành tỏi:0.02;Dầu ăn:0.03",
            "1|Gà nướng mật ong|🍗|135000|50000|20||Gà:0.4;Đường:0.02",
            "1|Cá diêu hồng chiên xù|🐟|165000|70000|20|Cá, gluten|Cá:0.6;Dầu ăn:0.1;Bột mì:0.05",
            "1|Tôm rang me|🦐|155000|75000|15|Hải sản|Tôm:0.25;Đường:0.03",
            "1|Mực xào sa tế|🦑|125000|55000|12|Hải sản, đậu phộng|Mực:0.25;Hành tỏi:0.02",
            "1|Sườn nướng BBQ|🍖|149000|60000|20||Thịt heo:0.3",
            "1|Rau muống xào tỏi|🥬|55000|14000|6||Rau xanh:0.3;Hành tỏi:0.02",
            "2|Lẩu Thái hải sản|🍲|389000|160000|25|Hải sản|Tôm:0.3;Mực:0.2;Cá:0.3;Rau xanh:0.4;Cà chua:0.2",
            "2|Lẩu bò nhúng dấm|🍲|359000|150000|25||Thịt bò:0.4;Rau xanh:0.4;Cà chua:0.2",
            "2|Bò nướng lá lốt|🍢|119000|50000|15||Thịt bò:0.2",
            "3|Cơm chiên dương châu|🍚|69000|22000|10|Trứng, hải sản|Gạo:0.2;Trứng:1;Tôm:0.04",
            "3|Phở bò tái|🍜|65000|24000|8||Bún phở:0.2;Thịt bò:0.1",
            "3|Mì xào hải sản|🍝|85000|35000|10|Hải sản, gluten|Bún phở:0.2;Tôm:0.06;Mực:0.06",
            "3|Cơm gà xối mỡ|🍗|75000|28000|12||Gạo:0.2;Gà:0.25;Dầu ăn:0.1",
            "4|Chè khúc bạch|🍮|38000|12000|4|Sữa|Sữa tươi:0.1;Đường:0.03;Trái cây:0.05",
            "4|Bánh flan|🍮|32000|9000|3|Sữa, trứng|Trứng:1;Sữa tươi:0.1;Đường:0.02",
            "4|Trái cây dĩa|🍉|59000|25000|5||Trái cây:0.4",
            "5|Trà đào cam sả|🍑|42000|11000|3||Trà:0.01;Trái cây:0.08;Đường:0.02",
            "5|Cà phê sữa đá|☕|35000|8000|3|Sữa|Cà phê:0.025;Sữa tươi:0.05",
            "5|Nước ép cam|🍊|45000|15000|3||Trái cây:0.4",
            "5|Bia Tiger|🍺|28000|14000|1||Bia:1",
            "5|Nước ngọt|🥤|20000|8000|1||Nước ngọt:1",
            "5|Sinh tố bơ|🥑|48000|17000|4|Sữa|Trái cây:0.25;Sữa tươi:0.1;Đường:0.02",
        };
        foreach (var line in menuDefs)
        {
            var p = line.Split('|');
            var m = new MenuItem { CategoryId = catIds[int.Parse(p[0])], Name = p[1], Icon = p[2], Price = decimal.Parse(p[3]), Cost = 0m, PrepMinutes = int.Parse(p[5]), Allergens = p[6], Description = "Được chế biến tươi tại nhà hàng Vị Nhà." };
            foreach (var part in p[7].Split(';'))
            {
                var kv = part.Split(':');
                m.Recipes.Add(new RecipeLine { IngredientId = ings.First(i => i.Name == kv[0]).Id, Qty = decimal.Parse(kv[1], System.Globalization.CultureInfo.InvariantCulture) });
            }
            db.MenuItems.Add(m);
        }
        db.SaveChanges();
        var menu = db.MenuItems.Include(x => x.Recipes).ThenInclude(x => x.Ingredient).ToList();
        // Giá vốn được tính từ định lượng công thức × giá nguyên liệu; không lấy cột giá vốn nhập tay trong dữ liệu mẫu.
        foreach (var item in menu)
        {
            item.Cost = item.Recipes.Count > 0 && item.Recipes.All(r => r.Qty > 0 && r.Ingredient != null && r.Ingredient.Cost > 0)
                ? item.Recipes.Sum(r => r.Qty * r.Ingredient.Cost) : 0m;
        }
        db.SaveChanges();
        int M(string n) => menu.First(x => x.Name == n).Id;

        var c1 = new Combo { Name = "Combo gia đình (4 người)", Price = 599000 };
        foreach (var n in new[] { "Bò lúc lắc", "Gà nướng mật ong", "Rau muống xào tỏi", "Cơm chiên dương châu", "Trái cây dĩa" }) c1.Items.Add(new ComboItem { MenuItemId = M(n) });
        var c2 = new Combo { Name = "Combo đôi", Price = 289000 };
        foreach (var n in new[] { "Gỏi cuốn tôm thịt", "Mực xào sa tế", "Cơm chiên dương châu", "Trà đào cam sả" }) c2.Items.Add(new ComboItem { MenuItemId = M(n) });
        db.Combos.AddRange(c1, c2);
        db.Promotions.AddRange(
            new Promotion { Name = "Happy hour đồ uống", Percent = 20, StartTime = "14:00", EndTime = "17:00", CategoryId = catIds[5] },
            new Promotion { Name = "Bữa trưa văn phòng", Percent = 10, StartTime = "11:00", EndTime = "13:30", CategoryId = catIds[3] });
        db.Coupons.AddRange(
            new Coupon { Code = "CHAOBAN", Percent = 10, MinTotal = 200000, ExpireDate = today.AddMonths(3) },
            new Coupon { Code = "GIAM50K", Amount = 50000, MinTotal = 300000, ExpireDate = today.AddMonths(1) },
            new Coupon { Code = "KHACHVIP", Percent = 15, MinTotal = 500000 });
        db.SaveChanges();

        // ---------- Nhập hàng + chi phí ----------
        foreach (var b in branches)
        {
            var po1 = new PurchaseOrder { BranchId = b.Id, SupplierId = sups[0].Id, Code = "PO" + b.Id + "A", Date = today.AddDays(-5), Status = "Đã nhận", Note = "Thịt và hải sản đầu tuần" };
            po1.Lines.Add(new PurchaseLine { IngredientId = ings[0].Id, Qty = 20, UnitCost = 255000 });
            po1.Lines.Add(new PurchaseLine { IngredientId = ings[3].Id, Qty = 10, UnitCost = 238000 });
            var po2 = new PurchaseOrder { BranchId = b.Id, SupplierId = sups[1].Id, Code = "PO" + b.Id + "B", Date = today, Note = "Rau củ giao sáng mai" };
            po2.Lines.Add(new PurchaseLine { IngredientId = ings[6].Id, Qty = 25, UnitCost = 30000, ExpiryDate = today.AddDays(5) });
            po2.Lines.Add(new PurchaseLine { IngredientId = ings[19].Id, Qty = 15, UnitCost = 45000, ExpiryDate = today.AddDays(7) });
            db.PurchaseOrders.AddRange(po1, po2);

            var cats2 = new[] { "Điện nước", "Mặt bằng", "Marketing", "Sửa chữa", "Nguyên liệu", "Vệ sinh" };
            for (var d = 0; d < 12; d++)
                db.Expenses.Add(new Expense { BranchId = b.Id, Date = today.AddDays(-rnd.Next(0, 28)), Category = cats2[rnd.Next(cats2.Length)], Amount = rnd.Next(3, 40) * 100000, Note = "Chi phí vận hành" });
        }
        db.SaveChanges();

        // ---------- Lịch sử bán hàng 14 ngày ----------
        var hours = new[] { 11, 12, 12, 12, 13, 18, 19, 19, 19, 20, 20, 21 };
        var methods = new[] { "Tiền mặt", "Tiền mặt", "Chuyển khoản", "Thẻ", "Ví MoMo" };
        var now = DateTime.Now;
        foreach (var b in branches)
        {
            var tables = db.Tables.Where(t => t.BranchId == b.Id).ToList();
            var ws = emps.Where(e => e.BranchId == b.Id && e.Role == Role.PhucVu).ToList();
            for (var d = 14; d >= 0; d--)
            {
                var day = today.AddDays(-d);
                for (var n = rnd.Next(7, 14); n > 0; n--)
                {
                    var opened = day.AddHours(hours[rnd.Next(hours.Length)]).AddMinutes(rnd.Next(0, 55));
                    if (opened > now.AddMinutes(-90)) continue;
                    var o = new Order
                    {
                        BranchId = b.Id, TableId = tables[rnd.Next(tables.Count)].Id, EmployeeId = ws[rnd.Next(ws.Count)].Id,
                        Pax = rnd.Next(2, 7), OpenedAt = opened, ClosedAt = opened.AddMinutes(rnd.Next(55, 95)), Status = St.Paid,
                    };
                    for (var k = rnd.Next(2, 6); k > 0; k--)
                    {
                        var m = menu[rnd.Next(menu.Count)];
                        o.Lines.Add(new OrderLine { MenuItemId = m.Id, Name = m.Name, Qty = rnd.Next(1, 4), Price = m.Price, Cost = m.Cost, PrepMinutes = m.PrepMinutes, Status = St.Served, SentAt = opened.AddMinutes(3), DoneAt = opened.AddMinutes(15), ServedAt = opened.AddMinutes(17) });
                    }
                    var sub = o.Lines.Sum(l => l.Qty * l.Price);
                    o.Total = sub + Math.Round(sub * b.ServiceRate) + Math.Round(sub * b.VatRate);
                    o.Payments.Add(new Payment { Method = methods[rnd.Next(methods.Length)], Amount = o.Total, PaidAt = o.ClosedAt.Value });
                    db.Orders.Add(o);
                }
            }
        }
        db.SaveChanges();

        // ---------- Đơn đang mở (để thấy màn hình bếp và sơ đồ bàn) + đặt bàn ----------
        foreach (var b in branches.Take(2))
        {
            var tables = db.Tables.Where(t => t.BranchId == b.Id).OrderBy(t => t.Id).ToList();
            var w = emps.First(e => e.BranchId == b.Id && e.Role == Role.PhucVu);
            var o1 = new Order { BranchId = b.Id, TableId = tables[2].Id, EmployeeId = w.Id, Pax = 4, CustomerName = "Anh Hải", OpenedAt = now.AddMinutes(-25) };
            void L(Order o, string n, int q, string st, int minsAgo, bool prio = false)
            {
                var m = menu.First(x => x.Name == n);
                o.Lines.Add(new OrderLine { MenuItemId = m.Id, Name = n, Qty = q, Price = m.Price, Cost = m.Cost, PrepMinutes = m.PrepMinutes, Status = st, Priority = prio, SentAt = st == St.New ? null : now.AddMinutes(-minsAgo), StartedAt = st == St.Cook || st == St.Done ? now.AddMinutes(-minsAgo + 2) : null, DoneAt = st == St.Done ? now.AddMinutes(-2) : null });
            }
            L(o1, "Gỏi cuốn tôm thịt", 2, St.Served, 22); L(o1, "Bò lúc lắc", 1, St.Cook, 12); L(o1, "Lẩu Thái hải sản", 1, St.Wait, 4, true); L(o1, "Bia Tiger", 4, St.Done, 10);
            var o2 = new Order { BranchId = b.Id, TableId = tables[8].Id, EmployeeId = w.Id, Pax = 6, CustomerName = "Công ty ABC", OpenedAt = now.AddMinutes(-8) };
            L(o2, "Cá diêu hồng chiên xù", 1, St.Wait, 6); L(o2, "Rau muống xào tỏi", 2, St.Wait, 6); L(o2, "Nước ngọt", 6, St.New, 0);
            db.Orders.AddRange(o1, o2);
            db.Reservations.AddRange(
                new Reservation { BranchId = b.Id, TableId = tables[4].Id, CustomerName = "Chị Mai", Phone = "0903 111 222", Date = today, Time = now.AddMinutes(40).ToString("HH:mm"), Pax = 4, Note = "Sinh nhật, cần bánh kem", Deposit = 200000 },
                new Reservation { BranchId = b.Id, TableId = tables[11].Id, CustomerName = "Anh Tuấn", Phone = "0988 333 444", Date = today, Time = "19:30", Pax = 8, Note = "Họp lớp" },
                new Reservation { BranchId = b.Id, TableId = tables[5].Id, CustomerName = "Cô Lan", Phone = "0912 555 666", Date = today.AddDays(1), Time = "12:00", Pax = 6 });
        }
        db.AuditLogs.Add(new AuditLog { BranchId = 1, Actor = "Hệ thống", Action = "Khởi tạo", Detail = "Nạp dữ liệu mẫu 4 chi nhánh và tài khoản khách hàng demo" });
        db.SaveChanges();
    }

    private static void EnsureDemoCustomer(AppDbContext db)
    {
        const string email = "khach@demo.vn";
        if (db.CustomerAccounts.Any(c => c.Email == email)) return;

        var customer = new CustomerAccount
        {
            FullName = "Khách hàng Demo",
            Email = email,
            Phone = "0900000000",
            DefaultAddress = "12 Nguyễn Huệ, Quận 1, TP. Hồ Chí Minh",
            Active = true,
            CreatedAt = DateTime.Now
        };
        customer.PasswordHash = new PasswordHasher<CustomerAccount>()
            .HashPassword(customer, "Demo@12345");
        db.CustomerAccounts.Add(customer);
        db.SaveChanges();
    }


    private static void EnsureDemoStaff(AppDbContext db)
    {
        // Tạo tài khoản demo nếu chưa có; không ghi đè mật khẩu/tài khoản đã tồn tại.
        var branchId = db.Branches.OrderBy(b => b.Id).Select(b => (int?)b.Id).FirstOrDefault() ?? 1;
        var definitions = new[]
        {
            (Email: "admin@demo.vn", Name: "Quản lý Demo", Role: Role.QuanLy),
            (Email: "phucvu@demo.vn", Name: "Nhân viên phục vụ Demo", Role: Role.PhucVu),
            (Email: "bep@demo.vn", Name: "Nhân viên bếp Demo", Role: Role.Bep)
        };
        foreach (var item in definitions)
        {
            var email = item.Email.ToLowerInvariant();
            if (db.StaffAccounts.Any(x => x.Email.ToLower() == email)) continue;
            var account = new StaffAccount
            {
                Email = email, FullName = item.Name, Role = item.Role,
                BranchId = branchId, Active = true, CreatedAt = DateTime.Now
            };
            account.PasswordHash = new PasswordHasher<StaffAccount>().HashPassword(account, "Demo@12345");
            db.StaffAccounts.Add(account);
        }
        db.SaveChanges();
    }

}
