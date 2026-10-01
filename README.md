# HECS Orders Viewer 🧾

A lightweight **C# / ASP.NET Core** web application that reads a legacy **Lotus Notes / HCL Domino** database export (DXL) and presents the purchase orders it contains as a clean, searchable, browser-based workspace.

This project was built as a pragmatic modernisation of an old IBM/Lotus Notes "HECS Order Tracking" application. Instead of standing up the full enterprise stack (.NET + PostgreSQL + Docker), it demonstrates the core idea — **extracting value from legacy data** — with a zero-infrastructure, single-file web app that runs anywhere .NET is installed.

---

## ✨ What it does

- **Parses Lotus Notes DXL** (`.dxl`) XML exports directly from the `data/` folder.
- **Filters** to `OrderForm` documents (the purchase orders).
- **Recreates the legacy business calculation** for order totals (line items × quantity, plus shipping, customs and currency-exchange uplift — mirroring rule `BL-002` from the original specification).
- Serves a modern, responsive **dark-themed UI** with:
  - 📋 A searchable, filterable **order list** with live stats (total orders, value, status counts).
  - 🔍 A detailed **order page** showing financial codes, supplier, cardholder, 20 line-item slots and computed totals.
  - 🏷️ Colour-coded **status pills** (New, Submitted, On Order, Received, Reconciled, Cancelled).

---

## 🛠️ Tech stack

| Layer | Technology |
| --- | --- |
| Language | **C# 12** |
| Framework | **ASP.NET Core 8** (Minimal APIs) |
| Runtime | **.NET 8 SDK** |
| Web server | **Kestrel** (built-in) |
| Data access | **`System.Xml.Linq`** (`XDocument`) — no database required |
| UI | Server-rendered **HTML + CSS** (no JS framework) |
| Dependencies | **None** beyond the .NET base libraries |

> There is also an earlier **Python** prototype (`app.py`) using only the standard library, kept for reference. The C# app is the primary deliverable.

---

## 📁 Project structure

```
hecs_orders_gd.nsf/
├── Program.cs          # The entire C# app: DXL parser, calculations, routes, HTML rendering
├── HecsOrders.csproj   # Project file (Microsoft.NET.Sdk.Web, net8.0)
├── app.py              # Original Python prototype (reference only)
├── data/               # ~1,200 Lotus Notes DXL documents (the legacy export)
├── design/             # Legacy Notes forms, views and agents (DXL)
├── views/              # Derived view projections
└── PROJECT-README.md   # This file
```

### Key components in `Program.cs`

| Component | Responsibility |
| --- | --- |
| `DxlStore.LoadOrders()` | Scans `data/`, parses each DXL file, keeps `OrderForm` documents. |
| `DxlStore.Summary()` | Computes per-order totals and extracts key fields. |
| `Render.List()` / `Render.Detail()` | Build the list and detail HTML pages. |
| Minimal API routes | `GET /` (list + search) and `GET /order/{noteId}` (detail). |

---

## 🚀 Getting started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or newer.

> If `dotnet` is installed under your user profile (`%USERPROFILE%\.dotnet`), add it to PATH first:
> ```powershell
> $env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"
> ```

### Run it

```powershell
cd "c:\Users\mbegum\Downloads\hecs_orders_gd.nsf"
dotnet run
```

Then open **http://127.0.0.1:8000** in your browser.

You should see console output like:

```
============================================================
 HECS Orders viewer (C#)
 Parsed 7 OrderForm documents from ...\data
 Open  ->  http://127.0.0.1:8000
============================================================
```

---

## 🧮 How the order total is calculated

Faithful to the legacy `BL-002` rule:

```
lineTotal   = quantity × unitPrice          (for each of 20 item slots)
baseTotal   = Σ lineTotals + shipping + customs
orderTotal  = baseTotal + (exchange × baseTotal)   // exchange is a fraction, e.g. 0.10 = +10%
```

The code deliberately preserves the original behaviour (e.g. exchange stored as a fraction) rather than "correcting" it, matching the strict-parity approach in the modernisation plan.

---

## 🗺️ Roadmap / ideas for the future

- [ ] Add create / edit order forms (currently read-only).
- [ ] Bilingual English / French labels (as in the original Notes app).
- [ ] Export filtered results to CSV / JSON.
- [ ] Persist to a real database (EF Core + PostgreSQL) per the full `plan.md`.
- [ ] Add the budget / credit-limit warnings (`BL-003`).
- [ ] Unit tests for the calculation rules.

---

## 📝 Notes & boundaries

- All data comes from the **static DXL export**; nothing is written back to the Notes database.
- Demo/legacy data only — no live services, no external calls, no email.
- This is a **focused viewer**, not a certified replica of the entire Notes runtime. See `plan.md` for the complete specification of the legacy system.

---

## 📄 License & provenance

Built from an exported Lotus Notes database (`HECS Order Tracking`). The legacy DXL content is retained unchanged; the C# application code in `Program.cs` is the original contribution of this project.
