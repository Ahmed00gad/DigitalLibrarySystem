# 📚 Digital Library Management System

A robust, enterprise-grade Windows Desktop Application built with **C#**, **.NET 10**, **Windows Forms (WinForms)**, and **SQLite Database**.

Designed strictly following **Object-Oriented Programming (OOP)**, **SOLID principles**, and **Clean Layered Architecture**. Ready to open and run directly in Visual Studio.

---

## 🚀 Features & Requirements Checklist

| # | Requirement | Implementation Details |
|---|---|---|
| **1** | **GUI with Windows Forms** | Rich modern tabbed interface with Segoe UI typography, dashboard stat cards, responsive DataGridViews, custom dialogs, and flat styling. |
| **2** | **OOP & Clean Code Organization** | Structured into `Models/`, `Data/`, `Repositories/`, `Services/`, `Network/`, `Exceptions/`, and `UI/`. Strict separation of concerns and generic repository abstractions. |
| **3** | **Database Connectivity** | SQLite database (`library.db`) via `Microsoft.Data.Sqlite`, automatic schema generation, indexes, relational foreign keys, and seed data. |
| **4** | **CRUD for Books & Members** | Full Create, Read, Update, Delete for books and members with integrity protection (guards against deleting books on active loan or members with borrowed items). |
| **5** | **Borrowing & Returning** | Complete circulation lifecycle with availability checks, borrow limits, due dates, return processing, and copy count updates. |
| **6** | **Automatic Overdue Fine Calculation** | Configurable daily fine rate, grace periods, and membership discounts (Student: 50% discount, Faculty: 75% discount + 5 grace days, Premium: 100% waiver). |
| **7** | **Search and Filtering** | Multi-attribute search across Title, Author, ISBN, and Description. Real-time filtering by Genre/Category, Membership Type, and Loan Status. |
| **8** | **Input Validation** | Strict validation rules: ISBN-10 and ISBN-13 format checks, Regex email verification, positive inventory limits, and `ErrorProvider` feedback on dialog fields. |
| **9** | **Exception Handling** | Custom exception hierarchy (`BookNotAvailableException`, `MemberLimitExceededException`, `ValidationException`, `EntityNotFoundException`) + Global `Application.ThreadException` and `AppDomain` handlers. |
| **10** | **Reports, Statistics, and Charts** | Executive reports generator, live metrics (total copies, active loans, fines collected), Top 5 borrowed books analytics, and CSV exports. |
| **11** | **GDI+ Graphics & Visualization** | Custom-built `GdiChartControl` rendering antialiased GDI+ **Donut/Pie Charts** (category breakdown) and **Bar Charts** (monthly loan volume) with legends and tooltips. |
| **12** | **Async & Multithreading** | Fully asynchronous database I/O (`OpenConnectionAsync`, `ExecuteReaderAsync`), async TCP server/client streams, and a periodic background worker timer auto-scanning overdue fines without freezing the UI. |
| **13** | **Client-Server / Networking Feature** | Embedded asynchronous TCP socket server (`LibraryNetworkServer` on port `8888`) with protocol commands (`PING`, `SEARCH`, `CHECK`, `STATS`, `OVERDUE`) and an integrated GUI Client console. |
| **14** | **Testing & Documentation** | Dedicated MSTest test project (`LibraryManagementSystem.Tests`) with **25 unit tests** passing with 100% success rate. Complete architecture and usage guide. |

---

## 🏛️ Project Architecture

```
DigitalLibrarySystem/
│
├── DigitalLibrarySystem.sln              # Visual Studio Solution File
├── DigitalLibrarySystem.slnx             # Modern .NET XML Solution File
├── README.md                             # Comprehensive Documentation
│
├── src/LibraryManagementSystem/          # Main Windows Forms Application
│   ├── Data/
│   │   ├── DatabaseContext.cs            # SQLite connection factory
│   │   └── DatabaseInitializer.cs        # Schema migration, indexes & seed data
│   │
│   ├── Models/
│   │   ├── BaseEntity.cs                 # Base entity with Id, CreatedAt, UpdatedAt
│   │   ├── Book.cs                       # Book model with ISBN, stock, location
│   │   ├── Member.cs                     # Member model with limits & active status
│   │   ├── MembershipType.cs             # Student, Regular, Faculty, Premium
│   │   ├── Loan.cs                       # Circulation loan record & status
│   │   ├── LoanStatus.cs                 # Active, Returned, Overdue
│   │   ├── LibraryStats.cs               # Aggregated metrics & analytics DTOs
│   │   └── LibrarySettings.cs            # Configurable rates, grace days, port
│   │
│   ├── Exceptions/
│   │   └── LibraryException.cs           # Custom domain exception hierarchy
│   │
│   ├── Repositories/
│   │   ├── IRepository.cs                # Generic repository interface
│   │   ├── IBookRepository.cs            # Book data access contract
│   │   ├── BookRepository.cs             # SQLite implementation
│   │   ├── IMemberRepository.cs          # Member data access contract
│   │   ├── MemberRepository.cs           # SQLite implementation
│   │   ├── ILoanRepository.cs            # Circulation data access contract
│   │   └── LoanRepository.cs             # SQLite implementation
│   │
│   ├── Services/
│   │   ├── IFineCalculatorService.cs     # Overdue fine calculation logic
│   │   ├── FineCalculatorService.cs      # Membership-aware daily fine engine
│   │   ├── ICirculationService.cs        # Borrow/Return transactional workflows
│   │   ├── CirculationService.cs         # Implementation
│   │   ├── IBookService.cs               # Book business logic & validation
│   │   ├── BookService.cs                # Implementation
│   │   ├── IMemberService.cs             # Member business logic & validation
│   │   ├── MemberService.cs              # Implementation
│   │   └── ReportService.cs              # Aggregated analytics & CSV exports
│   │
│   ├── Network/
│   │   ├── LibraryNetworkServer.cs       # Embedded async TCP Socket Server
│   │   └── LibraryNetworkClient.cs       # Async TCP client for remote queries
│   │
│   ├── UI/
│   │   ├── Controls/
│   │   │   ├── GdiChartControl.cs        # Custom GDI+ Pie & Bar Charts
│   │   │   └── StatCardControl.cs        # Modern dashboard metric card
│   │   ├── Dialogs/
│   │   │   ├── BookDialog.cs             # Add/Edit Book modal with ErrorProvider
│   │   │   ├── MemberDialog.cs           # Add/Edit Member modal with validation
│   │   │   ├── BorrowDialog.cs           # Issue loan modal with preview
│   │   │   └── ReturnDialog.cs           # Book return & fine settlement modal
│   │   └── MainForm.cs                   # Main window with all tabs & status
│   │
│   └── Program.cs                        # Entry point with global exception handlers
│
└── tests/LibraryManagementSystem.Tests/  # Unit Test Suite (25 Tests)
    ├── FineCalculatorServiceTests.cs     # Tests fine rates, grace days & discounts
    ├── CirculationServiceTests.cs        # Tests borrow/return workflows on SQLite
    ├── ValidationTests.cs                # Tests ISBN, email regex, limits
    └── NetworkProtocolTests.cs           # Tests TCP server command parsing
```

---

## 🛠️ How to Open and Run in Visual Studio

1. **Open the Project**:
   - Double-click `DigitalLibrarySystem.sln` to open it in **Visual Studio 2022** or **Visual Studio 2025**.
2. **Build the Solution**:
   - Press `Ctrl + Shift + B` or click **Build > Build Solution**.
3. **Run the Application**:
   - Press `F5` (or click **Debug > Start Debugging**) to run `LibraryManagementSystem`.
4. **Run Unit Tests**:
   - Open **Test > Test Explorer** in Visual Studio.
   - Click **Run All Tests** (`Ctrl + R, A`) to verify all 25 unit tests pass.

### Running from the Command Line (CLI):
```bash
# Navigate to project directory
cd C:\Users\ahmed\.gemini\antigravity\scratch\DigitalLibrarySystem

# Build the solution
dotnet build DigitalLibrarySystem.sln

# Run unit tests
dotnet test tests\LibraryManagementSystem.Tests\LibraryManagementSystem.Tests.csproj

# Run the Windows Forms Desktop App
dotnet run --project src\LibraryManagementSystem\LibraryManagementSystem.csproj
```

---

## 🌐 TCP Client-Server Protocol Specification

The embedded TCP server runs on `127.0.0.1:8888` (customizable). You can query it from the built-in GUI console or any external TCP socket client (e.g. `telnet`, `netcat`, or a custom remote client).

| Command | Arguments | Description | Example Response |
|---|---|---|---|
| `PING` | None | Health check | `PONG 1.0 (Digital Library Server Online)` |
| `HELP` | None | Displays available commands | `COMMANDS: PING \| SEARCH \| CHECK \| STATS \| OVERDUE \| QUIT` |
| `SEARCH` | `<query>` | Searches title, author, or description | `200 FOUND 2 BOOKS: [1] "Clean Code" by Robert C. Martin...` |
| `CHECK` | `<isbn>` | Checks availability & copy count | `200 "Clean Code" \| Status: AVAILABLE \| Copies: 4/5 \| Shelf: CS-101` |
| `STATS` | None | Returns live catalog metrics | `200 STATS: Books=15, TotalCopies=58, Available=49, ActiveLoans=3...` |
| `OVERDUE`| None | Lists all loans currently overdue | `200 OVERDUE_COUNT=1: [Loan #2] "Design Patterns" borrowed by Omar...` |
| `QUIT` | None | Closes connection | `200 GOODBYE` |

---

## 💳 Overdue Fine & Membership Rules

- **Base Daily Rate**: `$0.50 / day`
- **Grace Period**: `1 day` (no fines if returned within 1 day of due date)
- **Membership Privileges**:
  - 🎓 **Student**: 50% discount on all overdue fines.
  - 🏫 **Faculty**: 5 additional grace period days + 75% discount.
  - 👑 **Premium**: 100% fine waiver (fines are always `$0.00`).
  - 👤 **Regular**: Standard daily rate.

---

## 🗄️ Database & Pre-Seeded Accounts

On first launch, SQLite automatically creates the schema and populates rich realistic seed data:
- **15 Classical & Technical Books**: (Clean Code, Design Patterns, CLRS, Deep Learning, To Kill a Mockingbird, 1984, Sapiens, Meditations, etc.)
- **5 Members**:
  - `Ahmed Hassan` (Student, limit 3)
  - `Dr. Sarah Mohamed` (Faculty, limit 10)
  - `Omar Khaled` (Regular, limit 5)
  - `Mariam Youssef` (Premium, limit 8)
  - `Mahmoud Ali` (Regular, limit 5)
- **Active & Overdue Loans**: Pre-loaded to immediately showcase overdue fine calculations, GDI+ charts, and dashboard warning tiles.
