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

### Key components in `Program.cs`

| Component | Responsibility |
| --- | --- |
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

cd "c:\Users\mbegum\Downloads\hecs_orders_gd.nsf"
dotnet run
```

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
- [ ] Unit tests for the calculation rules.

---

## Notes and boundaries

- All data comes from the static DXL export; nothing is written back.
- Demo/legacy data only: no live services, no external calls, no email.
- This is a focused viewer, not a full replica of the Notes runtime.
