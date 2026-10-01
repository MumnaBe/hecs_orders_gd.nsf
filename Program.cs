using System.Globalization;
using System.Net;
using System.Text;
using System.Xml.Linq;
using HecsOrders;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var dataDir = Path.Combine(Directory.GetCurrentDirectory(), "data");
var orders = DxlStore.LoadOrders(dataDir);
Console.WriteLine(new string('=', 60));
Console.WriteLine(" HECS Orders viewer (C#)");
Console.WriteLine($" Parsed {orders.Count} OrderForm documents from {dataDir}");
Console.WriteLine(" Open  ->  http://127.0.0.1:8000");
Console.WriteLine(new string('=', 60));

app.MapGet("/", (string? q, string? status, string? category, string? sort) =>
    Results.Content(Render.List(orders, q ?? "", status ?? "", category ?? "", sort ?? ""), "text/html"));

app.MapGet("/dashboard", () =>
    Results.Content(Render.Dashboard(orders), "text/html"));

app.MapGet("/order/{noteId}", (string noteId) =>
{
    var match = orders.FirstOrDefault(o => o.NoteId == noteId);
    return match is null
        ? Results.Content(Render.Page("<p>Order not found.</p>"), "text/html", Encoding.UTF8, 404)
        : Results.Content(Render.Detail(match), "text/html");
});

// CSV export of the (optionally filtered) order list.
app.MapGet("/export.csv", (string? q, string? status, string? category) =>
    Results.Text(Export.Csv(orders, q ?? "", status ?? "", category ?? ""),
        "text/csv", Encoding.UTF8));

// Lightweight JSON API.
app.MapGet("/api/orders", () =>
    Results.Json(orders.Select(o =>
    {
        var s = DxlStore.Summary(o);
        return new
        {
            noteId = o.NoteId,
            reference = s.Ref,
            status = s.Status,
            project = s.Project,
            category = s.Category,
            supplier = s.Supplier,
            purchase = s.Purchase,
            currency = s.Currency,
            total = s.Total,
        };
    })));

app.MapGet("/api/orders/{noteId}", (string noteId) =>
{
    var match = orders.FirstOrDefault(o => o.NoteId == noteId);
    if (match is null) return Results.NotFound();
    var s = DxlStore.Summary(match);
    return Results.Json(new
    {
        noteId = match.NoteId,
        unid = match.Unid,
        reference = s.Ref,
        status = s.Status,
        project = s.Project,
        category = s.Category,
        supplier = s.Supplier,
        cardHolder = s.CardHolder,
        purchase = s.Purchase,
        currency = s.Currency,
        total = s.Total,
        lines = s.Lines,
    });
});

app.Run("http://127.0.0.1:8000");

namespace HecsOrders
{
    public record OrderDoc
    {
        public string File { get; init; } = "";
        public string Form { get; init; } = "";
        public string NoteId { get; init; } = "";
        public string Unid { get; init; } = "";
        public Dictionary<string, string> Items { get; init; } = new();
    }

    public record OrderLine(int N, string Desc, double Qty, double Price, double Total);

    public record OrderSummary
    {
        public string Ref { get; init; } = "";
        public string Project { get; init; } = "";
        public string Category { get; init; } = "";
        public string Status { get; init; } = "New";
        public string Supplier { get; init; } = "";
        public string CardHolder { get; init; } = "";
        public string Purchase { get; init; } = "";
        public string Currency { get; init; } = "";
        public string DateReq { get; init; } = "";
        public List<OrderLine> Lines { get; init; } = new();
        public double Total { get; init; }
    }

    public static class DxlStore
    {
        static readonly XNamespace Ns = "http://www.lotus.com/dxl";

        static string ItemValue(XElement item)
        {
            var parts = new List<string>();
            foreach (var child in item.Elements())
            {
                var tag = child.Name.LocalName;
                if (tag is "text" or "number" or "datetime")
                {
                    var v = child.Value.Trim();
                    if (v.Length > 0) parts.Add(v);
                }
                else if (tag is "textlist" or "datetimelist")
                {
                    foreach (var t in child.Elements())
                    {
                        var v = t.Value.Trim();
                        if (v.Length > 0) parts.Add(v);
                    }
                }
            }
            return string.Join(" ", parts);
        }

        static OrderDoc? ParseDocument(string path)
        {
            XDocument tree;
            try { tree = XDocument.Load(path); }
            catch { return null; }

            var root = tree.Root;
            if (root is null) return null;

            var items = new Dictionary<string, string>();
            foreach (var item in root.Elements(Ns + "item"))
            {
                var name = item.Attribute("name")?.Value ?? "";
                items[name] = ItemValue(item);
            }

            var noteinfo = root.Element(Ns + "noteinfo");
            return new OrderDoc
            {
                File = Path.GetFileName(path),
                Form = root.Attribute("form")?.Value ?? "",
                NoteId = noteinfo?.Attribute("noteid")?.Value ?? "",
                Unid = noteinfo?.Attribute("unid")?.Value ?? "",
                Items = items,
            };
        }

        public static List<OrderDoc> LoadOrders(string dataDir)
        {
            var orders = new List<OrderDoc>();
            if (!Directory.Exists(dataDir)) return orders;

            foreach (var file in Directory.EnumerateFiles(dataDir, "*.dxl").OrderBy(f => f))
            {
                var doc = ParseDocument(file);
                if (doc is not null && doc.Form == "OrderForm")
                    orders.Add(doc);
            }
            return orders;
        }

        static double Num(string? v) =>
            double.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0.0;

        public static OrderSummary Summary(OrderDoc doc)
        {
            var items = doc.Items;
            string Get(string k) => items.TryGetValue(k, out var v) ? v : "";

            double total = 0;
            var lines = new List<OrderLine>();
            for (int i = 1; i <= 20; i++)
            {
                var desc = Get($"i{i}Desc");
                var qty = Num(Get($"i{i}Quant"));
                var price = Num(Get($"i{i}PricePer"));
                if (qty != 0 || price != 0 || desc.Length > 0)
                {
                    var lineTotal = qty * price;
                    total += lineTotal;
                    lines.Add(new OrderLine(i, desc, qty, price, lineTotal));
                }
            }
            total += Num(Get("Shipping")) + Num(Get("Customs"));
            var exch = Num(Get("Exchange"));
            if (exch != 0) total += exch * total;

            var status = Get("Status");
            return new OrderSummary
            {
                Ref = Get("RefNumberTag") + Get("RefNumber"),
                Project = Get("ProjectSelection"),
                Category = Get("Category"),
                Status = string.IsNullOrEmpty(status) ? "New" : status,
                Supplier = Get("CompanyName"),
                CardHolder = Get("CardHolder"),
                Purchase = Get("PurchaseType"),
                Currency = Get("Currency"),
                DateReq = Get("DateReq"),
                Lines = lines,
                Total = total,
            };
        }
    }
}

namespace HecsOrders
{
    public static class Export
    {
        static string Field(string? v)
        {
            v ??= "";
            if (v.Contains(',') || v.Contains('"') || v.Contains('\n'))
                return "\"" + v.Replace("\"", "\"\"") + "\"";
            return v;
        }

        public static string Csv(List<OrderDoc> orders, string q, string statusFilter, string categoryFilter)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Reference,Status,Project,Category,Supplier,Payment,Currency,Total,NoteId");
            foreach (var doc in orders)
            {
                var s = DxlStore.Summary(doc);
                var hay = string.Join(" ", s.Ref, s.Project, s.Supplier, s.CardHolder, s.Status).ToLowerInvariant();
                if (q.Length > 0 && !hay.Contains(q.ToLowerInvariant())) continue;
                if (statusFilter.Length > 0 && s.Status != statusFilter) continue;
                if (categoryFilter.Length > 0 && s.Category != categoryFilter) continue;

                sb.AppendLine(string.Join(",",
                    Field(s.Ref), Field(s.Status), Field(s.Project), Field(s.Category),
                    Field(s.Supplier), Field(s.Purchase), Field(s.Currency),
                    s.Total.ToString("0.00", CultureInfo.InvariantCulture), Field(doc.NoteId)));
            }
            return sb.ToString();
        }
    }

    public static class Render
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

        // Inline, dependency-free SVG icons (Feather-style, 1.75 stroke).
        static string Icon(string name)
        {
            const string open = "<svg width=\"15\" height=\"15\" viewBox=\"0 0 24 24\" fill=\"none\" stroke=\"currentColor\" stroke-width=\"1.75\" stroke-linecap=\"round\" stroke-linejoin=\"round\">";
            string body = name switch
            {
                "list" => "<line x1='8' y1='6' x2='21' y2='6'/><line x1='8' y1='12' x2='21' y2='12'/><line x1='8' y1='18' x2='21' y2='18'/><line x1='3' y1='6' x2='3.01' y2='6'/><line x1='3' y1='12' x2='3.01' y2='12'/><line x1='3' y1='18' x2='3.01' y2='18'/>",
                "chart" => "<line x1='18' y1='20' x2='18' y2='10'/><line x1='12' y1='20' x2='12' y2='4'/><line x1='6' y1='20' x2='6' y2='14'/>",
                "download" => "<path d='M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4'/><polyline points='7 10 12 15 17 10'/><line x1='12' y1='15' x2='12' y2='3'/>",
                "code" => "<polyline points='16 18 22 12 16 6'/><polyline points='8 6 2 12 8 18'/>",
                "back" => "<line x1='19' y1='12' x2='5' y2='12'/><polyline points='12 19 5 12 12 5'/>",
                _ => "",
            };
            return open + body + "</svg>";
        }

        static string StatusClass(string? status)
        {
            var s = string.IsNullOrEmpty(status) ? "New" : status;
            if (s.StartsWith("On")) return "s-On";
            if (s.Contains("Received")) return "s-Received";
            return "s-" + s.Split(' ')[0];
        }

        public static string Page(string body)
        {
            const string css = "<style>"
              + ":root{--bg:#0f172a;--card:#1e293b;--ink:#e2e8f0;--muted:#94a3b8;--accent:#38bdf8;--line:#334155}"
              + "*{box-sizing:border-box}"
              + "body{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:var(--bg);color:var(--ink)}"
              + "header{background:linear-gradient(90deg,#0ea5e9,#6366f1);padding:18px 28px;color:#fff}"
              + "header h1{margin:0;font-size:20px}header p{margin:4px 0 0;opacity:.9;font-size:13px}"
              + ".wrap{max-width:1100px;margin:0 auto;padding:24px}"
              + ".bar{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:18px}"
              + "input,select{background:var(--card);border:1px solid var(--line);color:var(--ink);padding:9px 12px;border-radius:8px;font-size:14px}"
              + ".stats{display:flex;gap:14px;margin-bottom:18px;flex-wrap:wrap}"
              + ".stat{background:var(--card);border:1px solid var(--line);border-radius:12px;padding:14px 18px;min-width:140px}"
              + ".stat b{display:block;font-size:22px;color:var(--accent)}"
              + ".stat span{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.5px}"
              + "table{width:100%;border-collapse:collapse;background:var(--card);border-radius:12px;overflow:hidden}"
              + "th,td{padding:11px 14px;text-align:left;border-bottom:1px solid var(--line);font-size:14px}"
              + "th{background:#0b1222;color:var(--muted);text-transform:uppercase;font-size:11px;letter-spacing:.5px}"
              + "tr:hover td{background:#273449}a{color:var(--accent);text-decoration:none}"
              + ".pill{padding:3px 10px;border-radius:999px;font-size:12px;font-weight:600}"
              + ".s-New{background:#334155;color:#cbd5e1}.s-Submitted{background:#1d4ed8;color:#fff}"
              + ".s-On{background:#b45309;color:#fff}.s-Received{background:#15803d;color:#fff}"
              + ".s-Reconciled{background:#0f766e;color:#fff}.s-Cancelled{background:#7f1d1d;color:#fff}"
              + ".right{text-align:right}.back{display:inline-block;margin-bottom:16px}"
              + ".grid{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:12px}"
              + ".field{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:10px 14px}"
              + ".field span{display:block;color:var(--muted);font-size:11px;text-transform:uppercase}"
              + "button{background:#0ea5e9;border:none;color:#fff;padding:9px 18px;border-radius:8px;cursor:pointer}"
              + "nav{display:flex;gap:18px;margin-top:10px}nav a{color:#fff;font-size:13px;opacity:.95;font-weight:600}"
              + "nav a:hover{text-decoration:underline}"
              + ".btns{display:flex;gap:10px;margin-bottom:16px}"
              + ".btn{display:inline-block;background:var(--card);border:1px solid var(--line);color:var(--ink);padding:8px 14px;border-radius:8px;font-size:13px}"
              + ".bars{margin:14px 0}.barrow{display:flex;align-items:center;gap:10px;margin:6px 0;font-size:13px}"
              + ".barrow .lbl{width:150px;color:var(--muted)}.barrow .track{flex:1;background:#0b1222;border-radius:6px;height:16px;overflow:hidden}"
              + ".barrow .fill{height:100%;background:linear-gradient(90deg,#0ea5e9,#6366f1)}"
              + ".barrow .val{width:120px;text-align:right}"
              + "nav a{display:inline-flex;align-items:center;gap:6px}.btn{display:inline-flex;align-items:center;gap:6px}"
              + "h2{display:flex;align-items:center;gap:8px}svg{flex:0 0 auto;vertical-align:middle}"
              + "footer{color:var(--muted);text-align:center;padding:24px;font-size:12px}</style>";
            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
              + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
              + "<title>HECS Orders</title>" + css + "</head><body>"
              + "<header><h1>HECS Order Tracking</h1>"
              + "<p>Lightweight viewer &middot; reading live DXL export &middot; C# / ASP.NET Core</p>"
              + "<nav><a href=\"/\">" + Icon("list") + "Orders</a><a href=\"/dashboard\">" + Icon("chart") + "Dashboard</a>"
              + "<a href=\"/export.csv\">" + Icon("download") + "Export CSV</a><a href=\"/api/orders\">" + Icon("code") + "JSON API</a></nav></header>"
              + "<div class=\"wrap\">" + body + "</div>"
              + "<footer>Synthetic/legacy data from the Notes export. No external services.</footer>"
              + "</body></html>";
        }

        public static string List(List<OrderDoc> orders, string q, string statusFilter, string categoryFilter, string sort)
        {
            var statuses = orders.Select(o => DxlStore.Summary(o).Status).Distinct().OrderBy(s => s).ToList();
            var categories = orders.Select(o => DxlStore.Summary(o).Category)
                                   .Where(c => !string.IsNullOrEmpty(c)).Distinct().OrderBy(c => c).ToList();

            // Apply filters.
            var filtered = new List<(OrderDoc doc, OrderSummary s)>();
            foreach (var doc in orders)
            {
                var s = DxlStore.Summary(doc);
                var hay = string.Join(" ", s.Ref, s.Project, s.Supplier, s.CardHolder, s.Status).ToLowerInvariant();
                if (q.Length > 0 && !hay.Contains(q.ToLowerInvariant())) continue;
                if (statusFilter.Length > 0 && s.Status != statusFilter) continue;
                if (categoryFilter.Length > 0 && s.Category != categoryFilter) continue;
                filtered.Add((doc, s));
            }

            // Apply sorting.
            filtered = sort switch
            {
                "total_desc" => filtered.OrderByDescending(x => x.s.Total).ToList(),
                "total_asc" => filtered.OrderBy(x => x.s.Total).ToList(),
                "supplier" => filtered.OrderBy(x => x.s.Supplier).ToList(),
                "status" => filtered.OrderBy(x => x.s.Status).ToList(),
                _ => filtered.OrderBy(x => x.s.Ref).ToList(),
            };

            var rows = new StringBuilder();
            double totalValue = 0;
            foreach (var (doc, s) in filtered)
            {
                totalValue += s.Total;
                rows.Append("<tr><td><a href=\"/order/" + E(doc.NoteId) + "\">"
                  + E(string.IsNullOrEmpty(s.Ref) ? doc.NoteId : s.Ref) + "</a></td>"
                  + "<td><span class=\"pill " + StatusClass(s.Status) + "\">" + E(s.Status) + "</span></td>"
                  + "<td>" + E(s.Project) + "</td><td>" + E(s.Supplier) + "</td><td>" + E(s.Purchase) + "</td>"
                  + "<td class=\"right\">" + E(s.Currency) + " " + s.Total.ToString("N2", CultureInfo.InvariantCulture) + "</td></tr>");
            }

            var statusOpts = new StringBuilder();
            foreach (var s in statuses)
                statusOpts.Append("<option value=\"" + E(s) + "\"" + (s == statusFilter ? " selected" : "") + ">" + E(s) + "</option>");

            var catOpts = new StringBuilder();
            foreach (var c in categories)
                catOpts.Append("<option value=\"" + E(c) + "\"" + (c == categoryFilter ? " selected" : "") + ">" + E(c) + "</option>");

            string SortOpt(string val, string label) =>
                "<option value=\"" + val + "\"" + (sort == val ? " selected" : "") + ">" + label + "</option>";

            // Build an export link that keeps the current filters.
            var exportQ = $"/export.csv?q={Uri.EscapeDataString(q)}&status={Uri.EscapeDataString(statusFilter)}&category={Uri.EscapeDataString(categoryFilter)}";

            var body = "<form class=\"bar\" method=\"get\" action=\"/\">"
              + "<input name=\"q\" placeholder=\"Search ref, project, supplier...\" value=\"" + E(q) + "\" size=\"28\">"
              + "<select name=\"status\"><option value=\"\">All statuses</option>" + statusOpts + "</select>"
              + "<select name=\"category\"><option value=\"\">All categories</option>" + catOpts + "</select>"
              + "<select name=\"sort\">" + SortOpt("", "Sort: Reference") + SortOpt("total_desc", "Total (high→low)")
              + SortOpt("total_asc", "Total (low→high)") + SortOpt("supplier", "Supplier") + SortOpt("status", "Status") + "</select>"
              + "<button>Apply</button></form>"
              + "<div class=\"btns\"><a class=\"btn\" href=\"" + exportQ + "\">" + Icon("download") + "Export these to CSV</a>"
              + "<a class=\"btn\" href=\"/dashboard\">" + Icon("chart") + "View dashboard</a></div>"
              + "<div class=\"stats\">"
              + "<div class=\"stat\"><b>" + orders.Count + "</b><span>Total orders</span></div>"
              + "<div class=\"stat\"><b>" + filtered.Count + "</b><span>Shown</span></div>"
              + "<div class=\"stat\"><b>" + totalValue.ToString("N0", CultureInfo.InvariantCulture) + "</b><span>Value shown</span></div>"
              + "<div class=\"stat\"><b>" + statuses.Count + "</b><span>Statuses</span></div></div>"
              + "<table><tr><th>Reference</th><th>Status</th><th>Project</th><th>Supplier</th><th>Payment</th><th class=\"right\">Total</th></tr>"
              + (rows.Length > 0 ? rows.ToString() : "<tr><td colspan=\"6\">No matching orders.</td></tr>")
              + "</table>";
            return Page(body);
        }

        public static string Dashboard(List<OrderDoc> orders)
        {
            var summaries = orders.Select(DxlStore.Summary).ToList();
            double grand = summaries.Sum(s => s.Total);

            // Group helpers.
            string BarChart(string title, IEnumerable<IGrouping<string, OrderSummary>> groups)
            {
                var items = groups
                    .Select(g => (Key: string.IsNullOrEmpty(g.Key) ? "(none)" : g.Key,
                                  Count: g.Count(), Value: g.Sum(x => x.Total)))
                    .OrderByDescending(x => x.Value).ToList();
                double max = items.Count > 0 ? items.Max(i => i.Value) : 1;
                if (max <= 0) max = 1;
                var sb = new StringBuilder("<h3>" + E(title) + "</h3><div class=\"bars\">");
                foreach (var it in items)
                {
                    var pct = (int)Math.Round(it.Value / max * 100);
                    sb.Append("<div class=\"barrow\"><div class=\"lbl\">" + E(it.Key) + "</div>"
                      + "<div class=\"track\"><div class=\"fill\" style=\"width:" + pct + "%\"></div></div>"
                      + "<div class=\"val\">" + it.Value.ToString("N0", CultureInfo.InvariantCulture)
                      + " (" + it.Count + ")</div></div>");
                }
                sb.Append("</div>");
                return sb.ToString();
            }

            double avg = summaries.Count > 0 ? grand / summaries.Count : 0;
            double largest = summaries.Count > 0 ? summaries.Max(s => s.Total) : 0;

            var body = "<a class=\"back\" href=\"/\">&larr; Back to orders</a><h2>" + Icon("chart") + "Dashboard</h2>"
              + "<div class=\"stats\">"
              + "<div class=\"stat\"><b>" + summaries.Count + "</b><span>Orders</span></div>"
              + "<div class=\"stat\"><b>" + grand.ToString("N0", CultureInfo.InvariantCulture) + "</b><span>Total value</span></div>"
              + "<div class=\"stat\"><b>" + avg.ToString("N0", CultureInfo.InvariantCulture) + "</b><span>Average order</span></div>"
              + "<div class=\"stat\"><b>" + largest.ToString("N0", CultureInfo.InvariantCulture) + "</b><span>Largest order</span></div></div>"
              + BarChart("Value by status", summaries.GroupBy(s => s.Status))
              + BarChart("Value by category", summaries.GroupBy(s => s.Category))
              + BarChart("Top suppliers", summaries.GroupBy(s => s.Supplier));
            return Page(body);
        }

        public static string Detail(OrderDoc doc)
        {
            var s = DxlStore.Summary(doc);
            var lineRows = new StringBuilder();
            foreach (var l in s.Lines)
                lineRows.Append("<tr><td>" + l.N + "</td><td>" + E(l.Desc) + "</td>"
                  + "<td class=\"right\">" + l.Qty.ToString("0.##", CultureInfo.InvariantCulture) + "</td>"
                  + "<td class=\"right\">" + l.Price.ToString("N2", CultureInfo.InvariantCulture) + "</td>"
                  + "<td class=\"right\">" + l.Total.ToString("N2", CultureInfo.InvariantCulture) + "</td></tr>");

            string[] metaFields = { "ProjectSelection", "CompleteFinancialCode", "Category",
                "PurchaseType", "CardHolder", "CompanyName", "AcctNumber", "CompPhone",
                "Currency", "DateReq", "GSTExempt", "BudgetCalculated", "Sent" };
            var fields = new StringBuilder();
            foreach (var f in metaFields)
            {
                var val = doc.Items.TryGetValue(f, out var v) && v.Length > 0 ? v : "-";
                fields.Append("<div class=\"field\"><span>" + E(f) + "</span>" + E(val) + "</div>");
            }

            var body = "<a class=\"back\" href=\"/\">&larr; Back to orders</a>"
              + "<h2>" + E(string.IsNullOrEmpty(s.Ref) ? doc.NoteId : s.Ref)
              + " <span class=\"pill " + StatusClass(s.Status) + "\">" + E(s.Status) + "</span></h2>"
              + "<div class=\"grid\">" + fields + "</div>"
              + "<h3>Line items</h3><table>"
              + "<tr><th>#</th><th>Description</th><th class=\"right\">Qty</th><th class=\"right\">Unit price</th><th class=\"right\">Line total</th></tr>"
              + (lineRows.Length > 0 ? lineRows.ToString() : "<tr><td colspan=\"5\">No line items.</td></tr>")
              + "<tr><td colspan=\"4\" class=\"right\"><b>Order total (" + E(s.Currency) + ")</b></td>"
              + "<td class=\"right\"><b>" + s.Total.ToString("N2", CultureInfo.InvariantCulture) + "</b></td></tr></table>"
              + "<p style=\"color:#94a3b8;font-size:12px\">File: " + E(doc.File) + " &middot; UNID: " + E(doc.Unid) + "</p>";
            return Page(body);
        }
    }
}
