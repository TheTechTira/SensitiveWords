# 🧠 SensitiveWords Microservice (.NET 8)

A lightweight **C# .NET 8 microservice** that detects and masks (“bloops”) sensitive or disallowed words (e.g., SQL keywords, profanity, or internal restricted terms) from incoming messages before returning them to the client.

> Designed as part of a technical assessment demonstrating API design, Dapper-based data access, caching, and performance-oriented architecture.

---

## 🧩 Interview Challenge Summary

The technical assessment required designing and implementing a **C# .NET Core microservice** called **SensitiveWords API**, which detects and masks (“bloops”) sensitive words from user messages before displaying them back to the client application.

### 📋 Functional & Technical Requirements

| Category | Description |
| --- | --- |
| **1. RESTful API** | Implement a clean, modular REST API using **C# .NET Core** with proper versioning, separation of internal (CRUD) and external (business logic) endpoints. |
| **2. Swagger Documentation** | Integrate **Swagger (OpenAPI)** for automatic documentation, ensuring all endpoints, parameters, and responses are annotated and clearly described. |
| **3. Database CRUD Layer (MSSQL)** | Use **MSSQL** as the data backend with **ADO.NET or Dapper** (no Entity Framework or LINQ). Implement full **CRUD operations** for managing sensitive words. |
| **4. Unit Testing** | Add **unit tests** to ensure core business logic (masking/sanitization, regex matching, and caching) is reliable and well-covered. |
| **5. Performance Considerations** | Propose enhancements to optimize runtime performance, scalability, and database efficiency. |
| **6. Additional Enhancements** | Suggest improvements that would make the service more production-ready or complete (e.g., caching, rate limiting, CI/CD setup). |

### 💡 Business Scenario

A startup company wants to extend their chat system with a service that automatically filters out **sensitive or disallowed words** (like SQL keywords, profanity, or custom company terms).

**Workflow Example:**

1. A user sends a chat message through the client app.
2. The app **POSTs** the message to the **SensitiveWords API**.
3. The API retrieves its current list of sensitive words from the database.
4. Each sensitive word in the message is masked (e.g., replaced with `*****`).
5. The API returns the sanitized (“blooped”) message to the client.
6. The client displays the safe version to users.

**Example:**

```
Input:  "SELECT name FROM users"
Output: "****** name **** users"
```

<img width="1026" height="602" alt="image" src="https://github.com/user-attachments/assets/4b381b61-ec7e-4820-8645-d8bce575a4aa" />


### 🧱 Deliverables

- **Internal API:** CRUD operations for sensitive word management (`/api/v1/sensitive-words`).
- **External API:** Business logic endpoint that receives a message and returns the blooped version (`/api/v1/bloop`).
- **Database:** Preloaded with example sensitive words (e.g., `SELECT`, `DROP`, `DELETE`, etc.) from the `sql_sensitive_list.txt` file on application startup.
- **Deployment walkthrough:** A brief explanation of how to host the API in production.
- **Unit tests:** To validate logic correctness and reliability.

---

## 🧠 What I Understood and Implemented

The objective was to design a self-contained **Sensitive Words Microservice** in **C# .NET 8**, capable of receiving a message, detecting and masking (“blooping”) any sensitive words stored in a SQL database, and returning the sanitized output efficiently.

Below is how I approached and implemented the solution.

---

### 🗄️ Database Design (MSSQL)

- **Database:** `SensitiveWordsDB`
- **Tables:**
    - `SensitiveWords` — stores all words (with `Id`, `Word`, `IsActive`, timestamps, etc.).
    - `MetaData` — tracks change/version metadata to support cache invalidation later.
- **Seeder:** On startup, the service automatically pre-loads words from `sql_sensitive_list.txt` into the database if empty.

---

### ⚙️ API Design (RESTful Microservice)

- Implemented a **RESTful API** following clear separation of concerns and versioning (`v1`).
- Two logical API layers:
    1. **Internal CRUD API** — for internal administration.
        - `GET /sensitive-words` → List (paged, searchable)
        - `GET /sensitive-words/{id}` → Get single
        - `POST /sensitive-words` → Create (or revive deleted)
        - `PUT /sensitive-words/{id}` → Update
        - `DELETE /sensitive-words/{id}` → Soft or hard delete
    2. **External Business Logic API** — `/bloop` endpoint that accepts a message and returns its masked version.

---

### 🧩 Data Access Layer (Repository Pattern)

- Implemented a **Repository layer** using **Dapper** (chosen for its performance balance between raw ADO and EF).
- CRUD methods are parameterized and mapped to domain DTOs.
- All database calls are **async**, and connection handling is safe and scoped.

---

### 🧠 Business Logic: The “Bloop” Service

Handles the masking of sensitive words in incoming messages.

#### Core Flow

1. **Timer starts** for performance tracking.
2. **Load cached regex pattern**:
    - If cache version matches DB version → reuse regex.
    - Otherwise → fetch all active words, rebuild regex pattern, and cache it with version key.
3. Apply regex replacements:
    - Supports **whole-word** and **substring** matching modes.
    - Example:
        - Whole-word: `SELECT * FROM` → `***** * FROM`
        - Substring: `WeSELECT * FROM` → `We****** * FROM`
4. **Stop timer** and return a detailed response DTO containing:
    - Original text
    - Masked text
    - Match count
    - Elapsed time (ms)

#### Performance Enhancements

- **MemoryCache** stores compiled regex for the active words version.
- Planned production upgrade:
    - Use **push invalidation** (e.g., `IChangeToken` or message broker signal) to bump in-memory version keys.
    - Introduce a **composite cache** `{ Version, Regex }` with short TTL to eliminate most DB hits.

---

### 🧪 Unit Tests

- Added **unit tests** for:
    - Repository CRUD operations (via in-memory/local DB).
    - SensitiveWordService logic (CRUD tests, revive, delete, version bump, validation).
    - BloopService logic (regex replacement, cache behavior, word version changes).
- Used **xUnit** and **Moq** for isolation.
- Used **FluentAssertions** in unit tests for more friendly and reabible assertions.

---

### 📚 Swagger Documentation

- Integrated **Swagger / OpenAPI** (Swashbuckle).
- Annotated all endpoints, parameters, and responses with XML comments and attributes.
- Implemented **Internal vs External documentation grouping** using a custom `[Audience]` attribute.
- Automatically detects new API versions (`v2`, `v3`) and adds them to the Swagger UI without manual wiring.

---

### 🖥️ WebApp Demo (Bonus)

Added a demo project: **`SensitiveWords.WebApp`** — a C# .NET 8 MVC app with **SignalR** chat interface to simulate real usage.

- Each message sent is processed via the `SensitiveWords.API` before display.
- If the microservice is offline, the message displays with a `(failed)` postfix for clear feedback.
- Demonstrates real-time message sanitation in action.

#### To Run Both Projects

1. Open solution in **Visual Studio**.
2. Right-click solution → **Configure Startup Projects…**
3. Choose **Multiple startup projects** → Set both `SensitiveWords.API` and `SensitiveWords.WebApp` to *Start*.
4. Update `appsettings.json` in `SensitiveWords.WebApp` with your `SensitiveWords.API` localhost or URL:
    
    ```json
    "Microservice": {
      "SensitiveWordsApi": "https://localhost:7012"
    }
    ```
    
5. Run — both the API and WebApp will start simultaneously.

---

### 🚦 Additional Features

- **Rate Limiting:** Implemented a basic per-IP rate limiter in `Program.cs` (for `bloop` endpoint).
  - Future: make this configurable per client/API key.
- **Validation & Error Handling:**
  - Model validation with clear `400 Bad Request` responses.
  - Centralized exception middleware returning structured API errors.
- **Environment Awareness:**
  - Supports `Development` and `Production` modes with environment-specific Swagger exposure.

---

### ☁️ Deployment Thoughts

In production, this microservice would typically be deployed as:

- A **Docker container** behind an API gateway or reverse proxy (e.g., Nginx or AWS ALB).
- Backed by a **MSSQL database** (RDS/Azure SQL).
- Secured with:
    - JWT auth / OIDC for internal endpoints.
    - API-key or gateway auth for public `bloop` endpoint.
- Monitored via Application Insights or OpenTelemetry metrics.

---

> 🧠 **Developer Note:**  
> Everything above this point covers the original interview brief and my understanding of the challenge.  
> From here onward, you’ll find the full technical documentation and setup guide for the implemented microservice.

---

## ✅ Introduction

The **SensitiveWords API** is a self-contained microservice responsible for detecting and replacing sensitive words in any text input, providing a sanitized ("blooped") output string.

The service also includes:
- A **MSSQL database** for storing and managing sensitive words.
- An **internal CRUD API** for word management.
- An **external “Bloop” API** for message sanitization.
- A **SignalR demo web app** (`SensitiveWords.WebApp`) showing real-time usage of the service.

---

## ⚙️ Architecture Overview

### 🧩 Core Components

| Layer | Description |
|-------|--------------|
| **API** | Exposes REST endpoints for CRUD and business logic (blooping). |
| **Application / Services** | Contains core logic such as `BloopService` and `SensitiveWordsService`. |
| **Repository (Data)** | Dapper-based SQL layer for async CRUD operations. |
| **Domain Models** | Defines entities (`SensitiveWord`, `MetaData`, etc.) and DTOs. |
| **Infrastructure** | Handles caching, configuration, and database connections. |
| **WebApp Demo (SignalR)** | Real-time chat simulation showing the API in action. |

### 🧱 High-Level Flow

```text
Client App → SensitiveWords.API (/bloop) → Regex masking using cached sensitive words → Response returned
                       ↑
                MSSQL Database (SensitiveWords, MetaData)
```

---

## 🚀 Getting Started

### 1. Clone the Repository

```bash
git clone https://github.com/<your-username>/SensitiveWords.git
cd SensitiveWords
```

### 2. Database Setup

1. Ensure you have **SQL Server** running (local or container).
2. Use the provided `db.sql` script (inside the project root or `/Database`) to create and seed the database:

```bash
SQLCMD -S .\SQLEXPRESS -i db.sql
```
3. The database will create two tables:
   - `SensitiveWords` — stores sensitive words.
   - `MetaData` — stores version and change tracking for cache invalidation.

4. A seed file (`sql_sensitive_list.txt`) is automatically loaded on first startup if no data exists.

### 3. Configuration

In `appsettings.json` (under `SensitiveWords.API`):

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=SensitiveWordsDB;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

In `SensitiveWords.WebApp`:

```json
"Microservice": {
  "SensitiveWordsApi": "https://localhost:7012"
}
```

### 4. Running the Solution

1. Open the solution in **Visual Studio 2022+**.
2. Right-click the solution → **Configure Startup Projects…**
3. Select **Multiple startup projects** → set both:
   - `SensitiveWords.API`
   - `SensitiveWords.WebApp`
4. Run the solution — both API and WebApp should start simultaneously.

---

## 📚 API Endpoints

### 🔒 Internal CRUD API (`/api/v1/sensitive-words`)

| Method | Endpoint | Description |
|--------|-----------|-------------|
| `GET` | `/` | List sensitive words (paged, searchable) |
| `GET` | `/{id}` | Get a specific word by ID |
| `POST` | `/` | Create or revive a deleted word |
| `PUT` | `/{id}` | Update an existing word |
| `DELETE` | `/{id}?hard=true|false` | Delete (soft/hard) |

### 🌍 External Bloop API (`/api/v1/bloop`)

**POST** `/api/v1/bloop`

Request:
```json
{
  "message": "SELECT name FROM users",
  "wholeWord": true
}
```

Response:
```json
{
  "original": "SELECT name FROM users",
  "blooped": "****** name **** users",
  "matches": 2,
  "elapsedMs": 1.43
}
```

---

## 🧪 Testing

Unit tests were implemented using **xUnit** and **Moq**.

### Coverage Summary

| Component | Description |
|------------|-------------|
| **Repository Tests** | Verify CRUD operations with Dapper. |
| **SensitiveWordsService Tests** | Validate business logic, revive, delete, version bump, error handling. |
| **BloopService Tests** | Validate regex matching, cache reuse, version invalidation, and replacement accuracy. |

---

## 🧠 Design Choices

### Data Layer
- **Dapper** chosen for fine-grained SQL control and performance.
- Parameterized queries prevent SQL injection.

### Caching
- **MemoryCache** holds compiled regex and current word version.
- Future upgrade: push invalidation (e.g., message bus or `IChangeToken`).

### API Design
- Clean versioning with `[ApiVersion]`.
- `[Audience]` attribute automatically segregates **Internal** vs **External** Swagger docs.

### Documentation
- XML comments + annotations generate complete Swagger / OpenAPI definitions.
- Separate Swagger UI for internal (CRUD) and external (public) consumers.

### Error Handling
- Centralized middleware returns consistent structured responses.
- Built-in model validation ensures correct input shape.

### Performance
- Cached regex avoids repetitive DB lookups.
- Regex compiled once per version.
- Rate limiting added via `Program.cs`.

---

## 🧩 Enhancements / Next Steps

| Area | Potential Improvement |
|-------|-----------------------|
| **Caching** | Implement distributed cache (e.g., Redis) with pub/sub invalidation. |
| **Security** | Add JWT or API key authentication and per-client rate limits. |
| **DevOps** | Containerize and deploy via Docker + CI/CD pipeline. |
| **Metrics** | Integrate Application Insights or Prometheus for telemetry. |
| **Scalability** | Use message queue for async cache updates across multiple instances. |
| **Persistence** | Add word categories, audit logs, and user management for multi-tenant use. |

---

## ☁️ Deployment

Recommended production setup:

- **Containerized** with Docker.
- Deployed behind **API Gateway** (Nginx / AWS ALB).
- Database hosted on **Azure SQL** or **AWS RDS**.
- **Internal endpoints** protected via JWT/OIDC.
- **External endpoints** behind gateway auth and rate limiting.
- Monitored using **Application Insights** / **OpenTelemetry**.

---

## 👨‍💻 Author

**Jandre Janse van Vuuren**  
Software Architect & IoT Engineer — VUUREEN (Pty) Ltd  
📧 email@example.com  
🌐 [https://vuureen.com](https://vuureen.com)

---

> _“Clean code is not written faster — it simply lasts longer.”_ ✨
