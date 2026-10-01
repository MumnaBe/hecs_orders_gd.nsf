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

app.MapGet("/", (string? q, string? status) =>
    Results.Content(Render.List(orders, q ?? "", status ?? ""), "text/html"));

app.MapGet("/order/{noteId}", (string noteId) =>
{
    var match = orders.FirstOrDefault(o => o.NoteId == noteId);
    return match is null
        ? Results.Content(Render.Page("<p>Order not found.</p>"), "text/html", Encoding.UTF8, 404)
        : Results.Content(Render.Detail(match), "text/html");
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
    public static class Render
    {
        static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

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
              + "footer{color:var(--muted);text-align:center;padding:24px;font-size:12px}</style>";
            return "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">"
              + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">"
              + "<title>HECS Orders</title>" + css + "</head><body>"
              + "<header><h1>HECS Order Tracking</h1>"
              + "<p>Lightweight viewer &middot; reading live DXL export &middot; C# / ASP.NET Core</p></header>"
              + "<div class=\"wrap\">" + body + "</div>"
              + "<footer>Synthetic/legacy data from the Notes export. No external services.</footer>"
              + "</body></html>";
        }

        public static string List(List<OrderDoc> orders, string q, string statusFilter)
        {
            var statuses = orders.Select(o => DxlStore.Summary(o).Status).Distinct().OrderBy(s => s).ToList();
            var rows = new StringBuilder();
            double totalValue = 0; int shown = 0;

            foreach (var doc in orders)
            {
                var s = DxlStore.Summary(doc);
                var hay = string.Join(" ", s.Ref, s.Project, s.Supplier, s.CardHolder, s.Status).ToLowerInvariant();
                if (q.Length > 0 && !hay.Contains(q.ToLowerInvariant())) continue;
                if (statusFilter.Length > 0 && s.Status != statusFilter) continue;
                shown++; totalValue += s.Total;
                rows.Append("<tr><td><a href=\"/order/" + E(doc.NoteId) + "\">"
                  + E(string.IsNullOrEmpty(s.Ref) ? doc.NoteId : s.Ref) + "</a></td>"
                  + "<td><span class=\"pill " + StatusClass(s.Status) + "\">" + E(s.Status) + "</span></td>"
                  + "<td>" + E(s.Project) + "</td><td>" + E(s.Supplier) + "</td><td>" + E(s.Purchase) + "</td>"
                  + "<td class=\"right\">" + E(s.Currency) + " " + s.Total.ToString("N2", CultureInfo.InvariantCulture) + "</td></tr>");
            }

            var opts = new StringBuilder();
            foreach (var s in statuses)
                opts.Append("<option value=\"" + E(s) + "\"" + (s == statusFilter ? " selected" : "") + ">" + E(s) + "</option>");

            var body = "<form class=\"bar\" method=\"get\" action=\"/\">"
              + "<input name=\"q\" placeholder=\"Search ref, project, supplier...\" value=\"" + E(q) + "\" size=\"34\">"
              + "<select name=\"status\"><option value=\"\">All statuses</option>" + opts + "</select>"
              + "<button>Filter</button></form>"
              + "<div class=\"stats\">"
              + "<div class=\"stat\"><b>" + orders.Count + "</b><span>Total orders</span></div>"
              + "<div class=\"stat\"><b>" + shown + "</b><span>Shown</span></div>"
              + "<div class=\"stat\"><b>" + totalValue.ToString("N0", CultureInfo.InvariantCulture) + "</b><span>Value shown</span></div>"
              + "<div class=\"stat\"><b>" + statuses.Count + "</b><span>Statuses</span></div></div>"
              + "<table><tr><th>Reference</th><th>Status</th><th>Project</th><th>Supplier</th><th>Payment</th><th class=\"right\">Total</th></tr>"
              + (rows.Length > 0 ? rows.ToString() : "<tr><td colspan=\"6\">No matching orders.</td></tr>")
              + "</table>";
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
