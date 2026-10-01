

<<<<<<< HEAD
# HECS Orders Viewer

A lightweight C# / ASP.NET Core 8 web application that reads a legacy
Lotus Notes DXL export and presents it as a modern, searchable
order-tracking workspace, with a dashboard, CSV export, and a JSON API.

> Reconstructed from a 2002-era HCL Domino database (`hecs_orders_gd.nsf`).
> The original DXL export is unchanged; all data is synthetic/legacy and
> nothing is written back to the source.

---

## The problem and the solution

### The problem
- A 20-year-old Lotus Notes / HCL Domino procurement system
  (`hecs_orders_gd.nsf`) was no longer openable. All that survived was a
  DXL export: roughly 1,200 raw XML files with no UI and no way to read them.
- The original modernisation plan called for a heavy
  .NET 10 + PostgreSQL + Docker stack, but none of it was built, and the
  required infrastructure was not available on the machine.
- In short: the data was trapped in a dead, proprietary format, and the
  planned rebuild needed infrastructure that could not be stood up.

### The solution
- Instead of waiting on heavy infrastructure, this project is a lightweight,
  zero-dependency C# / ASP.NET Core 8 app that reads the DXL files directly
  and serves them as a modern, searchable interface.
- No database, no Docker, no external packages, just the .NET SDK.
- The original Notes business logic (order-total calculation) was faithfully
  recreated for strict legacy parity.

### Obstacles overcome
| Obstacle | Resolution |
| --- | --- |
| .NET SDK not installed (no admin rights) | Installed to the user profile via the official dotnet-install script |
| winget broken | Switched to Microsoft's direct install script |
| Heavy spec versus real constraints | Chose a runnable, dependency-free design |

### Result
From 1,200 unreadable XML files to a running C# web app with search, a
dashboard, CSV export, and a JSON API, built entirely on free tooling and
faithful to the original 2002 business rules.

---

## Features

- Searchable order list: filter by free text, status, and category.
- Sorting: by reference, total (high/low), supplier, or status.
- Analytics dashboard: value by status, by category, and top suppliers,
  plus headline stats (total value, average order, largest order).
- Order detail pages: metadata grid, line items, and a calculated total.
- Faithful legacy calculation: order totals recreate the original Notes
  logic (quantity x price per line, plus shipping, customs, and exchange).
- CSV export: downloads the currently filtered result set.
- JSON API: `/api/orders` and `/api/orders/{noteId}` for reuse.
- Zero external dependencies: standard .NET only; no database, no Docker.

---

## Tech stack

| Layer | Technology |
| --- | --- |
| Language | C# |
| Framework | ASP.NET Core 8 (Minimal APIs) |
| Web server | Kestrel (built-in) |
| Data access | System.Xml.Linq (`XDocument`) reading DXL directly |
| UI | Server-rendered HTML + CSS (no JS framework) |
| Dependencies | None beyond the .NET SDK |

---

## Project structure

| Path | Purpose |
| --- | --- |
| `Program.cs` | The entire app: DXL parser, calculations, routes, HTML rendering |
| `HecsOrders.csproj` | Project file (`Microsoft.NET.Sdk.Web`, `net8.0`) |
| `data/` | The Lotus Notes DXL export (source data) |
| `README.md` | This document |
=======
# HECS Orders Viewer 🧾

A lightweight **C# / ASP.NET Core** web application that reads a legacy **Lotus Notes / HCL Domino** database export (DXL) and presents the purchase orders it contains as a clean, searchable, browser-based workspace.

This project was built as a pragmatic modernisation of an old IBM/Lotus Notes "HECS Order Tracking" application. Instead of standing up the full enterprise stack (.NET + PostgreSQL + Docker), it demonstrates the core idea — **extracting value from legacy data** — with a zero-infrastructure, single-file web app that runs anywhere .NET is installed.
<img width="1920" height="969" alt="image" src="https://github.com/user-attachments/assets/3e6d2f75-6f44-414a-a6db-1db17067247d" />
<img width="1920" height="983" alt="image" src="https://github.com/user-attachments/assets/e8c773f3-1855-41be-8eee-2bb00d00a416" />


https://github.com/user-attachments/assets/2c80a834-6272-4a8e-841a-62cac6038494


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
>>>>>>> ee186b115b60f84ed012b5b257dd6eeabf241004

### Key components in `Program.cs`

| Component | Responsibility |
| --- | --- |
<<<<<<< HEAD
| `DxlStore.LoadOrders` | Reads every `.dxl`, keeps `form="OrderForm"` documents |
| `DxlStore.Summary` | Extracts fields, line items, and computes the order total |
| `Export.Csv` | Builds a CSV of the (optionally filtered) orders |
| `Render` | Produces the HTML for the list, dashboard, and detail pages |

---

## Getting started

### Prerequisites
- .NET 8 SDK (installed under your user profile at `%USERPROFILE%\.dotnet`).

### Run

```powershell
# ensure dotnet is on PATH for this terminal
$env:PATH = "$env:USERPROFILE\.dotnet;$env:PATH"

=======
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
>>>>>>> ee186b115b60f84ed012b5b257dd6eeabf241004
cd "c:\Users\mbegum\Downloads\hecs_orders_gd.nsf"
dotnet run
```

<<<<<<< HEAD
Then open http://127.0.0.1:8000.

> Tip: add `%USERPROFILE%\.dotnet` to your user Environment Variables
> to make `dotnet` available in every terminal automatically.

---

## Routes

| Route | Description |
| --- | --- |
| `/` | Order list with search, filters, and sorting |
| `/dashboard` | Analytics dashboard with bar charts |
| `/order/{noteId}` | Single order detail (fields + line items) |
| `/export.csv` | CSV download of the current filter set |
| `/api/orders` | JSON summary of all orders |
| `/api/orders/{noteId}` | JSON detail for one order |

---

## How order totals are calculated

For each order, the app sums the line items and applies legacy adjustments:

```
lineTotal   = quantity x pricePer           (for lines 1..20)
subtotal    = sum of lineTotal
total       = subtotal + shipping + customs
            + (exchange x total)            (exchange stored as a fraction)
```

The code deliberately preserves the original behaviour rather than
"correcting" it, matching a strict legacy-parity approach.

---

## Roadmap

- [ ] Create / edit order forms (currently read-only).
- [ ] Bilingual English / French labels (as in the original Notes app).
- [ ] Budget / credit-limit warnings.
- [ ] Persist to a real database (EF Core + PostgreSQL).
=======
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
>>>>>>> ee186b115b60f84ed012b5b257dd6eeabf241004
- [ ] Unit tests for the calculation rules.

---

<<<<<<< HEAD
## Notes and boundaries

- All data comes from the static DXL export; nothing is written back.
- Demo/legacy data only: no live services, no external calls, no email.
- This is a focused viewer, not a full replica of the Notes runtime.
=======
## 📝 Notes & boundaries

- All data comes from the **static DXL export**; nothing is written back to the Notes database.
- Demo/legacy data only — no live services, no external calls, no email.
- This is a **focused viewer**, not a certified replica of the entire Notes runtime. See `plan.md` for the complete specification of the legacy system.

---

## 📄 License & provenance

Built from an exported Lotus Notes database (`HECS Order Tracking`). The legacy DXL content is retained unchanged; the C# application code in `Program.cs` is the original contribution of this project.
>>>>>>> ee186b115b60f84ed012b5b257dd6eeabf241004
