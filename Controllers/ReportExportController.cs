using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VDK_BookRental.Data;
using VDK_BookRental.Models;

namespace VDK_BookRental.Controllers
{
    public class ReportExportController : Controller
    {
        private readonly AppDbContext _context;

        public ReportExportController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // XUẤT BÁO CÁO DOANH THU DẠNG EXCEL ĐƯỢC ĐỊNH DẠNG SẴN
        //
        // URL ví dụ:
        // /ReportExport/RevenueExcel?startDate=2026-07-01&endDate=2026-07-31
        //
        // Chỉ tính giao dịch Paid hoặc Completed.
        // =========================================================
        [HttpGet]
        public async Task<IActionResult> RevenueExcel(
            DateTime? startDate,
            DateTime? endDate)
        {
            var accessResult = CheckStaffAccess();

            if (accessResult != null)
            {
                return accessResult;
            }

            var today = DateTime.Today;

            var fromDate =
                startDate?.Date
                ?? new DateTime(
                    today.Year,
                    today.Month,
                    1
                );

            var toDate =
                endDate?.Date
                ?? today;

            if (fromDate > toDate)
            {
                TempData["ErrorMessage"] =
                    "Ngày bắt đầu không được lớn hơn ngày kết thúc.";

                return RedirectToAction(
                    "Index",
                    "Report"
                );
            }

            var toDateExclusive =
                toDate.AddDays(1);

            var rentals = await _context.Rentals
                .AsNoTracking()
                .Where(rental =>
                    rental.RentalDate >= fromDate &&
                    rental.RentalDate < toDateExclusive &&
                    rental.Payment != null &&
                    (
                        rental.Payment.Status == "Paid" ||
                        rental.Payment.Status == "Completed"
                    ))
                .Include(rental =>
                    rental.User)
                .Include(rental =>
                    rental.Payment)
                .Include(rental =>
                    rental.RentalDetails)
                    .ThenInclude(detail =>
                        detail.Book)
                .OrderBy(rental =>
                    rental.RentalDate)
                .ThenBy(rental =>
                    rental.Id)
                .ToListAsync();

            using var workbook = new XLWorkbook();

            CreateSummarySheet(
                workbook,
                rentals,
                fromDate,
                toDate
            );

            CreateRevenueDetailSheet(
                workbook,
                rentals,
                fromDate,
                toDate
            );

            using var stream =
                new MemoryStream();

            workbook.SaveAs(stream);

            var fileName =
                $"BaoCaoDoanhThu_" +
                $"{fromDate:yyyyMMdd}_" +
                $"{toDate:yyyyMMdd}.xlsx";

            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

        // =========================================================
        // SHEET 1: TỔNG HỢP
        // =========================================================
        private static void CreateSummarySheet(
            XLWorkbook workbook,
            IReadOnlyCollection<Rental> rentals,
            DateTime fromDate,
            DateTime toDate)
        {
            var worksheet =
                workbook.Worksheets.Add(
                    "Tổng hợp"
                );

            worksheet.ShowGridLines =
                false;

            var totalRevenue =
                rentals.Sum(rental =>
                    rental.TotalAmount);

            var totalQuantity =
                rentals.Sum(rental =>
                    rental.RentalDetails.Sum(
                        detail => detail.Quantity
                    ));

            var averageRevenue =
                rentals.Count == 0
                    ? 0
                    : totalRevenue / rentals.Count;

            // Tiêu đề.
            worksheet.Range("A1:H2")
                .Merge();

            worksheet.Cell("A1").Value =
                "BÁO CÁO DOANH THU THUÊ SÁCH";

            worksheet.Range("A1:H2").Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml("#0D6EFD");

            worksheet.Range("A1:H2").Style
                .Font.FontColor =
                    XLColor.White;

            worksheet.Range("A1:H2").Style
                .Font.Bold =
                    true;

            worksheet.Range("A1:H2").Style
                .Font.FontSize =
                    20;

            worksheet.Range("A1:H2").Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            worksheet.Range("A1:H2").Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            worksheet.Row(1).Height = 28;
            worksheet.Row(2).Height = 28;

            worksheet.Range("A3:H3")
                .Merge();

            worksheet.Cell("A3").Value =
                $"Từ ngày {fromDate:dd/MM/yyyy} đến ngày {toDate:dd/MM/yyyy}";

            worksheet.Range("A3:H3").Style
                .Font.FontColor =
                    XLColor.FromHtml("#475569");

            worksheet.Range("A3:H3").Style
                .Font.Italic =
                    true;

            worksheet.Range("A3:H3").Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            worksheet.Row(3).Height = 24;

            // KPI cards.
            CreateKpiCard(
                worksheet,
                "A5:B7",
                "TỔNG DOANH THU",
                totalRevenue,
                "#D1E7DD",
                "#146C43",
                isCurrency: true
            );

            CreateKpiCard(
                worksheet,
                "C5:D7",
                "GIAO DỊCH",
                rentals.Count,
                "#CFE2FF",
                "#084298"
            );

            CreateKpiCard(
                worksheet,
                "E5:F7",
                "SÁCH ĐÃ THUÊ",
                totalQuantity,
                "#FFF3CD",
                "#997404"
            );

            CreateKpiCard(
                worksheet,
                "G5:H7",
                "TRUNG BÌNH / ĐƠN",
                averageRevenue,
                "#EDE7F6",
                "#6F42C1",
                isCurrency: true
            );

            // Top sách.
            worksheet.Range("A9:E9")
                .Merge();

            worksheet.Cell("A9").Value =
                "TOP 5 SÁCH ĐƯỢC THUÊ NHIỀU NHẤT";

            StyleSectionHeader(
                worksheet.Range("A9:E9")
            );

            var topBooks = rentals
                .SelectMany(rental =>
                    rental.RentalDetails)
                .Where(detail =>
                    detail.Book != null)
                .GroupBy(detail =>
                    new
                    {
                        detail.BookId,
                        detail.Book!.Title
                    })
                .Select(group =>
                    new
                    {
                        group.Key.BookId,
                        group.Key.Title,
                        Quantity =
                            group.Sum(detail =>
                                detail.Quantity),
                        Revenue =
                            group.Sum(detail =>
                                detail.SubTotal)
                    })
                .OrderByDescending(item =>
                    item.Quantity)
                .ThenByDescending(item =>
                    item.Revenue)
                .Take(5)
                .ToList();

            var topHeaderRow = 10;

            var topHeaders =
                new[]
                {
                    "Hạng",
                    "Mã sách",
                    "Tên sách",
                    "Số lượng thuê",
                    "Doanh thu"
                };

            for (var column = 1;
                 column <= topHeaders.Length;
                 column++)
            {
                worksheet.Cell(
                    topHeaderRow,
                    column
                ).Value =
                    topHeaders[column - 1];
            }

            StyleTableHeader(
                worksheet.Range(
                    topHeaderRow,
                    1,
                    topHeaderRow,
                    5
                )
            );

            var currentRow =
                topHeaderRow + 1;

            if (topBooks.Count > 0)
            {
                for (var index = 0;
                     index < topBooks.Count;
                     index++)
                {
                    var item =
                        topBooks[index];

                    worksheet.Cell(
                        currentRow,
                        1
                    ).Value =
                        index + 1;

                    worksheet.Cell(
                        currentRow,
                        2
                    ).Value =
                        $"BOOK-{item.BookId:D3}";

                    worksheet.Cell(
                        currentRow,
                        3
                    ).Value =
                        item.Title;

                    worksheet.Cell(
                        currentRow,
                        4
                    ).Value =
                        item.Quantity;

                    worksheet.Cell(
                        currentRow,
                        5
                    ).Value =
                        item.Revenue;

                    worksheet.Cell(
                        currentRow,
                        5
                    ).Style.NumberFormat.Format =
                        "#,##0 \"VNĐ\"";

                    StyleDataRow(
                        worksheet.Range(
                            currentRow,
                            1,
                            currentRow,
                            5
                        ),
                        currentRow % 2 == 0
                    );

                    currentRow++;
                }
            }
            else
            {
                worksheet.Range(
                    currentRow,
                    1,
                    currentRow,
                    5
                ).Merge();

                worksheet.Cell(
                    currentRow,
                    1
                ).Value =
                    "Chưa có dữ liệu trong khoảng thời gian đã chọn.";

                worksheet.Range(
                    currentRow,
                    1,
                    currentRow,
                    5
                ).Style
                    .Alignment.Horizontal =
                        XLAlignmentHorizontalValues.Center;

                worksheet.Range(
                    currentRow,
                    1,
                    currentRow,
                    5
                ).Style
                    .Font.FontColor =
                        XLColor.FromHtml("#64748B");

                currentRow++;
            }

            // Doanh thu theo phương thức thanh toán.
            var paymentStartRow =
                currentRow + 2;

            worksheet.Range(
                paymentStartRow,
                1,
                paymentStartRow,
                5
            ).Merge();

            worksheet.Cell(
                paymentStartRow,
                1
            ).Value =
                "DOANH THU THEO PHƯƠNG THỨC THANH TOÁN";

            StyleSectionHeader(
                worksheet.Range(
                    paymentStartRow,
                    1,
                    paymentStartRow,
                    5
                )
            );

            var paymentHeadersRow =
                paymentStartRow + 1;

            worksheet.Cell(
                paymentHeadersRow,
                1
            ).Value =
                "Phương thức";

            worksheet.Cell(
                paymentHeadersRow,
                2
            ).Value =
                "Số giao dịch";

            worksheet.Cell(
                paymentHeadersRow,
                3
            ).Value =
                "Doanh thu";

            worksheet.Cell(
                paymentHeadersRow,
                4
            ).Value =
                "Tỷ trọng";

            worksheet.Cell(
                paymentHeadersRow,
                5
            ).Value =
                "Ghi chú";

            StyleTableHeader(
                worksheet.Range(
                    paymentHeadersRow,
                    1,
                    paymentHeadersRow,
                    5
                )
            );

            var payments = rentals
                .GroupBy(rental =>
                    string.IsNullOrWhiteSpace(
                        rental.Payment?.PaymentMethod)
                        ? "Chưa xác định"
                        : rental.Payment!.PaymentMethod)
                .Select(group =>
                    new
                    {
                        Method = group.Key,
                        Count = group.Count(),
                        Revenue =
                            group.Sum(rental =>
                                rental.TotalAmount)
                    })
                .OrderByDescending(item =>
                    item.Revenue)
                .ToList();

            var paymentRow =
                paymentHeadersRow + 1;

            if (payments.Count > 0)
            {
                foreach (var payment in payments)
                {
                    worksheet.Cell(
                        paymentRow,
                        1
                    ).Value =
                        payment.Method;

                    worksheet.Cell(
                        paymentRow,
                        2
                    ).Value =
                        payment.Count;

                    worksheet.Cell(
                        paymentRow,
                        3
                    ).Value =
                        payment.Revenue;

                    worksheet.Cell(
                        paymentRow,
                        3
                    ).Style.NumberFormat.Format =
                        "#,##0 \"VNĐ\"";

                    worksheet.Cell(
                        paymentRow,
                        4
                    ).Value =
                        totalRevenue == 0
                            ? 0
                            : payment.Revenue
                              / totalRevenue;

                    worksheet.Cell(
                        paymentRow,
                        4
                    ).Style.NumberFormat.Format =
                        "0.00%";

                    worksheet.Cell(
                        paymentRow,
                        5
                    ).Value =
                        payment.Count == 1
                            ? "1 giao dịch"
                            : $"{payment.Count} giao dịch";

                    StyleDataRow(
                        worksheet.Range(
                            paymentRow,
                            1,
                            paymentRow,
                            5
                        ),
                        paymentRow % 2 == 0
                    );

                    paymentRow++;
                }
            }
            else
            {
                worksheet.Range(
                    paymentRow,
                    1,
                    paymentRow,
                    5
                ).Merge();

                worksheet.Cell(
                    paymentRow,
                    1
                ).Value =
                    "Chưa có dữ liệu thanh toán.";

                worksheet.Range(
                    paymentRow,
                    1,
                    paymentRow,
                    5
                ).Style
                    .Alignment.Horizontal =
                        XLAlignmentHorizontalValues.Center;
            }

            worksheet.Column(1).Width = 12;
            worksheet.Column(2).Width = 15;
            worksheet.Column(3).Width = 35;
            worksheet.Column(4).Width = 18;
            worksheet.Column(5).Width = 20;
            worksheet.Column(6).Width = 15;
            worksheet.Column(7).Width = 18;
            worksheet.Column(8).Width = 18;

            var usedRange =
                worksheet.RangeUsed();

            if (usedRange != null)
            {
                usedRange.Style.Font.FontName =
                    "Calibri";

                usedRange.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;
            }

            worksheet.SheetView
                .FreezeRows(3);

            worksheet.PageSetup
                .PageOrientation =
                    XLPageOrientation.Landscape;
        }

        // =========================================================
        // SHEET 2: CHI TIẾT GIAO DỊCH
        // =========================================================
        private static void CreateRevenueDetailSheet(
            XLWorkbook workbook,
            IReadOnlyCollection<Rental> rentals,
            DateTime fromDate,
            DateTime toDate)
        {
            var worksheet =
                workbook.Worksheets.Add(
                    "Chi tiết giao dịch"
                );

            worksheet.ShowGridLines =
                false;

            const int columnCount = 14;

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Merge();

            worksheet.Cell(1, 1).Value =
                "CHI TIẾT GIAO DỊCH THUÊ SÁCH";

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml("#111827");

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Style
                .Font.FontColor =
                    XLColor.White;

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Style
                .Font.Bold =
                    true;

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Style
                .Font.FontSize =
                    19;

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            worksheet.Range(
                1,
                1,
                2,
                columnCount
            ).Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            worksheet.Range(
                3,
                1,
                3,
                columnCount
            ).Merge();

            worksheet.Cell(3, 1).Value =
                $"Kỳ báo cáo: {fromDate:dd/MM/yyyy} - {toDate:dd/MM/yyyy}";

            worksheet.Range(
                3,
                1,
                3,
                columnCount
            ).Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            worksheet.Range(
                3,
                1,
                3,
                columnCount
            ).Style
                .Font.Italic =
                    true;

            worksheet.Range(
                3,
                1,
                3,
                columnCount
            ).Style
                .Font.FontColor =
                    XLColor.FromHtml("#475569");

            // Dòng tổng quan.
            worksheet.Range("A5:C6")
                .Merge();

            worksheet.Cell("A5").Value =
                $"Tổng giao dịch\n{rentals.Count}";

            StyleMiniSummary(
                worksheet.Range("A5:C6"),
                "#CFE2FF",
                "#084298"
            );

            worksheet.Range("D5:F6")
                .Merge();

            worksheet.Cell("D5").Value =
                $"Tổng sách thuê\n" +
                $"{rentals.Sum(rental => rental.RentalDetails.Sum(detail => detail.Quantity))}";

            StyleMiniSummary(
                worksheet.Range("D5:F6"),
                "#FFF3CD",
                "#997404"
            );

            worksheet.Range("G5:J6")
                .Merge();

            worksheet.Cell("G5").Value =
                $"Tổng doanh thu\n" +
                $"{rentals.Sum(rental => rental.TotalAmount):N0} VNĐ";

            StyleMiniSummary(
                worksheet.Range("G5:J6"),
                "#D1E7DD",
                "#146C43"
            );

            worksheet.Range("K5:N6")
                .Merge();

            worksheet.Cell("K5").Value =
                $"Xuất lúc\n{DateTime.Now:dd/MM/yyyy HH:mm}";

            StyleMiniSummary(
                worksheet.Range("K5:N6"),
                "#EDE7F6",
                "#6F42C1"
            );

            var headerRow = 8;

            var headers =
                new[]
                {
                    "Mã đơn thuê",
                    "Khách hàng",
                    "Tên đăng nhập",
                    "Email",
                    "Số điện thoại",
                    "Ngày thuê",
                    "Ngày trả dự kiến",
                    "Trạng thái đơn",
                    "Mã thanh toán",
                    "Phương thức",
                    "Trạng thái thanh toán",
                    "Tổng SL",
                    "Danh sách sách",
                    "Tổng tiền"
                };

            for (var column = 1;
                 column <= headers.Length;
                 column++)
            {
                worksheet.Cell(
                    headerRow,
                    column
                ).Value =
                    headers[column - 1];
            }

            StyleTableHeader(
                worksheet.Range(
                    headerRow,
                    1,
                    headerRow,
                    columnCount
                )
            );

            var dataRow =
                headerRow + 1;

            foreach (var rental in rentals)
            {
                var customerName =
                    !string.IsNullOrWhiteSpace(
                        rental.User?.FullName)
                        ? rental.User.FullName
                        : rental.User?.UserName
                          ?? "Không xác định";

                var totalQuantity =
                    rental.RentalDetails.Sum(
                        detail => detail.Quantity
                    );

                var bookList =
                    string.Join(
                        Environment.NewLine,
                        rental.RentalDetails.Select(detail =>
                        {
                            var title =
                                detail.Book?.Title
                                ?? "Sách không tồn tại";

                            return
                                $"• {title} x{detail.Quantity} " +
                                $"({detail.RentalDays} ngày)";
                        })
                    );

                worksheet.Cell(
                    dataRow,
                    1
                ).Value =
                    $"RENT-{rental.Id:D4}";

                worksheet.Cell(
                    dataRow,
                    2
                ).Value =
                    customerName;

                worksheet.Cell(
                    dataRow,
                    3
                ).Value =
                    rental.User?.UserName
                    ?? string.Empty;

                worksheet.Cell(
                    dataRow,
                    4
                ).Value =
                    rental.User?.Email
                    ?? string.Empty;

                worksheet.Cell(
                    dataRow,
                    5
                ).Value =
                    rental.User?.Phone
                    ?? string.Empty;

                worksheet.Cell(
                    dataRow,
                    6
                ).Value =
                    rental.RentalDate;

                worksheet.Cell(
                    dataRow,
                    7
                ).Value =
                    rental.ReturnDate;

                worksheet.Cell(
                    dataRow,
                    8
                ).Value =
                    GetRentalStatusText(
                        rental.Status
                    );

                worksheet.Cell(
                    dataRow,
                    9
                ).Value =
                    rental.Payment != null
                        ? $"PAY-{rental.Payment.Id:D4}"
                        : string.Empty;

                worksheet.Cell(
                    dataRow,
                    10
                ).Value =
                    rental.Payment?.PaymentMethod
                    ?? string.Empty;

                worksheet.Cell(
                    dataRow,
                    11
                ).Value =
                    GetPaymentStatusText(
                        rental.Payment?.Status
                    );

                worksheet.Cell(
                    dataRow,
                    12
                ).Value =
                    totalQuantity;

                worksheet.Cell(
                    dataRow,
                    13
                ).Value =
                    bookList;

                worksheet.Cell(
                    dataRow,
                    14
                ).Value =
                    rental.TotalAmount;

                worksheet.Cell(
                    dataRow,
                    6
                ).Style.NumberFormat.Format =
                    "dd/MM/yyyy";

                worksheet.Cell(
                    dataRow,
                    7
                ).Style.NumberFormat.Format =
                    "dd/MM/yyyy";

                worksheet.Cell(
                    dataRow,
                    14
                ).Style.NumberFormat.Format =
                    "#,##0 \"VNĐ\"";

                worksheet.Cell(
                    dataRow,
                    13
                ).Style.Alignment.WrapText =
                    true;

                worksheet.Row(
                    dataRow
                ).Height =
                    Math.Max(
                        28,
                        rental.RentalDetails.Count * 19
                    );

                StyleDataRow(
                    worksheet.Range(
                        dataRow,
                        1,
                        dataRow,
                        columnCount
                    ),
                    dataRow % 2 == 0
                );

                dataRow++;
            }

            if (rentals.Count == 0)
            {
                worksheet.Range(
                    dataRow,
                    1,
                    dataRow + 1,
                    columnCount
                ).Merge();

                worksheet.Cell(
                    dataRow,
                    1
                ).Value =
                    "Không có giao dịch đã thanh toán trong khoảng thời gian này.";

                worksheet.Range(
                    dataRow,
                    1,
                    dataRow + 1,
                    columnCount
                ).Style
                    .Alignment.Horizontal =
                        XLAlignmentHorizontalValues.Center;

                worksheet.Range(
                    dataRow,
                    1,
                    dataRow + 1,
                    columnCount
                ).Style
                    .Alignment.Vertical =
                        XLAlignmentVerticalValues.Center;

                worksheet.Range(
                    dataRow,
                    1,
                    dataRow + 1,
                    columnCount
                ).Style
                    .Font.FontColor =
                        XLColor.FromHtml("#64748B");

                dataRow += 2;
            }

            var lastDataRow =
                dataRow - 1;

            var totalRow =
                dataRow + 1;

            worksheet.Range(
                totalRow,
                1,
                totalRow,
                12
            ).Merge();

            worksheet.Cell(
                totalRow,
                1
            ).Value =
                $"TỔNG CỘNG: {rentals.Count} GIAO DỊCH";

            worksheet.Cell(
                totalRow,
                13
            ).Value =
                "TỔNG DOANH THU";

            worksheet.Cell(
                totalRow,
                14
            ).Value =
                rentals.Sum(rental =>
                    rental.TotalAmount);

            worksheet.Range(
                totalRow,
                1,
                totalRow,
                columnCount
            ).Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml("#0D6EFD");

            worksheet.Range(
                totalRow,
                1,
                totalRow,
                columnCount
            ).Style
                .Font.FontColor =
                    XLColor.White;

            worksheet.Range(
                totalRow,
                1,
                totalRow,
                columnCount
            ).Style
                .Font.Bold =
                    true;

            worksheet.Range(
                totalRow,
                1,
                totalRow,
                columnCount
            ).Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            worksheet.Cell(
                totalRow,
                14
            ).Style.NumberFormat.Format =
                "#,##0 \"VNĐ\"";

            worksheet.Row(
                totalRow
            ).Height =
                27;

            if (lastDataRow >= headerRow + 1)
            {
                worksheet.Range(
                    headerRow,
                    1,
                    lastDataRow,
                    columnCount
                ).SetAutoFilter();
            }

            worksheet.SheetView
                .FreezeRows(headerRow);

            // Chiều rộng cột được đặt thủ công để không bị chữ dồn như CSV.
            worksheet.Column(1).Width = 15;
            worksheet.Column(2).Width = 24;
            worksheet.Column(3).Width = 18;
            worksheet.Column(4).Width = 30;
            worksheet.Column(5).Width = 16;
            worksheet.Column(6).Width = 14;
            worksheet.Column(7).Width = 17;
            worksheet.Column(8).Width = 17;
            worksheet.Column(9).Width = 17;
            worksheet.Column(10).Width = 18;
            worksheet.Column(11).Width = 21;
            worksheet.Column(12).Width = 11;
            worksheet.Column(13).Width = 48;
            worksheet.Column(14).Width = 18;

            var usedRange =
                worksheet.RangeUsed();

            if (usedRange != null)
            {
                usedRange.Style.Font.FontName =
                    "Calibri";

                usedRange.Style.Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;
            }

            worksheet.PageSetup
                .PageOrientation =
                    XLPageOrientation.Landscape;
        }

        // =========================================================
        // STYLE HELPERS
        // =========================================================
        private static void CreateKpiCard(
            IXLWorksheet worksheet,
            string rangeAddress,
            string label,
            decimal value,
            string backgroundColor,
            string fontColor,
            bool isCurrency = false)
        {
            var range =
                worksheet.Range(
                    rangeAddress
                );

            range.Merge();

            range.FirstCell().Value =
                isCurrency
                    ? $"{label}\n{value:N0} VNĐ"
                    : $"{label}\n{value:N0}";

            range.Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml(
                        backgroundColor
                    );

            range.Style
                .Font.FontColor =
                    XLColor.FromHtml(
                        fontColor
                    );

            range.Style
                .Font.Bold =
                    true;

            range.Style
                .Font.FontSize =
                    12;

            range.Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            range.Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            range.Style
                .Alignment.WrapText =
                    true;

            SetBorder(
                range,
                XLColor.FromHtml(
                    backgroundColor
                )
            );
        }

        private static void StyleMiniSummary(
            IXLRange range,
            string backgroundColor,
            string fontColor)
        {
            range.Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml(
                        backgroundColor
                    );

            range.Style
                .Font.FontColor =
                    XLColor.FromHtml(
                        fontColor
                    );

            range.Style
                .Font.Bold =
                    true;

            range.Style
                .Font.FontSize =
                    11;

            range.Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            range.Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            range.Style
                .Alignment.WrapText =
                    true;

            SetBorder(
                range,
                XLColor.FromHtml(
                    backgroundColor
                )
            );
        }

        private static void StyleSectionHeader(
            IXLRange range)
        {
            range.Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml("#111827");

            range.Style
                .Font.FontColor =
                    XLColor.White;

            range.Style
                .Font.Bold =
                    true;

            range.Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Left;

            range.Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            range.Style
                .Alignment.Indent =
                    1;

            range.Worksheet.Row(
                range.FirstRow().RowNumber()
            ).Height =
                25;
        }

        private static void StyleTableHeader(
            IXLRange range)
        {
            range.Style
                .Fill.BackgroundColor =
                    XLColor.FromHtml("#0D6EFD");

            range.Style
                .Font.FontColor =
                    XLColor.White;

            range.Style
                .Font.Bold =
                    true;

            range.Style
                .Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;

            range.Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            range.Style
                .Alignment.WrapText =
                    true;

            SetBorder(
                range,
                XLColor.FromHtml("#B6C2D1")
            );

            range.Worksheet.Row(
                range.FirstRow().RowNumber()
            ).Height =
                32;
        }

        private static void StyleDataRow(
            IXLRange range,
            bool useAlternateBackground)
        {
            range.Style
                .Fill.BackgroundColor =
                    useAlternateBackground
                        ? XLColor.FromHtml("#F8FAFC")
                        : XLColor.White;

            range.Style
                .Alignment.Vertical =
                    XLAlignmentVerticalValues.Center;

            SetBorder(
                range,
                XLColor.FromHtml("#DCE3EC")
            );
        }

        private static void SetBorder(
            IXLRange range,
            XLColor color)
        {
            range.Style.Border.TopBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.BottomBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.LeftBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.RightBorder =
                XLBorderStyleValues.Thin;

            range.Style.Border.TopBorderColor =
                color;

            range.Style.Border.BottomBorderColor =
                color;

            range.Style.Border.LeftBorderColor =
                color;

            range.Style.Border.RightBorderColor =
                color;
        }

        // =========================================================
        // PHÂN QUYỀN
        // =========================================================
        private IActionResult? CheckStaffAccess()
        {
            var userId =
                HttpContext.Session.GetString("UserId");

            var userRole =
                HttpContext.Session.GetString("UserRole");

            if (string.IsNullOrWhiteSpace(userId))
            {
                TempData["ErrorMessage"] =
                    "Vui lòng đăng nhập để tiếp tục.";

                return RedirectToAction(
                    "Login",
                    "Account"
                );
            }

            var hasAccess =
                string.Equals(
                    userRole,
                    "Staff",
                    StringComparison.OrdinalIgnoreCase
                )
                ||
                string.Equals(
                    userRole,
                    "Admin",
                    StringComparison.OrdinalIgnoreCase
                );

            if (!hasAccess)
            {
                TempData["ErrorMessage"] =
                    "Bạn không có quyền xuất báo cáo doanh thu.";

                return RedirectToAction(
                    "AccessDenied",
                    "Home"
                );
            }

            return null;
        }

        private static string GetRentalStatusText(
            string? status)
        {
            return status switch
            {
                "Pending" => "Chờ duyệt",
                "Approved" => "Đã duyệt",
                "Borrowing" => "Đang thuê",
                "Returned" => "Đã trả",
                "Cancelled" => "Đã hủy",
                _ => status ?? "Không xác định"
            };
        }

        private static string GetPaymentStatusText(
            string? status)
        {
            return status switch
            {
                "Pending" => "Chờ thanh toán",
                "AwaitingConfirmation" => "Chờ xác nhận",
                "Paid" => "Đã thanh toán",
                "Completed" => "Đã hoàn tất",
                "Rejected" => "Bị từ chối",
                "Cancelled" => "Đã hủy",
                _ => status ?? "Không xác định"
            };
        }
    }
}