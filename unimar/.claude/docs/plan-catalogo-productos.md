# Plan de Implementación — Sistema de Catálogo de Productos

**Metodología:** Spec Driven Development (SDD)
**Stack:** React + .NET 8 + SQL Server
**Fecha:** 2026-10-02

---

## Contexto

Una empresa gestiona su catálogo de productos en hojas de cálculo (10k–100k registros) y necesita una aplicación web para mantenerlo, con carga masiva, edición pre-commit, CRUD unitario, filtros dinámicos y autenticación por roles.

---

## Criterios de aceptación

- Carga masiva de Excel existentes, con manejo en lotes y encolamiento para evitar caídas.
- Edición, adición y eliminación de productos cargados **antes** de confirmar la carga.
- CRUD unitario para gestión individual de productos.
- Tabla con filtros dinámicos (búsqueda por campos / KPIs).
- Autenticación JWT con 2 roles (Admin, Usuario).

---

## Modelo de datos

### Tablas base
- `File` — archivo cargado + usuario que lo subió
- `FileProductos` — relación archivo ↔ productos
- `Productos`
- `Categoria`
- `Usuario`
- `Role`

### Tablas propuestas adicionales
- **UsuarioRole** — many-to-many si un usuario puede tener varios roles (si es 1:N, basta FK `RoleId` en `Usuario`).
- **FileStatus** (enum + columna en `File`) — estados: `Pendiente | Procesando | Completado | Fallido | Parcial`.
- **FileProcessingLog** — errores por fila durante procesamiento (fila, columna, mensaje). Crítico para 100k.
- **RefreshToken** — rotación de JWT sin re-login.
- **AuditLog** (opcional) — trazabilidad de modificaciones.
- **ProductoStaging** (recomendado) — tabla intermedia donde caen los productos del Excel antes del "guardar carga". Permite editar/agregar/remover sin tocar `Productos` definitivo. Al confirmar, se mueve a `Productos`.

---

## Fase 1 — Spec & Setup (SDD)

**Objetivo:** definir contratos antes de codear.

| # | Tarea |
|---|-------|
| 1.1 | Redactar `spec.md` con CAs, actores y flujos (carga masiva, CRUD, login) |
| 1.2 | Definir contratos de API en OpenAPI/Swagger |
| 1.3 | Definir contratos de eventos SignalR (`UploadProgress`, `UploadCompleted`, `UploadFailed`) |
| 1.4 | Modelar ERD final (incluyendo tablas propuestas) |
| 1.5 | Crear repos (back + front) y estructura de solución |

**Entregable:** `spec.md`, `openapi.yaml`, diagrama ERD.

---

## Fase 2 — Infraestructura base

**Objetivo:** proyectos corriendo en local.

| # | Tarea |
|---|-------|
| 2.1 | Solución .NET con capas: `Api`, `Application`, `Domain`, `Infrastructure`, `Worker` |
| 2.2 | Proyecto React con Vite + TypeScript | (Utilizar tailwindcss)
| 2.3 | SQL Server local + connection string en `appsettings` |
| 2.4 | Docker Compose (SQL Server + API + Worker + Front) para dev |
| 2.5 | Configurar CORS, Serilog, Swagger |

**Entregable:** `docker compose up` levanta todo.

---

## Fase 3 — Seguridad (JWT + Roles)

**Objetivo:** autenticación funcional.

| # | Tarea |
|---|-------|
| 3.1 | Entidades `Usuario`, `Role`, `RefreshToken` con Fluent API |
| 3.2 | Migración inicial + seed de roles (`Admin`, `Usuario`) |
| 3.3 | Endpoints: `POST /auth/login`, `POST /auth/refresh`, `POST /auth/logout` |
| 3.4 | Middleware JWT + `[Authorize(Roles="...")]` |
| 3.5 | Front: contexto de auth, interceptor Axios, guard de rutas |

**Entregable:** login funcional, rutas protegidas por rol.

---

## Fase 4 — CRUD Productos + Categoría

**Objetivo:** gestión unitaria.

| # | Tarea |
|---|-------|
| 4.1 | Entidades `Producto`, `Categoria` con Fluent API |
| 4.2 | Endpoints CRUD con paginación server-side |
| 4.3 | UI: formulario de alta/edición + tabla con TanStack Table |
| 4.4 | Validaciones (FluentValidation en back, Zod + RHF en front) |

**Entregable:** CRUD completo operativo.

(El acceso a bd es con windows, certificado en true)
---

## Fase 5 — Filtros dinámicos (KPIs)

**Objetivo:** búsqueda avanzada en tabla.

| # | Tarea |
|---|-------|
| 5.1 | DTO de filtros + query builder dinámico (EF `IQueryable` + `Expression`) |
| 5.2 | Endpoint `POST /productos/search` con filtros compuestos, orden y paginación |
| 5.3 | UI de filtros (chips, rangos, búsqueda por nombre/SKU) |

**Entregable:** tabla con filtros combinables.

---

## Fase 6 — Carga masiva Excel

**Objetivo:** flujo de carga + edición pre-commit.

| # | Tarea |
|---|-------|
| 6.1 | Librería backend: **ClosedXML** o **EPPlus** (streaming) |
| 6.2 | Librería front: **SheetJS (xlsx)** para preview inicial |
| 6.3 | Endpoint `POST /files/upload` → guarda archivo + registro `File` en estado `Pendiente` |
| 6.4 | Encolar job (ver Fase 7) |
| 6.5 | Vista "Preview/Edición" sobre `ProductoStaging` antes de confirmar |
| 6.6 | Endpoint `POST /files/{id}/commit` → mueve staging a `Productos` |

**Entregable:** flujo completo subida → preview → edición → commit.

---

## Fase 7 — Encolamiento + Worker + SignalR

**Objetivo:** procesamiento asíncrono con feedback en tiempo real.

| # | Tarea |
|---|-------|
| 7.1 | Elegir cola: **Hangfire** (recomendado, SQL Server storage) o MassTransit + RabbitMQ |
| 7.2 | Worker: `BackgroundService` procesa lotes de 500–1000 filas con `SqlBulkCopy` |
| 7.3 | Hub SignalR `UploadHub` emite progreso por `fileId` al grupo del usuario |
| 7.4 | Reintentos + manejo de errores por fila → `FileProcessingLog` |
| 7.5 | Front: suscripción al hub, barra de progreso %, UI no bloqueada |

**Entregable:** carga de 100k registros sin bloquear UI, con progreso en vivo.

---

## Fase 8 — Testing & QA

**Objetivo:** calidad y rendimiento.

| # | Tarea |
|---|-------|
| 8.1 | Unit tests (xUnit) para servicios de dominio y validaciones |
| 8.2 | Tests de integración con `WebApplicationFactory` + Testcontainers (SQL Server) |
| 8.3 | Carga sintética: generar Excel de 100k con **Bogus** y medir tiempos |
| 8.4 | Tests front (Vitest + Testing Library) para flujos críticos |

**Entregable:** suite de tests + reporte de performance.

---

## Fase 9 — Despliegue

**Objetivo:** producción.

| # | Tarea |
|---|-------|
| 9.1 | CI (GitHub Actions): build, test, publish, migraciones |
| 9.2 | Deploy en AWS EC2 o contenedores |
| 9.3 | Variables sensibles en Secrets Manager / env vars |
| 9.4 | Logs centralizados + health checks |

**Entregable:** sistema desplegado y monitoreado.

---

## Comandos de setup

### Backend (.NET 8)

```bash
# Solución y proyectos
mkdir CatalogoProductos && cd CatalogoProductos
dotnet new sln -n CatalogoProductos

dotnet new webapi -n CatalogoProductos.Api -o src/Api --use-controllers
dotnet new classlib -n CatalogoProductos.Application -o src/Application
dotnet new classlib -n CatalogoProductos.Domain -o src/Domain
dotnet new classlib -n CatalogoProductos.Infrastructure -o src/Infrastructure
dotnet new worker -n CatalogoProductos.Worker -o src/Worker

dotnet sln add src/Api src/Application src/Domain src/Infrastructure src/Worker

# Referencias
dotnet add src/Api reference src/Application src/Infrastructure
dotnet add src/Application reference src/Domain
dotnet add src/Infrastructure reference src/Application src/Domain
dotnet add src/Worker reference src/Application src/Infrastructure

# Paquetes Api
cd src/Api
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add package Microsoft.AspNetCore.SignalR
dotnet add package Swashbuckle.AspNetCore
dotnet add package Serilog.AspNetCore
dotnet add package Hangfire.AspNetCore
dotnet add package Hangfire.SqlServer

# Paquetes Infrastructure
cd ../Infrastructure
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
dotnet add package Microsoft.EntityFrameworkCore.Design
dotnet add package ClosedXML
dotnet add package Hangfire.Core

# Paquetes Application
cd ../Application
dotnet add package FluentValidation.AspNetCore
dotnet add package MediatR
dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection

# EF tools + primera migración
cd ../..
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial -p src/Infrastructure -s src/Api -o Persistence/Migrations
dotnet ef database update -p src/Infrastructure -s src/Api
```

### Frontend (React + Vite + TS)

```bash
npm create vite@latest catalogo-web -- --template react-ts
cd catalogo-web
npm install

npm install axios react-router-dom @tanstack/react-query @tanstack/react-table
npm install react-hook-form zod @hookform/resolvers
npm install @microsoft/signalr
npm install xlsx
npm install -D tailwindcss @tailwindcss/vite
npm install zustand
```

### Docker Compose (dev)

```yaml
# docker-compose.yml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "Your_strong_Pass123"
    ports: ["1433:1433"]
    volumes: [sqldata:/var/opt/mssql]
volumes:
  sqldata:
```

```bash
docker compose up -d
```

### Connection string (`appsettings.Development.json`)

```json
{
  "ConnectionStrings": {
    "Default": "Server=localhost,1433;Database=CatalogoDb;User Id=sa;Password=Your_strong_Pass123;TrustServerCertificate=True"
  },
  "Jwt": {
    "Issuer": "CatalogoApi",
    "Audience": "CatalogoWeb",
    "Key": "cambia-esto-por-una-clave-de-256-bits-minimo",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

---

## Resumen de fases

| Fase | Nombre | Entregable clave |
|------|--------|------------------|
| 1 | Spec & Setup | `spec.md`, OpenAPI, ERD |
| 2 | Infraestructura base | Proyectos levantados con Docker |
| 3 | Seguridad JWT | Login + roles funcionales |
| 4 | CRUD Productos | Gestión unitaria |
| 5 | Filtros dinámicos | Búsqueda avanzada |
| 6 | Carga masiva Excel | Upload + staging + commit |
| 7 | Worker + SignalR | Procesamiento async con progreso |
| 8 | Testing & QA | Suite de tests + benchmark |
| 9 | Despliegue | Sistema en producción |
