# Spec — Sistema de Catálogo de Productos

**Versión:** 0.1.0
**Estado:** Draft
**Metodología:** Spec Driven Development (SDD)
**Fecha:** 2026-10-02
**Relacionado:** [`.claude/docs/plan-catalogo-productos.md`](../.claude/docs/plan-catalogo-productos.md), [`openapi.yaml`](./openapi.yaml), [`erd.md`](./erd.md), [`signalr-events.md`](./signalr-events.md)

---

## 1. Contexto y objetivo

Una empresa gestiona su catálogo de productos en hojas de cálculo (10k–100k registros). Se requiere una aplicación web que permita:

- Cargar archivos Excel existentes de forma masiva, sin bloquear la UI ni caer el servidor con archivos grandes.
- Revisar y corregir los datos cargados **antes** de confirmarlos contra el catálogo real.
- Gestionar productos de forma unitaria (CRUD).
- Buscar y filtrar el catálogo de forma dinámica.
- Restringir el acceso mediante autenticación con roles.

## 2. Actores

| Actor | Descripción |
|---|---|
| **Admin** | Gestiona usuarios y catálogo completo. Puede cargar archivos, confirmar/descartar cargas, CRUD de productos y categorías, ver logs de error de procesamiento. |
| **Usuario** | Rol operativo. Puede cargar archivos, editar su staging y hacer CRUD de productos. No gestiona usuarios ni roles. (Alcance exacto de permisos por endpoint: ver §6 y `openapi.yaml`). |
| **Sistema (Worker)** | Actor no humano. Procesa archivos encolados en background y emite eventos de progreso. |

> **Decisión de diseño:** la diferenciación de permisos entre `Admin` y `Usuario` sobre endpoints específicos (p.ej. quién puede eliminar productos definitivos, quién administra usuarios) debe cerrarse antes de Fase 3. Hasta entonces, el supuesto de trabajo es: **Admin** = acceso total; **Usuario** = acceso a catálogo y cargas, sin gestión de usuarios/roles.

## 3. Alcance

### 3.1 Dentro de alcance
- Carga masiva de Excel (`.xlsx`) con procesamiento en lotes y cola asíncrona.
- Área de "staging" editable (agregar, editar, eliminar filas) antes de confirmar una carga.
- Confirmación (commit) o descarte de una carga.
- CRUD unitario de `Producto` y `Categoria`.
- Tabla de productos con filtros dinámicos combinables, orden y paginación server-side.
- Autenticación JWT (access + refresh token) con 2 roles.
- Feedback en tiempo real del progreso de carga vía SignalR.
- Registro de errores por fila durante el procesamiento.

### 3.2 Fuera de alcance (v0.1)
- Importación de formatos distintos a `.xlsx` (CSV, ODS) — queda como mejora futura.
- Múltiples roles por usuario (many-to-many) — se adopta relación 1:N (ver §7.1).
- Exportación de catálogo a Excel.
- Auditoría granular de todos los campos (AuditLog se modela pero su UI/consumo queda fuera de v0.1).
- Internacionalización / multi-moneda.

## 4. Criterios de aceptación (CA)

| ID | Criterio |
|---|---|
| CA-1 | El sistema permite cargar archivos Excel existentes (10k–100k filas) sin caer ni bloquear la aplicación, procesando en lotes y mediante cola. |
| CA-2 | Los productos de una carga pueden editarse, agregarse o eliminarse **antes** de confirmar la carga (commit), sin afectar el catálogo real hasta ese momento. |
| CA-3 | Existe CRUD unitario completo (crear, leer, actualizar, eliminar) para productos individuales. |
| CA-4 | La tabla de productos soporta filtros dinámicos combinables (por campo y por KPI), orden y paginación. |
| CA-5 | El acceso a la aplicación requiere autenticación JWT; existen 2 roles (`Admin`, `Usuario`) con permisos diferenciados. |

## 5. Flujos principales

### 5.1 Login y sesión
1. Usuario envía credenciales a `POST /auth/login`.
2. API valida contra `Usuario`, genera `accessToken` (JWT, corta duración) + `refreshToken` (persistido en `RefreshToken`, rotable).
3. Front almacena tokens, adjunta `Authorization: Bearer <accessToken>` en cada request.
4. Al expirar el access token, front llama `POST /auth/refresh` con el refresh token vigente; si es inválido/revocado, se fuerza logout.
5. `POST /auth/logout` revoca el refresh token activo.

**CA cubierto:** CA-5.

### 5.2 Carga masiva de Excel
1. Usuario sube un archivo vía `POST /files/upload` (multipart).
2. API guarda el archivo, crea registro `File` en estado `Pendiente`, y encola un job (Hangfire).
3. Worker toma el job, cambia estado a `Procesando`, lee el Excel en streaming y lo inserta en lotes de 500–1000 filas en `ProductoStaging` (vía `SqlBulkCopy`), validando cada fila.
4. Por cada fila inválida, se registra una entrada en `FileProcessingLog` (fila, columna, mensaje) pero el procesamiento continúa (no se aborta el archivo completo por una fila mala).
5. Worker emite progreso incremental por `UploadHub` (ver `signalr-events.md`) al grupo del usuario que subió el archivo.
6. Al terminar, `File.Status` pasa a `Completado` (sin errores), `Parcial` (con errores en algunas filas) o `Fallido` (error no recuperable, p.ej. archivo corrupto).
7. Front se suscribe al hub y muestra barra de progreso sin bloquear la UI.

**CA cubierto:** CA-1.

### 5.3 Preview / edición pre-commit
1. Usuario abre la vista de staging de un `File` (`GET /files/{id}/staging`), filtrable y paginada.
2. Puede: editar una fila (`PUT /files/{id}/staging/{stagingId}`), agregar una fila manual (`POST /files/{id}/staging`), eliminar una fila (`DELETE /files/{id}/staging/{stagingId}`).
3. Puede revisar errores de procesamiento (`GET /files/{id}/errors`).
4. Al confirmar: `POST /files/{id}/commit` — mueve las filas válidas de `ProductoStaging` a `Producto` (creando o actualizando por SKU), registra la relación de origen en `FileProducto`, y marca el `File` como confirmado. Las filas con error pendiente se excluyen del commit y quedan reportadas.
5. Alternativamente, el usuario puede descartar la carga completa (`DELETE /files/{id}` o endpoint `discard` — a definir en `openapi.yaml`), lo que limpia el staging sin tocar `Producto`.

**CA cubierto:** CA-2.

### 5.4 CRUD unitario de productos y categorías
- Alta, baja, modificación y consulta individual de `Producto` y `Categoria` vía endpoints REST estándar, con paginación server-side en listados y validación (FluentValidation en back, Zod en front).

**CA cubierto:** CA-3.

### 5.5 Filtros dinámicos
- `POST /productos/search` acepta una lista de filtros compuestos (campo, operador, valor), orden y paginación, traducidos a un `IQueryable` dinámico en el backend.
- Filtros soportados mínimos: por `Sku`/`Nombre` (texto, contiene), por `CategoriaId` (igual), por `Precio`/`Stock` (rango: `gte`/`lte`), por `CreatedAt` (rango de fechas).
- UI expone estos filtros como chips/rangos combinables sobre la tabla (TanStack Table).

**CA cubierto:** CA-4.

## 6. Reglas de negocio y permisos (borrador)

| Acción | Admin | Usuario |
|---|---|---|
| Login / logout / refresh | ✅ | ✅ |
| Subir archivo Excel | ✅ | ✅ |
| Editar/agregar/eliminar en staging propio | ✅ | ✅ |
| Commit / descartar carga propia | ✅ | ✅ |
| Ver cargas de otros usuarios | ✅ | ❌ |
| CRUD de `Producto` / `Categoria` | ✅ | ✅ |
| Eliminar `Producto` definitivo | ✅ | ⚠️ a confirmar |
| Gestión de usuarios y roles | ✅ | ❌ |
| Ver `AuditLog` | ✅ | ❌ |

Filas marcadas ⚠️ requieren confirmación del stakeholder antes de Fase 3.

## 7. Decisiones de diseño (resumen)

Estas decisiones fijan ambigüedades del plan original para poder definir contratos estables.

### 7.1 Relación Usuario–Role: 1:N (no many-to-many)
**Por qué:** el alcance solo define 2 roles fijos (`Admin`, `Usuario`) y no hay requisito de que un usuario tenga múltiples roles simultáneos.
**Cómo se aplica:** FK `RoleId` directamente en `Usuario`. No se crea tabla `UsuarioRole`. Si en el futuro se requieren roles combinables, se migra a tabla puente sin romper el contrato de `GET /auth/login` (el claim `role` en el JWT sigue siendo válido como string único).

### 7.2 Staging explícito vía `ProductoStaging` + `FileProducto` para linaje
**Por qué:** el modelo base pedía `FileProductos` (relación archivo↔productos) pero el CA-2 exige poder editar/agregar/eliminar antes de confirmar sin tocar el catálogo real.
**Cómo se aplica:** `ProductoStaging` es el área de trabajo pre-commit (vive y muere con el `File` hasta el commit o descarte). `FileProducto` es el registro de linaje post-commit: qué fila de qué archivo originó o actualizó qué `Producto` definitivo. Son tablas distintas con propósitos distintos.

### 7.3 Cola: Hangfire sobre SQL Server
**Por qué:** evita infraestructura adicional (no se requiere RabbitMQ) y reutiliza la base de datos ya definida. Ver Fase 7 del plan.
**Cómo se aplica:** jobs encolados en `POST /files/upload`, procesados por `BackgroundService` en lotes de 500–1000 filas con `SqlBulkCopy`.

### 7.4 Procesamiento parcial, no todo-o-nada
**Por qué:** con archivos de 100k filas, abortar todo por un error de fila sería inaceptable operativamente.
**Cómo se aplica:** cada fila se valida de forma independiente; las inválidas van a `FileProcessingLog` y no bloquean las demás. El estado final `Parcial` comunica que hubo filas con error.

### 7.5 Identificador de negocio: `Sku`
**Por qué:** el commit necesita un criterio de "crear vs actualizar" al mover staging → `Producto`.
**Cómo se aplica:** `Sku` es único en `Producto`. El commit hace upsert por `Sku`. Debe validarse unicidad de `Sku` dentro del propio staging antes de habilitar el commit.

## 8. Requisitos no funcionales

| ID | Requisito |
|---|---|
| RNF-1 | Soportar carga de archivos de hasta 100k filas sin caída del servicio ni bloqueo de la UI. |
| RNF-2 | Procesamiento en lotes de 500–1000 filas vía `SqlBulkCopy`. |
| RNF-3 | Feedback de progreso en tiempo real (SignalR) con latencia percibida < 2s entre actualizaciones. |
| RNF-4 | Listados de productos paginados server-side (no cargar el catálogo completo al front). |
| RNF-5 | Tokens de acceso JWT de corta duración (15 min) + refresh token rotable (7 días). |
| RNF-6 | Errores de fila durante procesamiento no deben perderse: deben quedar trazados en `FileProcessingLog`. |

## 9. Modelo de datos (resumen)

Ver diagrama completo y definiciones de columnas en [`erd.md`](./erd.md). Entidades:

- `Usuario`, `Role`, `RefreshToken` — identidad y sesión.
- `Producto`, `Categoria` — catálogo definitivo.
- `File`, `ProductoStaging`, `FileProcessingLog`, `FileProducto` — ciclo de vida de una carga masiva.
- `AuditLog` (opcional, modelada pero sin UI en v0.1) — trazabilidad de cambios.

## 10. Trazabilidad CA → Fase → Entregable

| CA | Fase(s) | Entregable técnico |
|---|---|---|
| CA-1 | 6, 7 | `POST /files/upload`, Worker + Hangfire, `UploadHub` |
| CA-2 | 6 | `ProductoStaging`, endpoints de staging, `POST /files/{id}/commit` |
| CA-3 | 4 | Endpoints CRUD `/productos`, `/categorias` |
| CA-4 | 5 | `POST /productos/search`, UI de filtros |
| CA-5 | 3 | `/auth/*`, middleware JWT, guard de rutas en front |

## 11. Abiertos / pendientes de decisión

- [ ] Confirmar permisos exactos de `Usuario` vs `Admin` sobre eliminación definitiva de productos (§6).
- [ ] Definir si existe límite de tamaño de archivo (MB) además del límite de filas.
- [ ] Definir formato exacto de columnas esperadas en el Excel de entrada (mapeo de encabezados → campos de `Producto`).
- [ ] Confirmar si `AuditLog` entra en el alcance de UI de v0.1 o queda solo como tabla preparada.
