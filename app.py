#!/usr/bin/env python3
"""
HECS Orders - lightweight viewer
================================

A zero-dependency web app (Python standard library only) that reads the
Lotus Notes DXL export in ./data and presents the OrderForm documents as a
browsable, searchable order-tracking workspace.

This is a pragmatic, runnable substitute for the heavy .NET/PostgreSQL stack
described in plan.md / README.md. It needs no installs, no Docker and no
database -- just Python 3.

Run:
    python app.py
Then open http://127.0.0.1:8000
"""

import html
import os
import re
import xml.etree.ElementTree as ET
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

HERE = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(HERE, "data")
NS = "{http://www.lotus.com/dxl}"
PORT = 8000

# ---------------------------------------------------------------------------
# DXL parsing
# ---------------------------------------------------------------------------

def _item_value(item):
    """Return a readable string for a DXL <item> element."""
    parts = []
    for child in item:
        tag = child.tag.replace(NS, "")
        if tag in ("text", "number"):
            parts.append((child.text or "").strip())
        elif tag == "textlist":
            for t in child:
                parts.append((t.text or "").strip())
        elif tag == "datetime":
            parts.append((child.text or "").strip())
        elif tag == "datetimelist":
            for t in child:
                parts.append((t.text or "").strip())
    return " ".join(p for p in parts if p)


def parse_document(path):
    """Parse one DXL file into a dict of items plus note metadata."""
    try:
        tree = ET.parse(path)
    except ET.ParseError:
        return None
    root = tree.getroot()
    form = root.get("form", "")
    doc = {"_file": os.path.basename(path), "_form": form, "_items": {}}

    noteinfo = root.find(f"{NS}noteinfo")
    if noteinfo is not None:
        doc["_noteid"] = noteinfo.get("noteid", "")
        doc["_unid"] = noteinfo.get("unid", "")
        created = noteinfo.find(f"{NS}created/{NS}datetime")
        if created is not None:
            doc["_created"] = (created.text or "").strip()

    for item in root.findall(f"{NS}item"):
        name = item.get("name", "")
        doc["_items"][name] = _item_value(item)
    return doc


_CACHE = None


def load_orders():
    """Load and cache all OrderForm documents."""
    global _CACHE
    if _CACHE is not None:
        return _CACHE
    orders = []
    if os.path.isdir(DATA_DIR):
        for fname in sorted(os.listdir(DATA_DIR)):
            if not fname.lower().endswith(".dxl"):
                continue
            doc = parse_document(os.path.join(DATA_DIR, fname))
            if doc and doc["_form"] == "OrderForm":
                orders.append(doc)
    _CACHE = orders
    return orders


# ---------------------------------------------------------------------------
# Business calculations (mirrors plan.md BL-002 at a basic level)
# ---------------------------------------------------------------------------

def _num(v):
    try:
        return float(v)
    except (TypeError, ValueError):
        return 0.0


def order_summary(doc):
    items = doc["_items"]
    total = 0.0
    lines = []
    for i in range(1, 21):
        desc = items.get(f"i{i}Desc", "")
        qty = _num(items.get(f"i{i}Quant"))
        price = _num(items.get(f"i{i}PricePer"))
        if qty or price or desc:
            line_total = qty * price
            total += line_total
            lines.append({"n": i, "desc": desc, "qty": qty,
                          "price": price, "total": line_total})
    total += _num(items.get("Shipping")) + _num(items.get("Customs"))
    exch = _num(items.get("Exchange"))
    if exch:
        total += exch * total
    return {
        "ref": items.get("RefNumberTag", "") + items.get("RefNumber", ""),
        "project": items.get("ProjectSelection", ""),
        "category": items.get("Category", ""),
        "status": items.get("Status", "") or "New",
        "supplier": items.get("CompanyName", ""),
        "cardholder": items.get("CardHolder", ""),
        "purchase": items.get("PurchaseType", ""),
        "currency": items.get("Currency", ""),
        "datereq": items.get("DateReq", ""),
        "lines": lines,
        "total": total,
    }


# ---------------------------------------------------------------------------
# HTML rendering
# ---------------------------------------------------------------------------

PAGE = """<!doctype html>
<html lang="en"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>HECS Orders</title>
<style>
 :root{{--bg:#0f172a;--card:#1e293b;--ink:#e2e8f0;--muted:#94a3b8;--accent:#38bdf8;--line:#334155;}}
 *{{box-sizing:border-box}}
 body{{margin:0;font-family:Segoe UI,system-ui,sans-serif;background:var(--bg);color:var(--ink)}}
 header{{background:linear-gradient(90deg,#0ea5e9,#6366f1);padding:18px 28px;color:#fff}}
 header h1{{margin:0;font-size:20px}}
 header p{{margin:4px 0 0;opacity:.9;font-size:13px}}
 .wrap{{max-width:1100px;margin:0 auto;padding:24px}}
 .bar{{display:flex;gap:12px;flex-wrap:wrap;margin-bottom:18px}}
 input,select{{background:var(--card);border:1px solid var(--line);color:var(--ink);
   padding:9px 12px;border-radius:8px;font-size:14px}}
 .stats{{display:flex;gap:14px;margin-bottom:18px;flex-wrap:wrap}}
 .stat{{background:var(--card);border:1px solid var(--line);border-radius:12px;
   padding:14px 18px;min-width:140px}}
 .stat b{{display:block;font-size:22px;color:var(--accent)}}
 .stat span{{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.5px}}
 table{{width:100%;border-collapse:collapse;background:var(--card);border-radius:12px;overflow:hidden}}
 th,td{{padding:11px 14px;text-align:left;border-bottom:1px solid var(--line);font-size:14px}}
 th{{background:#0b1222;color:var(--muted);text-transform:uppercase;font-size:11px;letter-spacing:.5px}}
 tr:hover td{{background:#273449}}
 a{{color:var(--accent);text-decoration:none}}
 .pill{{padding:3px 10px;border-radius:999px;font-size:12px;font-weight:600}}
 .s-New{{background:#334155;color:#cbd5e1}}
 .s-Submitted{{background:#1d4ed8;color:#fff}}
 .s-On{{background:#b45309;color:#fff}}
 .s-Received{{background:#15803d;color:#fff}}
 .s-Reconciled{{background:#0f766e;color:#fff}}
 .s-Cancelled{{background:#7f1d1d;color:#fff}}
 .right{{text-align:right}}
 .back{{display:inline-block;margin-bottom:16px}}
 .grid{{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:12px}}
 .field{{background:var(--card);border:1px solid var(--line);border-radius:10px;padding:10px 14px}}
 .field span{{display:block;color:var(--muted);font-size:11px;text-transform:uppercase}}
 footer{{color:var(--muted);text-align:center;padding:24px;font-size:12px}}
</style></head>
<body>
<header><h1>HECS Order Tracking</h1>
<p>Lightweight viewer &middot; reading live DXL export &middot; Python standard library only</p></header>
<div class="wrap">{body}</div>
<footer>Synthetic/legacy data from the Notes export. No external services. &middot; HECS Orders viewer</footer>
</body></html>"""


def status_class(status):
    s = status or "New"
    if s.startswith("On"):
        return "s-On"
    if "Received" in s:
        return "s-Received"
    key = s.split()[0]
    return f"s-{key}"


def render_list(orders, q="", status_filter=""):
    rows = []
    statuses = sorted({order_summary(o)["status"] for o in orders})
    total_value = 0.0
    shown = 0
    for doc in orders:
        s = order_summary(doc)
        hay = " ".join([s["ref"], s["project"], s["supplier"],
                        s["cardholder"], s["status"]]).lower()
        if q and q.lower() not in hay:
            continue
        if status_filter and s["status"] != status_filter:
            continue
        shown += 1
        total_value += s["total"]
        rows.append(f"""<tr>
          <td><a href="/order/{html.escape(doc['_noteid'])}">{html.escape(s['ref'] or doc['_noteid'])}</a></td>
          <td><span class="pill {status_class(s['status'])}">{html.escape(s['status'])}</span></td>
          <td>{html.escape(s['project'])}</td>
          <td>{html.escape(s['supplier'])}</td>
          <td>{html.escape(s['purchase'])}</td>
          <td class="right">{s['currency']} {s['total']:,.2f}</td>
        </tr>""")

    opts = "".join(
        f'<option value="{html.escape(s)}"{" selected" if s == status_filter else ""}>{html.escape(s)}</option>'
        for s in statuses)
    body = f"""
      <form class="bar" method="get" action="/">
        <input name="q" placeholder="Search ref, project, supplier..." value="{html.escape(q)}" size="34">
        <select name="status"><option value="">All statuses</option>{opts}</select>
        <button style="background:#0ea5e9;border:none;color:#fff;padding:9px 18px;border-radius:8px;cursor:pointer">Filter</button>
      </form>
      <div class="stats">
        <div class="stat"><b>{len(orders)}</b><span>Total orders</span></div>
        <div class="stat"><b>{shown}</b><span>Shown</span></div>
        <div class="stat"><b>{total_value:,.0f}</b><span>Value shown</span></div>
        <div class="stat"><b>{len(statuses)}</b><span>Statuses</span></div>
      </div>
      <table>
        <tr><th>Reference</th><th>Status</th><th>Project</th><th>Supplier</th><th>Payment</th><th class="right">Total</th></tr>
        {''.join(rows) or '<tr><td colspan="6">No matching orders.</td></tr>'}
      </table>"""
    return PAGE.format(body=body)


def render_detail(doc):
    s = order_summary(doc)
    line_rows = "".join(f"""<tr><td>{l['n']}</td><td>{html.escape(l['desc'])}</td>
       <td class="right">{l['qty']:g}</td><td class="right">{l['price']:,.2f}</td>
       <td class="right">{l['total']:,.2f}</td></tr>""" for l in s["lines"])

    meta_fields = ["ProjectSelection", "CompleteFinancialCode", "Category",
                   "PurchaseType", "CardHolder", "CompanyName", "AcctNumber",
                   "CompPhone", "Currency", "DateReq", "GSTExempt",
                   "BudgetCalculated", "Sent"]
    fields = "".join(
        f'<div class="field"><span>{html.escape(f)}</span>{html.escape(doc["_items"].get(f, "") or "-")}</div>'
        for f in meta_fields)

    body = f"""
      <a class="back" href="/">&larr; Back to orders</a>
      <h2>{html.escape(s['ref'] or doc['_noteid'])}
        <span class="pill {status_class(s['status'])}">{html.escape(s['status'])}</span></h2>
      <div class="grid">{fields}</div>
      <h3>Line items</h3>
      <table>
        <tr><th>#</th><th>Description</th><th class="right">Qty</th>
            <th class="right">Unit price</th><th class="right">Line total</th></tr>
        {line_rows or '<tr><td colspan="5">No line items.</td></tr>'}
        <tr><td colspan="4" class="right"><b>Order total ({s['currency']})</b></td>
            <td class="right"><b>{s['total']:,.2f}</b></td></tr>
      </table>
      <p style="color:#94a3b8;font-size:12px">File: {html.escape(doc['_file'])} &middot;
         UNID: {html.escape(doc.get('_unid',''))}</p>"""
    return PAGE.format(body=body)


# ---------------------------------------------------------------------------
# HTTP server
# ---------------------------------------------------------------------------

class Handler(BaseHTTPRequestHandler):
    def _send(self, text, code=200):
        data = text.encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "text/html; charset=utf-8")
        self.send_header("Content-Length", str(len(data)))
        self.end_headers()
        self.wfile.write(data)

    def do_GET(self):
        parsed = urlparse(self.path)
        orders = load_orders()
        if parsed.path == "/":
            qs = parse_qs(parsed.query)
            self._send(render_list(orders,
                                   q=qs.get("q", [""])[0],
                                   status_filter=qs.get("status", [""])[0]))
        elif parsed.path.startswith("/order/"):
            noteid = parsed.path.split("/order/", 1)[1]
            match = next((o for o in orders if o["_noteid"] == noteid), None)
            if match:
                self._send(render_detail(match))
            else:
                self._send(PAGE.format(body="<p>Order not found.</p>"), 404)
        else:
            self._send(PAGE.format(body="<p>Not found.</p>"), 404)

    def log_message(self, *args):
        pass  # keep the console quiet


def main():
    orders = load_orders()
    print("=" * 60)
    print(" HECS Orders viewer")
    print(f" Parsed {len(orders)} OrderForm documents from {DATA_DIR}")
    print(f" Open  ->  http://127.0.0.1:{PORT}")
    print(" Press Ctrl+C to stop")
    print("=" * 60)
    server = ThreadingHTTPServer(("127.0.0.1", PORT), Handler)
    try:
        server.serve_forever()
    except KeyboardInterrupt:
        print("\nStopped.")


if __name__ == "__main__":
    main()
